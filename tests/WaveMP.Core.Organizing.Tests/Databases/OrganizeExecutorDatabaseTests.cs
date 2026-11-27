using System.Runtime.Versioning;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Metadata.Files;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.Organizing.Extensions;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Jobs;
using WaveMP.Core.Organizing.Operations;
using WaveMP.Core.Organizing.OwnAlbums;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Core.Scanning.Edits;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Services;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Organizing.Tests.Databases;

/// <summary>
/// Applying, undoing, canceling and recovering organize operations on synthetic media in a temporary folder: files
/// move together and are never replaced, tags change only in audio files after a backup, the library keeps its ids.
/// </summary>
[TestFixture]
public sealed class OrganizeExecutorDatabaseTests : OrganizingDatabaseTestBase
{
    private static readonly TestTags _record = new() { Artist = "Band", AlbumArtist = "Band", Album = "Record" };

    private Func<string, bool> _isWritable = _ => true;

    protected override string DatabaseName => "wavemp_tests_organizing_executor";

    [SetUp]
    public void SetUpWriteAccess()
    {
        _isWritable = _ => true;
    }

    protected override void ConfigureServices(IServiceCollection services)
    {
        IFileWriteAccess writeAccess = Substitute.For<IFileWriteAccess>();
        writeAccess.IsWritable(Arg.Any<string>()).Returns(call => _isWritable(call.Arg<string>()));
        services.AddSingleton(writeAccess);
    }

    [Test]
    public async Task Applying_moves_every_file_of_a_song_together_and_keeps_its_id_and_the_data_of_the_users()
    {
        await AudioAsync("Messy/one.mp3", _record with { Title = "One", Track = "1" });
        await VideoAsync("Messy/one.mkv");
        await TestMediaGenerator.CreateImageAsync(Media.Combine("Messy/one.jpg"), 32, 32);
        await ScanAsync();
        Guid trackId = await TrackIdAsync("One");
        Dictionary<string, byte[]> contents = Contents("Messy/one.mp3", "Messy/one.mkv", "Messy/one.jpg");
        UserData before = await AddUserDataAsync(trackId);

        OrganizeOperation preview = await LibraryPreviewAsync();
        (await ApplyAsync(preview.Id)).Status.Should().Be(OrganizeApplyStatus.Queued);
        (await OperationAsync(preview.Id)).Status.Should().Be(OrganizeOperationStatus.Queued);
        LibraryOrganizeResult result = (await RunOrganizeJobAsync()).Single();

        (result.Status, result.DoneItems).Should().Be((OrganizeOperationStatus.Completed, 1));
        ContentsAt(contents, "Messy/one", "Band/Record/01 One");
        Directory.Exists(Media.Combine("Messy")).Should().BeFalse("the emptied folder is removed");
        await using (WaveMpDbContext db = CreateDbContext())
        {
            Track track = await db.Tracks.SingleAsync();
            (track.Id, track.DirectoryPath, track.BaseName).Should().Be((trackId, "Band/Record", "01 One"));
            (await db.MediaFiles.Where(file => file.TrackId == trackId).Select(file => file.RelativePath).ToListAsync())
                .Should().BeEquivalentTo("Band/Record/01 One.mp3", "Band/Record/01 One.mkv", "Band/Record/01 One.jpg");
            OrganizeItem item = await db.OrganizeItems.SingleAsync();
            (item.Status, item.CreatedFolders, item.RemovedFolders)
                .Should().BeEquivalentTo((OrganizeItemStatus.Done, new[] { "Band", "Band/Record" }, new[] { "Messy" }));
        }

        (await UserDataAsync(trackId)).Should().BeEquivalentTo(
            before with { SelectionPath = "Band/Record/01 One.mkv" });
        JobRequests.Should().Contain(request => request.JobId == JobIds.CoverCache);

        OrganizeOperation undo = await UndoPreviewAsync(preview.Id);
        (await ApplyAsync(undo.Id)).Status.Should().Be(OrganizeApplyStatus.Queued);
        (await RunOrganizeJobAsync()).Single().Status.Should().Be(OrganizeOperationStatus.Completed);

        ContentsAt(contents, "Messy/one", "Messy/one");
        Directory.Exists(Media.Combine("Band")).Should().BeFalse("the folders the operation created are removed");
        (await OperationAsync(preview.Id)).UndoneById.Should().Be(undo.Id);
        (await UserDataAsync(trackId)).Should().BeEquivalentTo(before);
        await using (WaveMpDbContext db = CreateDbContext())
        {
            (await db.Tracks.SingleAsync()).DirectoryPath.Should().Be("Messy");
        }
    }

