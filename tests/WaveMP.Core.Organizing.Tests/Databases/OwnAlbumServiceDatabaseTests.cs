using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Organizing.OwnAlbums;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Organizing.Tests.Databases;

[TestFixture]
public sealed class OwnAlbumServiceDatabaseTests : OrganizingDatabaseTestBase
{
    protected override string DatabaseName => "wavemp_tests_own_albums";

    protected override IDictionary<string, string?> ExtraSettings { get; } = new Dictionary<string, string?>
    {
        ["Organizing:MaxAlbumTracks"] = "3",
    };

    [Test]
    public async Task An_own_album_is_created_changed_listed_and_deleted()
    {
        OwnAlbumResult created = await CallAsync(service => service.CreateAsync(
            new OwnAlbumValues("  Summer Mix ", " Various ", 2024, "  "),
            null));

        created.IsOk.Should().BeTrue();
        OwnAlbumRow album = created.Item!.Album;
        (album.Title, album.AlbumArtist, album.Year, album.Genre).Should().Be(("Summer Mix", "Various", 2024, null));
        created.Item.Status.Should().Be(OwnAlbumStatus.Draft);

        Now = Now.AddMinutes(1);
        OwnAlbumResult changed = await CallAsync(service => service.UpdateAsync(
            album.Id,
            new OwnAlbumChanges("Winter Mix", null, 0, "Pop")));
        (changed.Item!.Album.Title, changed.Item.Album.AlbumArtist, changed.Item.Album.Year, changed.Item.Album.Genre)
            .Should().Be(("Winter Mix", "Various", null, "Pop"));
        changed.Item.Album.UpdatedAt.Should().Be(Now);

        Page<OwnAlbumItem> page = await CallAsync(service => service.ListAsync(
            new OwnAlbumListQuery(0, 10, OwnAlbumSortField.Title, SortDirection.Asc)));
        page.Items.Select(item => item.Album.Title).Should().Equal("Winter Mix");

        (await CallAsync(service => service.DeleteAsync(album.Id))).Should().BeTrue();
        (await CallAsync(service => service.DeleteAsync(album.Id))).Should().BeFalse();
        (await CallAsync(service => service.GetAsync(album.Id))).Should().BeNull();
    }

    [Test]
    public async Task Songs_are_added_in_order_sorted_and_checked()
    {
        await AudioAsync("In/a.mp3", new TestTags { Title = "A", Artist = "X" });
        await AudioAsync("In/b.mp3", new TestTags { Title = "B", Artist = "X" });
        await AudioAsync("In/c.mp3", new TestTags { Title = "C", Artist = "X" });
        await VideoAsync("In/clip.mkv", new TestTags { Title = "Clip", Artist = "X" });
        await ScanAsync();
        Guid a = await TrackIdAsync("A");
        Guid b = await TrackIdAsync("B");
        Guid c = await TrackIdAsync("C");
        Guid clip = await TrackIdAsync("Clip");
        Guid first = await CreateAsync("First");
        Guid second = await CreateAsync("Second");

        OwnAlbumResult added = await CallAsync(service => service.AddTracksAsync(first, [b, a, b]));
        added.IsOk.Should().BeTrue();
        added.Item!.Album.TrackCount.Should().Be(2);
        (await MembersAsync(first)).Should().Equal(b, a);

        (await CallAsync(service => service.ReplaceTracksAsync(first, [a, b]))).IsOk.Should().BeTrue();
        (await MembersAsync(first)).Should().Equal(a, b);

        OwnAlbumResult videoOnly = await CallAsync(service => service.AddTracksAsync(first, [clip]));
        videoOnly.Status.Should().Be(OwnAlbumResultStatus.TrackNoAudio);
        videoOnly.TrackIds.Should().Equal(clip);

        OwnAlbumResult unknown = await CallAsync(service => service.AddTracksAsync(first, [Guid.NewGuid()]));
        unknown.Status.Should().Be(OwnAlbumResultStatus.TrackNotFound);

        OwnAlbumResult taken = await CallAsync(service => service.AddTracksAsync(second, [a]));
        taken.Status.Should().Be(OwnAlbumResultStatus.TrackInOtherAlbum);
        taken.TrackIds.Should().Equal(a);

        OwnAlbumResult tooMany = await CallAsync(service => service.ReplaceTracksAsync(first, [a, b, c, clip]));
        tooMany.Status.Should().Be(OwnAlbumResultStatus.TooManyTracks);

        (await CallAsync(service => service.RemoveTrackAsync(first, c))).Status
            .Should().Be(OwnAlbumResultStatus.TrackNotFound);
        (await CallAsync(service => service.RemoveTrackAsync(first, a))).IsOk.Should().BeTrue();
        (await MembersAsync(first)).Should().Equal(b);
        await using WaveMpDbContext db = CreateDbContext();
        (await db.OwnAlbumTracks.CountAsync()).Should().Be(1, "a draft forgets songs that are taken out");
        (await CallAsync(service => service.AddTracksAsync(second, [a]))).IsOk.Should().BeTrue();
    }

