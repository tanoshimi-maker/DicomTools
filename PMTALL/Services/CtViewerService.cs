using System.Collections.ObjectModel;
using System.Globalization;
using PMTALL.Helpers;
using PMTALL.Models;
using FellowOakDicom;

namespace PMTALL.Services;

/// <summary>
/// Implements DICOM folder scanning, tree building, and CT/MR volume loading.
/// </summary>
public class CtViewerService : ICtViewerService
{
    public async Task<List<DicomFileInfo>> ScanFolderAsync(string path,
        IProgress<ProgressReport>? progress = null)
    {
        var results = new List<DicomFileInfo>();

        var allFiles = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                allFiles.Add(file);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            results.Add(new DicomFileInfo
            {
                SourcePath = path,
                IsValid = false,
                ErrorMessage = $"Access denied: {ex.Message}"
            });
            return results;
        }

        var total = allFiles.Count;
        progress?.Report(new ProgressReport { Current = 0, Total = total, Status = $"Scanning {total} files..." });

        await Task.Run(() =>
        {
            for (int i = 0; i < total; i++)
            {
                var filePath = allFiles[i];
                try
                {
                    var dicomFile = DicomFile.Open(filePath);
                    var ds = dicomFile.Dataset;

                    var info = new DicomFileInfo
                    {
                        SourcePath = filePath,
                        PatientId = DicomTagHelper.GetString(ds, DicomTag.PatientID),
                        PatientName = DicomTagHelper.GetString(ds, DicomTag.PatientName),
                        StudyInstanceUid = DicomTagHelper.GetString(ds, DicomTag.StudyInstanceUID),
                        StudyDate = DicomTagHelper.GetString(ds, DicomTag.StudyDate),
                        SeriesInstanceUid = DicomTagHelper.GetString(ds, DicomTag.SeriesInstanceUID),
                        SopInstanceUid = DicomTagHelper.GetString(ds, DicomTag.SOPInstanceUID),
                        Modality = DicomTagHelper.GetString(ds, DicomTag.Modality),
                        OriginalLastWriteTime = File.GetLastWriteTime(filePath),
                        IsValid = true,
                        Dataset = ds
                    };

                    results.Add(info);
                }
                catch
                {
                    results.Add(new DicomFileInfo
                    {
                        SourcePath = filePath,
                        IsValid = false,
                        ErrorMessage = "Not a valid DICOM file",
                        OriginalLastWriteTime = File.GetLastWriteTime(filePath)
                    });
                }

                if ((i + 1) % 50 == 0 || i == total - 1)
                {
                    progress?.Report(new ProgressReport
                    {
                        Current = i + 1,
                        Total = total,
                        Status = $"Scanning... {i + 1}/{total}"
                    });
                }
            }
        });

