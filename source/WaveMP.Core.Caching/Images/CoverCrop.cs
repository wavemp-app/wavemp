namespace WaveMP.Core.Caching.Images;

/// <summary>
/// A square section of a picture as fractions of its size, e.g. from the cropper of a client: the left edge at
/// <see cref="X"/> times the width, the top edge at <see cref="Y"/> times the height and the side
/// <see cref="Size"/> times the shorter side. Values outside the picture are clamped, so the section always lies in
/// the picture.
/// </summary>
/// <param name="X">Left edge, 0 to 1 of the width.</param>
/// <param name="Y">Top edge, 0 to 1 of the height.</param>
/// <param name="Size">Side, more than 0 and at most 1 of the shorter side.</param>
public sealed record CoverCrop(double X, double Y, double Size)
{
    /// <summary>
    /// Whether all values are finite and in their range.
    /// </summary>
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Size)
        && X is >= 0 and <= 1 && Y is >= 0 and <= 1 && Size is > 0 and <= 1;
}
