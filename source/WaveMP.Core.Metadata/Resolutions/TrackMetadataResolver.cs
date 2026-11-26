using WaveMP.Core.Metadata.Paths;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Metadata.Texts;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Metadata.Resolutions;

/// <summary>
/// Combines the metadata of a track field by field: tags first, then the folder and file name, and the file name as
/// title if nothing else is known. A title from the tags loses a leading artist name as <see cref="TitleCleaner"/>
/// describes, if <c>titleCleaning</c> asks for it.
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
        string? albumArtist = tags.AlbumArtist
            ?? (!tags.IsCompilation
                && (tags.Artist is null || tags.Artist.Equals(path.Artist, StringComparison.OrdinalIgnoreCase))
                    ? path.AlbumArtist
                    : null);

        return new ResolvedTrackMetadata(
            title,
            source,
            tags.Artist ?? path.Artist,
            albumArtist,
            tags.Album ?? path.Album,
            tags.TrackNumber ?? path.TrackNumber,
            tags.DiscNumber ?? path.DiscNumber,
            tags.Year ?? path.Year,
            tags.Genre,
            tags.IsCompilation,
            // An id only belongs to a name from the tags, never to one taken from the folder or file name.
            tags.Artist is null ? null : MusicBrainzIds.Single(tags.MusicBrainzArtistId),
            tags.AlbumArtist is null ? null : MusicBrainzIds.Single(tags.MusicBrainzAlbumArtistId),
            tags.Album is null ? null : MusicBrainzIds.Single(tags.MusicBrainzReleaseId));
    }
}
