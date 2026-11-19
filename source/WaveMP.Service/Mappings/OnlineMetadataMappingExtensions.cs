using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.OnlineMetadata.Covers;
using WaveMP.Core.OnlineMetadata.Matches;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Validations;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class OnlineMetadataMappingExtensions
{
    public static OnlineMetadataSettingsDto ToDto(this OnlineMetadataSettings settings)
    {
        return new OnlineMetadataSettingsDto
        {
            Enabled = settings.Enabled,
            MusicBrainzUrl = settings.MusicBrainzUrl,
            CoverArtArchiveUrl = settings.CoverArtArchiveUrl,
            MusicBrainzRequestIntervalMilliseconds = settings.MusicBrainzRequestIntervalMilliseconds,
        };
    }

    public static UpdateOnlineMetadataSettingsCommand ToCommand(this UpdateOnlineMetadataSettingsDto request)
    {
        return new UpdateOnlineMetadataSettingsCommand(
            request.Enabled,
            request.MusicBrainzUrl,
            request.CoverArtArchiveUrl,
            request.MusicBrainzRequestIntervalMilliseconds);
    }

    /// <summary>
    /// The JSON name of the field in <see cref="UpdateOnlineMetadataSettingsDto"/>, the key of its validation errors.
    /// </summary>
    public static string ToJsonName(this OnlineMetadataSettingsField field)
    {
        return field switch
        {
            OnlineMetadataSettingsField.MusicBrainzUrl => "musicBrainzUrl",
            OnlineMetadataSettingsField.CoverArtArchiveUrl => "coverArtArchiveUrl",
            OnlineMetadataSettingsField.MusicBrainzRequestIntervalMilliseconds =>
                "musicBrainzRequestIntervalMilliseconds",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field."),
        };
    }

    public static OnlineReleaseDto ToDto(this OnlineRelease release)
    {
        return new OnlineReleaseDto
        {
            Id = release.Id,
            Date = release.Date,
            Country = release.Country,
            Format = release.Format,
            TrackCount = release.TrackCount,
        };
    }

    public static OnlineTrackMatchDto ToDto(this TrackMatch match)
    {
        return new OnlineTrackMatchDto
        {
            RecordingId = match.RecordingId,
            Release = match.Release?.ToDto(),
            Agreement = match.Agreement,
            DurationMilliseconds = match.Duration is { } duration ? (long)duration.TotalMilliseconds : null,
            Title = match.Values.Title,
            Artist = match.Values.Artist,
            Album = match.Values.Album,
            AlbumArtist = match.Values.AlbumArtist,
            Year = match.Values.Year,
            TrackNumber = match.Values.TrackNumber,
            DiscNumber = match.Values.DiscNumber,
            Genre = match.Values.Genre,
        };
    }

    public static OnlineAlbumValuesDto ToDto(this OnlineAlbumValues values)
    {
        return new OnlineAlbumValuesDto
        {
            Album = values.Album,
            AlbumArtist = values.AlbumArtist,
            Year = values.Year,
            Genre = values.Genre,
        };
    }

    public static OnlineAlbumMatchDto ToDto(this AlbumMatch match)
    {
        return new OnlineAlbumMatchDto
        {
            Release = match.Release.ToDto(),
            Agreement = match.Agreement,
            Values = match.Values.ToDto(),
        };
    }

    public static OnlineCoverOutcomeDto ToDto(this CoverSaveOutcome outcome)
    {
        return outcome switch
        {
            CoverSaveOutcome.None => OnlineCoverOutcomeDto.None,
            CoverSaveOutcome.MediaFolder => OnlineCoverOutcomeDto.MediaFolder,
            CoverSaveOutcome.DataFolder => OnlineCoverOutcomeDto.DataFolder,
            CoverSaveOutcome.NotSaved => OnlineCoverOutcomeDto.NotSaved,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown outcome."),
        };
    }

    /// <summary>
    /// The changes of the request: members it contains, also with null, become changes; missing ones stay.
    /// </summary>
    public static TagChanges ToChanges(this UpdateAlbumTagsDto request)
    {
        return new TagChanges
        {
            Album = request.HasAlbum ? new TagChange<string?>(request.Album) : null,
            AlbumArtist = request.HasAlbumArtist ? new TagChange<string?>(request.AlbumArtist) : null,
            Year = request.HasYear ? new TagChange<int?>(request.Year) : null,
            Genre = request.HasGenre ? new TagChange<string?>(request.Genre) : null,
        };
    }
}
