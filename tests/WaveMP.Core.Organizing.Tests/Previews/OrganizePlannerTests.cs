using AwesomeAssertions;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Core.Organizing.Templates;
using WaveMP.Core.Settings.Templates;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Organizing.Tests.Previews;

public sealed class OrganizePlannerTests
{
    private const string DefaultTemplate = "{AlbumArtist}/{Album}/{TrackNumber:00} {Title}";

    private static readonly Guid _source = Guid.NewGuid();

    private FakeOrganizeDisk _disk = null!;
    private HashSet<Guid> _usable = null!;

    [SetUp]
    public void SetUp()
    {
        _disk = new FakeOrganizeDisk();
        _usable = [_source];
    }

    [Test]
    public void All_files_of_a_song_move_together_and_keep_one_base_name()
    {
        OrganizeSongRow song = Song("Incoming", "track", ["mp3", "mkv", "jpg", "lrc"]);

        OrganizeItem item = Plan(song).Single();

        item.Status.Should().Be(OrganizeItemStatus.Pending);
        item.Conflict.Should().BeNull();
        item.TrackId.Should().Be(song.TrackId);
        item.FromDirectoryPath.Should().Be("Incoming");
        item.ToDirectoryPath.Should().Be("Artist/Album");
        item.ToBaseName.Should().Be("01 Title");
        item.Moves.Select(move => move.ToPath).Should().Equal(
            "Artist/Album/01 Title.mp3",
            "Artist/Album/01 Title.mkv",
            "Artist/Album/01 Title.jpg",
            "Artist/Album/01 Title.lrc");
        item.Moves.Select(move => move.FileKind).Should().Equal(
            OrganizeFileKind.Audio,
            OrganizeFileKind.Video,
            OrganizeFileKind.Image,
            OrganizeFileKind.Lyrics);
        item.Moves.Should().OnlyContain(move => move.TempPath == null && move.Status == OrganizeMoveStatus.Pending);
    }

    [Test]
    public void A_song_at_its_place_gets_no_item_unless_asked_for()
    {
        OrganizeSongRow song = Song("Artist/Album", "01 Title", ["mp3"]);

        Plan(song).Should().BeEmpty();
        Planner().PlanSongs([new PlannedSong(song, Values(song))], skipUnchanged: false).Single().Moves
            .Should().BeEmpty();
    }

    [Test]
    public void A_song_at_its_place_with_tag_changes_gets_an_item_without_moves()
    {
        OrganizeSongRow song = Song("Artist/Album", "01 Title", ["mp3"]);
        OrganizeTagValues changes = new() { Fields = [OrganizeTagField.Year], Year = 2001 };

        OrganizeItem item = Plan(new PlannedSong(song, Values(song), changes)).Single();

        item.Moves.Should().BeEmpty();
        item.TagChanges.Should().BeSameAs(changes);
        item.TagsBefore!.Fields.Should().Equal(OrganizeTagField.Year);
        item.Conflict.Should().BeNull();
    }

    [Test]
    public void Special_characters_and_dot_names_in_values_never_create_extra_or_parent_folders()
    {
        OrganizeSongRow song = Song("Incoming", "track", ["mp3"], title: "..", albumArtist: "AC/DC", album: "../x:y?");

        OrganizeItem item = Plan(song).Single();

        item.Conflict.Should().BeNull();
        string[] segments = item.Moves.Single().ToPath!.Split('/');
        segments.Should().HaveCount(3);
        segments.Should().NotContain(segment => segment == ".." || segment == "." || segment.StartsWith('.'));
    }

    [Test]
    public void A_case_only_rename_passes_through_a_temporary_name()
    {
        OrganizeSongRow song = Song("Artist/Album", "01 title", ["mp3"]);

        OrganizeFileMove move = Plan(song).Single().Moves.Single();

        move.ToPath.Should().Be("Artist/Album/01 Title.mp3");
        move.TempPath.Should().StartWith("Artist/Album/" + OrganizePlanner.TemporaryPrefix).And.EndWith(".mp3");
    }

    [Test]
    public void A_file_of_another_song_with_the_new_base_name_is_a_conflict()
    {
        _disk.AddFile("Artist/Album/01 Title.flac");
        OrganizeSongRow song = Song("Incoming", "track", ["mp3"]);

        OrganizeItem item = Plan(song).Single();

        item.Conflict.Should().Be(OrganizeConflict.TargetExists);
        item.Status.Should().Be(OrganizeItemStatus.Conflict);
    }

