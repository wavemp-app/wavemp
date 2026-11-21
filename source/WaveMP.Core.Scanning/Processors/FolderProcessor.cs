using WaveMP.Core.Metadata.Paths;
using WaveMP.Core.Metadata.Resolutions;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Scanning.Groupings;
using WaveMP.Core.Scanning.Qualities;
using WaveMP.Core.Scanning.Selections;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Processors;

/// <summary>
/// Derives the tracks of a folder from its media files, without touching the database: groups the files by base
/// name, chooses the files of the highest quality (or the files the admin selected), finds a file name pattern of the
/// folder, resolves the metadata and the album of every track (see <see cref="AlbumResolver"/>), and orders the covers
/// of every track.
/// </summary>
public static class FolderProcessor
{
    /// <param name="directoryPath">Folder relative to the media folder.</param>
    /// <param name="files">The media files of the folder.</param>
    /// <param name="choices">The files the admin selected, by the normalized base name of the track. A selected file
    /// that is not part of the track (or not of the category) is ignored, and the quality rule chooses.</param>
    /// <param name="sourceId">The media source of the folder; keeps albums without any artist apart from those of
    /// other sources.</param>
    public static ProcessedFolder Process(
        string directoryPath,
        IReadOnlyList<MediaFile> files,
        IReadOnlyDictionary<string, FileChoices>? choices = null,
        Guid? sourceId = null)
    {
        IReadOnlyList<FileGroup> groups = FileGrouper.Group(files);
        FileGroup[] trackGroups = [.. groups.Where(group => group.IsTrack)];
        FileNamePattern? pattern = FileNamePatternDetector.Detect([.. trackGroups.Select(group => group.BaseName)]);
        string[] segments = directoryPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        List<(FileGroup Group, FileChoices Choice, MediaFile? Audio, MediaFile? Video, ResolvedTrackMetadata Metadata)>
            tracks = [];
        foreach (FileGroup group in trackGroups)
        {
            FileChoices choice = choices?.GetValueOrDefault(group.NormalizedBaseName) ?? FileChoices.None;
            MediaFile? audio = ChooseAudio(group, choice.Audio);
            MediaFile? video = ChooseVideo(group, choice.Video);
            MediaFile source = audio ?? video!;
            PathMetadata path = PathMetadataParser.Parse(segments, group.BaseName, pattern);
            tracks.Add((group, choice, audio, video, TrackMetadataResolver.Resolve(ToMediaTags(source.Tags), path)));
        }

        IReadOnlyList<AlbumAssignment?> albums = AlbumResolver.Resolve(
            [.. tracks.Select(track => track.Metadata)],
            AlbumFolderOf(sourceId, directoryPath));

        return new ProcessedFolder(
            [.. tracks.Select((track, index) => new ProcessedTrack(
                track.Group,
                track.Metadata,
                albums[index],
                track.Audio,
                track.Video,
                OrderCovers(track.Group, track.Audio, track.Choice.Cover)))],
            [.. groups.Where(group => !group.IsTrack)]);
    }

    /// <summary>
    /// Identifies the album folder of a folder for albums without any artist: the folder above a disc folder such as
    /// <c>CD1</c>, otherwise the folder itself, in its media source. Albums with an artist ignore it.
    /// </summary>
    internal static string AlbumFolderOf(Guid? sourceId, string directoryPath)
    {
        string albumFolder = AlbumCoverCandidates.ParentOfDiscFolder(directoryPath) ?? directoryPath;
        return (sourceId ?? Guid.Empty).ToString("N") + "/" + albumFolder;
    }

    /// <summary>
    /// The audio file the track plays: the selected one, if it is an audio file of the group, otherwise the one of the
    /// highest quality.
    /// </summary>
    internal static MediaFile? ChooseAudio(FileGroup group, Guid? selected = null)
    {
        return Choose(group.Audio.Order(AudioQualityComparer.Instance), selected);
    }

