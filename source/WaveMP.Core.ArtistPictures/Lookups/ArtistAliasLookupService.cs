using WaveMP.Core.Metadata.Texts;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Data.Texts;

namespace WaveMP.Core.ArtistPictures.Lookups;

internal sealed class ArtistAliasLookupService(
    IArtistPictureRepository pictures,
    IArtistAliasRepository aliases,
    ArtistLookupCache cache,
    IMusicBrainzArtistClient musicBrainz,
    ISettingsSnapshot<ArtistPictureSettings> settings,
    ISettingsSnapshot<OnlineMetadataSettings> onlineSettings,
    TimeProvider timeProvider) : IArtistAliasLookupService
{
    public async Task<ArtistAliasLookupResult> LookUpAsync(
        Guid artistId,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Current.OnlineLookupEnabled)
        {
            return ArtistAliasLookupResult.Failed(ArtistLookupStatus.Disabled);
        }

        ArtistLookupRow? artist = await pictures.FindLookupAsync(artistId, cancellationToken);
        if (artist is null)
        {
            return ArtistAliasLookupResult.Failed(ArtistLookupStatus.ArtistNotFound);
        }

        if (artist.MusicBrainzArtistId is not { } musicBrainzArtistId || musicBrainzArtistId == Guid.Empty)
        {
            return ArtistAliasLookupResult.Failed(ArtistLookupStatus.MusicBrainzArtistMissing);
        }

        (OnlineStatus status, MusicBrainzArtistDetails? details) =
            await new MusicBrainzArtistDetailsLoader(cache, musicBrainz, onlineSettings)
                .LoadAsync(musicBrainzArtistId, cancellationToken);
        if (details is null)
        {
            return ArtistAliasLookupResult.Failed(
                status == OnlineStatus.Disabled ? ArtistLookupStatus.Disabled : ArtistLookupStatus.Unavailable);
        }

        IReadOnlyList<ArtistAliasRow>? saved = await aliases.ReplaceMusicBrainzAliasesAsync(
            artistId,
            NamesOf(details),
            timeProvider.GetUtcNow(),
            cancellationToken);
        return saved is null
            ? ArtistAliasLookupResult.Failed(ArtistLookupStatus.ArtistNotFound)
            : new ArtistAliasLookupResult(ArtistLookupStatus.Found, saved);
    }

    /// <summary>
    /// The sort name and the aliases of a MusicBrainz artist as aliases: cleaned like names from tags (white space
    /// joined, control characters removed) and cut to the longest name.
    /// </summary>
    internal static IReadOnlyList<string> NamesOf(MusicBrainzArtistDetails details)
    {
        List<string> names = [];
        foreach (string? name in details.Aliases.Prepend(details.SortName))
        {
            if (MetadataText.Normalize(name) is not { } cleaned)
            {
                continue;
            }

            if (cleaned.Length > NameKeys.MaxLength)
            {
                int length = char.IsHighSurrogate(cleaned[NameKeys.MaxLength - 1])
                    ? NameKeys.MaxLength - 1
                    : NameKeys.MaxLength;
                cleaned = cleaned[..length].TrimEnd();
            }

            names.Add(cleaned);
        }

        return names;
    }
}
