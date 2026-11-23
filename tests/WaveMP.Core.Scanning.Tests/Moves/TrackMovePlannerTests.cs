using AwesomeAssertions;
using WaveMP.Core.Scanning.Discoveries;
using WaveMP.Core.Scanning.Moves;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Tests.Moves;

[TestFixture]
public class TrackMovePlannerTests
{
    private static readonly DateTimeOffset _time = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid _music = new("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid _videos = new("00000000-0000-0000-0000-00000000000b");

    [Test]
    public void Plan_FolderRenamed_PairsTheSongAndDefersItsTrackWithAllItsFiles()
    {
        Song song = new("Old/Song.mp3", 100, picture: "Old/Song.jpg");

        TrackMoves moves = PlanOne(
            [.. song.Files, .. new Song("Other/Stays.mp3", 100).Files],
            File("New/Song.mp3", 100),
            File("New/Song.jpg", 100, MediaFileKind.Image),
            File("Other/Stays.mp3", 100));

        moves.Moves.Should().Equal(
            new TrackMove(_music, "New/Song.mp3", song.TrackId, song.Evidence.Id, _music, "Old", "Old/Song.mp3"));
        moves.Find(_music, "New/Song.mp3").Should().Be(moves.Moves[0]);
        moves.Find(_music, "New/Song.jpg").Should().BeNull();
        moves.IsEmpty.Should().BeFalse();
        moves.IsDeferred(song.TrackId).Should().BeTrue();
        VanishedTrack deferred = moves.Deferred.Should().ContainSingle().Subject;
        (deferred.TrackId, deferred.SourceId, deferred.DirectoryPath).Should().Be((song.TrackId, _music, "Old"));
        deferred.Files.Should().Equal(
            (song.Files[0].Id, "Old/Song.jpg"),
            (song.Evidence.Id, "Old/Song.mp3"));
        moves.FindDeferred(song.TrackId).Should().Be(deferred);
    }

    [Test]
    public void Plan_FolderMovedIntoAnotherFolder_PairsTheSong()
    {
        Song song = new("Band/Album/01.flac", 500);

        TrackMoves moves = PlanOne(song.Files, File("Archive/Band/Album/01.flac", 500));

        moves.Moves.Should().ContainSingle().Which.Should().Be(new TrackMove(
            _music,
            "Archive/Band/Album/01.flac",
            song.TrackId,
            song.Evidence.Id,
            _music,
            "Band/Album",
            "Band/Album/01.flac"));
    }

    [Test]
    public void Plan_FileRenamedInItsFolder_PairsTheSong()
    {
        Song song = new("Album/Song.mp3", 100);
        Song other = new("Album/Other.mp3", 200);

        TrackMoves moves = PlanOne(
            [.. song.Files, .. other.Files],
            File("Album/Track.mp3", 100),
            File("Album/Other.mp3", 200));

        moves.Moves.Should().ContainSingle().Which.Should().Be(
            new TrackMove(
                _music,
                "Album/Track.mp3",
                song.TrackId,
                song.Evidence.Id,
                _music,
                "Album",
                "Album/Song.mp3"));
    }

    [Test]
    public void Plan_FolderRenamedWithSongsOfEqualSize_PairsThemByTheirFileNames()
    {
        Song first = new("Old/01 First.mp3", 100);
        Song second = new("Old/02 Second.mp3", 100);

        TrackMoves moves = PlanOne(
            [.. first.Files, .. second.Files],
            File("New/01 First.mp3", 100),
            File("New/02 Second.mp3", 100));

        moves.Moves.Select(move => (move.RelativePath, move.TrackId)).Should().Equal(
            ("New/01 First.mp3", first.TrackId),
            ("New/02 Second.mp3", second.TrackId));
    }

    [Test]
    public void Plan_SongsOfEqualSizeRenamed_PairsNothing()
    {
        Song first = new("Old/01 First.mp3", 100);
        Song second = new("Old/02 Second.mp3", 100);

        TrackMoves moves = PlanOne(
            [.. first.Files, .. second.Files],
            File("New/First.mp3", 100),
            File("New/Second.mp3", 100));

        moves.Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_TwoNewCopiesOfOneVanishedSong_PairsNothing()
    {
        Song song = new("Old/Song.mp3", 100);

        TrackMoves moves = PlanOne(song.Files, File("Copy 1/Song.mp3", 100), File("Copy 2/Song.mp3", 100));

        moves.Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_TwoNewCopiesOfWhichOneKeptTheName_PairsTheOneWithTheName()
    {
        Song song = new("Old/Song.mp3", 100);

        TrackMoves moves = PlanOne(song.Files, File("New/Song.mp3", 100), File("New/Song (copy).mp3", 100));

        moves.Moves.Should().ContainSingle().Which.RelativePath.Should().Be("New/Song.mp3");
    }

    [Test]
    public void Plan_TwoVanishedCopiesAndOneNewFile_PairsNothing()
    {
        Song first = new("Old 1/Song.mp3", 100);
        Song second = new("Old 2/Song.mp3", 100);

        TrackMoves moves = PlanOne([.. first.Files, .. second.Files], File("New/Song.mp3", 100));

        moves.Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_OnlyTheFileTheTrackDidNotPlayFound_PairsNothing()
    {
        Song song = new("Old/Song.flac", 900, duplicate: "Old/Song.mp3");

        TrackMoves moves = PlanOne(song.Files, File("New/Song.mp3", 100));

        moves.Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_TheFileTheTrackPlayedAndItsDuplicateMoved_PairsOnlyTheFileItPlayed()
    {
        Song song = new("Old/Song.flac", 900, duplicate: "Old/Song.mp3");

        TrackMoves moves = PlanOne(song.Files, File("New/Song.flac", 900), File("New/Song.mp3", 100));

        moves.Moves.Should().ContainSingle().Which.Should().Be(
            new TrackMove(_music, "New/Song.flac", song.TrackId, song.Evidence.Id, _music, "Old", "Old/Song.flac"));
        moves.Deferred.Should().ContainSingle().Which.Files.Should().HaveCount(2);
    }

    [Test]
    public void Plan_NewFileOfAnotherKindOrExtension_PairsNothing()
    {
        Song song = new("Old/Song.mp3", 100);

        TrackMoves moves = PlanOne(
            song.Files,
            File("New/Song.MKV", 100, MediaFileKind.Video),
            File("New/Song.ogg", 100),
            File("New/Song 2.mp3", 101));

        moves.Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_ExtensionInAnotherCase_PairsTheSong()
    {
        Song song = new("Old/Song.mp3", 100);

        TrackMoves moves = PlanOne(song.Files, File("New/Song.MP3", 100));

        moves.Moves.Should().ContainSingle().Which.TrackId.Should().Be(song.TrackId);
    }

    [Test]
    public void Plan_VideoWithoutAudio_PairsTheVideo()
    {
        Song song = new("Old/Clip.mkv", 700, kind: MediaFileKind.Video);

        TrackMoves moves = PlanOne(song.Files, File("New/Clip.mkv", 700, MediaFileKind.Video));

        moves.Moves.Should().ContainSingle().Which.FileId.Should().Be(song.Evidence.Id);
    }

    [Test]
    public void Plan_TrackWithAFileLeftInItsFolder_IsNoVanishedTrack()
    {
        Song song = new("Old/Song.mp3", 100, picture: "Old/Song.jpg");

        TrackMoves moves = PlanOne(
            song.Files,
            File("Old/Song.jpg", 100, MediaFileKind.Image),
            File("New/Song.mp3", 100));

        moves.Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_NewFileWithTheBaseNameOfTheTrackInItsFolder_IsNoVanishedTrack()
    {
        Song song = new("Old/Song.mp3", 100);

        TrackMoves moves = PlanOne(
            song.Files,
            File("Old/SONG.mkv", 700, MediaFileKind.Video),
            File("New/Song.mp3", 100));

        moves.Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_PicturesOfEqualSize_PairNothing()
    {
        Song song = new("Old/Song.mp3", 100, picture: "Old/Song.jpg");

        TrackMoves moves = PlanOne(song.Files, File("New/Song.jpg", 100, MediaFileKind.Image));

        moves.Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_NewFileInAFolderWithAStoredTrackOfItsBaseName_JoinsThatTrackAndPairsNothing()
    {
        Song song = new("Old/Song.mp3", 100);
        Song existing = new("New/Song.mkv", 700, kind: MediaFileKind.Video);

        TrackMoves moves = PlanOne(
            [.. song.Files, .. existing.Files],
            File("New/Song.mkv", 700, MediaFileKind.Video),
            File("New/Song.mp3", 100));

        moves.Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_NewFileThatJoinsAStoredTrack_StillMakesItsBucketAmbiguous()
    {
        Song song = new("Old/Song.mp3", 100);
        Song existing = new("Existing/Song.mkv", 700, kind: MediaFileKind.Video);

        TrackMoves moves = PlanOne(
            [.. song.Files, .. existing.Files],
            File("Existing/Song.mkv", 700, MediaFileKind.Video),
            File("Existing/Song.mp3", 100),
            File("New/Track.mp3", 100));

        moves.Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_NewFilesOfOneSongForTwoVanishedTracks_PairNothing()
    {
        Song audio = new("Old/Audio.mp3", 100);
        Song video = new("Old/Video.mkv", 700, kind: MediaFileKind.Video);

        TrackMoves moves = PlanOne(
            [.. audio.Files, .. video.Files],
            File("New/Song.mp3", 100),
            File("New/Song.mkv", 700, MediaFileKind.Video));

        moves.Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_UnchangedFolder_HasNoVanishedTrack()
    {
        Song song = new("Old/Song.mp3", 100);

        // The folder of the song is not in the scan's view of changed or deleted folders.
        ScanPlan plan = ScanPlanner.Plan(Disk(File("New/Song.mp3", 100)), [], 1);

        TrackMovePlanner.Plan([(_music, plan, song.Files)]).Should().BeSameAs(TrackMoves.None);
    }

    [Test]
    public void Plan_SongMovedToAnotherSource_PairsItAcrossTheSources()
    {
        Song song = new("Inbox/Song.mp3", 100);
        ScanPlan music = ScanPlanner.Plan(Disk(File("Other/Stays.mp3", 50)), song.Files, 1);
        ScanPlan videos = ScanPlanner.Plan(Disk(File("Album/Song.mp3", 100)), [], 1);

        TrackMoves moves = TrackMovePlanner.Plan([(_music, music, song.Files), (_videos, videos, [])]);

        moves.Moves.Should().ContainSingle().Which.Should().Be(
            new TrackMove(
                _videos,
                "Album/Song.mp3",
                song.TrackId,
                song.Evidence.Id,
                _music,
                "Inbox",
                "Inbox/Song.mp3"));
        moves.Deferred.Should().ContainSingle().Which.SourceId.Should().Be(_music);
    }

    [Test]
    public void Plan_NothingMoved_ReturnsNone()
    {
        Song song = new("Album/Song.mp3", 100);

        TrackMoves moves = PlanOne(song.Files, File("Album/Song.mp3", 100), File("Album/New.mp3", 300));

        moves.Should().BeSameAs(TrackMoves.None);
        TrackMoves.None.IsEmpty.Should().BeTrue();
        TrackMoves.None.Deferred.Should().BeEmpty();
        TrackMoves.None.IsDeferred(song.TrackId).Should().BeFalse();
        TrackMoves.None.FindDeferred(song.TrackId).Should().BeNull();
    }

    [Test]
    public void Plan_AnyOrderOfTheInput_GivesTheSameResult()
    {
        Song[] songs =
        [
            new("A/01.mp3", 100),
            new("A/02.mp3", 100),
            new("A/03.mp3", 300, picture: "A/03.jpg"),
            new("B/Clip.mkv", 700, kind: MediaFileKind.Video),
            new("C/Song.flac", 900, duplicate: "C/Song.mp3"),
        ];
        DiscoveredFile[] onDisk =
        [
            File("Z/A/01.mp3", 100),
            File("Z/A/02.mp3", 100),
            File("Z/A/03.mp3", 300),
            File("Z/A/03.jpg", 300, MediaFileKind.Image),
            File("Z/B/Clip.mkv", 700, MediaFileKind.Video),
            File("Z/C/Song.flac", 900),
            File("Z/C/Song.mp3", 100),
        ];
        MediaFileSnapshot[] stored = [.. songs.SelectMany(song => song.Files)];
        Song otherSong = new("Inbox/Other.mp3", 444);
        DiscoveredFile otherOnDisk = File("Moved/Other.mp3", 444);

        TrackMoves expected = TrackMovePlanner.Plan([
            (_music, ScanPlanner.Plan(Disk(onDisk), stored, 1), stored),
            (_videos, ScanPlanner.Plan(Disk(otherOnDisk), otherSong.Files, 1), otherSong.Files),
        ]);
        MediaFileSnapshot[] reversedStored = [.. Enumerable.Reverse(stored)];
        TrackMoves reversed = TrackMovePlanner.Plan([
            (_videos, ScanPlanner.Plan(Disk(otherOnDisk), otherSong.Files, 1), otherSong.Files),
            (_music, ScanPlanner.Plan(Disk([.. Enumerable.Reverse(onDisk)]), reversedStored, 1), reversedStored),
        ]);

        expected.Moves.Select(move => move.RelativePath).Should().Equal(
            "Z/A/01.mp3",
            "Z/A/02.mp3",
            "Z/A/03.mp3",
            "Z/B/Clip.mkv",
            "Z/C/Song.flac",
            "Moved/Other.mp3");
        reversed.Moves.Should().Equal(expected.Moves);
        reversed.Deferred.Select(Describe).Should().Equal(expected.Deferred.Select(Describe));
        expected.Deferred.Select(track => track.TrackId).Should().BeInAscendingOrder();
        expected.Deferred.Should().HaveCount(6);
    }

    private static string Describe(VanishedTrack track)
    {
        return $"{track.TrackId} {track.SourceId} {track.DirectoryPath} "
            + string.Join(",", track.Files.Select(file => $"{file.FileId}:{file.RelativePath}"));
    }

    private static TrackMoves PlanOne(IReadOnlyList<MediaFileSnapshot> stored, params DiscoveredFile[] onDisk)
    {
        return TrackMovePlanner.Plan([(_music, ScanPlanner.Plan(Disk(onDisk), stored, 1), stored)]);
    }

    /// <summary>
    /// The folders of the files, as the walker finds them.
    /// </summary>
    private static DiscoveredDirectory[] Disk(params DiscoveredFile[] files)
    {
        return [.. files
            .GroupBy(file => DirectoryOf(file.RelativePath))
            .Select(directory => new DiscoveredDirectory(directory.Key, [.. directory]))];
    }

    private static DiscoveredFile File(string path, long size, MediaFileKind kind = MediaFileKind.Audio)
    {
        return new DiscoveredFile(path, path[(path.LastIndexOf('/') + 1)..], kind, size, _time);
    }

    private static string DirectoryOf(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path[..slash];
    }

    /// <summary>
    /// A stored song: its primary file, optionally a duplicate of the same kind it does not play and a picture of the
    /// same size as the primary file.
    /// </summary>
    private sealed class Song
    {
        public Song(
            string path,
            long size,
            MediaFileKind kind = MediaFileKind.Audio,
            string? picture = null,
            string? duplicate = null)
        {
            Evidence = Stored(path, size, kind, isPrimary: true);
            List<MediaFileSnapshot> files = [Evidence];
            if (picture is not null)
            {
                files.Add(Stored(picture, size, MediaFileKind.Image, isPrimary: false));
            }

            if (duplicate is not null)
            {
                files.Add(Stored(duplicate, 100, kind, isPrimary: false));
            }

            Files = [.. files.OrderBy(file => file.RelativePath, StringComparer.Ordinal)];
        }

        public Guid TrackId { get; } = Guid.NewGuid();

        public MediaFileSnapshot Evidence { get; }

        public IReadOnlyList<MediaFileSnapshot> Files { get; }

        private MediaFileSnapshot Stored(string path, long size, MediaFileKind kind, bool isPrimary)
        {
            return new MediaFileSnapshot(
                Guid.NewGuid(),
                path,
                DirectoryOf(path),
                kind,
                size,
                _time,
                1,
                TrackId,
                isPrimary);
        }
    }
}
