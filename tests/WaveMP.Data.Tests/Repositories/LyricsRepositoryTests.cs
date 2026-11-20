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
            MediaSource.DefaultId,
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

        IReadOnlyList<LyricsTrackRow> rows = await repository.ListTracksToLookUpAsync(10, [excluded.Id], null);
        IReadOnlyList<LyricsTrackRow> first = await repository.ListTracksToLookUpAsync(1, [excluded.Id], null);

        rows.Select(row => row.TrackId).Should().Equal(older.Id, newer.Id);
        first.Select(row => row.TrackId).Should().Equal(older.Id);
    }

    [Test]
    public async Task ListTracksToLookUpAsync_LookUpAgainBefore_AddsTracksWhoseLookupFoundNothingOrLyricsBeforeAfterTheNewOnes()
    {
        DateTimeOffset now = LibrarySeed.DefaultTime.AddDays(100);
        DateTimeOffset longAgo = now.AddDays(-40);
        LibrarySeed seed = new();
        Track fresh = seed.Track("Fresh", firstSeenAt: LibrarySeed.DefaultTime.AddDays(5));
        Track notFound = seed.Track("Not found", firstSeenAt: LibrarySeed.DefaultTime.AddDays(1));
        Track instrumental = seed.Track("Instrumental", firstSeenAt: LibrarySeed.DefaultTime);
        Track recent = seed.Track("Recent");
        Track saved = seed.Track("Saved");
        Track notNeeded = seed.Track("Not needed", firstSeenAt: LibrarySeed.DefaultTime.AddDays(2));
        Track recentlyNotNeeded = seed.Track("Recently not needed");
        Track withFile = seed.Track("With file");
        seed.File($"{withFile.BaseName}.lrc", MediaFileKind.Lyrics, withFile, isPrimary: false);
        Track excluded = seed.Track("Excluded");
        await SaveAsync(seed);
        LyricsRepository repository = new(CreateDbContext());
        await repository.SaveDownloadAsync(Download(notFound, LyricsDownloadStatus.NotFound, null, longAgo));
        await repository.SaveDownloadAsync(Download(instrumental, LyricsDownloadStatus.Instrumental, null, longAgo));
        await repository.SaveDownloadAsync(Download(recent, LyricsDownloadStatus.NotFound, null, now.AddDays(-10)));
        await repository.SaveDownloadAsync(Download(saved, LyricsDownloadStatus.SavedToDatabase, "Line", longAgo));
        await repository.SaveDownloadAsync(Download(notNeeded, LyricsDownloadStatus.NotNeeded, null, longAgo));
        await repository.SaveDownloadAsync(
            Download(recentlyNotNeeded, LyricsDownloadStatus.NotNeeded, null, now.AddDays(-10)));
        await repository.SaveDownloadAsync(Download(withFile, LyricsDownloadStatus.NotFound, null, longAgo));
        await repository.SaveDownloadAsync(Download(excluded, LyricsDownloadStatus.NotFound, null, longAgo));

        IReadOnlyList<LyricsTrackRow> rows =
            await repository.ListTracksToLookUpAsync(10, [excluded.Id], now.AddDays(-30));
        IReadOnlyList<LyricsTrackRow> withoutAgain = await repository.ListTracksToLookUpAsync(10, [excluded.Id], null);

        rows.Select(row => row.TrackId).Should().Equal(fresh.Id, instrumental.Id, notFound.Id, notNeeded.Id);
        rows[2].Download.Should().Be(new LyricsDownloadRow(LyricsDownloadStatus.NotFound, null, longAgo));
        withoutAgain.Select(row => row.TrackId).Should().Equal(fresh.Id);
    }

    [Test]
    public async Task ListTracksToLookUpAsync_TracksWithLyricsWhosePrimaryFilesWereReadAfterTheCheck_AddsThem()
    {
        DateTimeOffset checkedAt = LibrarySeed.DefaultTime.AddDays(10);
        DateTimeOffset before = checkedAt.AddDays(-1);
        DateTimeOffset after = checkedAt.AddDays(1);
        LibrarySeed seed = new();
        Track changedAudio = seed.Track("Changed audio", firstSeenAt: LibrarySeed.DefaultTime);
        seed.File($"{changedAudio.BaseName}.mp3", MediaFileKind.Audio, changedAudio).ScannedAt = after;
        Track changedVideo = seed.Track("Changed video", firstSeenAt: LibrarySeed.DefaultTime.AddDays(1));
        seed.File($"{changedVideo.BaseName}.mp3", MediaFileKind.Audio, changedVideo).ScannedAt = before;
        seed.File($"{changedVideo.BaseName}.mkv", MediaFileKind.Video, changedVideo).ScannedAt = after;
        Track unchanged = seed.Track("Unchanged");
        seed.File($"{unchanged.BaseName}.mp3", MediaFileKind.Audio, unchanged).ScannedAt = before;
        Track changedDuplicate = seed.Track("Changed duplicate");
        seed.File($"{changedDuplicate.BaseName}.mp3", MediaFileKind.Audio, changedDuplicate).ScannedAt = before;
        seed.File($"{changedDuplicate.BaseName}.ogg", MediaFileKind.Audio, changedDuplicate, isPrimary: false)
            .ScannedAt = after;
        Track changedCover = seed.Track("Changed cover");
        seed.File($"{changedCover.BaseName}.mp3", MediaFileKind.Audio, changedCover).ScannedAt = before;
        seed.File($"{changedCover.BaseName}.jpg", MediaFileKind.Image, changedCover, isPrimary: false)
            .ScannedAt = after;
        Track notFound = seed.Track("Not found");
        seed.File($"{notFound.BaseName}.mp3", MediaFileKind.Audio, notFound).ScannedAt = after;
        await SaveAsync(seed);
        LyricsRepository repository = new(CreateDbContext());
        foreach (Track track in (Track[])[changedAudio, changedVideo, unchanged, changedDuplicate, changedCover])
        {
            await repository.SaveDownloadAsync(Download(track, LyricsDownloadStatus.NotNeeded, null, checkedAt));
        }

        await repository.SaveDownloadAsync(Download(notFound, LyricsDownloadStatus.NotFound, null, checkedAt));

        IReadOnlyList<LyricsTrackRow> rows = await repository.ListTracksToLookUpAsync(10, [], null);
        IReadOnlyList<LyricsTrackRow> withAgain =
            await repository.ListTracksToLookUpAsync(10, [], checkedAt.AddDays(-30));

        rows.Select(row => row.TrackId).Should().Equal(changedAudio.Id, changedVideo.Id);
        rows[0].Download.Should().Be(new LyricsDownloadRow(LyricsDownloadStatus.NotNeeded, null, checkedAt));
        withAgain.Select(row => row.TrackId).Should().Equal(changedAudio.Id, changedVideo.Id);
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
