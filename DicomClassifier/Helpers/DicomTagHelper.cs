using FellowOakDicom;

namespace DicomClassifier.Helpers;

/// <summary>
/// Helper methods for extracting DICOM tag values with proper fallback logic.
/// </summary>
public static class DicomTagHelper
{
    public static string? GetString(DicomDataset? ds, DicomTag tag)
    {
        if (ds == null) return null;
        return ds.TryGetString(tag, out var val) ? val : null;
    }

    /// <summary>
    /// Parse a DICOM date string (YYYYMMDD) into DateTime.
    /// </summary>
    public static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr) || dateStr.Length < 8)
            return null;
        if (DateTime.TryParseExact(dateStr[..8], "yyyyMMdd", null,
                System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    /// <summary>
    /// Parse a DICOM time string (HHMMSS.ffffff) into TimeSpan.
    /// </summary>
    public static TimeSpan? ParseTime(string? timeStr)
    {
        if (string.IsNullOrEmpty(timeStr)) return null;
        var clean = timeStr.Split('.')[0].PadRight(6, '0');
        if (clean.Length > 6) clean = clean[..6];
        if (TimeSpan.TryParseExact(clean, "hhmmss", null, out var ts))
            return ts;
        return null;
    }

    /// <summary>
    /// Combine DICOM date and time into DateTime.
    /// </summary>
    public static DateTime? CombineDateAndTime(string? dateStr, string? timeStr)
    {
        var date = ParseDate(dateStr);
        var time = ParseTime(timeStr);
        if (date == null) return null;
        return date.Value + (time ?? TimeSpan.Zero);
    }

    /// <summary>
    /// Get the best available acquisition timestamp with fallback chain.
    /// Priority: AcquisitionDateTime > ContentDateTime > SeriesDateTime > StudyDateTime
    /// </summary>
    public static DateTime? GetBestTimestamp(
        string? acquisitionDate, string? acquisitionTime,
        string? contentDate, string? contentTime,
        string? seriesDate, string? seriesTime,
        string? studyDate, string? studyTime)
    {
        // Acquisition DateTime
        var ts = CombineDateAndTime(acquisitionDate, acquisitionTime);
        if (ts != null) return ts;

        // Content DateTime
        ts = CombineDateAndTime(contentDate, contentTime);
        if (ts != null) return ts;

        // Series DateTime
        ts = CombineDateAndTime(seriesDate, seriesTime);
        if (ts != null) return ts;

        // Study DateTime
        ts = CombineDateAndTime(studyDate, studyTime);
        return ts;
    }

    /// <summary>
    /// Check if the dataset refers to a specific SOP instance (for RT binding).
    /// </summary>
    public static List<string> GetReferencedSopInstanceUids(DicomDataset? ds)
    {
        var results = new List<string>();
        if (ds == null) return results;

        // Check Referenced Series Sequence (3006,0010) or Referenced SOP Sequence
        if (ds.TryGetSequence(DicomTag.ReferencedStudySequence, out var refStudySeq))
        {
            foreach (var item in refStudySeq)
            {
                if (item.TryGetSequence(DicomTag.ReferencedSeriesSequence, out var refSeriesSeq))
                {
                    foreach (var seriesItem in refSeriesSeq)
                    {
                        if (seriesItem.TryGetSequence(DicomTag.ReferencedSOPSequence, out var refSopSeq))
                        {
                            foreach (var sopItem in refSopSeq)
                            {
                                var uid = GetString(sopItem, DicomTag.ReferencedSOPInstanceUID);
                                if (uid != null) results.Add(uid);
                            }
                        }
                    }
                }
            }
        }

        // Direct Referenced SOP in RT Structure Set
        if (ds.TryGetSequence(new DicomTag(0x3006, 0x0010), out var refFrameOfRefSeq))
        {
            foreach (var item in refFrameOfRefSeq)
            {
                if (item.TryGetSequence(new DicomTag(0x3006, 0x0012), out var rtRefStudySeq))
                {
                    foreach (var studyItem in rtRefStudySeq)
                    {
                        if (studyItem.TryGetSequence(new DicomTag(0x3006, 0x0014), out var rtRefSeriesSeq))
                        {
                            foreach (var seriesItem in rtRefSeriesSeq)
                            {
                                if (seriesItem.TryGetSequence(new DicomTag(0x3006, 0x0016), out var rtRefSopSeq))
                                {
                                    foreach (var sopItem in rtRefSopSeq)
                                    {
                                        var uid = GetString(sopItem, DicomTag.ReferencedSOPInstanceUID);
                                        if (uid != null) results.Add(uid);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // For RT Plan, check Referenced SOP directly
        if (ds.TryGetSequence(DicomTag.ReferencedSOPSequence, out var refSopSeq2))
        {
            foreach (var item in refSopSeq2)
            {
                var uid = GetString(item, DicomTag.ReferencedSOPInstanceUID);
                if (uid != null) results.Add(uid);
            }
        }

        return results;
    }
}
