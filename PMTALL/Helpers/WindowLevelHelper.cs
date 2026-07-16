using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PMTALL.Helpers;

/// <summary>
/// Window/Level math extracted to a reusable helper.
/// Converts short[] pixel data to Gray8 byte[] / WriteableBitmap with configurable window, level, and rescale.
/// </summary>
public static class WindowLevelHelper
{
    /// <summary>
    /// Apply window/level and rescale to a short[] pixel buffer, producing a Gray8 byte[].
    /// </summary>
    public static byte[] ApplyWindowLevel(
        short[] pixels,
        double windowCenter,
        double windowWidth,
        double rescaleIntercept = 0.0,
        double rescaleSlope = 1.0)
    {
        double low = windowCenter - windowWidth / 2.0;
        double high = windowCenter + windowWidth / 2.0;

        var result = new byte[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            // Apply rescale to get HU (or the modality-equivalent unit)
            double hu = pixels[i] * rescaleSlope + rescaleIntercept;

            if (hu <= low)
                result[i] = 0;
            else if (hu >= high)
                result[i] = 255;
            else
                result[i] = (byte)((hu - low) / windowWidth * 255.0);
        }
        return result;
    }

    /// <summary>
    /// Create a Gray8 WriteableBitmap from short[] pixel data with window/level applied.
    /// </summary>
    public static WriteableBitmap CreateBitmap(
        short[] pixels,
        int width,
        int height,
        double windowCenter,
        double windowWidth,
        double rescaleIntercept = 0.0,
        double rescaleSlope = 1.0)
    {
        if (pixels.Length == 0)
            return new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);

        var gray8 = ApplyWindowLevel(pixels, windowCenter, windowWidth, rescaleIntercept, rescaleSlope);

        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), gray8, width, 0);
        return bitmap;
    }
}