    /// <summary>
    /// The video file the track plays: the selected one, if it is a video file of the group, otherwise the one of the
    /// highest quality.
    /// </summary>
    internal static MediaFile? ChooseVideo(FileGroup group, Guid? selected = null)
    {
        return Choose(group.Video.Order(VideoQualityComparer.Instance), selected);
    }

    /// <summary>
    /// Picture files of the group first (best quality first), then pictures embedded in the primary audio file, in
    /// the other audio files and in the video files. A selected cover that is one of them comes first.
    /// </summary>
    internal static IReadOnlyList<CoverCandidate> OrderCovers(
        FileGroup group,
        MediaFile? primaryAudio,
        Guid? selected = null)
    {
        IEnumerable<CoverCandidate> pictures = group.Images
            .Where(image => image.Issue is null)
            .Order(ImageQualityComparer.Instance)
            .Select(image => new CoverCandidate(
                image,
                CoverSource.File,
                image.Width,
                image.Height,
                MimeTypeOf(image.Extension)));

        IEnumerable<MediaFile> embedding = (primaryAudio is null ? [] : (MediaFile[])[primaryAudio])
            .Concat(group.Audio.Where(file => file != primaryAudio).Order(AudioQualityComparer.Instance))
            .Concat(group.Video.Order(VideoQualityComparer.Instance))
            .Where(file => file.HasEmbeddedPicture);
        IEnumerable<CoverCandidate> embedded = embedding.Select(file => new CoverCandidate(
            file,
            CoverSource.Embedded,
            file.EmbeddedPictureWidth,
            file.EmbeddedPictureHeight,
            file.EmbeddedPictureMimeType));

        List<CoverCandidate> ordered = [.. pictures.Concat(embedded)];
        int index = selected is { } id ? ordered.FindIndex(candidate => candidate.File.Id == id) : -1;
        if (index > 0)
        {
            CoverCandidate chosen = ordered[index];
            ordered.RemoveAt(index);
            ordered.Insert(0, chosen);
        }

        return ordered;
    }

    private static MediaFile? Choose(IOrderedEnumerable<MediaFile> ordered, Guid? selected)
    {
        MediaFile[] files = [.. ordered];
        return (selected is { } id ? Array.Find(files, file => file.Id == id) : null) ?? files.FirstOrDefault();
    }

    public static string? MimeTypeOf(string extension)
    {
        return extension switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "webp" => "image/webp",
            "gif" => "image/gif",
            "bmp" => "image/bmp",
            _ => null,
        };
    }

    internal static MediaTags ToMediaTags(MediaFileTags tags)
    {
        return new MediaTags
        {
            Title = tags.Title,
            TitleSort = tags.TitleSort,
            Artist = tags.Artist,
            Artists = tags.Artists,
            ArtistSort = tags.ArtistSort,
            AlbumArtist = tags.AlbumArtist,
            AlbumArtistSort = tags.AlbumArtistSort,
            Album = tags.Album,
            AlbumSort = tags.AlbumSort,
            TrackNumber = tags.TrackNumber,
            TrackCount = tags.TrackCount,
            DiscNumber = tags.DiscNumber,
            DiscCount = tags.DiscCount,
            Year = tags.Year,
            Genre = tags.Genre,
            Genres = tags.Genres,
            Composer = tags.Composer,
            ReplayGainTrackGain = tags.ReplayGainTrackGain,
            ReplayGainTrackPeak = tags.ReplayGainTrackPeak,
            ReplayGainAlbumGain = tags.ReplayGainAlbumGain,
            ReplayGainAlbumPeak = tags.ReplayGainAlbumPeak,
            MusicBrainzTrackId = tags.MusicBrainzTrackId,
            MusicBrainzReleaseId = tags.MusicBrainzReleaseId,
            IsCompilation = tags.IsCompilation,
        };
    }
}
