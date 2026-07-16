using PMTALL.Models;

namespace PMTALL.Helpers;

/// <summary>
/// Multi-Planar Reconstruction (MPR) helper.
/// Extracts coronal and sagittal planes from an axial CT/MR volume via direct orthogonal sampling.
/// </summary>
public static class MprReconstructor
{
    /// <summary>
    /// Extract the coronal (XZ) plane at a given Y row index.
    /// Samples the same row across all Z slices.
    /// Output dimensions: width = Columns, height = Depth.
    /// </summary>
    public static short[] BuildCoronalPlane(CtVolume volume, int yRowIndex)
    {
        var cols = volume.Columns;
        var depth = volume.Depth;
        var rows = volume.Rows;

        if (yRowIndex < 0 || yRowIndex >= rows)
            return Array.Empty<short>();

        var result = new short[depth * cols];

        for (int z = 0; z < depth; z++)
        {
            var slice = volume.Slices[z];
            var srcOffset = yRowIndex * cols;
            var dstOffset = z * cols;

            Array.Copy(slice.Pixels, srcOffset, result, dstOffset, cols);
        }

        return result;
    }

    /// <summary>
    /// Extract the sagittal (YZ) plane at a given X column index.
    /// Samples the same column across all Z slices.
    /// Output dimensions: width = Rows, height = Depth.
    /// </summary>
    public static short[] BuildSagittalPlane(CtVolume volume, int xColIndex)
    {
        var cols = volume.Columns;
        var depth = volume.Depth;
        var rows = volume.Rows;

        if (xColIndex < 0 || xColIndex >= cols)
            return Array.Empty<short>();

        var result = new short[depth * rows];

        for (int z = 0; z < depth; z++)
        {
            var slice = volume.Slices[z];
            var dstOffset = z * rows;

            for (int y = 0; y < rows; y++)
            {
                result[dstOffset + y] = slice.Pixels[y * cols + xColIndex];
            }
        }

        return result;
    }
}
