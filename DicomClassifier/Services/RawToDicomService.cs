using System.Globalization;
using DicomClassifier.Models;
using FellowOakDicom;

namespace DicomClassifier.Services;

public class RawToDicomService : IRawToDicomService
{
    public short[] ReadSlice(string rawFilePath, int sliceIndex, RawConversionConfig config)
    {
        var sliceOffset = config.HeaderOffset + (long)sliceIndex * config.SliceSizeBytes;
        var pixels = new short[config.Width * config.Height];

        using var fs = new FileStream(rawFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        fs.Seek(sliceOffset, SeekOrigin.Begin);

        // Read the entire slice as bytes, then convert according to endianness.
        var bytes = new byte[config.SliceSizeBytes];
        fs.ReadExactly(bytes, 0, bytes.Length);

        if (config.IsLittleEndian == BitConverter.IsLittleEndian)
        {
            Buffer.BlockCopy(bytes, 0, pixels, 0, bytes.Length);
        }
        else
        {
            // Big-endian raw data on little-endian system: swap each pair
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (short)((bytes[i * 2] << 8) | bytes[i * 2 + 1]);
            }
        }

        return pixels;
    }

    public async Task<List<string>> ConvertAsync(
        RawConversionConfig config,
        string outputDirectory,
        IProgress<ProgressReport>? progress = null)
    {
        var sliceCount = config.SliceCount;
        var outputPaths = new List<string>(sliceCount);

        // Generate shared UIDs once for all slices
        var studyInstanceUid = DicomUID.Generate().UID;
        var seriesInstanceUid = DicomUID.Generate().UID;
        var frameOfReferenceUid = DicomUID.Generate().UID;

        Directory.CreateDirectory(outputDirectory);

        await Task.Run(() =>
        {
            for (int i = 0; i < sliceCount; i++)
            {
                var sliceIndex = config.SliceStart + i;

                // Read current slice pixel data
                var pixels = ReadSlice(config.RawFilePath, sliceIndex, config);

                // Build DicomDataset
                var dataset = BuildDataset(config, pixels, sliceIndex,
                    studyInstanceUid, seriesInstanceUid, frameOfReferenceUid);

                // Write DICOM file
                var instanceNumber = sliceIndex + 1;
                var fileName = $"CT_{config.PatientId}_{instanceNumber:D4}.dcm";
                var outputPath = Path.Combine(outputDirectory, fileName);

                var dicomFile = new DicomFile(dataset);
                dicomFile.Save(outputPath);
                outputPaths.Add(outputPath);

                // Report progress every 10 slices
                if ((i + 1) % 10 == 0 || i == sliceCount - 1)
                {
                    progress?.Report(new ProgressReport
                    {
                        Current = i + 1,
                        Total = sliceCount,
                        Status = $"Converting... {i + 1}/{sliceCount} (slice {sliceIndex + 1} of {config.Depth})"
                    });
                }
            }
        });

        return outputPaths;
    }

