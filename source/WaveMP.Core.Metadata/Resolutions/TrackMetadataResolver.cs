using WaveMP.Core.Metadata.Artists;
using WaveMP.Core.Metadata.Paths;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Metadata.Texts;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// Combines the metadata of a track field by field: tags first, then the folder and file name, and the file name as
/// title if nothing else is known. The artists and album artists come from the multi-values of the tags, otherwise from
/// their single text split at unambiguous separators (see <see cref="ArtistNames"/>). A title from the tags loses a
/// leading artist name as <see cref="TitleCleaner"/> describes, if <c>titleCleaning</c> asks for it.
/// </summary>
public static class TrackMetadataResolver
{
    public static ResolvedTrackMetadata Resolve(
        MediaTags tags,
        PathMetadata path,
        TitleCleaningMode titleCleaning = TitleCleaningMode.None)
    {
        (string title, TrackMetadataSource source) = tags.Title is { } tagTitle
            ? (TitleCleaner.Clean(tagTitle, tags.Artist, path.Artist, titleCleaning), TrackMetadataSource.Tags)
            : (path.Title, path.IsTitleFromPattern ? TrackMetadataSource.Path : TrackMetadataSource.FileName);

        // The artist of the folder becomes the album artist (e.g. a sampler named after its folder) only if the tags
        // name neither an album artist nor another artist. It never does with the compilation flag: without an album
        // artist tag, such a track belongs to the compilation of its folder (see AlbumResolver).
        bool hasAlbumArtistTag = tags.AlbumArtist is not null || tags.AlbumArtistValues.Count > 0;
        string? albumArtist = tags.AlbumArtist
            ?? (!hasAlbumArtistTag
                && !tags.IsCompilation
                && (tags.Artist is null || tags.Artist.Equals(path.Artist, StringComparison.OrdinalIgnoreCase))
                    ? path.AlbumArtist
                    : null);
        bool hasArtistTag = tags.Artist is not null || tags.ArtistValues.Count > 0;
        IReadOnlyList<ArtistName> artists = hasArtistTag
            ? ArtistNames.Resolve(
                tags.ArtistValues,
                tags.Artist,
                IdsOf(tags.MusicBrainzArtistIds, tags.MusicBrainzArtistId))
            : ArtistNames.Split(path.Artist);
        IReadOnlyList<ArtistName> albumArtists = hasAlbumArtistTag
            ? ArtistNames.Resolve(
                tags.AlbumArtistValues,
                tags.AlbumArtist,
                IdsOf(tags.MusicBrainzAlbumArtistIds, tags.MusicBrainzAlbumArtistId))
            : ArtistNames.Split(albumArtist);

        return new ResolvedTrackMetadata(
            title,
            source,
            tags.Artist ?? (hasArtistTag ? ArtistNames.DisplayText(artists) : path.Artist),
            albumArtist ?? (hasAlbumArtistTag ? ArtistNames.DisplayText(albumArtists) : null),
            tags.Album ?? path.Album,
            tags.TrackNumber ?? path.TrackNumber,
            tags.DiscNumber ?? path.DiscNumber,
            tags.Year ?? path.Year,
            tags.Genre,
            tags.IsCompilation,
            // An id only belongs to a name from the tags, never to one taken from the folder or file name.
            hasArtistTag ? MusicBrainzIds.Single(tags.MusicBrainzArtistId) : null,
            hasAlbumArtistTag ? MusicBrainzIds.Single(tags.MusicBrainzAlbumArtistId) : null,
            tags.Album is null ? null : MusicBrainzIds.Single(tags.MusicBrainzReleaseId),
            artists,
            [.. albumArtists.Select(artist => artist with { Role = ArtistRole.Main })]);
    }

    /// <summary>
    /// The MusicBrainz artists of a tag: all of them where the tags name several, otherwise the single one.
    /// </summary>
    private static IReadOnlyList<Guid> IdsOf(IReadOnlyList<string> all, string? single)
    {
        Guid[] ids = [.. all.Select(MusicBrainzIds.Single).OfType<Guid>()];
        return ids.Length > 0 || MusicBrainzIds.Single(single) is not { } id ? ids : [id];
    }
}
