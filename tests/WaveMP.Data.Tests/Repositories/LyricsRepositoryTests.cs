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
    /// <summary>
    /// The current version of the rules of the lookup.
    /// </summary>
    private const int MatcherVersion = 2;

    protected override string DatabaseName => "wavemp_tests_data_library_lyrics";

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
            null)
        { Artists = [new LyricsArtistRow("Band", ArtistRole.Main)] });
        (await repository.FindTrackAsync(Guid.NewGuid())).Should().BeNull();
    }

    [TestCase(null, 300)]
    [TestCase(0.3, 300)]
    [TestCase(0.8, 200)]
    public async Task FindTrackAsync_VideoWithSongSegment_HasTheDurationOfTheSongIfTheAnalysisIsSure(
        double? confidence,
        int expectedSeconds)
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Song", duration: TimeSpan.FromSeconds(300));
        MediaFile video = seed.File($"{track.BaseName}.mkv", MediaFileKind.Video, track);
        video.Duration = TimeSpan.FromSeconds(300);
        await SaveAsync(seed);
        if (confidence is { } value)
        {
            await new SongAnalysisRepository(CreateDbContext()).SaveAsync(
            [
                new MediaFileAnalysis
                {
                    MediaFileId = video.Id,
                    Version = 1,
                    FileSize = video.Size,
                    FileModifiedAt = video.ModifiedAt,
                    AnalyzedAt = LibrarySeed.DefaultTime,
                    Status = SongAnalysisStatus.Analyzed,
                    Method = SongSegmentMethod.Lyrics,
                    Confidence = value,
                    Pieces = [new SongPiece(40_000, 0, 200_000)],
                },
            ]);
        }

        LyricsTrackRow? row = await new LyricsRepository(CreateDbContext()).FindTrackAsync(track.Id);

        row!.Duration.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Test]
    public async Task FindTrackAsync_CorrectedSongSegment_HasTheDurationOfTheCorrectedSong()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Song", duration: TimeSpan.FromSeconds(300));
        MediaFile audio = seed.File($"{track.BaseName}.mp3", MediaFileKind.Audio, track);
        audio.Duration = TimeSpan.FromSeconds(300);
        await SaveAsync(seed);
        await new TrackRepository(CreateDbContext())
            .SetManualSongSegmentAsync(track.Id, MediaFileKind.Audio, 10_000, 250_000);

        LyricsTrackRow? row = await new LyricsRepository(CreateDbContext()).FindTrackAsync(track.Id);

        row!.Duration.Should().Be(TimeSpan.FromSeconds(240));
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
        row.Download.Should().Be(new LyricsDownloadRow(
            LyricsDownloadStatus.SavedToDatabase,
            "[00:01.00]Line",
            checkedAt.AddDays(1),
            MatcherVersion: MatcherVersion));
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

        IReadOnlyList<LyricsTrackRow> rows =
            await repository.ListTracksToLookUpAsync(10, [excluded.Id], null, MatcherVersion);
        IReadOnlyList<LyricsTrackRow> first =
            await repository.ListTracksToLookUpAsync(1, [excluded.Id], null, MatcherVersion);

        rows.Select(row => row.TrackId).Should().Equal(older.Id, newer.Id);
        first.Select(row => row.TrackId).Should().Equal(older.Id);
    }

    [Test]
    public async Task ListTracksToLookUpAsync_NotFoundWithOlderRules_AddsThemAtOnceAfterTheNewOnes()
    {
        LibrarySeed seed = new();
        Track fresh = seed.Track("Fresh", firstSeenAt: LibrarySeed.DefaultTime.AddDays(5));
        Track oldRules = seed.Track("Old rules", firstSeenAt: LibrarySeed.DefaultTime);
        Track currentRules = seed.Track("Current rules");
        Track instrumental = seed.Track("Instrumental");
        await SaveAsync(seed);
        LyricsRepository repository = new(CreateDbContext());
        DateTimeOffset checkedAt = LibrarySeed.DefaultTime.AddDays(10);
        await repository.SaveDownloadAsync(
            Download(oldRules, LyricsDownloadStatus.NotFound, null, checkedAt, matcherVersion: 1));
        await repository.SaveDownloadAsync(Download(currentRules, LyricsDownloadStatus.NotFound, null, checkedAt));
        await repository.SaveDownloadAsync(
            Download(instrumental, LyricsDownloadStatus.Instrumental, null, checkedAt, matcherVersion: 1));

        IReadOnlyList<LyricsTrackRow> rows = await repository.ListTracksToLookUpAsync(10, [], null, MatcherVersion);

        rows.Select(row => row.TrackId).Should().Equal(fresh.Id, oldRules.Id);
    }

    [Test]
    public async Task SaveDownloadAsync_OtherVersion_KeepsTheMarkAndTheVersionOfTheRules()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Song");
        await SaveAsync(seed);
        LyricsDownload download = Download(track, LyricsDownloadStatus.Uploaded, "Line", LibrarySeed.DefaultTime);
        download.OtherVersion = true;

        await new LyricsRepository(CreateDbContext()).SaveDownloadAsync(download);

        (await new LyricsRepository(CreateDbContext()).FindTrackAsync(track.Id))!.Download.Should().Be(
            new LyricsDownloadRow(
                LyricsDownloadStatus.Uploaded,
                "Line",
                LibrarySeed.DefaultTime,
                true,
                MatcherVersion));
    }

    [Test]
    public async Task ListTracksToLookUpAsync_LookUpAgainBefore_AddsNotFoundAndLyricsBeforeTracksAfterTheNewOnes()
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
            await repository.ListTracksToLookUpAsync(10, [excluded.Id], now.AddDays(-30), MatcherVersion);
        IReadOnlyList<LyricsTrackRow> withoutAgain =
            await repository.ListTracksToLookUpAsync(10, [excluded.Id], null, MatcherVersion);

        rows.Select(row => row.TrackId).Should().Equal(fresh.Id, instrumental.Id, notFound.Id, notNeeded.Id);
        rows[2].Download.Should().Be(
            new LyricsDownloadRow(LyricsDownloadStatus.NotFound, null, longAgo, MatcherVersion: MatcherVersion));
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

        IReadOnlyList<LyricsTrackRow> rows = await repository.ListTracksToLookUpAsync(10, [], null, MatcherVersion);
        IReadOnlyList<LyricsTrackRow> withAgain =
            await repository.ListTracksToLookUpAsync(10, [], checkedAt.AddDays(-30), MatcherVersion);

        rows.Select(row => row.TrackId).Should().Equal(changedAudio.Id, changedVideo.Id);
        rows[0].Download.Should().Be(
            new LyricsDownloadRow(LyricsDownloadStatus.NotNeeded, null, checkedAt, MatcherVersion: MatcherVersion));
        withAgain.Select(row => row.TrackId).Should().Equal(changedAudio.Id, changedVideo.Id);
    }

    [Test]
    public async Task CountDownloadsAsync_Results_CountsThemPerStatus()
    {
        LibrarySeed seed = new();
        Track first = seed.Track("First");
        Track second = seed.Track("Second");
        Track instrumental = seed.Track("Instrumental");
        Track saved = seed.Track("Saved");
        seed.Track("Never looked up");
        await SaveAsync(seed);
        LyricsRepository repository = new(CreateDbContext());
        DateTimeOffset checkedAt = LibrarySeed.DefaultTime;
        await repository.SaveDownloadAsync(Download(first, LyricsDownloadStatus.NotFound, null, checkedAt));
        await repository.SaveDownloadAsync(Download(second, LyricsDownloadStatus.NotFound, null, checkedAt));
        await repository.SaveDownloadAsync(
            Download(instrumental, LyricsDownloadStatus.Instrumental, null, checkedAt));
        await repository.SaveDownloadAsync(Download(saved, LyricsDownloadStatus.SavedToFile, null, checkedAt));

        IReadOnlyDictionary<LyricsDownloadStatus, int> counts =
            await new LyricsRepository(CreateDbContext()).CountDownloadsAsync();

        counts.Should().BeEquivalentTo(new Dictionary<LyricsDownloadStatus, int>
        {
            [LyricsDownloadStatus.NotFound] = 2,
            [LyricsDownloadStatus.Instrumental] = 1,
            [LyricsDownloadStatus.SavedToFile] = 1,
        });
    }

    [Test]
    public async Task DeleteDownloadsAsync_Statuses_RemovesOnlyTheseResultsSoTheTracksAreLookedUpAgain()
    {
        LibrarySeed seed = new();
        Track notFound = seed.Track("Not found", firstSeenAt: LibrarySeed.DefaultTime);
        Track instrumental = seed.Track("Instrumental", firstSeenAt: LibrarySeed.DefaultTime.AddDays(1));
        Track saved = seed.Track("Saved");
        Track notNeeded = seed.Track("Not needed");
        await SaveAsync(seed);
        LyricsRepository repository = new(CreateDbContext());
        DateTimeOffset checkedAt = LibrarySeed.DefaultTime.AddDays(10);
        await repository.SaveDownloadAsync(Download(notFound, LyricsDownloadStatus.NotFound, null, checkedAt));
        await repository.SaveDownloadAsync(
            Download(instrumental, LyricsDownloadStatus.Instrumental, null, checkedAt));
        await repository.SaveDownloadAsync(
            Download(saved, LyricsDownloadStatus.SavedToDatabase, "[00:01.00]Line", checkedAt));
        await repository.SaveDownloadAsync(Download(notNeeded, LyricsDownloadStatus.NotNeeded, null, checkedAt));
        (await repository.ListTracksToLookUpAsync(10, [], null, MatcherVersion)).Should().BeEmpty();

        int removed = await new LyricsRepository(CreateDbContext()).DeleteDownloadsAsync(
            [LyricsDownloadStatus.NotFound, LyricsDownloadStatus.Instrumental]);

        removed.Should().Be(2);
        LyricsRepository reader = new(CreateDbContext());
        (await reader.ListTracksToLookUpAsync(10, [], null, MatcherVersion)).Select(row => row.TrackId)
            .Should().Equal(notFound.Id, instrumental.Id);
        (await reader.FindTrackAsync(saved.Id))!.Download.Should().Be(new LyricsDownloadRow(
            LyricsDownloadStatus.SavedToDatabase,
            "[00:01.00]Line",
            checkedAt,
            MatcherVersion: MatcherVersion));
        (await reader.FindTrackAsync(notNeeded.Id))!.Download!.Status.Should().Be(LyricsDownloadStatus.NotNeeded);
    }

    [Test]
    public async Task DeleteDownloadsAsync_Track_RemovesOnlyItsResultWithOneOfTheStatuses()
    {
        LibrarySeed seed = new();
        Track track = seed.Track("Song");
        Track other = seed.Track("Other");
        Track saved = seed.Track("Saved");
        await SaveAsync(seed);
        LyricsRepository repository = new(CreateDbContext());
        DateTimeOffset checkedAt = LibrarySeed.DefaultTime;
        await repository.SaveDownloadAsync(Download(track, LyricsDownloadStatus.NotFound, null, checkedAt));
        await repository.SaveDownloadAsync(Download(other, LyricsDownloadStatus.NotFound, null, checkedAt));
        await repository.SaveDownloadAsync(Download(saved, LyricsDownloadStatus.SavedToFile, null, checkedAt));
        LyricsDownloadStatus[] statuses = [LyricsDownloadStatus.NotFound, LyricsDownloadStatus.Instrumental];

        (await repository.DeleteDownloadsAsync(statuses, track.Id)).Should().Be(1);
        (await repository.DeleteDownloadsAsync(statuses, track.Id)).Should().Be(0);
        (await repository.DeleteDownloadsAsync(statuses, saved.Id)).Should().Be(0);

        LyricsRepository reader = new(CreateDbContext());
        (await reader.FindTrackAsync(track.Id))!.Download.Should().BeNull();
        (await reader.FindTrackAsync(other.Id))!.Download.Should().NotBeNull();
        (await reader.FindTrackAsync(saved.Id))!.Download.Should().NotBeNull();
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
        DateTimeOffset checkedAt,
        int matcherVersion = MatcherVersion)
    {
        return new LyricsDownload
        {
            TrackId = track.Id,
            Status = status,
            Content = content,
            CheckedAt = checkedAt,
            MatcherVersion = matcherVersion,
        };
    }
}
