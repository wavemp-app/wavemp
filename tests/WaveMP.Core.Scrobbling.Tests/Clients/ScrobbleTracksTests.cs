using AwesomeAssertions;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Scrobbling.Tests.Clients;

[TestFixture]
public class ScrobbleTracksTests
{
    [Test]
    public void From_TrackWithArtistAndAlbum_TakesAllDetails()
    {
        TrackRow track = Track("Title", "Artist", "Album", "Album Artist") with
        {
            TrackNumber = 4,
            Duration = TimeSpan.FromMinutes(3),
        };

        ScrobbleTrack? song = ScrobbleTracks.From(track);

        song.Should().Be(new ScrobbleTrack("Artist", "Title", "Album", "Album Artist", 4, TimeSpan.FromMinutes(3)));
    }

    [Test]
    public void From_TrackWithoutArtist_TakesTheAlbumArtist()
    {
        ScrobbleTrack? song = ScrobbleTracks.From(Track("Title", null, "Album", "Album Artist"));

        song!.Artist.Should().Be("Album Artist");
    }

    [Test]
    public void From_TrackWithoutAnyArtist_ReturnsNull()
    {
        ScrobbleTracks.From(Track("Title", " ", null, null)).Should().BeNull();
    }

    [Test]
    public void From_QueueEntry_TakesTheStoredSongAndTime()
    {
        DateTimeOffset playedAt = new(2026, 11, 18, 9, 0, 0, TimeSpan.Zero);
        ScrobbleQueueEntry entry = new()
        {
            ConnectionId = Guid.NewGuid(),
            Artist = "Artist",
            Title = "Title",
            Album = "Album",
            PlayedAt = playedAt,
            NextAttemptAt = playedAt,
        };

        ScrobblePlay play = ScrobbleTracks.From(entry);

        play.Should().Be(new ScrobblePlay(new ScrobbleTrack("Artist", "Title", "Album"), playedAt));
    }

    internal static TrackRow Track(string title, string? artist, string? album, string? albumArtist)
    {
        return new TrackRow(
            Guid.NewGuid(),
            title,
            artist is null ? null : Guid.NewGuid(),
            artist,
            album is null ? null : Guid.NewGuid(),
            album,
            albumArtist is null ? null : Guid.NewGuid(),
            albumArtist,
            false,
            null,
            null,
            null,
            null,
            TimeSpan.FromMinutes(3),
            true,
            false,
            false,
            DateTimeOffset.UnixEpoch);
    }
}
