using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;

namespace WaveMP.Core.ArtistPictures.Lookups;

/// <summary>
/// The details of a MusicBrainz artist (its Wikidata item, sort name and aliases) for the online lookup of pictures and
/// aliases, kept in the cache of the lookup, so both ask MusicBrainz once per artist.
/// </summary>
internal sealed class MusicBrainzArtistDetailsLoader(
    ArtistLookupCache cache,
    IMusicBrainzArtistClient musicBrainz,
    ISettingsSnapshot<OnlineMetadataSettings> onlineSettings)
{
    /// <summary>
    /// The details from the cache or from MusicBrainz. An artist MusicBrainz does not know has empty details; any
    /// other failure returns its status and no details.
    /// </summary>
    public async Task<(OnlineStatus Status, MusicBrainzArtistDetails? Details)> LoadAsync(
        Guid musicBrainzArtistId,
        CancellationToken cancellationToken)
    {
        string name = "musicbrainz/artist-"
            + ArtistLookupCache.Hash(onlineSettings.Current.MusicBrainzUrl, musicBrainzArtistId.ToString("N"))
            + ".json";
        MusicBrainzArtistDetails? details = await cache.ReadAsync<MusicBrainzArtistDetails>(name, cancellationToken);
        if (details is not null)
        {
            return (OnlineStatus.Found, details);
        }

        OnlineResponse<MusicBrainzArtistDetails> response =
            await musicBrainz.GetArtistAsync(musicBrainzArtistId, cancellationToken);
        if (response.Status is not (OnlineStatus.Found or OnlineStatus.NotFound))
        {
            return (response.Status, null);
        }

        details = response.Value ?? new MusicBrainzArtistDetails(null, null, []);
        await cache.WriteAsync(name, details, cancellationToken);
        return (OnlineStatus.Found, details);
    }
}