    /// <summary>
    /// Build a complete DicomDataset for a single CT slice.
    /// </summary>
    private static DicomDataset BuildDataset(
        RawConversionConfig config,
        short[] pixels,
        int sliceIndex,
        string studyInstanceUid,
        string seriesInstanceUid,
        string frameOfReferenceUid)
    {
        var ds = new DicomDataset();

        // --- Patient Module ---
        ds.AddOrUpdate(DicomTag.PatientID, config.PatientId);
        ds.AddOrUpdate(DicomTag.PatientName, config.PatientName);
        ds.AddOrUpdate(DicomTag.PatientSex, config.PatientSex);
        if (!string.IsNullOrEmpty(config.PatientBirthDate))
            ds.AddOrUpdate(DicomTag.PatientBirthDate, config.PatientBirthDate);

        // --- General Study Module ---
        ds.AddOrUpdate(DicomTag.StudyInstanceUID, studyInstanceUid);
        ds.AddOrUpdate(DicomTag.StudyDate, config.StudyDate);
        if (!string.IsNullOrEmpty(config.StudyDate))
            ds.AddOrUpdate(DicomTag.StudyDate, config.StudyDate);
        ds.AddOrUpdate(DicomTag.StudyID, config.StudyId);
        if (!string.IsNullOrEmpty(config.StudyDescription))
            ds.AddOrUpdate(DicomTag.StudyDescription, config.StudyDescription);

        // --- General Series Module ---
        ds.AddOrUpdate(DicomTag.Modality, "CT");
        ds.AddOrUpdate(DicomTag.SeriesInstanceUID, seriesInstanceUid);
        ds.AddOrUpdate(DicomTag.SeriesNumber, "1");

        // --- Frame of Reference Module ---
        ds.AddOrUpdate(DicomTag.FrameOfReferenceUID, frameOfReferenceUid);

        // --- General Equipment Module ---
        ds.AddOrUpdate(DicomTag.Manufacturer, "DicomToolkits");
        ds.AddOrUpdate(DicomTag.ManufacturerModelName, "RawConverter");

        // --- General Image Module ---
        ds.AddOrUpdate(DicomTag.SOPClassUID, DicomUID.CTImageStorage.UID);
        ds.AddOrUpdate(DicomTag.SOPInstanceUID, DicomUID.Generate().UID);
        ds.AddOrUpdate(DicomTag.InstanceNumber, (sliceIndex + 1).ToString());
        ds.AddOrUpdate(DicomTag.ImageType, @"ORIGINAL\PRIMARY\AXIAL");
        ds.AddOrUpdate(DicomTag.AcquisitionDate, config.StudyDate);

        // --- Image Plane Module ---
        var sliceLocation = config.SliceLocationStart + sliceIndex * config.SliceThickness;
        ds.AddOrUpdate(DicomTag.PixelSpacing,
            $"{config.PixelSpacingRow:F6}\\{config.PixelSpacingCol:F6}");
        ds.AddOrUpdate(DicomTag.SliceThickness, config.SliceThickness.ToString("F6", CultureInfo.InvariantCulture));
        ds.AddOrUpdate(DicomTag.SliceLocation, sliceLocation.ToString("F6", CultureInfo.InvariantCulture));
        ds.AddOrUpdate(DicomTag.ImageOrientationPatient, config.ImageOrientationPatient);
        ds.AddOrUpdate(DicomTag.ImagePositionPatient,
            $"0.000000\\0.000000\\{sliceLocation:F6}");

        // --- Image Pixel Module ---
        ds.AddOrUpdate(DicomTag.SamplesPerPixel, (ushort)1);
        ds.AddOrUpdate(DicomTag.PhotometricInterpretation, "MONOCHROME2");
        ds.AddOrUpdate(DicomTag.Rows, (ushort)config.Height);
        ds.AddOrUpdate(DicomTag.Columns, (ushort)config.Width);
        ds.AddOrUpdate(DicomTag.BitsAllocated, (ushort)config.BitsAllocated);
        ds.AddOrUpdate(DicomTag.BitsStored, (ushort)config.BitsStored);
        ds.AddOrUpdate(DicomTag.HighBit, (ushort)config.HighBit);
        ds.AddOrUpdate(DicomTag.PixelRepresentation, (ushort)config.PixelRepresentation);

        // --- CT Image Module ---
        ds.AddOrUpdate(DicomTag.RescaleIntercept, config.RescaleIntercept.ToString("F6", CultureInfo.InvariantCulture));
        ds.AddOrUpdate(DicomTag.RescaleSlope, config.RescaleSlope.ToString("F6", CultureInfo.InvariantCulture));
        ds.AddOrUpdate(DicomTag.RescaleType, "HU");

        // --- VOI LUT Module ---
        ds.AddOrUpdate(DicomTag.WindowCenter,
            config.WindowCenter.ToString("F6", CultureInfo.InvariantCulture));
        ds.AddOrUpdate(DicomTag.WindowWidth,
            config.WindowWidth.ToString("F6", CultureInfo.InvariantCulture));

        // --- Equipment ---
        ds.AddOrUpdate(DicomTag.KVP, config.Kvp.ToString("F6", CultureInfo.InvariantCulture));

        // --- Pixel Data ---
        // DicomTag.PixelData has VR OB (Other Byte). fo-dicom v5.1.3 requires byte[].
        var pixelBytes = new byte[pixels.Length * sizeof(short)];
        Buffer.BlockCopy(pixels, 0, pixelBytes, 0, pixelBytes.Length);
        ds.AddOrUpdate(DicomTag.PixelData, pixelBytes);

        return ds;
    }
}