    [Test]
    public void Existing_folders_are_reused_in_their_spelling_and_a_name_differing_in_case_is_a_conflict()
    {
        _disk.AddFile("ARTIST/album/01 TITLE.MP3");
        OrganizeSongRow song = Song("Incoming", "track", ["mp3"]);

        OrganizeItem item = Plan(song).Single();

        item.ToDirectoryPath.Should().Be("ARTIST/album");
        item.Conflict.Should().Be(OrganizeConflict.TargetExists);
    }

    [Test]
    public void New_folders_differing_only_in_case_or_normalization_get_one_spelling()
    {
        OrganizeSongRow first = Song("In1", "a", ["mp3"], albumArtist: "Björk", title: "One");
        OrganizeSongRow second = Song("In2", "b", ["mp3"], albumArtist: "BJÖRK", title: "Two");

        IReadOnlyList<OrganizeItem> items = Plan(first, second);

        items.Select(item => item.ToDirectoryPath).Should().Equal("Björk/Album", "Björk/Album");
        items.Should().OnlyContain(item => item.Conflict == null);
    }

    [Test]
    public void A_file_where_a_folder_is_needed_is_a_conflict()
    {
        _disk.AddFile("Artist");
        OrganizeSongRow song = Song("Incoming", "track", ["mp3"]);

        Plan(song).Single().Conflict.Should().Be(OrganizeConflict.TargetExists);
    }

    [Test]
    public void Two_songs_with_the_same_target_mark_only_the_later_one()
    {
        OrganizeSongRow first = Song("In1", "a", ["mp3"]);
        OrganizeSongRow second = Song("In2", "b", ["flac"]);

        IReadOnlyList<OrganizeItem> items = Plan(first, second);

        items[0].Conflict.Should().BeNull();
        items[1].Conflict.Should().Be(OrganizeConflict.DuplicateTarget);
    }

    [Test]
    public void Duplicate_targets_are_found_across_batches()
    {
        OrganizePlanner planner = Planner();
        OrganizeSongRow first = Song("In1", "a", ["mp3"]);
        OrganizeSongRow second = Song("In2", "b", ["mp3"]);

        planner.PlanSongs([new PlannedSong(first, Values(first))], skipUnchanged: true);
        OrganizeItem item = planner.PlanSongs([new PlannedSong(second, Values(second))], skipUnchanged: true).Single();

        item.Conflict.Should().Be(OrganizeConflict.DuplicateTarget);
    }

    [Test]
    public void A_linked_folder_in_the_source_or_target_is_a_conflict()
    {
        _disk.UnsafeFolders.Add("Incoming");
        OrganizeSongRow fromLink = Song("Incoming", "track", ["mp3"]);
        _disk.UnsafeFolders.Add("Other");
        OrganizeSongRow intoLink = Song("Somewhere", "x", ["mp3"], albumArtist: "Other", title: "Else");
        _disk.AddFolder("Other");

        IReadOnlyList<OrganizeItem> items = Plan(fromLink, intoLink);

        items.Should().OnlyContain(item => item.Conflict == OrganizeConflict.UnsafePath);
    }

    [Test]
    public void A_file_changed_or_deleted_since_the_scan_is_a_conflict()
    {
        OrganizeSongRow changed = Song("In1", "a", ["mp3"], title: "One");
        _disk.AddFile("In1/a.mp3", size: 999);
        OrganizeSongRow deleted = Song("In2", "b", ["mp3"], title: "Two");
        _disk.RemoveFile("In2/b.mp3");

        IReadOnlyList<OrganizeItem> items = Plan(changed, deleted);

        items.Should().OnlyContain(item => item.Conflict == OrganizeConflict.SourceMissing);
    }

    [Test]
    public void A_song_of_an_inactive_source_is_a_conflict()
    {
        _usable.Clear();

        Plan(Song("Incoming", "track", ["mp3"])).Single().Conflict.Should().Be(OrganizeConflict.SourceInactive);
    }

    [Test]
    public void A_read_only_audio_file_is_a_conflict_only_when_tags_change()
    {
        OrganizeSongRow song = Song("Incoming", "track", ["mp3"]);
        _disk.ReadOnlyFiles.Add("Incoming/track.mp3");
        OrganizeTagValues changes = new() { Fields = [OrganizeTagField.Year], Year = 2001 };

        Plan(song).Single().Conflict.Should().BeNull();
        Planner().PlanSongs([new PlannedSong(song, Values(song), changes)], skipUnchanged: true).Single().Conflict
            .Should().Be(OrganizeConflict.NotWritable);
    }

