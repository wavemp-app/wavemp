using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Scrobbling.Clients;

/// <summary>
/// Turns tracks of the library and queued scrobbles into songs for the services.
/// </summary>
public static class ScrobbleTracks
{
    /// <summary>
    /// The song of a track, or <c>null</c> if it has no artist (the services require one). The artist of the track
    /// comes first, then the artist of its album.
    /// </summary>
    public static ScrobbleTrack? From(TrackRow track)
    {
        string? artist = FirstText(track.ArtistName, track.AlbumArtistName);
        if (artist is null || string.IsNullOrWhiteSpace(track.Title))
        {
            return null;
        }

        return new ScrobbleTrack(
            artist,
            track.Title.Trim(),
            FirstText(track.AlbumTitle),
            FirstText(track.AlbumArtistName),
            track.TrackNumber,
            track.Duration);
    }

    /// <summary>
    /// The play of a queued scrobble.
    /// </summary>
    public static ScrobblePlay From(ScrobbleQueueEntry entry)
    {
        return new ScrobblePlay(
            new ScrobbleTrack(
                entry.Artist,
                entry.Title,
                entry.Album,
                entry.AlbumArtist,
                entry.TrackNumber,
                entry.Duration),
            entry.PlayedAt);
    }

    private static string? FirstText(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
