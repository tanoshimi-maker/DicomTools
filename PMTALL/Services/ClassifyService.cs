using System.Text.RegularExpressions;
using PMTALL.Helpers;
using PMTALL.Models;
using FellowOakDicom;

namespace PMTALL.Services;

public class ClassifyService : IClassifyService
{
    public OperationPlan BuildClassifyPlan(
        List<DicomFileInfo> files,
        string outputRoot,
        TopLevelStrategy topStrategy,
        PatientFolderNaming namingRule,
        bool enableSortWithinPatient)
    {
        var plan = new OperationPlan { IsInPlace = false };

        // Step 1: Group valid files by PatientId
        var validFiles = files.Where(f => f.IsValid).ToList();
        var invalidFiles = files.Where(f => !f.IsValid).ToList();

        var patientGroups = new Dictionary<string, List<DicomFileInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in validFiles)
        {
            var key = f.PatientId ?? $"ANON_{Guid.NewGuid():N}";
            if (!patientGroups.ContainsKey(key))
                patientGroups[key] = new List<DicomFileInfo>();
            patientGroups[key].Add(f);
        }

        // Build patient info objects
        var patients = new List<PatientInfo>();
        foreach (var kvp in patientGroups)
        {
            var patient = new PatientInfo
            {
                PatientId = kvp.Key,
                PatientName = kvp.Value.First().PatientName
            };

            // Group by Study
            var studyGroups = kvp.Value
                .GroupBy(f => f.StudyInstanceUid ?? $"NO_STUDY_{Guid.NewGuid():N}");

            foreach (var studyGrp in studyGroups)
            {
                var first = studyGrp.First();
                var study = new StudyInfo
                {
                    StudyInstanceUid = studyGrp.Key,
                    StudyDate = first.StudyDate,
                    StudyTime = first.StudyTime
                };

                // Group by Series
                var seriesGroups = studyGrp
                    .GroupBy(f => f.SeriesInstanceUid ?? $"NO_SERIES_{Guid.NewGuid():N}");

                foreach (var seriesGrp in seriesGroups)
                {
                    var sFirst = seriesGrp.First();
                    var series = new SeriesInfo
                    {
                        SeriesInstanceUid = seriesGrp.Key,
                        SeriesDate = sFirst.SeriesDate,
                        SeriesTime = sFirst.SeriesTime,
                        Modality = sFirst.Modality,
                        FrameOfReferenceUid = sFirst.FrameOfReferenceUid,
                        Files = seriesGrp.ToList()
                    };

                    // Extract referenced SOP UIDs from files in this series
                    foreach (var sf in series.Files)
                    {
                        if (sf.ReferencedSopInstanceUid != null)
                            series.ReferencedSopInstanceUids.Add(sf.ReferencedSopInstanceUid);
                    }

                    study.Series[seriesGrp.Key] = series;
                }

                patient.Studies[studyGrp.Key] = study;
            }

            // Earliest study date for ordering
            patient.EarliestStudyDate = patient.Studies.Values
                .Where(s => s.StudyDate != null)
                .Select(s => s.StudyDate)
                .OrderBy(d => d)
                .FirstOrDefault();

            patients.Add(patient);
        }

        // Sort patients by earliest study date
        patients = patients
            .OrderBy(p => p.EarliestStudyDate != null ? p.EarliestStudyDate : "99999999")
            .ToList();

