using PMTALL.Models;

namespace PMTALL.Services;

public enum TopLevelStrategy
{
    YearMonthPatient,
    DirectPatient
}

public enum PatientFolderNaming
{
    Id_Name,
    Name_Id,
    IdOnly,
    NameOnly,
    AnonymousSequence,
    DateSequence
}

public interface IClassifyService
{
    /// <summary>
    /// Build an operation plan to classify DICOM files into organized folder structure.
    /// </summary>
    OperationPlan BuildClassifyPlan(
        List<DicomFileInfo> files,
        string outputRoot,
        TopLevelStrategy topStrategy,
        PatientFolderNaming namingRule,
        bool enableSortWithinPatient);

    /// <summary>
    /// Build an operation plan to classify DICOM files by Patient → Date → Modality (CT/CBCT).
    /// Detects CBCT via Manufacturer's Model Name when modality is CT.
    /// RT objects (RTSTRUCT, REG, RTDOS, RTPLAN) are placed with their associated imaging series.
    /// Original filenames are preserved.
    /// </summary>
    OperationPlan BuildClassifyPlanByPatientDateModality(
        List<DicomFileInfo> files,
        string outputRoot,
        bool enableSortByTime);

    /// <summary>
    /// Special date mode: uses RT files' date tags (e.g. StructureSetDate, RTPlanDate, etc.)
    /// instead of corrupted StudyDate as folder naming base.
    /// RT files are matched to CT/CBCT via references, and the RT's date becomes the date folder.
    /// Unreferenced CT/CBCT fall back to their StudyDate with a warning marker.
    /// </summary>
    OperationPlan BuildClassifyPlanSpecialDateMode(
        List<DicomFileInfo> files,
        string outputRoot,
        bool enableSortByTime,
        SpecialDateConfig dateConfig);
}
