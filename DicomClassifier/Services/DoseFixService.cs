using System.Globalization;
using DicomClassifier.Models;
using FellowOakDicom;

namespace DicomClassifier.Services;

public class DoseFixService : IDoseFixService
{
    private static readonly double[] DefaultIop = [1, 0, 0, 0, 1, 0];

    public OperationPlan BuildDoseFixPlan(
        List<DicomFileInfo> files,
        string targetRoot,
        bool enhanceMode)
    {
        var plan = new OperationPlan();
        var rtdoseFiles = files.Where(f => f.IsValid && f.Modality == "RTDOSE").ToList();
        var otherFiles = files.Where(f => f.Modality != "RTDOSE" || !f.IsValid).ToList();

        // Build a lookup: for each RTDOSE, find CT IOP by FrameOfReferenceUID
        var ctIopCache = new Dictionary<string, double[]>(StringComparer.Ordinal);
        if (enhanceMode)
        {
            var ctFiles = files.Where(f => f.IsValid && f.Modality == "CT").ToList();
            foreach (var ct in ctFiles)
            {
                if (ct.FrameOfReferenceUid != null && !ctIopCache.ContainsKey(ct.FrameOfReferenceUid))
                {
                    if (ct.Dataset != null)
                    {
                        var iop = GetDoubleValues(ct.Dataset, DicomTag.ImageOrientationPatient, 6);
                        if (iop.Length == 6)
                            ctIopCache[ct.FrameOfReferenceUid] = iop;
                    }
                }
            }
        }

        var rtdoseCount = 0;

        foreach (var file in rtdoseFiles)
        {
            try
            {
                var srcDir = Path.GetDirectoryName(file.SourcePath)!;
                var relativePath = GetRelativePath(srcDir, GetSourceRoot(files, file.SourcePath));
                var destDir = Path.Combine(targetRoot, relativePath);
                var destPath = Path.Combine(destDir, file.FileName);

                double[] newIop;
                string iopSource;

                if (enhanceMode && file.FrameOfReferenceUid != null &&
                    ctIopCache.TryGetValue(file.FrameOfReferenceUid, out var cachedIop))
                {
                    newIop = cachedIop;
                    iopSource = $"CT (FrameOfReference: {file.FrameOfReferenceUid})";
                }
                else
                {
                    newIop = DefaultIop;
                    iopSource = enhanceMode
                        ? $"default — no matching CT found for FrameOfReference {file.FrameOfReferenceUid}"
                        : "default (Normal mode)";
                }

                var newIpp = ComputeNewIpp(file, newIop);

                var op = new FileOperation
                {
                    Type = OperationType.DoseFix,
                    SourcePath = file.SourcePath,
                    DestinationPath = destPath,
                    FixIop = newIop,
                    FixIpp = newIpp
                };

                plan.Operations.Add(op);
                rtdoseCount++;

                plan.Warnings.Add($"RTDOSE: {file.FileName} → IOP from {iopSource}");
            }
            catch (Exception ex)
            {
                plan.Errors.Add($"Failed to process RTDOSE {file.SourcePath}: {ex.Message}");
            }
        }

        // Copy non-RTDOSE files as-is
        foreach (var file in otherFiles)
        {
            try
            {
                var srcDir = Path.GetDirectoryName(file.SourcePath)!;
                var relativePath = GetRelativePath(srcDir, GetSourceRoot(files, file.SourcePath));
                var destDir = Path.Combine(targetRoot, relativePath);
                var destPath = Path.Combine(destDir, file.FileName);

                plan.Operations.Add(new FileOperation
                {
                    Type = OperationType.Copy,
                    SourcePath = file.SourcePath,
                    DestinationPath = destPath
                });
            }
            catch (Exception ex)
            {
                plan.Errors.Add($"Failed to process {file.SourcePath}: {ex.Message}");
            }
        }

        plan.FileCount = files.Count;
        plan.DirectoryCount = 0;
        plan.IsInPlace = false;
        plan.Warnings.Insert(0, $"DoseFix plan: {rtdoseCount} RTDOSE files to fix, {otherFiles.Count} other files to copy.");

        return plan;
    }