    [Test]
    public async Task Own_album_writes_audio_tags_after_a_backup_and_its_cover_and_undo_restores_all()
    {
        await AudioAsync(
            "Incoming/b.mp3",
            new TestTags { Title = "Beta", Artist = "Band", Album = "Old", Track = "7" });
        await VideoAsync("Incoming/b.mkv", new TestTags { Title = "Beta", Album = "Video album" });
        await ScanAsync();
        Guid trackId = await TrackIdAsync("Beta");
        byte[] video = await File.ReadAllBytesAsync(Media.Combine("Incoming/b.mkv"));
        Guid album = await OwnAlbumAsync("Mix", "Various", [trackId]);
        byte[] cover = await UploadCoverAsync(album);

        OrganizeOperation preview =
            (await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null))).Operation!;
        (await ApplyAsync(preview.Id)).Status.Should().Be(OrganizeApplyStatus.Queued);
        (await RunOrganizeJobAsync()).Single().Status.Should().Be(OrganizeOperationStatus.Completed);

        AlbumOf("Various/Mix/01 Beta.mp3").Should().Be(("Mix", "Various", 1u));
        (await File.ReadAllBytesAsync(Media.Combine("Various/Mix/01 Beta.mkv"))).Should().Equal(
            video,
            "videos keep their tags");
        (await File.ReadAllBytesAsync(Media.Combine("Various/Mix/cover.jpg"))).Should().Equal(cover);
        Directory.Exists(Media.Combine("Incoming")).Should().BeFalse();
        await using (WaveMpDbContext db = CreateDbContext())
        {
            TagBackup backup = await db.TagBackups.SingleAsync();
            (backup.Album, backup.TrackNumber, backup.RelativePath).Should().Be(("Old", 7, "Incoming/b.mp3"));
            OrganizeItem song = await db.OrganizeItems.SingleAsync(item => item.Kind == OrganizeItemKind.Track);
            song.TagBackupId.Should().Be(backup.Id);
            (await db.OwnAlbums.SingleAsync()).AppliedAt.Should().Be(Now);
            (await db.Tracks.SingleAsync()).Id.Should().Be(trackId);
            (await db.Albums.Include(row => row.Tracks).SingleAsync(row => row.Title == "Mix")).Tracks
                .Should().ContainSingle();
        }

        OrganizeOperation undo = await UndoPreviewAsync(preview.Id);
        (await ApplyAsync(undo.Id)).Status.Should().Be(OrganizeApplyStatus.Queued);
        (await RunOrganizeJobAsync()).Single().Status.Should().Be(OrganizeOperationStatus.Completed);

        AlbumOf("Incoming/b.mp3").Should().Be(("Old", null, 7u));
        (await File.ReadAllBytesAsync(Media.Combine("Incoming/b.mkv"))).Should().Equal(video);
        Directory.Exists(Media.Combine("Various")).Should().BeFalse("the cover and the created folders are gone");
        Snapshot().Should().OnlyContain(entry => entry.StartsWith("Incoming", StringComparison.Ordinal));
        await using (WaveMpDbContext db = CreateDbContext())
        {
            (await db.OwnAlbums.SingleAsync()).AppliedAt.Should().BeNull("its only run was undone");
            (await db.Tracks.SingleAsync()).Id.Should().Be(trackId);
        }
    }

    [Test]
    public async Task A_file_that_appears_at_the_target_after_the_preview_stops_the_run_and_stays_untouched()
    {
        await AudioAsync("Messy/one.mp3", _record with { Title = "One", Track = "1" });
        await ScanAsync();
        IReadOnlyList<string> before = Snapshot();
        OrganizeOperation preview = await LibraryPreviewAsync();
        Directory.CreateDirectory(Media.Combine("Band/Record"));
        byte[] intruder = [1, 2, 3, 4];
        await File.WriteAllBytesAsync(Media.Combine("Band/Record/01 ONE.mp3"), intruder);

        (await ApplyAsync(preview.Id)).Status.Should().Be(OrganizeApplyStatus.Queued);
        LibraryOrganizeResult result = (await RunOrganizeJobAsync()).Single();

        (result.Status, result.DoneItems, result.FailedItems).Should().Be((OrganizeOperationStatus.Stopped, 0, 1));
        (await File.ReadAllBytesAsync(Media.Combine("Band/Record/01 ONE.mp3"))).Should().Equal(intruder);
        Snapshot().Where(entry => !entry.StartsWith("Band", StringComparison.Ordinal)).Should().Equal(before);
        OrganizeOperation stopped = await OperationAsync(preview.Id);
        stopped.ErrorCode.Should().Be(OrganizeErrorCode.ItemFailed);
        await using WaveMpDbContext db = CreateDbContext();
        OrganizeItem item = await db.OrganizeItems.SingleAsync();
        (item.Status, item.Conflict).Should().Be((OrganizeItemStatus.Failed, OrganizeConflict.TargetExists));
        (await db.Tracks.SingleAsync()).DirectoryPath.Should().Be("Messy");
    }

    [Test]
    public async Task Conflicts_of_the_preview_are_only_skipped_on_request_and_never_overwritten()
    {
        await AudioAsync("Messy/one.mp3", _record with { Title = "One", Track = "1" });
        await AudioAsync("Messy/two.mp3", _record with { Title = "Two", Track = "2" });
        Directory.CreateDirectory(Media.Combine("Band/Record"));
        byte[] other = [9, 8, 7];
        await File.WriteAllBytesAsync(Media.Combine("Band/Record/01 One.txt"), other);
        await ScanAsync();
        byte[] one = await File.ReadAllBytesAsync(Media.Combine("Messy/one.mp3"));
        OrganizeOperation preview = await LibraryPreviewAsync();
        preview.ConflictItems.Should().Be(1);

        (await ApplyAsync(preview.Id)).Status.Should().Be(OrganizeApplyStatus.Conflicts);
        (await ApplyAsync(preview.Id, skipConflicts: true)).Status.Should().Be(OrganizeApplyStatus.Queued);
        LibraryOrganizeResult result = (await RunOrganizeJobAsync()).Single();

        (result.Status, result.DoneItems, result.SkippedItems).Should().Be((OrganizeOperationStatus.Completed, 1, 1));
        File.Exists(Media.Combine("Band/Record/02 Two.mp3")).Should().BeTrue();
        (await File.ReadAllBytesAsync(Media.Combine("Messy/one.mp3"))).Should().Equal(one);
        (await File.ReadAllBytesAsync(Media.Combine("Band/Record/01 One.txt"))).Should().Equal(other);
        await using WaveMpDbContext db = CreateDbContext();
        (await db.OrganizeItems.Where(item => item.Conflict != null).Select(item => item.Status).SingleAsync())
            .Should().Be(OrganizeItemStatus.Skipped);
    }

    [Test]
    public async Task A_song_changed_after_the_preview_is_not_moved()
    {
        await AudioAsync("Messy/one.mp3", _record with { Title = "One", Track = "1" });
        await ScanAsync();
        OrganizeOperation preview = await LibraryPreviewAsync();
        await File.AppendAllTextAsync(Media.Combine("Messy/one.mp3"), "changed");
        byte[] changed = await File.ReadAllBytesAsync(Media.Combine("Messy/one.mp3"));

        await ApplyAsync(preview.Id);
        (await RunOrganizeJobAsync()).Single().Status.Should().Be(OrganizeOperationStatus.Stopped);

        (await File.ReadAllBytesAsync(Media.Combine("Messy/one.mp3"))).Should().Equal(changed);
        Directory.Exists(Media.Combine("Band")).Should().BeFalse();
        await using WaveMpDbContext db = CreateDbContext();
        (await db.OrganizeItems.SingleAsync()).Conflict.Should().Be(OrganizeConflict.SourceMissing);
    }

    [Test]
    public async Task An_audio_file_that_cannot_be_written_is_left_as_it_is()
    {
        await AudioAsync("Incoming/b.mp3", new TestTags { Title = "Beta", Artist = "Band", Album = "Old" });
        await ScanAsync();
        Guid album = await OwnAlbumAsync("Mix", "Various", [await TrackIdAsync("Beta")]);
        OrganizeOperation preview =
            (await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null))).Operation!;
        IReadOnlyList<string> before = Snapshot();
        _isWritable = _ => false;

        await ApplyAsync(preview.Id);
        (await RunOrganizeJobAsync()).Single().Status.Should().Be(OrganizeOperationStatus.Stopped);

        Snapshot().Should().Equal(before);
        await using WaveMpDbContext db = CreateDbContext();
        (await db.OrganizeItems.SingleAsync()).Conflict.Should().Be(OrganizeConflict.NotWritable);
        (await db.TagBackups.CountAsync()).Should().Be(0);
    }

    [Test]
    [Platform(Exclude = "Win", Reason = "Folder permissions are checked through Unix file modes.")]
    [UnsupportedOSPlatform("windows")]
    public async Task A_song_in_a_folder_without_write_permission_is_put_back_and_its_folders_removed()
    {
        if (Environment.UserName == "root")
        {
            Assert.Ignore("root may write into every folder.");
        }

        await AudioAsync("Messy/one.mp3", _record with { Title = "One", Track = "1" });
        await ScanAsync();
        OrganizeOperation preview = await LibraryPreviewAsync();
        IReadOnlyList<string> before = Snapshot();
        string folder = Media.Combine("Messy");
        File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            await ApplyAsync(preview.Id);
            (await RunOrganizeJobAsync()).Single().Status.Should().Be(OrganizeOperationStatus.Stopped);
        }
        finally
        {
            File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        Snapshot().Should().Equal(before);
        await using WaveMpDbContext db = CreateDbContext();
        OrganizeItem item = await db.OrganizeItems.SingleAsync();
        (item.Status, item.Conflict).Should().Be((OrganizeItemStatus.RolledBack, OrganizeConflict.NotWritable));
    }

    [Test]
    public async Task A_preview_cannot_be_applied_once_outdated_busy_expired_or_read_only()
    {
        await AudioAsync("Messy/one.mp3", _record with { Title = "One", Track = "1" });
        await ScanAsync();
        Guid album = await OwnAlbumAsync("Mix", "Various", [await TrackIdAsync("One")]);

        (await ApplyAsync(Guid.NewGuid())).Status.Should().Be(OrganizeApplyStatus.NotFound);
        OrganizePreviewResult planning = await PreviewAsync(service => service.PreviewLibraryAsync(null, null));
        (await ApplyAsync(planning.Operation!.Id)).Status.Should().Be(OrganizeApplyStatus.NotPreviewed);
        await RunOrganizeJobAsync();
        OrganizeOperation library = await OperationAsync(planning.Operation.Id);

        WriteProbe.Probe(Arg.Any<string>()).Returns(MediaFolderState.ReadOnly);
        (await ApplyAsync(library.Id)).Status.Should().Be(OrganizeApplyStatus.MediaReadOnly);
        WriteProbe.Probe(Arg.Any<string>()).Returns(MediaFolderState.Writable);

        RejectOrganizeJob = true;
        (await ApplyAsync(library.Id)).Status.Should().Be(OrganizeApplyStatus.Busy);
        (await OperationAsync(library.Id)).Status.Should().Be(OrganizeOperationStatus.Previewed);
        RejectOrganizeJob = false;

        OrganizeOperation own = (await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null))).Operation!;
        (await ApplyAsync(library.Id)).Status.Should().Be(OrganizeApplyStatus.Queued);
        (await ApplyAsync(own.Id)).Status.Should().Be(OrganizeApplyStatus.Busy, "another operation is queued");
        (await CancelAsync(library.Id)).Status.Should().Be(OrganizeCancelStatus.Requested);
        (await RunOrganizeJobAsync()).Single().Status.Should().Be(OrganizeOperationStatus.Canceled);
        File.Exists(Media.Combine("Messy/one.mp3")).Should().BeTrue();

        Now = Now.AddMinutes(1);
        await InScopeAsync(services => services.GetRequiredService<IOwnAlbumService>()
            .UpdateAsync(album, new OwnAlbumChanges(null, null, 2001, null)));
        (await ApplyAsync(own.Id)).Status.Should().Be(OrganizeApplyStatus.PreviewOutdated);

        OrganizeOperation again = (await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null))).Operation!;
        await SetTemplateAsync("{Artist}/{Title}");
        (await ApplyAsync(again.Id)).Status.Should().Be(OrganizeApplyStatus.PreviewOutdated);

        OrganizeOperation late = (await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null))).Operation!;
        Now = Now.AddHours(2);
        (await ApplyAsync(late.Id)).Status.Should().Be(OrganizeApplyStatus.PreviewExpired);
        (await CancelAsync(late.Id)).Status.Should().Be(OrganizeCancelStatus.NotCancelable);
        (await CancelAsync(Guid.NewGuid())).Status.Should().Be(OrganizeCancelStatus.NotFound);
        Snapshot().Should().ContainSingle(entry => entry.StartsWith("Messy/one.mp3|", StringComparison.Ordinal));
    }

    [Test]
    public async Task Canceling_stops_between_two_songs_and_keeps_what_is_done()
    {
        await AudioAsync("In/a.mp3", new TestTags { Title = "Alpha", Artist = "X" });
        await AudioAsync("In/b.mp3", new TestTags { Title = "Beta", Artist = "Y" });
        await ScanAsync();
        Guid album = await OwnAlbumAsync("Mix", "Various", [await TrackIdAsync("Alpha"), await TrackIdAsync("Beta")]);
        OrganizeOperation preview =
            (await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null))).Operation!;
        OrganizeRunRegistry registry = Services.GetRequiredService<OrganizeRunRegistry>();
        _isWritable = _ =>
        {
            // While the first song runs, the admin asks the operation to stop.
            registry.RequestCancel(preview.Id);
            return true;
        };

        await ApplyAsync(preview.Id);
        LibraryOrganizeResult result = (await RunOrganizeJobAsync()).Single();

        (result.Status, result.DoneItems).Should().Be((OrganizeOperationStatus.Canceled, 1));
        File.Exists(Media.Combine("Various/Mix/01 Alpha.mp3")).Should().BeTrue();
        File.Exists(Media.Combine("In/b.mp3")).Should().BeTrue();
        AlbumOf("In/b.mp3").Album.Should().BeNull("the second song was not touched");
        await using WaveMpDbContext db = CreateDbContext();
        (await db.OrganizeItems.OrderBy(item => item.Sequence).Select(item => item.Status).ToListAsync())
            .Should().Equal(OrganizeItemStatus.Done, OrganizeItemStatus.Pending);
    }

    [Test]
    public async Task The_planning_of_a_library_preview_can_be_canceled()
    {
        await AudioAsync("Messy/one.mp3", _record with { Title = "One", Track = "1" });
        await ScanAsync();
        OrganizePreviewResult planning = await PreviewAsync(service => service.PreviewLibraryAsync(null, null));

        (await CancelAsync(planning.Operation!.Id)).Status.Should().Be(OrganizeCancelStatus.Requested);
        await RunOrganizeJobAsync();

        (await OperationAsync(planning.Operation.Id)).Status.Should().Be(OrganizeOperationStatus.Canceled);
        RejectOrganizeJob = true;
        (await PreviewAsync(service => service.PreviewLibraryAsync(null, null))).Status
            .Should().Be(OrganizePreviewStatus.Busy);
        await using WaveMpDbContext db = CreateDbContext();
        (await db.OrganizeOperations.CountAsync()).Should().Be(1, "the rejected preview was removed");
    }

    [Test]
    public async Task A_run_waits_for_a_scan_that_holds_the_library()
    {
        await AudioAsync("Messy/one.mp3", _record with { Title = "One", Track = "1" });
        await ScanAsync();
        OrganizeOperation preview = await LibraryPreviewAsync();
        await ApplyAsync(preview.Id);
        LibraryMaintenanceLock maintenance = Services.GetRequiredService<LibraryMaintenanceLock>();

        Task<IReadOnlyList<LibraryOrganizeResult>> run;
        using (await maintenance.AcquireAsync())
        {
            run = RunOrganizeJobAsync();
            await Task.Delay(300);
            run.IsCompleted.Should().BeFalse();
            File.Exists(Media.Combine("Messy/one.mp3")).Should().BeTrue();
        }

        (await run).Single().Status.Should().Be(OrganizeOperationStatus.Completed);
        File.Exists(Media.Combine("Band/Record/01 One.mp3")).Should().BeTrue();
    }

    [Test]
    public async Task A_start_after_a_crash_puts_back_the_half_done_song_from_the_journal()
    {
        await AudioAsync(
            "Incoming/b.mp3",
            new TestTags { Title = "Beta", Artist = "Band", Album = "Old", Track = "7" });
        await VideoAsync("Incoming/b.mkv");
        await ScanAsync();
        Guid trackId = await TrackIdAsync("Beta");
        Dictionary<string, byte[]> contents = Contents("Incoming/b.mp3", "Incoming/b.mkv");
        Guid album = await OwnAlbumAsync("Mix", "Various", [trackId]);
        OrganizeOperation preview =
            (await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null))).Operation!;
        await ApplyAsync(preview.Id);
        JobRequests.Clear();

        // What a run did up to the crash: the journal, the tag backup and the new tags, the folders, the audio file.
        await InScopeAsync(async services =>
        {
            IOrganizeRepository repository = services.GetRequiredService<IOrganizeRepository>();
            OrganizeItem item = (await repository.ListItemsAfterAsync(preview.Id, -1, 10)).Single();
            OrganizeFileMove audio = item.Moves.Single(move => move.FileKind == OrganizeFileKind.Audio);
            await repository.StartRunAsync(preview.Id, Now, null);
            await repository.BeginItemAsync(item.Id, ["Various", "Various/Mix"]);
            ITagWriter tags = services.GetRequiredService<ITagWriter>();
            string source = Media.Combine(audio.FromPath!);
            TagSnapshotResult snapshot = tags.ReadSnapshot(source);
            snapshot.IsRead.Should().BeTrue();
            await repository.AddItemTagBackupAsync(
                item.Id,
                TagBackups.Create(snapshot.Snapshot!, audio.MediaFileId, audio.FromPath!, Now),
                album,
                trackId);
            tags.Write(source, MediaFileKind.Audio, OrganizeTags.ToTagChanges(item.TagChanges!))
                .Should().Be(TagWriteStatus.Written);
            Directory.CreateDirectory(Media.Combine("Various/Mix"));
            File.Move(source, Media.Combine(audio.ToPath!));
            return true;
        });

        await Services.InitializeWaveMpOrganizingAsync();

        AlbumOf("Incoming/b.mp3").Should().Be(("Old", null, 7u));
        (await File.ReadAllBytesAsync(Media.Combine("Incoming/b.mkv"))).Should().Equal(contents["Incoming/b.mkv"]);
        Directory.Exists(Media.Combine("Various")).Should().BeFalse();
        OrganizeOperation interrupted = await OperationAsync(preview.Id);
        (interrupted.Status, interrupted.ErrorCode)
            .Should().Be((OrganizeOperationStatus.Interrupted, OrganizeErrorCode.Interrupted));
        await using (WaveMpDbContext db = CreateDbContext())
        {
            (await db.OrganizeItems.SingleAsync()).Status.Should().Be(OrganizeItemStatus.RolledBack);
            Track track = await db.Tracks.Include(row => row.Album).SingleAsync();
            (track.Id, track.DirectoryPath, track.Album!.Title).Should().Be((trackId, "Incoming", "Old"));
        }

        await Services.InitializeWaveMpOrganizingAsync();
        File.Exists(Media.Combine("Incoming/b.mp3")).Should().BeTrue("a second start changes nothing");
    }

    [Test]
    public async Task A_compilation_without_album_artist_stays_one_album()
    {
        // WMP-116: the album of a compilation without album artist is found by its folder.
        TestTags hits = new() { Album = "Hits", Compilation = "1" };
        await AudioAsync("Hits/1.mp3", hits with { Title = "One", Artist = "A", Track = "1" });
        await AudioAsync("Hits/2.mp3", hits with { Title = "Two", Artist = "B", Track = "2" });
        await ScanAsync();

        OrganizeOperation preview = await LibraryPreviewAsync();
        await ApplyAsync(preview.Id);
        (await RunOrganizeJobAsync()).Single().DoneItems.Should().Be(2);

        await using WaveMpDbContext db = CreateDbContext();
        List<Track> tracks = await db.Tracks.ToListAsync();
        tracks.Select(track => track.DirectoryPath).Distinct().Should().ContainSingle().Which.Should().NotBe("Hits");
        Album album = await db.Albums.SingleAsync();
        album.IsCompilation.Should().BeTrue();
        tracks.Should().OnlyContain(track => track.AlbumId == album.Id);
    }

    [Test]
    public async Task The_picture_in_the_artist_folder_stays_the_picture_of_the_artist()
    {
        // WMP-128: songs of two albums leave the artist folder, its picture stays there.
        await AudioAsync(
            "Band/a.mp3",
            new TestTags { Title = "A", Artist = "Band", AlbumArtist = "Band", Album = "One" });
        await AudioAsync(
            "Band/b.mp3",
            new TestTags { Title = "B", Artist = "Band", AlbumArtist = "Band", Album = "Two" });
        await TestMediaGenerator.CreateImageAsync(Media.Combine("Band/artist.jpg"), 32, 32);
        await ScanAsync();

        OrganizeOperation preview = await LibraryPreviewAsync();
        await ApplyAsync(preview.Id);
        (await RunOrganizeJobAsync()).Single().DoneItems.Should().Be(2);

        File.Exists(Media.Combine("Band/artist.jpg")).Should().BeTrue();
        await using WaveMpDbContext db = CreateDbContext();
        (await db.Tracks.Select(track => track.DirectoryPath).ToListAsync())
            .Should().BeEquivalentTo("Band/One", "Band/Two");
        Artist artist = await db.Artists.Include(row => row.LocalPictureFile).SingleAsync(row => row.Name == "Band");
        artist.LocalPictureFile!.RelativePath.Should().Be("Band/artist.jpg");
    }

    private async Task<OrganizeApplyResult> ApplyAsync(Guid operationId, bool skipConflicts = false)
    {
        return await InScopeAsync(services =>
            services.GetRequiredService<IOrganizeService>().ApplyAsync(operationId, skipConflicts));
    }

    /// <summary>
    /// Changes the layout template; every test starts with the default one.
    /// </summary>
    private async Task SetTemplateAsync(string template)
    {
        OrganizingSettingsResult result = await InScopeAsync(services =>
            services.GetRequiredService<IOrganizingSettingsService>()
                .UpdateAsync(new UpdateOrganizingSettingsCommand(template)));
        result.IsSuccess.Should().BeTrue();
    }

    private async Task<OrganizeCancelResult> CancelAsync(Guid operationId)
    {
        return await InScopeAsync(services => services.GetRequiredService<IOrganizeService>().CancelAsync(operationId));
    }

    private Task<OrganizePreviewResult> PreviewAsync(Func<IOrganizePreviewService, Task<OrganizePreviewResult>> action)
    {
        return InScopeAsync(services => action(services.GetRequiredService<IOrganizePreviewService>()));
    }

    private async Task<OrganizeOperation> UndoPreviewAsync(Guid operationId)
    {
        OrganizePreviewResult undo = await PreviewAsync(service => service.PreviewUndoAsync(operationId, null));
        undo.Status.Should().Be(OrganizePreviewStatus.Created);
        undo.Operation!.ConflictItems.Should().Be(0);
        return undo.Operation;
    }

    private async Task<Guid> OwnAlbumAsync(string title, string albumArtist, IReadOnlyList<Guid> trackIds)
    {
        return await InScopeAsync(async services =>
        {
            IOwnAlbumService service = services.GetRequiredService<IOwnAlbumService>();
            OwnAlbumResult created = await service.CreateAsync(
                new OwnAlbumValues(title, albumArtist, null, null),
                null);
            (await service.ReplaceTracksAsync(created.Item!.Album.Id, trackIds)).IsOk.Should().BeTrue();
            return created.Item!.Album.Id;
        });
    }

    private async Task<byte[]> UploadCoverAsync(Guid album)
    {
        string picture = Path.Combine(Data.Path, "upload.jpg");
        await TestMediaGenerator.CreateImageAsync(picture, 32, 32);
        byte[] content = await File.ReadAllBytesAsync(picture);
        await using (FileStream upload = File.OpenRead(picture))
        {
            await InScopeAsync(
                services => services.GetRequiredService<IOwnAlbumService>().SetCoverAsync(album, upload));
        }

        File.Delete(picture);
        return content;
    }

    private Dictionary<string, byte[]> Contents(params string[] relativePaths)
    {
        return relativePaths.ToDictionary(path => path, path => File.ReadAllBytes(Media.Combine(path)));
    }

    /// <summary>
    /// Every file is at its new base name with its old content.
    /// </summary>
    private void ContentsAt(Dictionary<string, byte[]> contents, string oldBase, string newBase)
    {
        foreach ((string path, byte[] content) in contents)
        {
            string moved = newBase + path[oldBase.Length..];
            File.ReadAllBytes(Media.Combine(moved)).Should().Equal(content, moved);
            if (moved != path)
            {
                File.Exists(Media.Combine(path)).Should().BeFalse(path);
            }
        }
    }

    private (string? Album, string? AlbumArtist, uint Track) AlbumOf(string relativePath)
    {
        using var file = TagLib.File.Create(Media.Combine(relativePath));
        return (file.Tag.Album, file.Tag.FirstAlbumArtist, file.Tag.Track);
    }

    private async Task<UserData> AddUserDataAsync(Guid trackId)
    {
        await using WaveMpDbContext db = CreateDbContext();
        string name = "Tester " + Guid.NewGuid().ToString("N");
        User user = new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Initials = "T",
            Color = "blue",
            Role = UserRole.User,
            IsBuiltIn = false,
            CreatedAt = Now,
        };
        Playlist playlist = new()
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Name = "Mine",
            NormalizedName = "MINE",
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        MediaFile video = await db.MediaFiles.SingleAsync(
            file => file.TrackId == trackId && file.Kind == MediaFileKind.Video);
        db.Users.Add(user);
        db.Playlists.Add(playlist);
        db.PlaylistEntries.Add(new PlaylistEntry
        {
            Id = Guid.CreateVersion7(),
            PlaylistId = playlist.Id,
            TrackId = trackId,
            Position = 0,
            AddedAt = Now,
        });
        db.Favorites.Add(
            new Favorite
            {
                Id = Guid.CreateVersion7(),
                UserId = user.Id,
                Type = FavoriteType.Track,
                TrackId = trackId,
                CreatedAt = Now,
            });
        db.TrackRatings.Add(new TrackRating { UserId = user.Id, TrackId = trackId, Rating = 4, UpdatedAt = Now });
        db.PlayHistory.Add(
            new PlayHistoryEntry { Id = Guid.CreateVersion7(), UserId = user.Id, TrackId = trackId, PlayedAt = Now });
        db.TrackFileSelections.Add(new TrackFileSelection
        {
            TrackId = trackId,
            Category = FileSelectionCategory.Video,
            MediaFileId = video.Id,
            RelativePath = video.RelativePath,
            IsMissing = false,
            SelectedAt = Now,
        });
        await db.SaveChangesAsync();
        return await UserDataAsync(trackId);
    }

    private async Task<UserData> UserDataAsync(Guid trackId)
    {
        await using WaveMpDbContext db = CreateDbContext();
        TrackFileSelection selection = await db.TrackFileSelections.SingleAsync(row => row.TrackId == trackId);
        return new UserData(
            await db.Favorites.CountAsync(row => row.TrackId == trackId),
            await db.TrackRatings.Where(row => row.TrackId == trackId).Select(row => row.Rating).SingleAsync(),
            await db.PlayHistory.CountAsync(row => row.TrackId == trackId),
            await db.PlaylistEntries.CountAsync(row => row.TrackId == trackId),
            selection.MediaFileId,
            selection.RelativePath,
            selection.IsMissing);
    }

    private sealed record UserData(
        int Favorites,
        int Rating,
        int Plays,
        int PlaylistEntries,
        Guid SelectionFileId,
        string SelectionPath,
        bool SelectionMissing);
}