        // Step 2: Generate folder paths and file operations
        var allOperations = new List<FileOperation>();
        var studyDateCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var patient in patients)
        {
            string patientFolderName = GetPatientFolderName(patient, namingRule, patients.IndexOf(patient));

            // For each study in the patient
            foreach (var studyKvp in patient.Studies)
            {
                var study = studyKvp.Value;
                string yearMonth = "";

                if (topStrategy == TopLevelStrategy.YearMonthPatient)
                {
                    var year = FileNameHelper.GetYear(study.StudyDate);
                    var month = FileNameHelper.GetMonth(study.StudyDate);
                    yearMonth = Path.Combine(year, month);
                }

                // Study folder name: YYMMDD with dedup counter
                var shortDate = FileNameHelper.FormatDateShort(study.StudyDate);
                var studyFolderKey = $"{patientFolderName}/{shortDate}";
                if (!studyDateCounters.ContainsKey(studyFolderKey))
                    studyDateCounters[studyFolderKey] = 0;
                var studyCounter = studyDateCounters[studyFolderKey]++;
                var studyFolderName = studyCounter > 0 ? $"{shortDate}_{studyCounter}" : shortDate;

                string studyBasePath;
                if (topStrategy == TopLevelStrategy.YearMonthPatient)
                    studyBasePath = Path.Combine(outputRoot, yearMonth, patientFolderName, studyFolderName);
                else
                    studyBasePath = Path.Combine(outputRoot, patientFolderName, studyFolderName);

                allOperations.Add(new FileOperation
                {
                    Type = OperationType.CreateDirectory,
                    DestinationPath = studyBasePath
                });

                // Handle series binding: RT objects to CT series
                var ctSeries = study.Series.Values
                    .Where(s => s.IsCtSeries)
                    .ToList();

                var processedSeries = new HashSet<string>();

                foreach (var seriesKvp in study.Series)
                {
                    var series = seriesKvp.Value;

                    if (processedSeries.Contains(seriesKvp.Key))
                        continue;

                    // Determine target series folder
                    string seriesFolderName;

                    if (series.IsRtObject && ctSeries.Count > 0)
                    {
                        // Try to bind to a CT series
                        var targetCt = FindMatchingCtSeries(series, ctSeries);
                        if (targetCt != null)
                        {
                            // Place in the CT series folder
                            seriesFolderName = GetSeriesFolderName(targetCt);
                            var ctFolderPath = Path.Combine(studyBasePath, seriesFolderName);
                            allOperations.Add(new FileOperation
                            {
                                Type = OperationType.CreateDirectory,
                                DestinationPath = ctFolderPath
                            });

                            // Add RT files to CT folder
                            foreach (var file in series.Files)
                            {
                                var destPath = Path.Combine(ctFolderPath, file.FileName);
                                allOperations.Add(new FileOperation
                                {
                                    Type = OperationType.Copy,
                                    SourcePath = file.SourcePath,
                                    DestinationPath = destPath
                                });
                            }

                            processedSeries.Add(seriesKvp.Key);
                            continue;
                        }
                    }

                    // Normal series folder
                    seriesFolderName = GetSeriesFolderName(series);
                    var seriesPath = Path.Combine(studyBasePath, seriesFolderName);
                    allOperations.Add(new FileOperation
                    {
                        Type = OperationType.CreateDirectory,
                        DestinationPath = seriesPath
                    });

                    // Add files
                    if (enableSortWithinPatient && series.Files.Count > 1)
                    {
                        var sortedFiles = series.Files
                            .Where(f => f.AcquisitionTimestamp.HasValue)
                            .OrderBy(f => f.AcquisitionTimestamp!.Value)
                            .ToList();

                        var unsortedFiles = series.Files
                            .Where(f => !f.AcquisitionTimestamp.HasValue)
                            .ToList();

                        for (int i = 0; i < sortedFiles.Count; i++)
                        {
                            var prefix = FileNameHelper.GetAlphaPrefix(i, sortedFiles.Count);
                            var newName = prefix + sortedFiles[i].FileName;
                            var destPath = Path.Combine(seriesPath, newName);
                            allOperations.Add(new FileOperation
                            {
                                Type = OperationType.Copy,
                                SourcePath = sortedFiles[i].SourcePath,
                                DestinationPath = destPath
                            });
                        }

                        foreach (var uf in unsortedFiles)
                        {
                            var destPath = Path.Combine(seriesPath, uf.FileName);
                            allOperations.Add(new FileOperation
                            {
                                Type = OperationType.Copy,
                                SourcePath = uf.SourcePath,
                                DestinationPath = destPath
                            });
                        }
                    }
                    else
                    {
                        foreach (var file in series.Files)
                        {
                            var destPath = Path.Combine(seriesPath, file.FileName);
                            allOperations.Add(new FileOperation
                            {
                                Type = OperationType.Copy,
                                SourcePath = file.SourcePath,
                                DestinationPath = destPath
                            });
                        }
                    }

                    processedSeries.Add(seriesKvp.Key);
                }
            }
        }

        // Handle invalid files
        if (invalidFiles.Count > 0)
        {
            var unclassifiedDir = Path.Combine(outputRoot, "_Unclassified");
            allOperations.Add(new FileOperation
            {
                Type = OperationType.CreateDirectory,
                DestinationPath = unclassifiedDir
            });

            foreach (var invalid in invalidFiles)
            {
                allOperations.Add(new FileOperation
                {
                    Type = OperationType.Copy,
                    SourcePath = invalid.SourcePath,
                    DestinationPath = Path.Combine(unclassifiedDir, invalid.FileName)
                });
                plan.Errors.Add($"[{invalid.FileName}] {invalid.ErrorMessage}");
            }
        }

        // Deduplicate and finalize
        plan.Operations = allOperations
            .GroupBy(o => $"{o.Type}:{o.DestinationPath}:{o.SourcePath}")
            .Select(g => g.First())
            .ToList();

        plan.FileCount = validFiles.Count;
        plan.DirectoryCount = plan.Operations.Count(o => o.Type == OperationType.CreateDirectory);

        return plan;
    }

    private static string GetPatientFolderName(PatientInfo patient, PatientFolderNaming rule, int index)
    {
        var id = FileNameHelper.SanitizeForPath(patient.PatientId);
        var name = FileNameHelper.SanitizeForPath(patient.PatientName);

        return rule switch
        {
            PatientFolderNaming.Id_Name => $"{id}_{name}",
            PatientFolderNaming.Name_Id => $"{name}_{id}",
            PatientFolderNaming.IdOnly => id,
            PatientFolderNaming.NameOnly => name,
            PatientFolderNaming.AnonymousSequence => (index + 1).ToString("D4"),
            PatientFolderNaming.DateSequence => FileNameHelper.FormatDateLong(patient.EarliestStudyDate),
            _ => $"{id}_{name}"
        };
    }

    private static string GetSeriesFolderName(SeriesInfo series)
    {
        var modality = series.Modality ?? "UNKNOWN";
        var date = FileNameHelper.FormatDateShort(series.SeriesDate);
        return $"{modality}_{date}";
    }

    private static SeriesInfo? FindMatchingCtSeries(SeriesInfo rtSeries, List<SeriesInfo> ctSeries)
    {
        // Try matching by Frame of Reference UID first
        if (rtSeries.FrameOfReferenceUid != null)
        {
            var match = ctSeries.FirstOrDefault(ct =>
                ct.FrameOfReferenceUid == rtSeries.FrameOfReferenceUid);
            if (match != null) return match;
        }

        // Try matching by referenced SOP UIDs
        foreach (var refUid in rtSeries.ReferencedSopInstanceUids)
        {
            var match = ctSeries.FirstOrDefault(ct =>
                ct.Files.Any(f =>
                    f.SopInstanceUid == refUid ||
                    f.SeriesInstanceUid == refUid));
            if (match != null) return match;
        }

        // If there's exactly one CT series, bind to it
        if (ctSeries.Count == 1)
            return ctSeries[0];

        return null;
    }

    // ================================================================
    // New classification: Patient → Date → Modality (CT / CBCT)
    // ================================================================

    public OperationPlan BuildClassifyPlanByPatientDateModality(
        List<DicomFileInfo> files,
        string outputRoot,
        bool enableSortByTime)
    {
        var plan = new OperationPlan { IsInPlace = false };

        var validFiles = files.Where(f => f.IsValid).ToList();
        var invalidFiles = files.Where(f => !f.IsValid).ToList();

        // Group by PatientId
        var patientGroups = new Dictionary<string, List<DicomFileInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in validFiles)
        {
            var key = f.PatientId ?? $"ANON_{Guid.NewGuid():N}";
            if (!patientGroups.ContainsKey(key))
                patientGroups[key] = new List<DicomFileInfo>();
            patientGroups[key].Add(f);
        }

        var patients = new List<PatientInfo>();
        foreach (var kvp in patientGroups)
        {
            var patient = new PatientInfo
            {
                PatientId = kvp.Key,
                PatientName = kvp.Value.First().PatientName
            };

            // Group by StudyInstanceUid
            var studyGroups = kvp.Value
                .GroupBy(f => f.StudyInstanceUid ?? $"NO_STUDY_{Guid.NewGuid():N}");

            foreach (var studyGrp in studyGroups)
            {
                var first = studyGrp.First();
                var study = new StudyInfo
                {
                    StudyInstanceUid = studyGrp.Key,
                    StudyDate = first.StudyDate,
                    StudyTime = first.StudyTime
                };

                // Group by Series
                var seriesGroups = studyGrp
                    .GroupBy(f => f.SeriesInstanceUid ?? $"NO_SERIES_{Guid.NewGuid():N}");

                foreach (var seriesGrp in seriesGroups)
                {
                    var sFirst = seriesGrp.First();
                    var series = new SeriesInfo
                    {
                        SeriesInstanceUid = seriesGrp.Key,
                        SeriesDate = sFirst.SeriesDate,
                        SeriesTime = sFirst.SeriesTime,
                        Modality = sFirst.Modality,
                        FrameOfReferenceUid = sFirst.FrameOfReferenceUid,
                        Files = seriesGrp.ToList()
                    };

                    foreach (var sf in series.Files)
                    {
                        if (sf.ReferencedSopInstanceUid != null)
                            series.ReferencedSopInstanceUids.Add(sf.ReferencedSopInstanceUid);
                    }

                    study.Series[seriesGrp.Key] = series;
                }

                patient.Studies[studyGrp.Key] = study;
            }

            patient.EarliestStudyDate = patient.Studies.Values
                .Where(s => s.StudyDate != null)
                .Select(s => s.StudyDate)
                .OrderBy(d => d)
                .FirstOrDefault();

            patients.Add(patient);
        }

        // Sort patients by earliest study date
        patients = patients
            .OrderBy(p => p.EarliestStudyDate != null ? p.EarliestStudyDate : "99999999")
            .ToList();

        // Build operations
        var allOperations = new List<FileOperation>();
        var seriesFolderMapping = new Dictionary<string, string>();

        foreach (var patient in patients)
        {
            string patientFolder = FileNameHelper.SanitizeForPath(patient.PatientId);
            var patientPath = Path.Combine(outputRoot, patientFolder);
            allOperations.Add(CreateDirOp(patientPath));

            // Sort studies by date for consistent ordering
            var orderedStudies = patient.Studies.Values
                .OrderBy(s => s.StudyDate ?? "99999999")
                .ToList();

            foreach (var study in orderedStudies)
            {
                // If StudyDate is missing, use a fallback folder name
                var dateFolder = FileNameHelper.FormatDateLong(study.StudyDate);
                if (dateFolder == "00000000")
                    dateFolder = "UnknownDate_" + Guid.NewGuid().ToString("N")[..6];
                var datePath = Path.Combine(patientPath, dateFolder);
                allOperations.Add(CreateDirOp(datePath));

                // Classify series within this study
                var ctSeriesList = new List<SeriesInfo>();
                var cbctSeriesList = new List<SeriesInfo>();
                var imageSeriesList = new List<SeriesInfo>();
                var rtSeriesList = new List<SeriesInfo>();
                var otherSeriesList = new List<SeriesInfo>();

                foreach (var series in study.Series.Values)
                {
                    if (series.IsCbct)
                        cbctSeriesList.Add(series);
                    else if (series.IsCtSeries)
                        ctSeriesList.Add(series);
                    else if (series.Modality is "MR" or "PT" or "PET" or "SPECT" or "US" or "XA" or "DX" or "CR")
                        imageSeriesList.Add(series);
                    else if (series.IsRtObject)
                        rtSeriesList.Add(series);
                    else
                        otherSeriesList.Add(series);
                }

                // Process imaging series → CT and CBCT folders
                var processedSeries = new HashSet<string>();

                // Process CT series
                foreach (var ctSeries in ctSeriesList)
                {
                    var ctFolderPath = Path.Combine(datePath, "CT");
                    allOperations.Add(CreateDirOp(ctFolderPath));
                    AddFilesToFolder(ctSeries, ctFolderPath, allOperations, enableSortByTime);
                    seriesFolderMapping[ctSeries.SeriesInstanceUid!] = ctFolderPath;
                    processedSeries.Add(ctSeries.SeriesInstanceUid!);
                }

                // Process CBCT series
                foreach (var cbctSeries in cbctSeriesList)
                {
                    var cbctFolderPath = Path.Combine(datePath, "CBCT");
                    allOperations.Add(CreateDirOp(cbctFolderPath));
                    AddFilesToFolder(cbctSeries, cbctFolderPath, allOperations, enableSortByTime);
                    seriesFolderMapping[cbctSeries.SeriesInstanceUid!] = cbctFolderPath;
                    processedSeries.Add(cbctSeries.SeriesInstanceUid!);
                }

                // Process other imaging series → their own modality folders
                foreach (var imgSeries in imageSeriesList)
                {
                    var modalityFolder = imgSeries.Modality ?? "OTHER";
                    var imgFolderPath = Path.Combine(datePath, modalityFolder);
                    allOperations.Add(CreateDirOp(imgFolderPath));
                    AddFilesToFolder(imgSeries, imgFolderPath, allOperations, enableSortByTime);
                    seriesFolderMapping[imgSeries.SeriesInstanceUid!] = imgFolderPath;
                    processedSeries.Add(imgSeries.SeriesInstanceUid!);
                }

                // Bind RT series to CT or CBCT
                var allImageSeries = new List<SeriesInfo>();
                allImageSeries.AddRange(ctSeriesList);
                allImageSeries.AddRange(cbctSeriesList);

                foreach (var rtSeries in rtSeriesList)
                {
                    var targetSeries = FindMatchingCtSeries(rtSeries, allImageSeries);
                    if (targetSeries != null)
                    {
                        string rtFolderName = targetSeries.IsCbct ? "CBCT" : "CT";
                        var rtFolderPath = Path.Combine(datePath, rtFolderName);
                        // Directory already created above, just add files
                        AddFilesToFolder(rtSeries, rtFolderPath, allOperations, enableSortByTime);
                        seriesFolderMapping[rtSeries.SeriesInstanceUid!] = rtFolderPath;
                    }
                    else
                    {
                        // Unbound RT → put in UnReference folder
                        var rtFolderPath = Path.Combine(datePath, "UnReference");
                        allOperations.Add(CreateDirOp(rtFolderPath));
                        AddFilesToFolder(rtSeries, rtFolderPath, allOperations, enableSortByTime);
                    }
                    processedSeries.Add(rtSeries.SeriesInstanceUid!);
                }

                // Other modalities
                foreach (var otherSeries in otherSeriesList)
                {
                    var modalityFolder = otherSeries.Modality ?? "OTHER";
                    var otherFolderPath = Path.Combine(datePath, modalityFolder);
                    allOperations.Add(CreateDirOp(otherFolderPath));
                    AddFilesToFolder(otherSeries, otherFolderPath, allOperations, enableSortByTime);
                    processedSeries.Add(otherSeries.SeriesInstanceUid!);
                }
            }
        }

        // ================================================================
        // Handle .dir index files: match to series or study-level folder
        // ================================================================
        var dirFiles = invalidFiles
            .Where(f => Path.GetExtension(f.SourcePath).Equals(".dir", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var trulyInvalidFiles = invalidFiles
            .Where(f => !Path.GetExtension(f.SourcePath).Equals(".dir", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dirFiles.Count > 0)
        {
            foreach (var dirFile in dirFiles)
            {
                var dirFileName = Path.GetFileName(dirFile.SourcePath);
                var matched = false;

                // Pattern 1: {Modality}_{SeriesUID}.dir — e.g. CT_1.2.246.xxx.dir
                var seriesMatch = Regex.Match(dirFileName, @"^(\w+)_([\d\.]+)\.dir$");
                if (seriesMatch.Success)
                {
                    var uid = seriesMatch.Groups[2].Value;
                    if (seriesFolderMapping.TryGetValue(uid, out var folderPath))
                    {
                        allOperations.Add(new FileOperation
                        {
                            Type = OperationType.Copy,
                            SourcePath = dirFile.SourcePath,
                            DestinationPath = Path.Combine(folderPath, dirFileName)
                        });
                        matched = true;
                    }
                }

                // Pattern 2: RTDIR.dir — multi-series directory, match by reading content
                if (!matched && string.Equals(dirFileName, "RTDIR.dir", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var lines = File.ReadAllLines(dirFile.SourcePath);
                        foreach (var line in lines)
                        {
                            var uidCandidate = line.Trim();
                            // Looks like a UID: only digits and dots, reasonably long
                            if (uidCandidate.Length > 20 && uidCandidate.Contains('.') &&
                                uidCandidate.All(c => char.IsDigit(c) || c == '.'))
                            {
                                if (seriesFolderMapping.TryGetValue(uidCandidate, out var seriesPath))
                                {
                                    var dateFolderPath = Path.GetDirectoryName(seriesPath)!;
                                    allOperations.Add(new FileOperation
                                    {
                                        Type = OperationType.Copy,
                                        SourcePath = dirFile.SourcePath,
                                        DestinationPath = Path.Combine(dateFolderPath, dirFileName)
                                    });
                                    matched = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch { /* read error, fall through to unclassified */ }
                }

                if (!matched)
                {
                    trulyInvalidFiles.Add(dirFile);
                }
            }
        }

        // Handle truly invalid files (non-DICOM, corrupted, unmatched .dir, etc.)
        if (trulyInvalidFiles.Count > 0)
        {
            var unclassifiedDir = Path.Combine(outputRoot, "_Unclassified");
            allOperations.Add(CreateDirOp(unclassifiedDir));
            foreach (var invalid in trulyInvalidFiles)
            {
                allOperations.Add(new FileOperation
                {
                    Type = OperationType.Copy,
                    SourcePath = invalid.SourcePath,
                    DestinationPath = Path.Combine(unclassifiedDir, invalid.FileName)
                });
                plan.Errors.Add($"[{invalid.FileName}] {invalid.ErrorMessage}");
            }
        }

        // Deduplicate
        plan.Operations = allOperations
            .GroupBy(o => $"{o.Type}:{o.DestinationPath}:{o.SourcePath}")
            .Select(g => g.First())
            .ToList();

        plan.FileCount = validFiles.Count;
        plan.DirectoryCount = plan.Operations.Count(o => o.Type == OperationType.CreateDirectory);

        return plan;
    }

    // ================================================================
    // Special date mode: use RT files' date tags as classification base
    // ================================================================

    public OperationPlan BuildClassifyPlanSpecialDateMode(
        List<DicomFileInfo> files,
        string outputRoot,
        bool enableSortByTime,
        SpecialDateConfig dateConfig)
    {
        var plan = new OperationPlan { IsInPlace = false };

        var validFiles = files.Where(f => f.IsValid).ToList();
        var invalidFiles = files.Where(f => !f.IsValid).ToList();

        // Group by PatientId
        var patientGroups = new Dictionary<string, List<DicomFileInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in validFiles)
        {
            var key = f.PatientId ?? $"ANON_{Guid.NewGuid():N}";
            if (!patientGroups.ContainsKey(key))
                patientGroups[key] = new List<DicomFileInfo>();
            patientGroups[key].Add(f);
        }

        var allOperations = new List<FileOperation>();
        var seriesFolderMapping = new Dictionary<string, string>();

        foreach (var kvp in patientGroups)
        {
            string patientFolder = FileNameHelper.SanitizeForPath(kvp.Key);
            var patientPath = Path.Combine(outputRoot, patientFolder);
            allOperations.Add(CreateDirOp(patientPath));

            // Group ALL files by SeriesInstanceUid (ignore corrupted StudyInstanceUid)
            var seriesGroups = kvp.Value
                .GroupBy(f => f.SeriesInstanceUid ?? $"NO_SERIES_{Guid.NewGuid():N}");

            var allSeries = new List<SeriesInfo>();
            foreach (var seriesGrp in seriesGroups)
            {
                var sFirst = seriesGrp.First();
                var series = new SeriesInfo
                {
                    SeriesInstanceUid = seriesGrp.Key,
                    SeriesDate = sFirst.SeriesDate,
                    SeriesTime = sFirst.SeriesTime,
                    Modality = sFirst.Modality,
                    FrameOfReferenceUid = sFirst.FrameOfReferenceUid,
                    Files = seriesGrp.ToList()
                };

                foreach (var sf in series.Files)
                {
                    if (sf.ReferencedSopInstanceUid != null)
                        series.ReferencedSopInstanceUids.Add(sf.ReferencedSopInstanceUid);
                }

                allSeries.Add(series);
            }

            // Separate RT series from non-RT series
            var rtSeriesList = allSeries.Where(s => s.IsRtObject).ToList();
            var nonRtSeriesList = allSeries.Where(s => !s.IsRtObject).ToList();

            // Extract the selected date tag value from each RT series
            var rtDateMap = new Dictionary<string, string>(); // SeriesInstanceUid → date string
            foreach (var rt in rtSeriesList)
            {
                string? dateValue = null;
                foreach (var file in rt.Files)
                {
                    if (file.Dataset != null)
                    {
                        dateValue = DicomTagHelper.GetString(file.Dataset, dateConfig.Tag);
                        if (!string.IsNullOrEmpty(dateValue))
                            break;
                    }
                }
                if (!string.IsNullOrEmpty(dateValue) && dateValue.Length >= 8)
                    rtDateMap[rt.SeriesInstanceUid!] = dateValue;
            }

            // Classify non-RT series
            var ctSeriesList = nonRtSeriesList.Where(s => s.IsCtSeries).ToList();
            var cbctSeriesList = nonRtSeriesList.Where(s => s.IsCbct).ToList();
            var imageSeriesList = nonRtSeriesList.Where(s =>
                s.Modality is "MR" or "PT" or "PET" or "SPECT" or "US" or "XA" or "DX" or "CR").ToList();
            var otherSeriesList = nonRtSeriesList.Where(s =>
                !s.IsCtSeries && !s.IsCbct &&
                !(s.Modality is "MR" or "PT" or "PET" or "SPECT" or "US" or "XA" or "DX" or "CR")).ToList();

            var allImageSeries = new List<SeriesInfo>();
            allImageSeries.AddRange(ctSeriesList);
            allImageSeries.AddRange(cbctSeriesList);

            // Match each RT series to CT/CBCT, group by RT date
            // dateGroupMap: date string → list of series (RT + their matched CT/CBCT)
            var dateGroupMap = new Dictionary<string, List<SeriesInfo>>();
            var referencedImageUids = new HashSet<string>(); // SeriesInstanceUids of matched images

            foreach (var rt in rtSeriesList)
            {
                if (!rtDateMap.TryGetValue(rt.SeriesInstanceUid!, out var dateStr))
                    continue;

                if (!dateGroupMap.ContainsKey(dateStr))
                    dateGroupMap[dateStr] = new List<SeriesInfo>();

                dateGroupMap[dateStr].Add(rt);

                // Find CT/CBCT referenced by this RT
                var match = FindMatchingCtSeries(rt, allImageSeries);
                if (match != null && !referencedImageUids.Contains(match.SeriesInstanceUid!))
                {
                    dateGroupMap[dateStr].Add(match);
                    referencedImageUids.Add(match.SeriesInstanceUid!);
                }
            }

            // RT series without extractable date
            var rtNoDateInitial = rtSeriesList
                .Where(s => !rtDateMap.ContainsKey(s.SeriesInstanceUid!))
                .ToList();

            // CT/CBCT not matched to any RT
            var unreferencedInitial = allImageSeries
                .Where(s => !referencedImageUids.Contains(s.SeriesInstanceUid!))
                .ToList();

            // ================================================================
            // Integrity check: inherit date via StudyInstanceUid
            // Series sharing StudyInstanceUid with an already-assigned series
            // inherit the same date folder, even without the selected tag.
            // ================================================================
            var assignedStudyUids = new HashSet<string>();
            foreach (var group in dateGroupMap.Values)
                foreach (var s in group)
                {
                    var uid = s.Files.FirstOrDefault()?.StudyInstanceUid;
                    if (uid != null) assignedStudyUids.Add(uid);
                }

            // RT files without the date tag → inherit via StudyInstanceUid
            foreach (var rt in rtNoDateInitial.ToList())
            {
                var studyUid = rt.Files.FirstOrDefault()?.StudyInstanceUid;
                if (studyUid != null && assignedStudyUids.Contains(studyUid))
                {
                    foreach (var dg in dateGroupMap)
                    {
                        if (dg.Value.Any(s =>
                            s.Files.Any(f => f.StudyInstanceUid == studyUid)))
                        {
                            dg.Value.Add(rt);
                            rtNoDateInitial.Remove(rt);
                            break;
                        }
                    }
                }
            }

            // Unreferenced CT/CBCT → inherit via StudyInstanceUid
            foreach (var img in unreferencedInitial.ToList())
            {
                var studyUid = img.Files.FirstOrDefault()?.StudyInstanceUid;
                if (studyUid != null && assignedStudyUids.Contains(studyUid))
                {
                    foreach (var dg in dateGroupMap)
                    {
                        if (dg.Value.Any(s =>
                            s.Files.Any(f => f.StudyInstanceUid == studyUid)))
                        {
                            dg.Value.Add(img);
                            referencedImageUids.Add(img.SeriesInstanceUid!);
                            unreferencedInitial.Remove(img);
                            break;
                        }
                    }
                }
            }

            var rtNoDate = rtNoDateInitial;
            var unreferencedImages = unreferencedInitial;

            // Process each date group
            foreach (var dateGroup in dateGroupMap)
            {
                var dateFolder = FileNameHelper.FormatDateLong(dateGroup.Key);
                if (dateFolder == "00000000")
                    dateFolder = "UnknownDate_" + Guid.NewGuid().ToString("N")[..6];
                var datePath = Path.Combine(patientPath, dateFolder);
                allOperations.Add(CreateDirOp(datePath));

                // Classify series in this group by modality
                var groupCt = new List<SeriesInfo>();
                var groupCbct = new List<SeriesInfo>();
                var groupImg = new List<SeriesInfo>();
                var groupRt = new List<SeriesInfo>();
                var groupOther = new List<SeriesInfo>();

                foreach (var s in dateGroup.Value)
                {
                    if (s.IsCbct) groupCbct.Add(s);
                    else if (s.IsCtSeries) groupCt.Add(s);
                    else if (s.Modality is "MR" or "PT" or "PET" or "SPECT" or "US" or "XA" or "DX" or "CR")
                        groupImg.Add(s);
                    else if (s.IsRtObject) groupRt.Add(s);
                    else groupOther.Add(s);
                }

                // Process CT
                foreach (var s in groupCt)
                {
                    var folderPath = Path.Combine(datePath, "CT");
                    allOperations.Add(CreateDirOp(folderPath));
                    AddFilesToFolder(s, folderPath, allOperations, enableSortByTime);
                    seriesFolderMapping[s.SeriesInstanceUid!] = folderPath;
                }

                // Process CBCT
                foreach (var s in groupCbct)
                {
                    var folderPath = Path.Combine(datePath, "CBCT");
                    allOperations.Add(CreateDirOp(folderPath));
                    AddFilesToFolder(s, folderPath, allOperations, enableSortByTime);
                    seriesFolderMapping[s.SeriesInstanceUid!] = folderPath;
                }

                // Process other imaging
                foreach (var s in groupImg)
                {
                    var modalityFolder = s.Modality ?? "OTHER";
                    var folderPath = Path.Combine(datePath, modalityFolder);
                    allOperations.Add(CreateDirOp(folderPath));
                    AddFilesToFolder(s, folderPath, allOperations, enableSortByTime);
                    seriesFolderMapping[s.SeriesInstanceUid!] = folderPath;
                }

                // Bind RT series to CT/CBCT within this date group
                var groupImageSeries = new List<SeriesInfo>();
                groupImageSeries.AddRange(groupCt);
                groupImageSeries.AddRange(groupCbct);

                foreach (var rt in groupRt)
                {
                    var target = FindMatchingCtSeries(rt, groupImageSeries);
                    if (target != null)
                    {
                        var rtFolderName = target.IsCbct ? "CBCT" : "CT";
                        var rtFolderPath = Path.Combine(datePath, rtFolderName);
                        AddFilesToFolder(rt, rtFolderPath, allOperations, enableSortByTime);
                        seriesFolderMapping[rt.SeriesInstanceUid!] = rtFolderPath;
                    }
                    else
                    {
                        var rtFolderPath = Path.Combine(datePath, "UnReference");
                        allOperations.Add(CreateDirOp(rtFolderPath));
                        AddFilesToFolder(rt, rtFolderPath, allOperations, enableSortByTime);
                    }
                }

                // Other modalities
                foreach (var s in groupOther)
                {
                    var modalityFolder = s.Modality ?? "OTHER";
                    var folderPath = Path.Combine(datePath, modalityFolder);
                    allOperations.Add(CreateDirOp(folderPath));
                    AddFilesToFolder(s, folderPath, allOperations, enableSortByTime);
                    seriesFolderMapping[s.SeriesInstanceUid!] = folderPath;
                }
            }

            // RT series with no extractable date → UnReference folder under a "NoDate" group
            if (rtNoDate.Count > 0)
            {
                var noDatePath = Path.Combine(patientPath, "UnReference");
                allOperations.Add(CreateDirOp(noDatePath));
                foreach (var rt in rtNoDate)
                {
                    AddFilesToFolder(rt, noDatePath, allOperations, enableSortByTime);
                }
                plan.Warnings.Add($"[{string.Join(", ", rtNoDate.Select(r => r.Modality ?? "?"))}] No {dateConfig.DisplayName} tag found, placed in UnReference");
            }

            // Unreferenced CT/CBCT → fallback date with marker
            if (unreferencedImages.Count > 0)
            {
                foreach (var series in unreferencedImages)
                {
                    var fallbackDate = FileNameHelper.FormatDateLong(series.SeriesDate ?? series.Files.FirstOrDefault()?.StudyDate);
                    if (fallbackDate == "00000000")
                        fallbackDate = "UnknownDate_" + Guid.NewGuid().ToString("N")[..6];
                    else
                        fallbackDate += "_DateUnknown";

                    var datePath = Path.Combine(patientPath, fallbackDate);
                    allOperations.Add(CreateDirOp(datePath));

                    // Determine modality folder
                    string modalityFolder;
                    if (series.IsCbct) modalityFolder = "CBCT";
                    else if (series.IsCtSeries) modalityFolder = "CT";
                    else modalityFolder = series.Modality ?? "OTHER";

                    var folderPath = Path.Combine(datePath, modalityFolder);
                    allOperations.Add(CreateDirOp(folderPath));
                    AddFilesToFolder(series, folderPath, allOperations, enableSortByTime);
                    seriesFolderMapping[series.SeriesInstanceUid!] = folderPath;

                    plan.Warnings.Add($"[{series.Modality} {(series.SeriesInstanceUid?.Length > 20 ? series.SeriesInstanceUid[..20] + "..." : series.SeriesInstanceUid)}] Not referenced by any RT ({dateConfig.DisplayName}), date may be inaccurate");
                }
            }
        }

        // ================================================================
        // Handle .dir index files (same logic as BuildClassifyPlanByPatientDateModality)
        // ================================================================
        var dirFiles = invalidFiles
            .Where(f => Path.GetExtension(f.SourcePath).Equals(".dir", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var trulyInvalidFiles = invalidFiles
            .Where(f => !Path.GetExtension(f.SourcePath).Equals(".dir", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dirFiles.Count > 0)
        {
            foreach (var dirFile in dirFiles)
            {
                var dirFileName = Path.GetFileName(dirFile.SourcePath);
                var matched = false;

                var seriesMatch = Regex.Match(dirFileName, @"^(\w+)_([\d\.]+)\.dir$");
                if (seriesMatch.Success)
                {
                    var uid = seriesMatch.Groups[2].Value;
                    if (seriesFolderMapping.TryGetValue(uid, out var folderPath))
                    {
                        allOperations.Add(new FileOperation
                        {
                            Type = OperationType.Copy,
                            SourcePath = dirFile.SourcePath,
                            DestinationPath = Path.Combine(folderPath, dirFileName)
                        });
                        matched = true;
                    }
                }

                if (!matched && string.Equals(dirFileName, "RTDIR.dir", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var lines = File.ReadAllLines(dirFile.SourcePath);
                        foreach (var line in lines)
                        {
                            var uidCandidate = line.Trim();
                            if (uidCandidate.Length > 20 && uidCandidate.Contains('.') &&
                                uidCandidate.All(c => char.IsDigit(c) || c == '.'))
                            {
                                if (seriesFolderMapping.TryGetValue(uidCandidate, out var seriesPath))
                                {
                                    var dateFolderPath = Path.GetDirectoryName(seriesPath)!;
                                    allOperations.Add(new FileOperation
                                    {
                                        Type = OperationType.Copy,
                                        SourcePath = dirFile.SourcePath,
                                        DestinationPath = Path.Combine(dateFolderPath, dirFileName)
                                    });
                                    matched = true;
                                    break;
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (!matched)
                    trulyInvalidFiles.Add(dirFile);
            }
        }

        // Handle truly invalid files
        if (trulyInvalidFiles.Count > 0)
        {
            var unclassifiedDir = Path.Combine(outputRoot, "_Unclassified");
            allOperations.Add(CreateDirOp(unclassifiedDir));
            foreach (var invalid in trulyInvalidFiles)
            {
                allOperations.Add(new FileOperation
                {
                    Type = OperationType.Copy,
                    SourcePath = invalid.SourcePath,
                    DestinationPath = Path.Combine(unclassifiedDir, invalid.FileName)
                });
                plan.Errors.Add($"[{invalid.FileName}] {invalid.ErrorMessage}");
            }
        }

        // Deduplicate
        plan.Operations = allOperations
            .GroupBy(o => $"{o.Type}:{o.DestinationPath}:{o.SourcePath}")
            .Select(g => g.First())
            .ToList();

        plan.FileCount = validFiles.Count;
        plan.DirectoryCount = plan.Operations.Count(o => o.Type == OperationType.CreateDirectory);

        return plan;
    }

    private static void AddFilesToFolder(SeriesInfo series, string folderPath,
        List<FileOperation> operations, bool sortByTime)
    {
        if (sortByTime && series.Files.Count > 1)
        {
            var sortedFiles = series.Files
                .Where(f => f.AcquisitionTimestamp.HasValue)
                .OrderBy(f => f.AcquisitionTimestamp!.Value)
                .ToList();

            var unsortedFiles = series.Files
                .Where(f => !f.AcquisitionTimestamp.HasValue)
                .ToList();

            // Add prefix for ordering but preserve original filename
            for (int i = 0; i < sortedFiles.Count; i++)
            {
                // Only prefix when needed for ordering
                string destName = sortedFiles[i].FileName;
                // We keep original names — files are just copied as-is
                // The sort order is implicit in the copy order
                var destPath = Path.Combine(folderPath, destName);
                operations.Add(new FileOperation
                {
                    Type = OperationType.Copy,
                    SourcePath = sortedFiles[i].SourcePath,
                    DestinationPath = destPath
                });
            }

            foreach (var uf in unsortedFiles)
            {
                var destPath = Path.Combine(folderPath, uf.FileName);
                operations.Add(new FileOperation
                {
                    Type = OperationType.Copy,
                    SourcePath = uf.SourcePath,
                    DestinationPath = destPath
                });
            }
        }
        else
        {
            foreach (var file in series.Files)
            {
                var destPath = Path.Combine(folderPath, file.FileName);
                operations.Add(new FileOperation
                {
                    Type = OperationType.Copy,
                    SourcePath = file.SourcePath,
                    DestinationPath = destPath
                });
            }
        }
    }

    private static FileOperation CreateDirOp(string path)
    {
        return new FileOperation
        {
            Type = OperationType.CreateDirectory,
            DestinationPath = path
        };
    }
}
