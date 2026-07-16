using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using PMTALL.Models;

namespace PMTALL.Services;

public class ExportService : IExportService
{
    public Task ExportToExcelAsync(StatisticsReport report, string filePath)
    {
        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Statistics");

            // Title
            ws.Cell(1, 1).Value = "DICOM Toolkits - Statistics Report";
            ws.Range(1, 1, 1, 4).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0x00B4D8);
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.White;

            int row = 3;
            AddStatRow(ws, row++, "Total Patients", report.TotalPatients);
            AddStatRow(ws, row++, "Total Studies", report.TotalStudies);
            AddStatRow(ws, row++, "Total Series", report.TotalSeries);
            AddStatRow(ws, row++, "Total Files", report.TotalFiles);
            AddStatRow(ws, row++, "Valid Files", report.ValidFiles);
            AddStatRow(ws, row++, "Invalid Files", report.InvalidFiles);
            row++;

            AddStatRow(ws, row++, "Gender - Male", report.MaleCount);
            AddStatRow(ws, row++, "Gender - Female", report.FemaleCount);
            AddStatRow(ws, row++, "Gender - Unknown", report.GenderUnknownCount);
            row++;

            if (report.AverageAge.HasValue)
                AddStatRow(ws, row++, "Average Age (years)", $"{report.AverageAge:F1} (from {report.AgeComputedCount} patients)");
            row++;

            AddStatRow(ws, row++, "Total RTPLAN Instances", report.TotalRtPlanCount);
            AddStatRow(ws, row++, "Total RTDOSE Instances", report.TotalRtDoseCount);
            AddStatRow(ws, row++, "Total RTSTRUCT Instances", report.TotalRtStructCount);
            row++;

            AddStatRow(ws, row++, "Total Folder Size", FormatFileSize(report.TotalFolderSizeBytes));
            AddStatRow(ws, row++, "Dose Files Size", FormatFileSize(report.DoseFilesSizeBytes));
            row++;

            AddStatRow(ws, row++, "Average Studies/Patient", report.AverageStudiesPerPatient);
            AddStatRow(ws, row++, "Average Instances/Patient", report.AverageInstancesPerPatient);
            row++;

            AddStatRow(ws, row++, "Earliest Study Date", report.EarliestStudyDate ?? "N/A");
            AddStatRow(ws, row++, "Latest Study Date", report.LatestStudyDate ?? "N/A");
            row++;

            AddStatRow(ws, row++, "Missing Key Tags Count", report.MissingKeyTagsCount);
            AddStatRow(ws, row++, "Anonymization", $"{report.AnonymizationPercent}% ({report.FilesWithPatientName} files have PatientName)");
            row++;

            // Modality distribution
            ws.Cell(row, 1).Value = "Modality Distribution";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            foreach (var kvp in report.ModalityCounts.OrderByDescending(k => k.Value))
            {
                AddStatRow(ws, row++, $"  {kvp.Key}", kvp.Value);
            }
            row++;

            // Manufacturer distribution
            ws.Cell(row, 1).Value = "Manufacturer / Device Distribution";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            foreach (var kvp in report.ManufacturerCounts.OrderByDescending(k => k.Value))
            {
                AddStatRow(ws, row++, $"  {kvp.Key}", kvp.Value);
            }

            // Auto-fit columns
            ws.Columns(1, 2).AdjustToContents();

            workbook.SaveAs(filePath);
        });
    }

    public Task ExportToJsonAsync(StatisticsReport report, string filePath)
    {
        return Task.Run(() =>
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var json = JsonSerializer.Serialize(report, options);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        });
    }

    public Task ExportToCsvAsync(StatisticsReport report, string filePath)
    {
        return Task.Run(() =>
        {
            using var sw = new StreamWriter(filePath, false, Encoding.UTF8);
            // BOM for UTF-8
            sw.Write('﻿');

            sw.WriteLine("Metric,Value");
            WriteCsvLine(sw, "Total Patients", report.TotalPatients);
            WriteCsvLine(sw, "Total Studies", report.TotalStudies);
            WriteCsvLine(sw, "Total Series", report.TotalSeries);
            WriteCsvLine(sw, "Total Files", report.TotalFiles);
            WriteCsvLine(sw, "Valid Files", report.ValidFiles);
            WriteCsvLine(sw, "Invalid Files", report.InvalidFiles);
            WriteCsvLine(sw, "Male", report.MaleCount);
            WriteCsvLine(sw, "Female", report.FemaleCount);
            WriteCsvLine(sw, "Gender Unknown", report.GenderUnknownCount);
            if (report.AverageAge.HasValue)
                WriteCsvLine(sw, "Average Age (years)", $"{report.AverageAge:F1}");
            WriteCsvLine(sw, "Total RTPLAN", report.TotalRtPlanCount);
            WriteCsvLine(sw, "Total RTDOSE", report.TotalRtDoseCount);
            WriteCsvLine(sw, "Total RTSTRUCT", report.TotalRtStructCount);
            WriteCsvLine(sw, "Total Folder Size", FormatFileSize(report.TotalFolderSizeBytes));
            WriteCsvLine(sw, "Dose Files Size", FormatFileSize(report.DoseFilesSizeBytes));
            WriteCsvLine(sw, "Avg Studies/Patient", report.AverageStudiesPerPatient);
            WriteCsvLine(sw, "Avg Instances/Patient", report.AverageInstancesPerPatient);
            WriteCsvLine(sw, "Earliest Study Date", report.EarliestStudyDate ?? "N/A");
            WriteCsvLine(sw, "Latest Study Date", report.LatestStudyDate ?? "N/A");
            WriteCsvLine(sw, "Missing Key Tags", report.MissingKeyTagsCount);
            WriteCsvLine(sw, "Anonymization %", $"{report.AnonymizationPercent}%");

            foreach (var kvp in report.ModalityCounts.OrderByDescending(k => k.Value))
                WriteCsvLine(sw, $"Modality: {kvp.Key}", kvp.Value);

            foreach (var kvp in report.ManufacturerCounts.OrderByDescending(k => k.Value))
                WriteCsvLine(sw, $"Manufacturer: {kvp.Key}", kvp.Value);
        });
    }

    private static void AddStatRow(IXLWorksheet ws, int row, string label, object value)
    {
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 2).Value = value.ToString() ?? "";
        // Alternate row background
        if (row % 2 == 0)
        {
            ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromArgb(0x2A, 0x2A, 0x3E);
        }
    }

    private static void WriteCsvLine(StreamWriter sw, string label, object value)
    {
        sw.WriteLine($"\"{label}\",\"{value}\"");
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