    [Test]
    public void A_video_only_song_cannot_get_tags()
    {
        OrganizeSongRow song = Song("Incoming", "track", ["mkv"]);

        OrganizeItem item = Plan(new PlannedSong(song, Values(song), RequiresAudio: true)).Single();

        item.Conflict.Should().Be(OrganizeConflict.NoAudio);
    }

    [Test]
    public void A_path_longer_than_allowed_is_a_conflict()
    {
        OrganizeSongRow song = Song("Incoming", "track", ["mp3"]);
        OrganizePlanner planner = new(Guid.NewGuid(), Template(DefaultTemplate), 8, _disk, _usable);

        planner.PlanSongs([new PlannedSong(song, Values(song))], skipUnchanged: true).Single().Conflict
            .Should().Be(OrganizeConflict.PathTooLong);
    }

    [Test]
    public void Moving_files_without_tags_warns_that_the_library_reads_other_values()
    {
        OrganizeSongRow untagged = Song("Old Artist/Old Album", "Old Title", ["mp3"], tags: false);
        OrganizeSongRow tagged = Song("Incoming", "track", ["mp3"], title: "Two");

        IReadOnlyList<OrganizeItem> items = Plan(untagged, tagged);

        items[0].Warnings.Should().Contain(OrganizeWarning.MetadataChanges);
        items[1].Warnings.Should().BeEmpty();
    }

    [Test]
    public void Files_without_a_song_follow_when_all_songs_of_the_folder_move_into_one_folder()
    {
        OrganizeSongRow first = Song("Old", "a", ["mp3"], title: "One");
        OrganizeSongRow second = Song("Old", "b", ["mp3"], title: "Two", trackNumber: 2);
        OrganizePlanner planner = Planner();
        planner.PlanSongs([new PlannedSong(first, Values(first)), new PlannedSong(second, Values(second))], true);
        _disk.AddFile("Old/cover.jpg");

        OrganizeItem item = planner.PlanFolderFiles([Folder("Old", [first, second], "cover.jpg")]).Single();

        planner.SourceFolders.Should().Equal((_source, "Old"));
        item.Kind.Should().Be(OrganizeItemKind.FolderFiles);
        item.Conflict.Should().BeNull();
        item.Moves.Single().FromPath.Should().Be("Old/cover.jpg");
        item.Moves.Single().ToPath.Should().Be("Artist/Album/cover.jpg");
    }

    [Test]
    public void Files_without_a_song_stay_when_the_songs_go_to_different_folders_or_one_stays()
    {
        // An artist folder with a picture and songs of two albums: the picture stays for the artist (WMP-128).
        OrganizeSongRow first = Song("Artist", "a", ["mp3"], album: "One");
        OrganizeSongRow second = Song("Artist", "b", ["mp3"], album: "Two");
        OrganizeSongRow staying = Song("Kept/Album", "01 Title", ["mp3"], albumArtist: "Kept");
        OrganizeSongRow leaving = Song("Kept/Album", "x", ["mp3"], albumArtist: "Kept", title: "X", trackNumber: 2);
        OrganizePlanner planner = Planner();
        planner.PlanSongs(
            [.. new[] { first, second, staying, leaving }.Select(song => new PlannedSong(song, Values(song)))],
            skipUnchanged: true);
        _disk.AddFile("Artist/artist.jpg");
        _disk.AddFile("Kept/Album/cover.jpg");

        planner.PlanFolderFiles(
        [
            Folder("Artist", [first, second], "artist.jpg"),
            Folder("Kept/Album", [staying, leaving], "cover.jpg"),
        ]).Should().BeEmpty();
    }

    [Test]
    public void The_cover_of_an_own_album_goes_into_the_album_folder_once_per_source()
    {
        OrganizeSongRow first = Song("In1", "a", ["mp3"], title: "One");
        OrganizeSongRow second = Song("In2", "b", ["mp3"], title: "Two", trackNumber: 2);
        OrganizePlanner planner = Planner();
        planner.PlanSongs([new PlannedSong(first, Values(first)), new PlannedSong(second, Values(second))], true);
        _disk.AddFile("Artist/Album/folder.jpg");

        OrganizeItem item = planner.PlanCover(Cover(), [first.TrackId, second.TrackId], ["cover", "folder"]).Single();

        item.Kind.Should().Be(OrganizeItemKind.AlbumCover);
        item.Conflict.Should().BeNull();
        item.Warnings.Should().Equal(OrganizeWarning.OtherCoverExists);
        OrganizeFileMove move = item.Moves.Single();
        move.FromPath.Should().BeNull();
        move.ToPath.Should().Be("Artist/Album/cover.jpg");
        move.FileKind.Should().Be(OrganizeFileKind.AlbumCover);
    }

