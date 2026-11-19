using WaveMP.Data.Entities;

namespace WaveMP.Core.Library.Covers;

/// <summary>
/// The letter of a <see cref="CoverSource"/> in keys and file names: <c>f</c> for picture files, <c>e</c> for embedded
/// pictures and <c>o</c> for online covers.
/// </summary>
public static class CoverSourceCodes
{
    public static char Of(CoverSource source)
    {
        return source switch
        {
            CoverSource.File => 'f',
            CoverSource.Embedded => 'e',
            CoverSource.Online => 'o',
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown cover source."),
        };
    }
}
