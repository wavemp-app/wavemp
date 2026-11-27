using AwesomeAssertions;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class MediaSourceRepositoryTests : LibraryDatabaseTests
{
    protected override string DatabaseName => "wavemp_tests_data_media_sources";

    [Test]
    public async Task ListAsync_Always_StartsWithTheFirstSourceOfTheMigrationAndSortsByName()
    {
        LibrarySeed seed = new();
        seed.Source("Zulu");
        seed.Source("Alpha");
        await SaveAsync(seed);

        IReadOnlyList<MediaSource> sources = await CreateRepository().ListAsync();

        sources.Select(source => source.Name).Should().Equal("Alpha", "Media", "Zulu");
        sources.Single(source => source.Name == "Media").Id.Should().Be(MediaSource.DefaultId);
    }

    [Test]
    public async Task AddAsync_NameTakenWithAnotherCase_ThrowsNameConflict()
    {
        LibrarySeed seed = new();
        seed.Source("Videos");
        await SaveAsync(seed);
        MediaSource duplicate = NewSource("VIDEOS");

        Func<Task> act = () => CreateRepository().AddAsync(duplicate);

        await act.Should().ThrowAsync<MediaSourceNameConflictException>();
        (await CreateRepository().NameExistsAsync("VIDEOS")).Should().BeTrue();
    }

    [Test]
    public async Task NameExistsAsync_OwnName_IsNotTaken()
    {
        LibrarySeed seed = new();
        MediaSource videos = seed.Source("Videos");
        await SaveAsync(seed);

        (await CreateRepository().NameExistsAsync("VIDEOS", videos.Id)).Should().BeFalse();
        (await CreateRepository().NameExistsAsync("VIDEOS", Guid.NewGuid())).Should().BeTrue();
    }

    [Test]
    public async Task CountAsync_TracksAndFiles_AreCountedPerSource()
    {
        LibrarySeed seed = new();
        MediaSource videos = seed.Source();
        Track song = seed.Track("Song");
        seed.File("Song.mp3", MediaFileKind.Audio, song);
        seed.File("Song.jpg", MediaFileKind.Image, song, isPrimary: false);
        Track clip = seed.Track("Clip", source: videos);
        seed.File("Clip.mkv", MediaFileKind.Video, clip);
        await SaveAsync(seed);

        IReadOnlyDictionary<Guid, MediaSourceCounts> counts = await CreateRepository().CountAsync();

        counts[MediaSource.DefaultId].Should().Be(new MediaSourceCounts(1, 2));
        counts[videos.Id].Should().Be(new MediaSourceCounts(1, 1));
    }

    [Test]
    public async Task DeleteAsync_Source_RemovesItsTracksFilesAndTheAlbumsAndArtistsLeftWithoutTracks()
    {
        LibrarySeed seed = new();
        MediaSource videos = seed.Source();
        Artist shared = seed.Artist("Shared");
        Artist only = seed.Artist("Only Videos");
        Album album = seed.Album("Clips", only);
        Track song = seed.Track("Song", shared);
        seed.File("Song.mp3", MediaFileKind.Audio, song);
        Track clip = seed.Track("Clip", shared, source: videos);
        seed.File("Clip.mkv", MediaFileKind.Video, clip);
        Track live = seed.Track("Live", only, album, source: videos);
        seed.File("Clips/Live.mkv", MediaFileKind.Video, live);
        await SaveAsync(seed);

        bool deleted = await CreateRepository().DeleteAsync(videos.Id);

        deleted.Should().BeTrue();
        (await CreateRepository().FindAsync(videos.Id)).Should().BeNull();
        await using WaveMpDbContext db = CreateDbContext();
        db.Tracks.Select(track => track.Title).Should().Equal("Song");
        db.MediaFiles.Select(file => file.RelativePath).Should().Equal("Song.mp3");
        db.Albums.Should().BeEmpty();
        db.Artists.Select(artist => artist.Name).Should().Equal("Shared");
        (await CreateRepository().DeleteAsync(videos.Id)).Should().BeFalse();
    }

    [Test]
    public async Task ListQueries_SourceFilter_ReturnOnlyTheTracksAlbumsAndArtistsOfTheSource()
    {
        LibrarySeed seed = new();
        MediaSource videos = seed.Source();
        Artist band = seed.Artist("Band");
        Artist singer = seed.Artist("Singer");
        Album album = seed.Album("Album", band);
        seed.Track("Song", band, album);
        seed.Track("Clip", singer, source: videos);
        seed.Track("Live", band, album, source: videos);
        await SaveAsync(seed);

        Page<TrackRow> tracks = await new TrackRepository(CreateDbContext()).ListAsync(
            new TrackListQuery(0, 100, TrackSortField.Title, SortDirection.Asc, SourceId: videos.Id));
        Page<AlbumRow> albums = await new AlbumRepository(CreateDbContext()).ListAsync(
            new AlbumListQuery(0, 100, AlbumSortField.Title, SortDirection.Asc, SourceId: MediaSource.DefaultId));
        Page<ArtistRow> artists = await new ArtistRepository(CreateDbContext()).ListAsync(
            new ArtistListQuery(0, 100, ArtistSortField.Name, SortDirection.Asc, SourceId: MediaSource.DefaultId));
        Page<ArtistRow> videoArtists = await new ArtistRepository(CreateDbContext()).ListAsync(
            new ArtistListQuery(0, 100, ArtistSortField.Name, SortDirection.Asc, SourceId: videos.Id));

        tracks.Items.Select(track => track.Title).Should().Equal("Clip", "Live");
        tracks.Total.Should().Be(2);
        albums.Items.Select(row => row.Title).Should().Equal("Album");
        artists.Items.Select(artist => artist.Name).Should().Equal("Band");
        videoArtists.Items.Select(artist => artist.Name).Should().Equal("Band", "Singer");
    }

    private static MediaSource NewSource(string name)
    {
        return new MediaSource
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Path = "/sources/" + Guid.NewGuid().ToString("N"),
            IsActive = true,
            CreatedAt = LibrarySeed.DefaultTime,
            UpdatedAt = LibrarySeed.DefaultTime,
        };
    }

    private MediaSourceRepository CreateRepository()
    {
        return new MediaSourceRepository(CreateDbContext());
    }
}