    [Test]
    public void An_existing_cover_with_the_same_content_needs_no_item_and_another_one_is_a_conflict()
    {
        OrganizeSongRow song = Song("Artist/Album", "01 Title", ["mp3"]);
        _disk.AddFile("Artist/Album/Cover.JPG");

        OrganizePlanner same = Planner();
        same.PlanSongs([new PlannedSong(song, Values(song))], skipUnchanged: true);
        _disk.SameContent.Add("Artist/Album/cover.jpg");
        same.PlanCover(Cover(), [song.TrackId], ["cover"]).Should().BeEmpty();

        _disk.SameContent.Clear();
        OrganizePlanner other = Planner();
        other.PlanSongs([new PlannedSong(song, Values(song))], skipUnchanged: true);
        other.PlanCover(Cover(), [song.TrackId], ["cover"]).Single().Conflict.Should().Be(OrganizeConflict.CoverExists);
    }

    [Test]
    public void An_undo_moves_the_files_back_and_restores_unchanged_tags()
    {
        (OrganizeItem done, OrganizeFileRow audio, TagBackup backup) = DoneItem(tagsChanged: false);

        OrganizeItem item = Planner().PlanUndo([done], Files(audio), Backups(backup)).Single();

        item.UndoOfItemId.Should().Be(done.Id);
        item.Conflict.Should().BeNull();
        item.FromDirectoryPath.Should().Be("Artist/Album");
        item.ToDirectoryPath.Should().Be("Incoming");
        item.Moves.Single().FromPath.Should().Be("Artist/Album/01 Title.mp3");
        item.Moves.Single().ToPath.Should().Be("Incoming/track.mp3");
        item.TagChanges!.Year.Should().Be(1999);
        item.TagChanges.Fields.Should().Equal(OrganizeTagField.Year);
        item.Warnings.Should().BeEmpty();
    }

    [Test]
    public void An_undo_keeps_tags_that_were_edited_since()
    {
        (OrganizeItem done, OrganizeFileRow audio, TagBackup backup) = DoneItem(tagsChanged: true);

        OrganizeItem item = Planner().PlanUndo([done], Files(audio), Backups(backup)).Single();

        item.TagChanges.Should().BeNull();
        item.Warnings.Should().Equal(OrganizeWarning.TagsChangedSince);
        item.Conflict.Should().BeNull();
    }

    [Test]
    public void An_undo_of_a_file_moved_or_changed_since_is_a_conflict()
    {
        (OrganizeItem done, OrganizeFileRow audio, TagBackup backup) = DoneItem(tagsChanged: false);
        _disk.RemoveFile("Artist/Album/01 Title.mp3");
        _disk.AddFile("Elsewhere/01 Title.mp3");
        OrganizeFileRow moved = audio with { RelativePath = "Elsewhere/01 Title.mp3" };

        Planner().PlanUndo([done], Files(moved), Backups(backup)).Single().Conflict
            .Should().Be(OrganizeConflict.ChangedSince);
    }

    [Test]
    public void An_undo_into_a_name_taken_since_is_a_conflict()
    {
        (OrganizeItem done, OrganizeFileRow audio, TagBackup backup) = DoneItem(tagsChanged: false);
        _disk.AddFile("Incoming/track.flac");

        Planner().PlanUndo([done], Files(audio), Backups(backup)).Single().Conflict
            .Should().Be(OrganizeConflict.TargetExists);
    }

    private (OrganizeItem Done, OrganizeFileRow Audio, TagBackup Backup) DoneItem(bool tagsChanged)
    {
        var fileId = Guid.NewGuid();
        _disk.AddFile("Artist/Album/01 Title.mp3");
        _disk.AddFolder("Incoming");
        TagBackup backup = new()
        {
            Id = Guid.NewGuid(),
            MediaFileId = fileId,
            RelativePath = "Incoming/track.mp3",
            Year = 1999,
            CreatedAt = FakeOrganizeDisk.Modified,
        };
        OrganizeItem done = new()
        {
            Id = Guid.NewGuid(),
            OperationId = Guid.NewGuid(),
            Sequence = 0,
            Kind = OrganizeItemKind.Track,
            Status = OrganizeItemStatus.Done,
            TrackId = Guid.NewGuid(),
            SourceId = _source,
            FromDirectoryPath = "Incoming",
            FromBaseName = "track",
            ToDirectoryPath = "Artist/Album",
            ToBaseName = "01 Title",
            DisplayTitle = "Title",
            TagChanges = new OrganizeTagValues { Fields = [OrganizeTagField.Year], Year = 2001 },
            TagBackupId = backup.Id,
        };
        done.Moves.Add(new OrganizeFileMove
        {
            Id = Guid.NewGuid(),
            ItemId = done.Id,
            Sequence = 0,
            MediaFileId = fileId,
            SourceId = _source,
            FileKind = OrganizeFileKind.Audio,
            FromPath = "Incoming/track.mp3",
            ToPath = "Artist/Album/01 Title.mp3",
            Size = 100,
            ModifiedAt = FakeOrganizeDisk.Modified,
            Status = OrganizeMoveStatus.Moved,
        });
        OrganizeFileRow audio = new(
            fileId,
            "Artist/Album/01 Title.mp3",
            "01 Title.mp3",
            MediaFileKind.Audio,
            100,
            FakeOrganizeDisk.Modified,
            true,
            true,
            new MediaFileTags { Title = "Title", Year = tagsChanged ? 2010 : 2001 });
        return (done, audio, backup);
    }