    public void FixDoseFile(string sourcePath, string destinationPath, double[] newIop, double[] newIpp)
    {
        var dicomFile = DicomFile.Open(sourcePath);
        var ds = dicomFile.Dataset;

        // Set new IOP (6 values)
        SetDoubleValues(ds, DicomTag.ImageOrientationPatient, newIop);

        // Set new IPP (3 values)
        SetDoubleValues(ds, DicomTag.ImagePositionPatient, newIpp);

        // Ensure output directory exists
        var destDir = Path.GetDirectoryName(destinationPath);
        if (destDir != null) Directory.CreateDirectory(destDir);

        dicomFile.Save(destinationPath);
    }

    private static double[] ComputeNewIpp(DicomFileInfo file, double[] newIop)
    {
        var ds = file.Dataset!;

        var ippOld = GetDoubleValues(ds, DicomTag.ImagePositionPatient, 3);
        var iopOld = GetDoubleValues(ds, DicomTag.ImageOrientationPatient, 6);
        var pixelSpacing = GetDoubleValues(ds, DicomTag.PixelSpacing, 2);
        var rows = ds.GetSingleValue<int>(DicomTag.Rows);
        var columns = ds.GetSingleValue<int>(DicomTag.Columns);

        double centerCol = (columns - 1) / 2.0;
        double centerRow = (rows - 1) / 2.0;

        // Original grid center in patient coordinates (using old IOP)
        double centerX = ippOld[0]
                         + centerCol * pixelSpacing[1] * iopOld[0]
                         + centerRow * pixelSpacing[0] * iopOld[3];
        double centerY = ippOld[1]
                         + centerCol * pixelSpacing[1] * iopOld[1]
                         + centerRow * pixelSpacing[0] * iopOld[4];
        double centerZ = ippOld[2]; // Z center unchanged

        // Back-project to new IPP using the new IOP
        double newIppX = centerX
                         - centerCol * pixelSpacing[1] * newIop[0]
                         - centerRow * pixelSpacing[0] * newIop[3];
        double newIppY = centerY
                         - centerCol * pixelSpacing[1] * newIop[1]
                         - centerRow * pixelSpacing[0] * newIop[4];
        double newIppZ = centerZ;

        return [newIppX, newIppY, newIppZ];
    }

    private static double[] GetDoubleValues(DicomDataset ds, DicomTag tag, int expectedCount)
    {
        var str = ds.GetString(tag);
        if (string.IsNullOrEmpty(str))
            return new double[expectedCount];

        var parts = str.Split('\\');
        var result = new double[expectedCount];
        for (int i = 0; i < Math.Min(parts.Length, expectedCount); i++)
        {
            double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]);
        }
        return result;
    }

    private static void SetDoubleValues(DicomDataset ds, DicomTag tag, double[] values)
    {
        var str = string.Join("\\",
            values.Select(v => v.ToString("F10", CultureInfo.InvariantCulture)));
        ds.AddOrUpdate(tag, str);
    }

    private static string GetRelativePath(string dirPath, string rootPath)
    {
        var dir = dirPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (dir.Equals(root, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (dir.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            return dir[(root.Length + 1)..];

        // Fallback: files directly in root
        return dir;
    }

    /// <summary>
    /// Determine the common root directory from the scanned file paths.
    /// </summary>
    private static string GetSourceRoot(List<DicomFileInfo> files, string currentPath)
    {
        if (files.Count == 0)
            return Path.GetDirectoryName(currentPath) ?? string.Empty;

        var dirs = files
            .Select(f => Path.GetDirectoryName(f.SourcePath))
            .Where(d => d != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (dirs.Count <= 1)
            return dirs.FirstOrDefault() ?? Path.GetDirectoryName(currentPath) ?? string.Empty;

        // Find common prefix
        var first = dirs[0]!.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var commonParts = new List<string>(first);

        for (int i = 1; i < dirs.Count; i++)
        {
            var parts = dirs[i]!.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (int j = 0; j < commonParts.Count && j < parts.Length; j++)
            {
                if (!string.Equals(commonParts[j], parts[j], StringComparison.OrdinalIgnoreCase))
                {
                    commonParts = commonParts.Take(j).ToList();
                    break;
                }
            }
            if (commonParts.Count > parts.Length)
                commonParts = commonParts.Take(parts.Length).ToList();
        }

        return string.Join(Path.DirectorySeparatorChar.ToString(), commonParts);
    }
}
