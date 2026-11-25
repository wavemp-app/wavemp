using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WaveMP.Core.Organizing.Files;
using WaveMP.Core.Organizing.Operations;
using WaveMP.Core.Organizing.OwnAlbums;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Organizing.Tests.Databases;

/// <summary>
/// Previews of own albums, of the library and of undos on synthetic media; a preview never touches a file.
/// </summary>
[TestFixture]
public sealed class OrganizePreviewServiceDatabaseTests : OrganizingDatabaseTestBase
{
    protected override string DatabaseName => "wavemp_tests_organize_previews";

    [Test]
    public async Task The_preview_of_an_own_album_plans_tags_and_moves_without_touching_a_file()
    {
        await AudioAsync(
            "Incoming/b.mp3",
            new TestTags { Title = "Beta", Artist = "Band", Album = "Old", Track = "7" });
        await AudioAsync("Incoming/a.flac", new TestTags { Title = "Alpha", Artist = "Solo" }, AudioFormat.Flac);
        await ScanAsync();
        Guid alpha = await TrackIdAsync("Alpha");
        Guid beta = await TrackIdAsync("Beta");
        Guid album = await OwnAlbumAsync("Mix", "Various", [alpha, beta]);
        IReadOnlyList<string> before = Snapshot();

        OrganizePreviewResult result = await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null));

        result.Status.Should().Be(OrganizePreviewStatus.Created);
        OrganizeOperation operation = result.Operation!;
        (operation.Kind, operation.Status, operation.OwnAlbumId, operation.DisplayTitle)
            .Should().Be((OrganizeOperationKind.OwnAlbum, OrganizeOperationStatus.Previewed, album, "Mix"));
        (operation.TotalItems, operation.ConflictItems).Should().Be((2, 0));
        operation.PreviewedAt.Should().Be(Now);
        operation.ExpiresAt.Should().Be(Now.AddHours(1));

        IReadOnlyList<OrganizeItem> items = await ItemsAsync(operation.Id);
        items.Select(item => (item.TrackId, item.ToDirectoryPath, item.ToBaseName)).Should().Equal(
            (alpha, "Various/Mix", "01 Alpha"),
            (beta, "Various/Mix", "02 Beta"));
        items[0].Moves.Single().ToPath.Should().Be("Various/Mix/01 Alpha.flac");
        OrganizeTagValues changes = items[1].TagChanges!;
        changes.Fields.Should().BeEquivalentTo(
        [
            OrganizeTagField.Album,
            OrganizeTagField.AlbumArtist,
            OrganizeTagField.TrackNumber,
            OrganizeTagField.TrackCount,
        ]);
        (changes.Album, changes.AlbumArtists.Single(), changes.TrackNumber, changes.TrackCount)
            .Should().Be(("Mix", "Various", 2, 2));
        (items[1].TagsBefore!.Album, items[1].TagsBefore!.TrackNumber).Should().Be(("Old", 7));
        Snapshot().Should().Equal(before);
    }

    [Test]
    public async Task An_own_album_named_like_an_album_of_the_library_warns_that_they_merge()
    {
        await AudioAsync("Various/Mix/01 One.mp3", new TestTags { Title = "One", Artist = "Various", Album = "Mix" });
        await AudioAsync("Incoming/two.mp3", new TestTags { Title = "Two", Artist = "Other" });
        await ScanAsync();
        Guid album = await OwnAlbumAsync("Mix", "Various", [await TrackIdAsync("Two")]);

        OrganizePreviewResult result = await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null));

        OrganizeItem item = (await ItemsAsync(result.Operation!.Id)).Single();
        item.Warnings.Should().Contain(OrganizeWarning.AlbumMerges);
        item.ToBaseName.Should().Be("01 Two");
        item.Conflict.Should().BeNull();
    }

    [Test]
    public async Task The_cover_of_an_own_album_is_planned_for_its_album_folder()
    {
        await AudioAsync("Incoming/one.mp3", new TestTags { Title = "One", Artist = "X" });
        await ScanAsync();
        Guid album = await OwnAlbumAsync("Mix", "Various", [await TrackIdAsync("One")]);
        string picture = Media.Combine("upload.jpg");
        await TestMediaGenerator.CreateImageAsync(picture, 32, 32);
        await using (FileStream upload = File.OpenRead(picture))
        {
            await InScopeAsync(
                services => services.GetRequiredService<IOwnAlbumService>().SetCoverAsync(album, upload));
        }

        File.Delete(picture);
        OrganizePreviewResult result = await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null));

        OrganizeItem cover = (await ItemsAsync(result.Operation!.Id)).Single(
            item => item.Kind == OrganizeItemKind.AlbumCover);
        cover.Moves.Single().ToPath.Should().Be("Various/Mix/cover.jpg");
        cover.Moves.Single().FromPath.Should().BeNull();
    }

    [Test]
    public async Task The_preview_of_an_own_album_reports_why_it_cannot_be_made()
    {
        await AudioAsync("Incoming/one.mp3", new TestTags { Title = "One", Artist = "X" });
        await ScanAsync();
        Guid empty = await OwnAlbumAsync("Empty", "Various", []);
        Guid album = await OwnAlbumAsync("Mix", "Various", [await TrackIdAsync("One")]);

        (await PreviewAsync(service => service.PreviewOwnAlbumAsync(Guid.NewGuid(), null))).Status
            .Should().Be(OrganizePreviewStatus.OwnAlbumNotFound);
        (await PreviewAsync(service => service.PreviewOwnAlbumAsync(empty, null))).Status
            .Should().Be(OrganizePreviewStatus.AlbumEmpty);

        WriteProbe.Probe(Arg.Any<string>()).Returns(MediaFolderState.ReadOnly);
        (await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null))).Status
            .Should().Be(OrganizePreviewStatus.MediaReadOnly);
        (await PreviewAsync(service => service.PreviewLibraryAsync(null, null))).Status
            .Should().Be(OrganizePreviewStatus.MediaReadOnly);

        WriteProbe.Probe(Arg.Any<string>()).Returns(MediaFolderState.Writable);
        await AddOperationAsync(OrganizeOperationStatus.Running);
        (await PreviewAsync(service => service.PreviewOwnAlbumAsync(album, null))).Status
            .Should().Be(OrganizePreviewStatus.Busy);
        await using WaveMpDbContext db = CreateDbContext();
        (await db.OrganizeOperations.CountAsync()).Should().Be(1, "no preview was stored");
    }

    [Test]
    public async Task The_library_preview_moves_misplaced_songs_with_their_folder_files_and_skips_placed_ones()
    {
        TestTags album = new() { Artist = "Band", AlbumArtist = "Band", Album = "Record" };
        await AudioAsync("Messy/one.mp3", album with { Title = "One", Track = "1" });
        await AudioAsync("Messy/two.mp3", album with { Title = "Two", Track = "2" });
        await TestMediaGenerator.CreateImageAsync(Media.Combine("Messy/cover.jpg"), 32, 32);
        await AudioAsync("Band/Record/03 Three.mp3", album with { Title = "Three", Track = "3" });
        await ScanAsync();
        IReadOnlyList<string> before = Snapshot();

        OrganizeOperation operation = await LibraryPreviewAsync();

        operation.Status.Should().Be(OrganizeOperationStatus.Previewed);
        operation.Kind.Should().Be(OrganizeOperationKind.Library);
        IReadOnlyList<OrganizeItem> items = await ItemsAsync(operation.Id);
        items.Should().HaveCount(3);
        items.Where(item => item.Kind == OrganizeItemKind.Track).Select(item => item.ToBaseName)
            .Should().BeEquivalentTo("01 One", "02 Two");
        OrganizeItem folder = items.Single(item => item.Kind == OrganizeItemKind.FolderFiles);
        folder.Moves.Single().ToPath.Should().Be("Band/Record/cover.jpg");
        items.Should().OnlyContain(item => item.Conflict == null && item.TagChanges == null);
        operation.TotalItems.Should().Be(3);
        Snapshot().Should().Equal(before);
    }

    [Test]
    public async Task A_relocated_song_keeps_its_id_and_its_undo_moves_it_back()
    {
        await AudioAsync(
            "Messy/one.mp3",
            new TestTags { Title = "One", Artist = "Band", Album = "Record", Track = "1" });
        await ScanAsync();
        Guid trackId = await TrackIdAsync("One");
        OrganizeOperation operation = await LibraryPreviewAsync();
        OrganizeItem item = (await ItemsAsync(operation.Id)).Single();
        OrganizeFileMove move = item.Moves.Single();

        // What the executor of the next step does: move the file without replacing, then record it in one transaction.
        Directory.CreateDirectory(Media.Combine("Band/Record"));
        INoReplaceFileMover mover = Services.GetRequiredService<INoReplaceFileMover>();
        mover.Move(Media.Combine(move.FromPath!), Media.Combine(move.ToPath!)).IsMoved.Should().BeTrue();
        await InScopeAsync(async services =>
        {
            await services.GetRequiredService<IOrganizeRepository>().RelocateTrackAsync(new TrackRelocation(
                operation.Id,
                item.Id,
                trackId,
                item.ToDirectoryPath,
                item.ToBaseName,
                [new FileRelocation(move.Id, move.MediaFileId, move.ToPath)],
                ["Band", "Band/Record"],
                Now));
            return true;
        });
        await SetStatusAsync(operation.Id, OrganizeOperationStatus.Completed);

        await using (WaveMpDbContext db = CreateDbContext())
        {
            Track track = await db.Tracks.SingleAsync();
            (track.Id, track.DirectoryPath, track.BaseName).Should().Be((trackId, "Band/Record", "01 One"));
            MediaFile file = await db.MediaFiles.SingleAsync();
            (file.Id, file.RelativePath, file.FileName)
                .Should().Be((move.MediaFileId!.Value, "Band/Record/01 One.mp3", "01 One.mp3"));
            OrganizeItem done = await db.OrganizeItems.Include(row => row.Moves).SingleAsync();
            (done.Status, done.Moves.Single().Status).Should().Be((OrganizeItemStatus.Done, OrganizeMoveStatus.Moved));
            done.CreatedFolders.Should().Equal("Band", "Band/Record");
            (await db.OrganizeOperations.SingleAsync()).DoneItems.Should().Be(1);
        }

        OrganizePreviewResult undo = await PreviewAsync(service => service.PreviewUndoAsync(operation.Id, null));

        undo.Status.Should().Be(OrganizePreviewStatus.Created);
        (undo.Operation!.Kind, undo.Operation.UndoOfId).Should().Be((OrganizeOperationKind.Undo, operation.Id));
        OrganizeItem back = (await ItemsAsync(undo.Operation.Id)).Single();
        back.Conflict.Should().BeNull();
        back.UndoOfItemId.Should().Be(item.Id);
        back.Moves.Single().FromPath.Should().Be("Band/Record/01 One.mp3");
        back.Moves.Single().ToPath.Should().Be("Messy/one.mp3");

        (await PreviewAsync(service => service.PreviewUndoAsync(undo.Operation.Id, null))).Status
            .Should().Be(OrganizePreviewStatus.NotUndoable);
        await using (WaveMpDbContext db = CreateDbContext())
        {
            await db.OrganizeOperations.Where(row => row.Id == operation.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.UndoneById, undo.Operation.Id));
        }

        (await PreviewAsync(service => service.PreviewUndoAsync(operation.Id, null))).Status
            .Should().Be(OrganizePreviewStatus.AlreadyUndone);
    }

    [Test]
    public async Task Only_operations_that_changed_files_can_be_undone()
    {
        await AudioAsync(
            "Messy/one.mp3",
            new TestTags { Title = "One", Artist = "Band", Album = "Record", Track = "1" });
        await ScanAsync();
        OrganizeOperation preview = await LibraryPreviewAsync();

        (await PreviewAsync(service => service.PreviewUndoAsync(preview.Id, null))).Status
            .Should().Be(OrganizePreviewStatus.NotUndoable);
        await SetStatusAsync(preview.Id, OrganizeOperationStatus.Completed);
        (await PreviewAsync(service => service.PreviewUndoAsync(preview.Id, null))).Status
            .Should().Be(OrganizePreviewStatus.NotUndoable, "no item is done");
        (await PreviewAsync(service => service.PreviewUndoAsync(Guid.NewGuid(), null))).Status
            .Should().Be(OrganizePreviewStatus.OperationNotFound);
    }

    [Test]
    public async Task Previews_expire_and_can_be_discarded_but_runs_stay_in_the_log()
    {
        await AudioAsync(
            "Messy/one.mp3",
            new TestTags { Title = "One", Artist = "Band", Album = "Record", Track = "1" });
        await ScanAsync();
        OrganizeOperation first = await LibraryPreviewAsync();
        OrganizeOperation second = await LibraryPreviewAsync();
        Guid run = await AddOperationAsync(OrganizeOperationStatus.Completed);

        Now = Now.AddHours(2);
        OrganizeOperation? expired = await OrganizeAsync(service => service.GetOperationAsync(first.Id));
        expired!.Status.Should().Be(OrganizeOperationStatus.Expired);

        (await OrganizeAsync(service => service.DiscardAsync(first.Id)))
            .Should().Be(OrganizeDiscardStatus.Discarded);
        (await OrganizeAsync(service => service.DiscardAsync(first.Id)))
            .Should().Be(OrganizeDiscardStatus.NotFound);
        (await OrganizeAsync(service => service.DiscardAsync(run))).Should().Be(OrganizeDiscardStatus.NotPreview);

        Page<OrganizeOperation> log = await OrganizeAsync(service => service.ListOperationsAsync(
            new OrganizeOperationListQuery(0, 10, null, null)));
        log.Items.Select(operation => operation.Id).Should().BeEquivalentTo([second.Id, run]);
        await using WaveMpDbContext db = CreateDbContext();
        (await db.OrganizeItems.CountAsync(item => item.OperationId == first.Id)).Should().Be(0);
    }

    [Test]
    public async Task The_availability_names_every_source_and_the_running_operation()
    {
        OrganizeAvailability available = await OrganizeAsync(service => service.GetAvailabilityAsync());
        available.Sources.Should().ContainSingle().Which.Status.Should().Be(SourceAvailabilityStatus.Available);
        available.ActiveOperationId.Should().BeNull();

        WriteProbe.Probe(Arg.Any<string>()).Returns(MediaFolderState.ReadOnly);
        Guid running = await AddOperationAsync(OrganizeOperationStatus.Queued);
        OrganizeAvailability readOnly = await OrganizeAsync(service => service.GetAvailabilityAsync());
        readOnly.Sources.Single().Status.Should().Be(SourceAvailabilityStatus.ReadOnly);
        readOnly.ActiveOperationId.Should().Be(running);
    }

    private async Task<Guid> OwnAlbumAsync(string title, string albumArtist, IReadOnlyList<Guid> trackIds)
    {
        return await InScopeAsync(async services =>
        {
            IOwnAlbumService service = services.GetRequiredService<IOwnAlbumService>();
            OwnAlbumResult created = await service.CreateAsync(
                new OwnAlbumValues(title, albumArtist, null, null),
                null);
            if (trackIds.Count > 0)
            {
                (await service.ReplaceTracksAsync(created.Item!.Album.Id, trackIds)).IsOk.Should().BeTrue();
            }

            return created.Item!.Album.Id;
        });
    }

    private async Task<Guid> AddOperationAsync(OrganizeOperationStatus status)
    {
        await using WaveMpDbContext db = CreateDbContext();
        OrganizeOperation operation = new()
        {
            Id = Guid.CreateVersion7(),
            Kind = OrganizeOperationKind.Library,
            Status = status,
            Template = "{Title}",
            CreatedAt = Now,
        };
        db.OrganizeOperations.Add(operation);
        await db.SaveChangesAsync();
        return operation.Id;
    }

    private async Task SetStatusAsync(Guid operationId, OrganizeOperationStatus status)
    {
        await using WaveMpDbContext db = CreateDbContext();
        await db.OrganizeOperations.Where(row => row.Id == operationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, status));
    }

    private async Task<IReadOnlyList<OrganizeItem>> ItemsAsync(Guid operationId)
    {
        Page<OrganizeItem>? page = await OrganizeAsync(service => service.ListItemsAsync(
            new OrganizeItemListQuery(operationId, 0, 100, OrganizeItemFilter.All)));
        return page!.Items;
    }

    private Task<OrganizePreviewResult> PreviewAsync(Func<IOrganizePreviewService, Task<OrganizePreviewResult>> action)
    {
        return InScopeAsync(services => action(services.GetRequiredService<IOrganizePreviewService>()));
    }

    private Task<T> OrganizeAsync<T>(Func<IOrganizeService, Task<T>> action)
    {
        return InScopeAsync(services => action(services.GetRequiredService<IOrganizeService>()));
    }
}