    private static Dictionary<Guid, OrganizeFileRow> Files(params OrganizeFileRow[] files)
    {
        return files.ToDictionary(file => file.MediaFileId);
    }

    private static Dictionary<Guid, TagBackup> Backups(params TagBackup[] backups)
    {
        return backups.ToDictionary(backup => backup.Id);
    }

    private IReadOnlyList<OrganizeItem> Plan(params OrganizeSongRow[] songs)
    {
        return Plan([.. songs.Select(song => new PlannedSong(song, Values(song)))]);
    }

    private IReadOnlyList<OrganizeItem> Plan(params PlannedSong[] songs)
    {
        return Planner().PlanSongs(songs, skipUnchanged: true);
    }

    private OrganizePlanner Planner()
    {
        return new OrganizePlanner(Guid.NewGuid(), Template(DefaultTemplate), 2000, _disk, _usable);
    }

    private static LayoutTemplate Template(string text)
    {
        return LayoutTemplateSyntax.Parse(text).Template!;
    }

    private static TemplateValues Values(OrganizeSongRow song)
    {
        return new TemplateValues(
            song.Title,
            song.ArtistName,
            song.AlbumArtistName,
            song.AlbumTitle,
            song.Year,
            song.TrackNumber,
            song.DiscNumber,
            song.Genre,
            song.IsCompilation);
    }

    private static PlannedCover Cover()
    {
        return new PlannedCover("/data/own-album-covers/x.jpg", "jpg", 10, FakeOrganizeDisk.Modified, "Album");
    }

    private OrganizeFolderRow Folder(string directory, OrganizeSongRow[] songs, params string[] looseFiles)
    {
        return new OrganizeFolderRow(
            _source,
            directory,
            [.. songs.Select(song => song.TrackId)],
            [.. looseFiles.Select(name => new OrganizeFileRow(
                Guid.NewGuid(),
                FolderNames.Combine(directory, name),
                name,
                MediaFileKind.Image,
                100,
                FakeOrganizeDisk.Modified,
                false,
                true,
                null))]);
    }

    /// <summary>
    /// A song with files of the given extensions in <paramref name="directory"/>, all present on the fake disk.
    /// </summary>
    private OrganizeSongRow Song(
        string directory,
        string baseName,
        string[] extensions,
        string title = "Title",
        string albumArtist = "Artist",
        string album = "Album",
        int trackNumber = 1,
        bool tags = true)
    {
        List<OrganizeFileRow> files = [];
        foreach (string extension in extensions)
        {
            string name = baseName + "." + extension;
            string path = FolderNames.Combine(directory, name);
            MediaFileKind kind = extension switch
            {
                "mp3" or "flac" => MediaFileKind.Audio,
                "mkv" or "mp4" => MediaFileKind.Video,
                "lrc" => MediaFileKind.Lyrics,
                _ => MediaFileKind.Image,
            };
            MediaFileTags? fileTags = tags && kind is MediaFileKind.Audio or MediaFileKind.Video
                ? new MediaFileTags
                {
                    Title = title,
                    Artist = albumArtist,
                    AlbumArtist = albumArtist,
                    Album = album,
                    TrackNumber = trackNumber,
                }
                : null;
            files.Add(new OrganizeFileRow(
                Guid.NewGuid(),
                path,
                name,
                kind,
                100,
                FakeOrganizeDisk.Modified,
                files.Count == 0,
                true,
                fileTags));
            _disk.AddFile(path);
        }

        return new OrganizeSongRow(
            Guid.NewGuid(),
            _source,
            directory,
            baseName,
            title,
            albumArtist,
            null,
            album,
            albumArtist,
            false,
            null,
            trackNumber,
            null,
            null,
            files);
    }
}
