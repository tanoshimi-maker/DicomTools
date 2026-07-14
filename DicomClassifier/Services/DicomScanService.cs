using DicomClassifier.Helpers;
using DicomClassifier.Models;
using FellowOakDicom;

namespace DicomClassifier.Services;

public class DicomScanService : IDicomScanService
{
    public Task<List<DicomFileInfo>> ScanDirectoryAsync(string directoryPath,
        IProgress<ProgressReport>? progress = null)
    {
        return ScanFilesAsync(directoryPath, false, progress);
    }

    public Task<List<DicomFileInfo>> ScanTreeAsync(string rootPath,
        IProgress<ProgressReport>? progress = null)
    {
        return ScanFilesAsync(rootPath, true, progress);
    }

    private async Task<List<DicomFileInfo>> ScanFilesAsync(string path, bool recursive,
        IProgress<ProgressReport>? progress = null)
    {
        var results = new List<DicomFileInfo>();
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        // Enumerate ALL files. We cannot rely on file extension — many DICOM files
        // (e.g. "CT_1.2.246.352.221.48467085...") have dots but no .dcm extension.
        // DicomFile.Open + try-catch handles rejection of non-DICOM files.
        var allFiles = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", searchOption))
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

                    var acquisitionDate = DicomTagHelper.GetString(ds, DicomTag.AcquisitionDate);
                    var acquisitionTime = DicomTagHelper.GetString(ds, DicomTag.AcquisitionTime);
                    var contentDate = DicomTagHelper.GetString(ds, DicomTag.ContentDate);
                    var contentTime = DicomTagHelper.GetString(ds, DicomTag.ContentTime);
                    var seriesDate = DicomTagHelper.GetString(ds, DicomTag.SeriesDate);
                    var seriesTime = DicomTagHelper.GetString(ds, DicomTag.SeriesTime);
                    var studyDate = DicomTagHelper.GetString(ds, DicomTag.StudyDate);
                    // Fallback to InstanceCreationDate when StudyDate is empty
                    if (string.IsNullOrEmpty(studyDate))
                        studyDate = DicomTagHelper.GetString(ds, DicomTag.InstanceCreationDate);
                    var studyTime = DicomTagHelper.GetString(ds, DicomTag.StudyTime);

                    var info = new DicomFileInfo
                    {
                        SourcePath = filePath,
                        PatientId = DicomTagHelper.GetString(ds, DicomTag.PatientID),
                        ManufacturerModelName = DicomTagHelper.GetString(ds, DicomTag.ManufacturerModelName),
                        PatientName = DicomTagHelper.GetString(ds, DicomTag.PatientName),
                        StudyInstanceUid = DicomTagHelper.GetString(ds, DicomTag.StudyInstanceUID),
                        StudyDate = studyDate,
                        StudyTime = studyTime,
                        SeriesInstanceUid = DicomTagHelper.GetString(ds, DicomTag.SeriesInstanceUID),
                        SopInstanceUid = DicomTagHelper.GetString(ds, DicomTag.SOPInstanceUID),
                        SeriesDate = seriesDate,
                        SeriesTime = seriesTime,
                        Modality = DicomTagHelper.GetString(ds, DicomTag.Modality),
                        AcquisitionDate = acquisitionDate,
                        AcquisitionTime = acquisitionTime,
                        ContentDate = contentDate,
                        ContentTime = contentTime,
                        FrameOfReferenceUid = DicomTagHelper.GetString(ds, DicomTag.FrameOfReferenceUID),
                        AcquisitionTimestamp = DicomTagHelper.GetBestTimestamp(
                            acquisitionDate, acquisitionTime,
                            contentDate, contentTime,
                            seriesDate, seriesTime,
                            studyDate, studyTime),
                        IsValid = true,
                        OriginalLastWriteTime = File.GetLastWriteTime(filePath),
                        Dataset = ds
                    };

                    info.ReferencedSopInstanceUid = DicomTagHelper.GetReferencedSopInstanceUids(ds).FirstOrDefault();

                    results.Add(info);
                }
                catch (Exception ex)
                {
                    results.Add(new DicomFileInfo
                    {
                        SourcePath = filePath,
                        IsValid = false,
                        ErrorMessage = $"Parse error: {ex.Message}",
                        OriginalLastWriteTime = File.GetLastWriteTime(filePath)
                    });
                }

                if ((i + 1) % 10 == 0 || i == total - 1)
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
}