        return results;
    }

    public ObservableCollection<DicomTreeNode> BuildFolderTree(List<DicomFileInfo> files)
    {
        var root = new ObservableCollection<DicomTreeNode>();

        // Only include valid, viewable (CT/MR) files
        var viewableFiles = files
            .Where(f => f.IsValid && f.Modality is "CT" or "MR")
            .ToList();

        // Group by directory
        var byDirectory = viewableFiles
            .GroupBy(f => Path.GetDirectoryName(f.SourcePath) ?? "Unknown")
            .OrderBy(g => g.Key);

        foreach (var dirGroup in byDirectory)
        {
            var dirNode = new DicomTreeNode
            {
                DisplayName = Path.GetFileName(dirGroup.Key) ?? dirGroup.Key,
                DetailText = $"{dirGroup.Count()} files",
                NodeType = "Folder",
                IsExpanded = false
            };

            // Group by Series
            var seriesGroups = dirGroup
                .GroupBy(f => f.SeriesInstanceUid)
                .OrderBy(g => g.Key);

            foreach (var seriesGroup in seriesGroups)
            {
                var first = seriesGroup.First();
                var modality = first.Modality ?? "?";
                var seriesNode = new SeriesTreeNode
                {
                    DisplayName = $"Series [{modality}]",
                    DetailText = $"{seriesGroup.Count()} slice(s)",
                    NodeType = "Series",
                    SeriesInstanceUid = seriesGroup.Key ?? "",
                    Modality = modality,
                    SliceCount = seriesGroup.Count(),
                    Files = seriesGroup.ToList(),
                    IsExpanded = false
                };

                // Add individual file nodes
                foreach (var f in seriesGroup.OrderBy(f => f.FileName))
                {
                    seriesNode.Children.Add(new DicomTreeNode
                    {
                        DisplayName = f.FileName,
                        DetailText = "",
                        NodeType = "File"
                    });
                }

                dirNode.Children.Add(seriesNode);
            }

            root.Add(dirNode);
        }

        return root;
    }

    public ObservableCollection<DicomTreeNode> BuildDicomHierarchyTree(List<DicomFileInfo> files)
    {
        var root = new ObservableCollection<DicomTreeNode>();

        var viewableFiles = files
            .Where(f => f.IsValid && f.Modality is "CT" or "MR")
            .ToList();

        // Patient → Study → Series → Files
        var byPatient = viewableFiles
            .GroupBy(f => f.PatientId ?? "Unknown Patient")
            .OrderBy(g => g.Key);

        foreach (var patientGroup in byPatient)
        {
            var patientName = patientGroup.First().PatientName ?? "";
            var patientNode = new DicomTreeNode
            {
                DisplayName = string.IsNullOrEmpty(patientName)
                    ? patientGroup.Key
                    : $"{patientGroup.Key} ({patientName})",
                DetailText = $"{patientGroup.Count()} files",
                NodeType = "Patient",
                IsExpanded = true
            };

            var byStudy = patientGroup
                .GroupBy(f => f.StudyInstanceUid)
                .OrderBy(g => g.First().StudyDate ?? "");

            foreach (var studyGroup in byStudy)
            {
                var studyDate = studyGroup.First().StudyDate ?? "";
                var studyNode = new DicomTreeNode
                {
                    DisplayName = string.IsNullOrEmpty(studyDate)
                        ? $"Study [{studyGroup.Key?[..Math.Min(8, studyGroup.Key?.Length ?? 0)]}...]"
                        : $"Study ({DicomTagHelper.ParseDate(studyDate)?.ToString("yyyy-MM-dd") ?? studyDate})",
                    DetailText = $"{studyGroup.Count()} files",
                    NodeType = "Study",
                    IsExpanded = true
                };

                var bySeries = studyGroup
                    .GroupBy(f => f.SeriesInstanceUid)
                    .OrderBy(g => g.Key);

                foreach (var seriesGroup in bySeries)
                {
                    var first = seriesGroup.First();
                    var modality = first.Modality ?? "?";
                    var seriesNode = new SeriesTreeNode
                    {
                        DisplayName = $"Series [{modality}]",
                        DetailText = $"{seriesGroup.Count()} slice(s)",
                        NodeType = "Series",
                        SeriesInstanceUid = seriesGroup.Key ?? "",
                        Modality = modality,
                        SliceCount = seriesGroup.Count(),
                        Files = seriesGroup.ToList(),
                        IsExpanded = false
                    };

                    foreach (var f in seriesGroup.OrderBy(f => f.FileName))
                    {
                        seriesNode.Children.Add(new DicomTreeNode
                        {
                            DisplayName = f.FileName,
                            DetailText = "",
                            NodeType = "File"
                        });
                    }

                    studyNode.Children.Add(seriesNode);
                }

                patientNode.Children.Add(studyNode);
            }

            root.Add(patientNode);
        }

        return root;
    }

    public async Task<CtVolume> LoadVolumeAsync(
        string seriesInstanceUid,
        List<DicomFileInfo> seriesFiles,
        IProgress<ProgressReport>? progress = null)
    {
        var volume = new CtVolume
        {
            SeriesInstanceUid = seriesInstanceUid,
            StudyInstanceUid = seriesFiles.FirstOrDefault()?.StudyInstanceUid ?? "",
            PatientId = seriesFiles.FirstOrDefault()?.PatientId ?? "",
            PatientName = seriesFiles.FirstOrDefault()?.PatientName ?? "",
            Modality = seriesFiles.FirstOrDefault(f => f.Modality != null)?.Modality ?? "?",
        };

        var slices = new List<CtSlice>();
        var validFiles = seriesFiles.Where(f => f.IsValid && f.Dataset != null).ToList();
        var total = validFiles.Count;

        for (int i = 0; i < total; i++)
        {
            var file = validFiles[i];
            var ds = file.Dataset!;

            try
            {
                var iop = DicomTagHelper.GetDoubleValues(ds, DicomTag.ImageOrientationPatient, 6);
                var ipp = DicomTagHelper.GetDoubleValues(ds, DicomTag.ImagePositionPatient, 3);
                var pixelSpacing = DicomTagHelper.GetDoubleValues(ds, DicomTag.PixelSpacing, 2);
                var rows = DicomTagHelper.GetIntValue(ds, DicomTag.Rows, 512);
                var cols = DicomTagHelper.GetIntValue(ds, DicomTag.Columns, 512);
                var sliceThickness = DicomTagHelper.GetDoubleValues(ds, DicomTag.SliceThickness, 1)[0];

                // Read pixel data
                // fo-dicom 5.1.3: PixelData has VR OB, TryGetValues returns byte[]
                var pixelData = ds.TryGetValues<byte>(DicomTag.PixelData, out var bytes)
                    ? bytes
                    : Array.Empty<byte>();

                var shortPixels = new short[rows * cols];
                if (bytes != null && bytes.Length >= rows * cols * 2)
                {
                    Buffer.BlockCopy(bytes, 0, shortPixels, 0, Math.Min(bytes.Length, shortPixels.Length * 2));
                }

                // Rescale
                double intercept = 0, slope = 1;
                var rescaleInterceptStr = DicomTagHelper.GetString(ds, DicomTag.RescaleIntercept);
                var rescaleSlopeStr = DicomTagHelper.GetString(ds, DicomTag.RescaleSlope);
                if (rescaleInterceptStr != null) double.TryParse(rescaleInterceptStr, NumberStyles.Float, CultureInfo.InvariantCulture, out intercept);
                if (rescaleSlopeStr != null) double.TryParse(rescaleSlopeStr, NumberStyles.Float, CultureInfo.InvariantCulture, out slope);

                // DICOM window/level
                double wc = 40, ww = 400;
                var wcStr = DicomTagHelper.GetString(ds, DicomTag.WindowCenter);
                var wwStr = DicomTagHelper.GetString(ds, DicomTag.WindowWidth);
                if (wcStr != null)
                {
                    // WindowCenter may contain multiple values (backslash-separated), take the first
                    var firstWc = wcStr.Split('\\')[0];
                    double.TryParse(firstWc, NumberStyles.Float, CultureInfo.InvariantCulture, out wc);
                }
                if (wwStr != null)
                {
                    var firstWw = wwStr.Split('\\')[0];
                    double.TryParse(firstWw, NumberStyles.Float, CultureInfo.InvariantCulture, out ww);
                }

                // Compute Z position along slice normal
                // For standard axial: normal = cross(rowVec, colVec) = (0,0,1), so Z = IPP[2]
                var rowVec = new[] { iop[0], iop[1], iop[2] };
                var colVec = new[] { iop[3], iop[4], iop[5] };
                var normal = new[]
                {
                    rowVec[1] * colVec[2] - rowVec[2] * colVec[1],
                    rowVec[2] * colVec[0] - rowVec[0] * colVec[2],
                    rowVec[0] * colVec[1] - rowVec[1] * colVec[0]
                };

                double zPos = ipp[0] * normal[0] + ipp[1] * normal[1] + ipp[2] * normal[2];

                var slice = new CtSlice
                {
                    SourcePath = file.SourcePath,
                    SopInstanceUid = file.SopInstanceUid ?? "",
                    ImagePositionPatient = ipp,
                    ImageOrientationPatient = iop,
                    PixelSpacing = pixelSpacing,
                    SliceThickness = sliceThickness,
                    Rows = rows,
                    Columns = cols,
                    Pixels = shortPixels,
                    RescaleIntercept = intercept,
                    RescaleSlope = slope,
                    WindowCenter = wc,
                    WindowWidth = ww,
                    ZPosition = zPos,
                    InstanceNumber = DicomTagHelper.GetIntValue(ds, DicomTag.InstanceNumber, i + 1)
                };

                slices.Add(slice);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading slice {file.SourcePath}: {ex.Message}");
            }

            if ((i + 1) % 50 == 0 || i == total - 1)
            {
                progress?.Report(new ProgressReport
                {
                    Current = i + 1,
                    Total = total,
                    Status = $"Loading slices... {i + 1}/{total}"
                });
            }
        }

        // Sort slices by Z position (inferior → superior)
        volume.Slices = slices.OrderBy(s => s.ZPosition).ToList();

        // Try to get series description from the first slice
        if (volume.Slices.Count > 0)
        {
            var firstFile = validFiles.FirstOrDefault();
            if (firstFile?.Dataset != null)
            {
                volume.SeriesDescription = DicomTagHelper.GetString(firstFile.Dataset, DicomTag.SeriesDescription) ?? "";
            }
        }

        return volume;
    }
}
