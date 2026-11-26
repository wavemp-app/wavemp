using WaveMP.Data.Texts;

namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// Decides which albums are compilations: an album is one if at least one of its tracks is marked as part of a
/// compilation in its tags (ID3 <c>TCMP</c>, MP4 <c>cpil</c>, Vorbis comment <c>COMPILATION</c>) or if its artist is
/// "Various Artists", compared like all names (by its normalized name, see <see cref="NameKeys"/>).
/// </summary>
public static class AlbumCompilations
{
    /// <summary>
    /// The album artist that marks an album as a compilation.
    /// </summary>
    public const string VariousArtists = "Various Artists";

    /// <summary>
    /// The normalized name of <see cref="VariousArtists"/>, as artists are compared by (see <see cref="NameKeys"/>).
    /// </summary>
    public static readonly string VariousArtistsKey = NameKeys.Of(VariousArtists)!;
}