    [Test]
    public async Task Songs_taken_out_of_an_applied_album_are_kept_as_removed_until_they_come_back()
    {
        await AudioAsync("In/a.mp3", new TestTags { Title = "A", Artist = "X" });
        await AudioAsync("In/b.mp3", new TestTags { Title = "B", Artist = "X" });
        await ScanAsync();
        Guid a = await TrackIdAsync("A");
        Guid b = await TrackIdAsync("B");
        Guid album = await CreateAsync("Mix");
        await CallAsync(service => service.ReplaceTracksAsync(album, [a, b]));
        await using (WaveMpDbContext db = CreateDbContext())
        {
            await db.OwnAlbums.Where(row => row.Id == album)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.AppliedAt, Now)
                    .SetProperty(row => row.UpdatedAt, Now));
        }

        (await CallAsync(service => service.GetAsync(album)))!.Status.Should().Be(OwnAlbumStatus.Applied);
        Now = Now.AddMinutes(1);
        OwnAlbumResult removed = await CallAsync(service => service.RemoveTrackAsync(album, a));

        removed.Item!.Status.Should().Be(OwnAlbumStatus.Changed);
        (removed.Item.Album.TrackCount, removed.Item.Album.RemovedCount).Should().Be((1, 1));
        Page<OwnAlbumTrackItem>? tracks = await CallAsync(service => service.ListTracksAsync(album, 0, 10));
        tracks!.Items.Select(item => (item.Track.Id, item.Member.State)).Should().Equal(
            (b, OwnAlbumTrackState.Member),
            (a, OwnAlbumTrackState.Removed));
        tracks.Total.Should().Be(2);

        OwnAlbumResult back = await CallAsync(service => service.AddTracksAsync(album, [a]));
        (back.Item!.Album.TrackCount, back.Item.Album.RemovedCount).Should().Be((2, 0));
        (await MembersAsync(album)).Should().Equal(b, a);
    }

    [Test]
    public async Task The_cover_is_stored_as_uploaded_delivered_and_removed_with_the_album()
    {
        string picture = Media.Combine("upload.png");
        await TestMediaGenerator.CreateImageAsync(picture, 40, 40);
        Guid album = await CreateAsync("Mix");

        OwnAlbumResult saved;
        await using (FileStream upload = File.OpenRead(picture))
        {
            saved = await CallAsync(service => service.SetCoverAsync(album, upload));
        }

        saved.IsOk.Should().BeTrue();
        saved.Item!.Album.CoverFileName.Should().Be(album + ".png");
        saved.Item.Album.CoverContentType.Should().Be("image/png");
        string stored = Path.Combine(Data.Path, "own-album-covers", album + ".png");
        File.ReadAllBytes(stored).Should().Equal(File.ReadAllBytes(picture));

        (bool exists, OwnAlbumCover? cover) = await CallAsync(service => service.OpenCoverAsync(album));
        exists.Should().BeTrue();
        await using (cover!.Content)
        {
            cover.ContentType.Should().Be("image/png");
            cover.ETag.Should().NotBeNullOrEmpty();
        }

        await using (MemoryStream text = new("not a picture"u8.ToArray()))
        {
            (await CallAsync(service => service.SetCoverAsync(album, text))).Status
                .Should().Be(OwnAlbumResultStatus.CoverUnsupportedFormat);
        }

        File.Exists(stored).Should().BeTrue("a rejected upload keeps the cover");
        (await CallAsync(service => service.DeleteAsync(album))).Should().BeTrue();
        File.Exists(stored).Should().BeFalse();
        (await CallAsync(service => service.OpenCoverAsync(album))).AlbumExists.Should().BeFalse();
    }

    [Test]
    public async Task Covers_of_albums_that_no_longer_exist_are_removed_on_start()
    {
        string folder = Path.Combine(Data.Path, "own-album-covers");
        string orphan = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".jpg");
        await File.WriteAllBytesAsync(orphan, [1, 2, 3]);

        await CallAsync(async service =>
        {
            await service.RemoveOrphanCoversAsync();
            return true;
        });

        File.Exists(orphan).Should().BeFalse();
    }

    private async Task<Guid> CreateAsync(string title)
    {
        OwnAlbumResult result = await CallAsync(service => service.CreateAsync(
            new OwnAlbumValues(title, "Various", null, null),
            null));
        return result.Item!.Album.Id;
    }

    private async Task<IReadOnlyList<Guid>> MembersAsync(Guid album)
    {
        return await InScopeAsync<IReadOnlyList<Guid>>(async services =>
        {
            IReadOnlyList<OwnAlbumMemberRow> members = await services.GetRequiredService<IOwnAlbumRepository>()
                .ListMembersAsync(album);
            return [.. members.Where(member => member.State == OwnAlbumTrackState.Member)
                .OrderBy(member => member.Position)
                .Select(member => member.TrackId)];
        });
    }

    private Task<T> CallAsync<T>(Func<IOwnAlbumService, Task<T>> action)
    {
        return InScopeAsync(services => action(services.GetRequiredService<IOwnAlbumService>()));
    }
}
