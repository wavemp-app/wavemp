using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class LyricsRepositoryTests : LibraryDatabaseTests
{
    protected override string DatabaseName => "wavemp_tests_library_lyrics";

    [Test]
    public async Task FindTrackAsync_Track_ReturnsItsNamesAndFiles()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        Album album = seed.Album("Album", artist);
        Track track = seed.Track("Song", artist, album, duration: TimeSpan.FromSeconds(200));
        seed.File($"Album/{track.BaseName}.mp3", MediaFileKind.Audio, track);
        seed.File($"Album/{track.BaseName}.mkv", MediaFileKind.Video, track);
        seed.File($"Album/{track.BaseName}.lrc", MediaFileKind.Lyrics, track, isPrimary: false);
        await SaveAsync(seed);
        LyricsRepository repository = new(CreateDbContext());

        LyricsTrackRow? row = await repository.FindTrackAsync(track.Id);

        row.Should().BeEquivalentTo(new LyricsTrackRow(
            track.Id,
            "Album",
            track.BaseName,
            "Song",
            "Band",
            "Album",
            TimeSpan.FromSeconds(200),
            $"Album/{track.BaseName}.lrc",
            $"Album/{track.BaseName}.mp3",
            $"Album/{track.BaseName}.mkv",
            null));
        (await repository.FindTrackAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Test]
    public async Task SaveDownloadAsync_TwiceForATrack_ReplacesTheResult()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Song");
        await SaveAsync(seed);
        LyricsRepository repository = new(CreateDbContext());
        DateTimeOffset checkedAt = LibrarySeed.DefaultTime;

        (await repository.SaveDownloadAsync(Download(track, LyricsDownloadStatus.NotFound, null, checkedAt)))
            .Should().BeTrue();
        (await repository.SaveDownloadAsync(
                Download(track, LyricsDownloadStatus.SavedToDatabase, "[00:01.00]Line", checkedAt.AddDays(1))))
            .Should().BeTrue();

        LyricsTrackRow row = (await new LyricsRepository(CreateDbContext()).FindTrackAsync(track.Id))!;
        row.Download.Should().Be(
            new LyricsDownloadRow(LyricsDownloadStatus.SavedToDatabase, "[00:01.00]Line", checkedAt.AddDays(1)));
        await using WaveMpDbContext db = CreateDbContext();
        (await db.LyricsDownloads.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task SaveDownloadAsync_UnknownTrack_ReturnsFalse()
    {
        LyricsRepository repository = new(CreateDbContext());

        (await repository.SaveDownloadAsync(new LyricsDownload
        {
            TrackId = Guid.NewGuid(),
            Status = LyricsDownloadStatus.NotFound,
            CheckedAt = LibrarySeed.DefaultTime,
        })).Should().BeFalse();
    }

    [Test]
    public async Task ListTracksToLookUpAsync_Tracks_SkipsTracksWithLyricsFilesLookupsOrExclusionsOldestFirst()
    {
        LibrarySeed seed = new();
        Track newer = seed.Track("Newer", firstSeenAt: LibrarySeed.DefaultTime.AddDays(2));
        Track older = seed.Track("Older", firstSeenAt: LibrarySeed.DefaultTime);
        Track withFile = seed.Track("With file");
        seed.File($"{withFile.BaseName}.lrc", MediaFileKind.Lyrics, withFile, isPrimary: false);
        Track lookedUp = seed.Track("Looked up");
        Track excluded = seed.Track("Excluded");
        await SaveAsync(seed);
        LyricsRepository repository = new(CreateDbContext());
        await repository.SaveDownloadAsync(
            Download(lookedUp, LyricsDownloadStatus.NotFound, null, LibrarySeed.DefaultTime));

        IReadOnlyList<LyricsTrackRow> rows = await repository.ListTracksToLookUpAsync(10, [excluded.Id]);
        IReadOnlyList<LyricsTrackRow> first = await repository.ListTracksToLookUpAsync(1, [excluded.Id]);

        rows.Select(row => row.TrackId).Should().Equal(older.Id, newer.Id);
        first.Select(row => row.TrackId).Should().Equal(older.Id);
    }

    [Test]
    public async Task DeletingATrack_RemovesItsLookup()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Song");
        await SaveAsync(seed);
        await new LyricsRepository(CreateDbContext())
            .SaveDownloadAsync(Download(track, LyricsDownloadStatus.NotFound, null, LibrarySeed.DefaultTime));

        await using WaveMpDbContext db = CreateDbContext();
        await db.Tracks.Where(item => item.Id == track.Id).ExecuteDeleteAsync();

        (await db.LyricsDownloads.AnyAsync()).Should().BeFalse();
    }

    private static LyricsDownload Download(
        Track track,
        LyricsDownloadStatus status,
        string? content,
        DateTimeOffset checkedAt)
    {
        return new LyricsDownload { TrackId = track.Id, Status = status, Content = content, CheckedAt = checkedAt };
    }
}
