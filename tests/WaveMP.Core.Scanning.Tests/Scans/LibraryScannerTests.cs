using System.Data.Common;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Jobs.Extensions;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Metadata.Extensions;
using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Scanning.Extensions;
using WaveMP.Core.Scanning.Plans;
using WaveMP.Core.Scanning.Scans;
using WaveMP.Core.Scanning.Writers;
using WaveMP.Core.Settings.Extensions;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;
using WaveMP.Testing.Databases;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Scanning.Tests.Scans;

/// <summary>
/// Scans synthetic media created with ffmpeg into a real PostgreSQL database, with TagLib# and ffprobe.
/// </summary>
[TestFixture]
[Category("Integration")]
public class LibraryScannerTests
{
    private const string DatabaseName = "wavemp_tests_scanning";

    private TestDatabase _database = null!;
    private TemporaryDirectory _media = null!;
    private ServiceProvider _services = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateMigratedAsync(DatabaseName);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _database.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        await using WaveMpDbContext dbContext = _database.CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE covers, media_files, tracks, albums, artists RESTART IDENTITY CASCADE");
        await dbContext.MediaSources.Where(source => source.Id != MediaSource.DefaultId).ExecuteDeleteAsync();
        await dbContext.MediaSources
            .Where(source => source.Id == MediaSource.DefaultId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(source => source.Path, (string?)null)
                .SetProperty(source => source.IsActive, true));
        _media = new TemporaryDirectory();
        _services = CreateServices(_media.Path);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _services.DisposeAsync();
        _media.Dispose();
    }

    [Test]
    public async Task Scan_FilesWithTheSameBaseName_BecomeOneTrack()
    {
        await AudioAsync("Song.mp3");
        await VideoAsync("Song.mkv", 320, 240);
        await ImageAsync("Song.jpg", 100, 100);
        await AudioAsync("Other.MP3");
        await ImageAsync("other.jpg", 50, 50);

        ScanResult result = await ScanAsync();

        result.Statistics.FilesAdded.Should().Be(5);
        result.Statistics.TracksAdded.Should().Be(2);
        await using WaveMpDbContext db = _database.CreateDbContext();
        Track song = await LoadTrackAsync(db, "SONG");
        song.HasAudio.Should().BeTrue();
        song.HasVideo.Should().BeTrue();
        song.Files.Should().HaveCount(3);
        song.Files.Where(file => file.IsPrimary).Select(file => file.Extension).Should().BeEquivalentTo("mp3", "mkv");
        MediaFile video = song.Files.Single(file => file.Kind == MediaFileKind.Video);
        (video.Codec, video.AudioCodec, video.PixelFormat).Should().Be(("mpeg4", "aac", "yuv420p"));
        video.MetadataVersion.Should().Be(ScannerVersions.VideoMetadata);
        MediaFile audio = song.Files.Single(file => file.Kind == MediaFileKind.Audio);
        (audio.AudioCodec, audio.PixelFormat).Should().Be(((string?)null, (string?)null));
        audio.MetadataVersion.Should().Be(ScannerVersions.AudioMetadata);
        song.Covers.Should().ContainSingle(cover => cover.IsPrimary && cover.Source == CoverSource.File);
        (await LoadTrackAsync(db, "OTHER")).Files.Select(file => file.FileName)
            .Should().BeEquivalentTo("Other.MP3", "other.jpg");
    }

    [Test]
    public async Task Scan_Duplicates_ChoosesTheHighestQualityAndKeepsAllFiles()
    {
        await AudioAsync("Dup.flac", AudioFormat.Flac);
        await AudioAsync("Dup.mp3", AudioFormat.Mp3, bitrate: 320);
        await AudioAsync("Lossy.mp3", AudioFormat.Mp3, bitrate: 320);
        await AudioAsync("Lossy.m4a", AudioFormat.M4a, bitrate: 128);
        await AudioAsync("Hires.flac", AudioFormat.Flac, sampleRate: 96000, bitDepth: 24);
        await AudioAsync("Hires.wav", AudioFormat.Wav);
        await VideoAsync("Clip.mp4", 320, 240);
        await VideoAsync("Clip.mkv", 640, 480);
        await AudioAsync("Art.mp3");
        await ImageAsync("Art.jpg", 100, 100);
        await ImageAsync("Art.png", 300, 300);

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.MediaFiles.CountAsync()).Should().Be(11);
        (await PrimaryAsync(db, "DUP")).Should().Equal("Dup.flac");
        (await PrimaryAsync(db, "LOSSY")).Should().Equal("Lossy.mp3");
        (await PrimaryAsync(db, "HIRES")).Should().Equal("Hires.flac");
        (await PrimaryAsync(db, "CLIP")).Should().Equal("Clip.mkv");
        Track art = await LoadTrackAsync(db, "ART");
        art.Covers.Single(cover => cover.IsPrimary).MediaFile!.FileName.Should().Be("Art.png");
        art.Covers.Should().HaveCount(2);
    }

    [Test]
    public async Task Scan_AlbumFolders_FindTheAlbumCoverByItsNameAlsoAboveDiscFolders()
    {
        Directory.CreateDirectory(_media.Combine("Band/Album"));
        await AudioAsync("Band/Album/01 - One.mp3");
        await AudioAsync("Band/Album/02 - Two.mp3");
        await ImageAsync("Band/Album/folder.jpg", 400, 400);
        await ImageAsync("Band/Album/cover.jpg", 100, 100);
        await AudioAsync("Band/Double/CD1/01 Start.mp3");
        await AudioAsync("Band/Double/CD2/01 End.mp3");
        await ImageAsync("Band/Double/front.png", 200, 200);

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Album album = await LoadAlbumAsync(db, "Album");
        album.Tracks.Should().HaveCount(2);
        album.Covers.OrderByDescending(cover => cover.IsPrimary).Select(cover => cover.MediaFile!.FileName)
            .Should().Equal("cover.jpg", "folder.jpg");
        Album twoDiscs = await LoadAlbumAsync(db, "Double");
        twoDiscs.Tracks.Select(track => (track.Title, track.DiscNumber))
            .Should().BeEquivalentTo([("Start", 1), ("End", (int?)2)]);
        twoDiscs.Covers.Should().ContainSingle(cover => cover.IsPrimary)
            .Which.MediaFile!.FileName.Should().Be("front.png");
        twoDiscs.AlbumArtist!.Name.Should().Be("Band");
    }

    [Test]
    public async Task Scan_EmbeddedCover_IsATrackCoverAfterThePictureFile()
    {
        string source = _media.Combine("source/picture.jpg");
        await TestMediaGenerator.CreateImageAsync(source, 120, 120);
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Embedded.mp3"),
            AudioFormat.Mp3,
            embeddedCoverPath: source);
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Both.mp3"),
            AudioFormat.Mp3,
            embeddedCoverPath: source);
        await ImageAsync("Both.jpg", 300, 300);
        File.Delete(source);

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Track embedded = await LoadTrackAsync(db, "EMBEDDED");
        embedded.Covers.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Source = CoverSource.Embedded, Width = 120, Height = 120, MimeType = "image/jpeg", IsPrimary = true });
        Track both = await LoadTrackAsync(db, "BOTH");
        both.Covers.OrderByDescending(cover => cover.IsPrimary).Select(cover => cover.Source)
            .Should().Equal(CoverSource.File, CoverSource.Embedded);
    }

    [Test]
    public async Task Scan_FilesWithoutTags_DeriveTheMetadataFromTheirPaths()
    {
        await AudioAsync("Artist/Album/01 - Title.mp3");
        await AudioAsync("Artist/Album/CD2/03 Disc Title.mp3");
        await AudioAsync("Artist/Song.mp3");
        await AudioAsync("Artist2 - Album2/02. Named.mp3");
        await AudioAsync("A - X.mp3");
        await AudioAsync("A - Y.mp3");
        await AudioAsync("B - Z.mp3");
        await AudioAsync("weird.mp3");

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        List<Track> tracks = await db.Tracks.Include(track => track.Artist).Include(track => track.Album).ToListAsync();
        Describe(tracks, "Artist/Album", "01 - Title").Should().Be(("Title", "Artist", "Album", 1, null));
        Describe(tracks, "Artist/Album/CD2", "03 Disc Title").Should().Be(("Disc Title", "Artist", "Album", 3, 2));
        Describe(tracks, "Artist", "Song").Should().Be(("Song", "Artist", null, null, null));
        Describe(tracks, "Artist2 - Album2", "02. Named").Should().Be(("Named", "Artist2", "Album2", 2, null));
        Describe(tracks, string.Empty, "A - X").Should().Be(("X", "A", null, null, null));
        Describe(tracks, string.Empty, "B - Z").Should().Be(("Z", "B", null, null, null));
        Track weird = tracks.Single(track => track.BaseName == "weird");
        weird.Title.Should().Be("weird");
        weird.MetadataSource.Should().Be(TrackMetadataSource.FileName);
        tracks.Single(track => track.BaseName == "01 - Title").MetadataSource.Should().Be(TrackMetadataSource.Path);
        (await LoadAlbumAsync(db, "Album")).Tracks.Should().HaveCount(2);
    }

    [Test]
    public async Task Scan_FilesInTheRootWithACommonPattern_UseIt()
    {
        await AudioAsync("01 - A - X.mp3");
        await AudioAsync("02 - B - Y.mp3");
        await AudioAsync("03 - C - Z.mp3");

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        List<Track> tracks = await db.Tracks.Include(track => track.Artist).Include(track => track.Album).ToListAsync();
        Describe(tracks, string.Empty, "02 - B - Y").Should().Be(("Y", "B", null, 2, null));
    }

    [Test]
    public async Task Scan_TaggedFiles_PreferTheTagsAndFillMissingFieldsFromThePath()
    {
        await AudioAsync("Wrong Artist/Wrong Album/09 - Wrong.mp3", tags: new TestTags
        {
            Title = "Right Title",
            Artist = "Right Artist",
            Album = "Right Album",
            Track = "4",
            Date = "1999",
            Genre = "Jazz",
        });
        await AudioAsync("Folder Artist/Folder Album/05 Name.flac", AudioFormat.Flac, tags: new TestTags
        {
            Title = "Tagged",
            Artist = "Folder Artist",
        });

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        List<Track> tracks = await db.Tracks.Include(track => track.Artist).Include(track => track.Album).ToListAsync();
        Track right = tracks.Single(track => track.Title == "Right Title");
        (right.Artist!.Name, right.Album!.Title, right.TrackNumber, right.Year, right.Genre)
            .Should().Be(("Right Artist", "Right Album", 4, 1999, "Jazz"));
        right.MetadataSource.Should().Be(TrackMetadataSource.Tags);
        Track partial = tracks.Single(track => track.Title == "Tagged");
        (partial.Album!.Title, partial.TrackNumber).Should().Be(("Folder Album", 5));
    }

    [Test]
    public async Task Scan_SamplerWithoutAlbumArtistTags_IsAnAlbumPerArtist()
    {
        await AudioAsync("Sampler/Hits/01.mp3", tags: new TestTags { Title = "One", Artist = "A", Album = "Hits" });
        await AudioAsync("Sampler/Hits/02.mp3", tags: new TestTags { Title = "Two", Artist = "B", Album = "Hits" });
        await AudioAsync("Solo/Record/01.mp3", tags: new TestTags { Title = "Three", Artist = "C", Album = "Record" });
        await AudioAsync("Solo/Record/02.mp3", tags: new TestTags { Title = "Four", Artist = "C", Album = "Record" });

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        List<Album> hits = await db.Albums
            .Include(album => album.AlbumArtist)
            .Include(album => album.Tracks)
            .Where(album => album.Title == "Hits")
            .ToListAsync();
        hits.Select(album => (album.AlbumArtist!.Name, album.Tracks.Count, album.IsCompilation))
            .Should().BeEquivalentTo([("A", 1, false), ("B", 1, false)]);
        Album record = await LoadAlbumAsync(db, "Record");
        record.AlbumArtist!.Name.Should().Be("C");
        record.Tracks.Should().HaveCount(2);
        (await db.Artists.Select(artist => artist.Name).ToListAsync()).Should().BeEquivalentTo("A", "B", "C");
    }

    [Test]
    public async Task Scan_CompilationWithAlbumArtistTag_StaysOneAlbum()
    {
        TestTags hits = new() { Album = "Hits", AlbumArtist = "Various Artists" };
        await AudioAsync("Hits/01.mp3", tags: hits with { Title = "One", Artist = "A" });
        await AudioAsync("Hits/02.mp3", tags: hits with { Title = "Two", Artist = "B feat. C" });

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Album album = await LoadAlbumAsync(db, "Hits");
        album.AlbumArtist!.Name.Should().Be("Various Artists");
        album.Tracks.Should().HaveCount(2);
        album.IsCompilation.Should().BeTrue();
    }

    [TestCase(" various ARTISTS ", true)]
    [TestCase("Various Artists Collective", false)]
    [TestCase("Various", false)]
    public async Task Scan_AlbumArtist_MarksTheAlbumAsCompilationOnlyForVariousArtists(
        string albumArtist,
        bool isCompilation)
    {
        TestTags hits = new() { Album = "Hits", AlbumArtist = albumArtist };
        await AudioAsync("Hits/01.mp3", tags: hits with { Title = "One", Artist = "A" });
        await AudioAsync("Hits/02.mp3", tags: hits with { Title = "Two", Artist = "B" });

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        (await LoadAlbumAsync(db, "Hits")).IsCompilation.Should().Be(isCompilation);
    }

    // MP4 cpil, the Vorbis comment COMPILATION of FLAC and Ogg, and for MP3 the user text frame that ffmpeg writes
    // (found through ffprobe); the frame TCMP is covered by the next test.
    [TestCase(AudioFormat.Mp3, "mp3")]
    [TestCase(AudioFormat.M4a, "m4a")]
    [TestCase(AudioFormat.Flac, "flac")]
    [TestCase(AudioFormat.Opus, "opus")]
    public async Task Scan_CompilationFlagOnOneSong_MarksItsAlbumAsCompilation(AudioFormat format, string extension)
    {
        TestTags mix = new() { Album = "Mix", AlbumArtist = "DJ" };
        await AudioAsync($"Mix/01.{extension}", format, mix with { Title = "One", Artist = "A", Compilation = "1" });
        await AudioAsync($"Mix/02.{extension}", format, mix with { Title = "Two", Artist = "B" });
        await AudioAsync(
            $"Band/Record/01.{extension}",
            format,
            new TestTags { Title = "Three", Artist = "C", Album = "Record", Compilation = "0" });

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Album album = await LoadAlbumAsync(db, "Mix");
        album.Tracks.Should().HaveCount(2);
        album.IsCompilation.Should().BeTrue();
        (await LoadAlbumAsync(db, "Record")).IsCompilation.Should().BeFalse();
    }

    [Test]
    public async Task Scan_Id3TcmpFrame_MarksTheAlbumAsCompilation()
    {
        await AudioAsync("Mix/01.mp3", tags: new TestTags { Title = "One", Artist = "A", Album = "Mix" });
        SetTcmpFrame("Mix/01.mp3");

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        (await LoadAlbumAsync(db, "Mix")).IsCompilation.Should().BeTrue();
    }

    [Test]
    public async Task Scan_FlaggedSongInAnotherFolder_KeepsTheAlbumACompilationWhenOnlyOneFolderIsReadAgain()
    {
        TestTags mix = new() { Album = "Mix", AlbumArtist = "DJ" };
        await AudioAsync("Mix/CD1/01.mp3", tags: mix with { Title = "One", Artist = "A", Compilation = "1" });
        await AudioAsync("Mix/CD2/01.mp3", tags: mix with { Title = "Two", Artist = "B" });
        await ScanAsync();

        await ChangeAudioAsync("Mix/CD2/01.mp3", mix with { Title = "Two again", Artist = "B" });
        (await RefreshAsync("Mix/CD2", "Mix/CD2/01.mp3")).Should().BeTrue();

        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            Album album = await LoadAlbumAsync(db, "Mix");
            album.Tracks.Should().HaveCount(2);
            album.IsCompilation.Should().BeTrue();
        }

        // Without the flag on any song, the album is no compilation any more.
        await ChangeAudioAsync("Mix/CD1/01.mp3", mix with { Title = "One", Artist = "A" });
        await ScanAsync();

        await using WaveMpDbContext after = _database.CreateDbContext();
        (await LoadAlbumAsync(after, "Mix")).IsCompilation.Should().BeFalse();
    }

    [Test]
    public async Task Rescan_FilesReadByAnOlderVersion_MarkTheirAlbumsAsCompilations()
    {
        await AudioAsync("Mix/01.mp3", tags: new TestTags { Title = "One", Artist = "A", Album = "Mix" });
        SetTcmpFrame("Mix/01.mp3");
        await AudioAsync("Hits/01.mp3", tags: new TestTags { Album = "Hits", AlbumArtist = "Various Artists" });
        await ScanAsync();

        // The library as the version before the compilation flag left it: no flags and no compilations.
        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            await db.MediaFiles.ExecuteUpdateAsync(setters => setters
                .SetProperty(file => file.MetadataVersion, 1)
                .SetProperty(file => file.Tags.IsCompilation, false));
            await db.Albums.ExecuteUpdateAsync(setters => setters.SetProperty(album => album.IsCompilation, false));
        }

        ScanResult result = await ScanAsync();

        result.Statistics.FilesUpdated.Should().Be(2);
        await using WaveMpDbContext after = _database.CreateDbContext();
        (await after.Albums.Select(album => album.IsCompilation).ToListAsync()).Should().Equal(true, true);
        (await after.MediaFiles.SingleAsync(file => file.RelativePath == "Mix/01.mp3")).Tags.IsCompilation
            .Should().BeTrue();
    }

    [Test]
    public async Task Scan_SameArtistAndAlbumInSeveralFolders_IsOneAlbumAndFoldersDoNotDefineAlbums()
    {
        await AudioAsync("One/01.mp3", tags: new TestTags { Title = "A", Artist = "K.Flay", Album = "2 Weak" });
        await AudioAsync("Two/02.mp3", tags: new TestTags { Title = "B", Artist = " k.flay", Album = "2 WEAK " });
        await AudioAsync("One/03.mp3", tags: new TestTags { Title = "C", Artist = "Someone", Album = "2 Weak" });
        await AudioAsync("One/04.mp3", tags: new TestTags { Title = "D", Artist = "K.Flay", Album = "2 Weaker" });

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        List<Track> tracks = await db.Tracks.ToListAsync();
        Guid AlbumOf(string title) => tracks.Single(track => track.Title == title).AlbumId!.Value;
        AlbumOf("B").Should().Be(AlbumOf("A"));
        AlbumOf("C").Should().NotBe(AlbumOf("A"));
        AlbumOf("D").Should().NotBe(AlbumOf("A"));
        (await db.Albums.CountAsync()).Should().Be(3);
    }

    [Test]
    public async Task Scan_AlbumsOfAnOlderGroupingVersion_AreFormedAgainWithoutChangedFiles()
    {
        await AudioAsync("Sampler/Hits/01.mp3", tags: new TestTags { Title = "One", Artist = "A", Album = "Hits" });
        await AudioAsync("Sampler/Hits/02.mp3", tags: new TestTags { Title = "Two", Artist = "B", Album = "Hits" });
        await ImageAsync("Sampler/Hits/cover.jpg", 100, 100);
        await ScanAsync();

        // An album as an older version formed it: one compilation for both tracks, with an outdated key.
        var stale = Guid.CreateVersion7();
        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            db.Albums.Add(new Album
            {
                Id = stale,
                Title = "Hits",
                NormalizedTitle = "HITS",
                AlbumKey = new string('0', 64),
                IsCompilation = true,
                FirstSeenAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            await db.Tracks.ExecuteUpdateAsync(setters => setters.SetProperty(track => track.AlbumId, stale));
            await db.MediaSources.ExecuteUpdateAsync(setters =>
                setters.SetProperty(source => source.AlbumGroupingVersion, 1));
        }

        ScanResult result = await ScanAsync();

        result.Statistics.FilesUpdated.Should().Be(0);
        result.Statistics.TracksUpdated.Should().Be(2);
        await using WaveMpDbContext after = _database.CreateDbContext();
        (await after.Albums.AnyAsync(album => album.Id == stale)).Should().BeFalse();
        List<Album> albums = await after.Albums
            .Include(album => album.AlbumArtist)
            .Include(album => album.Covers)
            .ToListAsync();
        albums.Select(album => album.AlbumArtist!.Name).Should().BeEquivalentTo("A", "B");
        albums.Should().AllSatisfy(album => album.Covers.Should().ContainSingle());
        (await after.MediaSources.Select(source => source.AlbumGroupingVersion).Distinct().ToListAsync())
            .Should().Equal(ScannerVersions.AlbumGrouping);
    }

    [TestCase(AudioFormat.Mp3, "mp3")]
    [TestCase(AudioFormat.M4a, "m4a")]
    [TestCase(AudioFormat.Flac, "flac")]
    [TestCase(AudioFormat.Opus, "opus")]
    public async Task Scan_CompilationFlagWithoutAlbumArtist_IsOneAlbumOfTheFolder(AudioFormat format, string extension)
    {
        TestTags hits = new() { Album = "Hits", Compilation = "1" };
        await AudioAsync($"Hits/01.{extension}", format, hits with { Title = "One", Artist = "A" });
        await AudioAsync($"Hits/02.{extension}", format, hits with { Title = "Two", Artist = "B" });
        await AudioAsync(
            $"Hits/03.{extension}",
            format,
            hits with { Title = "Three", Artist = "C", AlbumArtist = " " });

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Album album = await db.Albums.Include(item => item.Tracks).ThenInclude(track => track.Artist).SingleAsync();
        (album.Title, album.AlbumArtistId, album.IsCompilation).Should().Be(("Hits", (Guid?)null, true));
        album.Tracks.Select(track => (track.Title, track.Artist!.Name))
            .Should().BeEquivalentTo([("One", "A"), ("Two", "B"), ("Three", "C")]);
        (await db.Artists.Select(artist => artist.Name).ToListAsync()).Should().BeEquivalentTo("A", "B", "C");
    }

    [Test]
    public async Task Scan_Id3TcmpFramesWithoutAlbumArtist_AreOneAlbum()
    {
        await AudioAsync("Hits/01.mp3", tags: new TestTags { Title = "One", Artist = "A", Album = "Hits" });
        await AudioAsync("Hits/02.mp3", tags: new TestTags { Title = "Two", Artist = "B", Album = "Hits" });
        SetTcmpFrame("Hits/01.mp3");
        SetTcmpFrame("Hits/02.mp3");

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Album album = await LoadAlbumAsync(db, "Hits");
        (album.AlbumArtistId, album.Tracks.Count, album.IsCompilation).Should().Be(((Guid?)null, 2, true));
    }

    [Test]
    public async Task Scan_CompilationFlagInASamplerFolder_TakesNoAlbumArtistFromTheFolder()
    {
        await AudioAsync("Sampler/Hits/01 - A - One.mp3", tags: new TestTags { Compilation = "1" });
        await AudioAsync("Sampler/Hits/02 - B - Two.mp3", tags: new TestTags { Compilation = "1" });

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Album album = await LoadAlbumAsync(db, "Hits");
        (album.AlbumArtistId, album.Tracks.Count, album.IsCompilation).Should().Be(((Guid?)null, 2, true));
        (await db.Artists.Select(artist => artist.Name).ToListAsync()).Should().BeEquivalentTo("A", "B");
    }

    [Test]
    public async Task Scan_CompilationsOfTheSameNameInTwoFolders_AreTwoAlbums()
    {
        TestTags hits = new() { Album = "Hits", Compilation = "1" };
        await AudioAsync("One/Hits/01.mp3", tags: hits with { Title = "One", Artist = "A" });
        await AudioAsync("One/Hits/02.mp3", tags: hits with { Title = "Two", Artist = "B" });
        await AudioAsync("Two/Hits/01.mp3", tags: hits with { Title = "Three", Artist = "A" });
        await AudioAsync("Two/Hits/02.mp3", tags: hits with { Title = "Four", Artist = "B" });

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        List<Album> albums = await db.Albums.Include(album => album.Tracks).ToListAsync();
        albums.Should().HaveCount(2);
        albums.Should().AllSatisfy(album =>
        {
            (album.AlbumArtistId, album.Tracks.Count, album.IsCompilation).Should().Be(((Guid?)null, 2, true));
            album.Tracks.Select(track => track.DirectoryPath).Distinct().Should().ContainSingle();
        });
    }

    [Test]
    public async Task Scan_CompilationsOfTheSameNameAndFolderInTwoSources_AreTwoAlbums()
    {
        using TemporaryDirectory other = new();
        TestTags hits = new() { Album = "Hits", Compilation = "1" };
        await AudioAsync("Hits/01.mp3", tags: hits with { Title = "One", Artist = "A" });
        await AudioAsync("Hits/02.mp3", tags: hits with { Title = "Two", Artist = "B" });
        await TestMediaGenerator.CreateAudioAsync(
            other.Combine("Hits/01.mp3"),
            AudioFormat.Mp3,
            hits with { Title = "Three", Artist = "A" });
        await TestMediaGenerator.CreateAudioAsync(
            other.Combine("Hits/02.mp3"),
            AudioFormat.Mp3,
            hits with { Title = "Four", Artist = "B" });
        await AddSourceAsync("Other", other.Path);

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        List<Album> albums = await db.Albums.Include(album => album.Tracks).ToListAsync();
        albums.Should().HaveCount(2);
        albums.Should().AllSatisfy(album =>
        {
            (album.AlbumArtistId, album.Tracks.Count, album.IsCompilation).Should().Be(((Guid?)null, 2, true));
            album.Tracks.Select(track => track.SourceId).Distinct().Should().ContainSingle();
        });
    }

    [Test]
    public async Task Scan_CompilationInDiscFolders_IsOneAlbumWithTheCoverOfTheFolderAbove()
    {
        TestTags hits = new() { Album = "Hits", Compilation = "1" };
        await AudioAsync("Hits/CD1/01.mp3", tags: hits with { Title = "One", Artist = "A" });
        await AudioAsync("Hits/CD2/01.mp3", tags: hits with { Title = "Two", Artist = "B" });
        await ImageAsync("Hits/cover.jpg", 100, 100);

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Album album = await LoadAlbumAsync(db, "Hits");
        (album.AlbumArtistId, album.Tracks.Count, album.IsCompilation).Should().Be(((Guid?)null, 2, true));
        album.Covers.Should().ContainSingle().Which.MediaFile!.RelativePath.Should().Be("Hits/cover.jpg");
    }

    [TestCase(null)]
    [TestCase("0")]
    public async Task Scan_SongsWithoutTheCompilationFlag_KeepTheRuleOfArtistAndAlbumName(string? flag)
    {
        TestTags hits = new() { Album = "Hits", Compilation = flag };
        await AudioAsync("One/01.mp3", tags: hits with { Title = "One", Artist = "A" });
        await AudioAsync("One/02.mp3", tags: hits with { Title = "Two", Artist = "B" });
        await AudioAsync("Two/01.mp3", tags: hits with { Title = "Three", Artist = "A" });

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        List<Album> albums = await db.Albums
            .Include(album => album.AlbumArtist)
            .Include(album => album.Tracks)
            .ToListAsync();
        albums.Select(album => (album.AlbumArtist!.Name, album.Tracks.Count, album.IsCompilation))
            .Should().BeEquivalentTo([("A", 2, false), ("B", 1, false)]);
    }

    [Test]
    public async Task Scan_CompilationFlagWithAlbumArtist_IsOneAlbumAcrossFolders()
    {
        TestTags mix = new() { Album = "Mix", AlbumArtist = "DJ", Compilation = "1" };
        await AudioAsync("One/01.mp3", tags: mix with { Title = "One", Artist = "A" });
        await AudioAsync("Two/01.mp3", tags: mix with { Title = "Two", Artist = "B" });

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Album album = await LoadAlbumAsync(db, "Mix");
        (album.AlbumArtist!.Name, album.Tracks.Count, album.IsCompilation).Should().Be(("DJ", 2, true));
    }

    [Test]
    public async Task Scan_CompilationsOfGroupingVersion2_AreFormedAgainAsOneAlbumWithoutReadingFiles()
    {
        (Dictionary<string, Guid> trackIds, Guid unaffectedAlbumId) = await CreateLibraryOfGroupingVersion2Async();

        ScanResult result = await ScanAsync();

        result.Statistics.FilesUpdated.Should().Be(0);
        result.Statistics.TracksUpdated.Should().Be(3);
        Guid albumId = await ExpectRegroupedCompilationAsync(trackIds, unaffectedAlbumId);

        ScanResult again = await ScanAsync();

        (again.Statistics.FilesUpdated, again.Statistics.TracksUpdated).Should().Be((0, 0));
        (await ExpectRegroupedCompilationAsync(trackIds, unaffectedAlbumId)).Should().Be(albumId);
    }

    [Test]
    public async Task Scan_RegroupWithoutTheMediaFolder_LeavesAConsistentLibraryAlthoughTheScanFails()
    {
        (Dictionary<string, Guid> trackIds, Guid unaffectedAlbumId) = await CreateLibraryOfGroupingVersion2Async();
        await using ServiceProvider services = CreateServices(_media.Combine("missing"));

        Func<Task> act = () => ScanAsync(services: services);

        (await act.Should().ThrowAsync<JobFailedException>()).Which.Code
            .Should().Be(JobErrorCode.MediaFolderNotFound);
        await ExpectRegroupedCompilationAsync(trackIds, unaffectedAlbumId);
    }

    [Test]
    public async Task Scan_RegroupOfADeactivatedSource_LeavesAConsistentLibraryAlthoughNoSourceCanBeScanned()
    {
        (Dictionary<string, Guid> trackIds, Guid unaffectedAlbumId) = await CreateLibraryOfGroupingVersion2Async();
        await SetActiveAsync(MediaSource.DefaultId, false);

        Func<Task> act = () => ScanAsync();

        (await act.Should().ThrowAsync<JobFailedException>()).Which.Code.Should().Be(JobErrorCode.NoMediaSources);
        await ExpectRegroupedCompilationAsync(trackIds, unaffectedAlbumId);
    }

    [Test]
    public async Task Scan_RegroupWithFailedCleanUp_KeepsTheOldVersionAndIsFinishedByTheNextScan()
    {
        (Dictionary<string, Guid> trackIds, Guid unaffectedAlbumId) = await CreateLibraryOfGroupingVersion2Async();
        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            // The clean-up fails when it removes the old albums.
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE FUNCTION fail_album_delete() RETURNS trigger LANGUAGE plpgsql
                    AS 'BEGIN RAISE EXCEPTION ''albums cannot be deleted''; END';
                CREATE TRIGGER fail_album_delete BEFORE DELETE ON albums
                    FOR EACH ROW EXECUTE FUNCTION fail_album_delete();
                """);
        }

        try
        {
            Func<Task> act = () => ScanAsync();

            await act.Should().ThrowAsync<DbException>();
            await using WaveMpDbContext db = _database.CreateDbContext();
            (await db.MediaSources.SingleAsync()).AlbumGroupingVersion.Should().Be(2);
        }
        finally
        {
            await using WaveMpDbContext db = _database.CreateDbContext();
            await db.Database.ExecuteSqlRawAsync("DROP FUNCTION fail_album_delete() CASCADE");
        }

        ScanResult result = await ScanAsync();

        result.Statistics.FilesUpdated.Should().Be(0);
        await ExpectRegroupedCompilationAsync(trackIds, unaffectedAlbumId);
    }

    [Test]
    public async Task Rescan_FlaggedLibraryReadByAnOlderVersion_IsReadAgainAndFormsOneAlbum()
    {
        TestTags hits = new() { Album = "Hits", Compilation = "1" };
        await AudioAsync("Hits/01.mp3", tags: hits with { Title = "One", Artist = "A" });
        await AudioAsync("Hits/02.mp3", tags: hits with { Title = "Two", Artist = "B" });
        await ImageAsync("Hits/cover.jpg", 100, 100);
        await ScanAsync();

        // The library as a version before the compilation flag left it: neither flags nor the current grouping.
        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            await db.MediaFiles
                .Where(file => file.Kind == MediaFileKind.Audio)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(file => file.MetadataVersion, 1)
                    .SetProperty(file => file.Tags.IsCompilation, false));
            await db.MediaSources.ExecuteUpdateAsync(setters =>
                setters.SetProperty(source => source.AlbumGroupingVersion, 2));
        }

        ScanResult result = await ScanAsync();

        result.Statistics.FilesUpdated.Should().Be(2);
        await using WaveMpDbContext after = _database.CreateDbContext();
        Album album = await LoadAlbumAsync(after, "Hits");
        (album.AlbumArtistId, album.Tracks.Count, album.IsCompilation).Should().Be(((Guid?)null, 2, true));
        album.Covers.Should().ContainSingle();
    }

    [Test]
    public async Task RefreshFolder_ChangedCompilationFlag_SplitsAndJoinsTheAlbum()
    {
        TestTags hits = new() { Album = "Hits" };
        TestTags flagged = hits with { Compilation = "1" };
        await AudioAsync("Hits/01.mp3", tags: flagged with { Title = "One", Artist = "A" });
        await AudioAsync("Hits/02.mp3", tags: flagged with { Title = "Two", Artist = "B" });
        await ScanAsync();

        await ChangeAudioAsync("Hits/01.mp3", hits with { Title = "One", Artist = "A" });
        await ChangeAudioAsync("Hits/02.mp3", hits with { Title = "Two", Artist = "B" });
        (await RefreshAsync("Hits", "Hits/01.mp3", "Hits/02.mp3")).Should().BeTrue();

        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            List<Album> albums = await db.Albums.Include(album => album.AlbumArtist).ToListAsync();
            albums.Select(album => (album.AlbumArtist!.Name, album.IsCompilation))
                .Should().BeEquivalentTo([("A", false), ("B", false)]);
        }

        await AudioAsync("Hits/01.mp3", tags: flagged with { Title = "One", Artist = "A" });
        await AudioAsync("Hits/02.mp3", tags: flagged with { Title = "Two", Artist = "B" });
        File.SetLastWriteTimeUtc(_media.Combine("Hits/01.mp3"), DateTime.UtcNow.AddMinutes(2));
        File.SetLastWriteTimeUtc(_media.Combine("Hits/02.mp3"), DateTime.UtcNow.AddMinutes(2));
        await ScanAsync();

        await using WaveMpDbContext after = _database.CreateDbContext();
        Album joined = await after.Albums.Include(album => album.Tracks).SingleAsync();
        (joined.AlbumArtistId, joined.Tracks.Count, joined.IsCompilation).Should().Be(((Guid?)null, 2, true));
    }

    [Test]
    public async Task Rescan_WithoutChanges_ReadsNothingAgain()
    {
        await AudioAsync("Band/Album/01 - One.mp3");
        await ImageAsync("Band/Album/cover.jpg", 100, 100);
        await ScanAsync();
        DateTimeOffset firstSeen = await FirstSeenAsync("01 - ONE");

        ScanResult second = await ScanAsync();

        second.Statistics.Should().BeEquivalentTo(new
        {
            FilesFound = 2,
            FilesUnchanged = 2,
            FilesAdded = 0,
            FilesUpdated = 0,
            FilesRemoved = 0,
            DirectoriesChanged = 0,
            TracksTotal = 1,
            AlbumsTotal = 1,
        });
        (await FirstSeenAsync("01 - ONE")).Should().Be(firstSeen);
    }

    [Test]
    public async Task Rescan_ChangedTags_UpdatesTheFileAndKeepsTheFirstSeenDate()
    {
        await AudioAsync("Band/Album/01 - One.mp3", tags: new TestTags { Title = "Before" });
        await ScanAsync();
        DateTimeOffset firstSeen = await FirstSeenAsync("01 - ONE");

        await AudioAsync("Band/Album/01 - One.mp3", tags: new TestTags { Title = "After" });
        File.SetLastWriteTimeUtc(_media.Combine("Band/Album/01 - One.mp3"), DateTime.UtcNow.AddMinutes(1));
        ScanResult result = await ScanAsync();

        result.Statistics.FilesUpdated.Should().Be(1);
        result.Statistics.TracksUpdated.Should().Be(1);
        await using WaveMpDbContext db = _database.CreateDbContext();
        Track track = await LoadTrackAsync(db, "01 - ONE");
        track.Title.Should().Be("After");
        track.FirstSeenAt.Should().Be(firstSeen);
    }

    [Test]
    public async Task Rescan_ChangedAlbumSpellingAndYear_UpdatesTheAlbum()
    {
        TestTags tags = new() { Artist = "Band", Album = "the album", Date = "2001" };
        await AudioAsync("Band/Album/01 - One.mp3", tags: tags with { Title = "One" });
        await AudioAsync("Band/Album/02 - Two.mp3", tags: tags with { Title = "Two" });
        await ScanAsync();
        Guid albumId;
        await using (WaveMpDbContext before = _database.CreateDbContext())
        {
            albumId = (await LoadAlbumAsync(before, "the album")).Id;
        }

        TestTags changed = tags with { Album = "The Album", Date = "2002" };
        await ChangeAudioAsync("Band/Album/01 - One.mp3", changed with { Title = "One" });
        await ChangeAudioAsync("Band/Album/02 - Two.mp3", changed with { Title = "Two" });
        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Album album = await LoadAlbumAsync(db, "The Album");
        album.Id.Should().Be(albumId, "only the spelling changed, so it is the same album");
        album.Year.Should().Be(2002);
        album.Tracks.Should().HaveCount(2);
    }

    [Test]
    public async Task Rescan_FolderWithoutYear_KeepsTheYearOfTheAlbum()
    {
        await AudioAsync("Band/Album/01 - One.mp3", tags: new TestTags { Artist = "Band", Album = "A", Date = "2001" });
        await ScanAsync();

        await ChangeAudioAsync("Band/Album/01 - One.mp3", new TestTags { Artist = "Band", Album = "A" });
        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        (await LoadAlbumAsync(db, "A")).Year.Should().Be(2001);
    }

    [Test]
    public async Task Rescan_BetterFileAdded_ChangesThePrimaryFile()
    {
        await AudioAsync("Song.mp3");
        await ScanAsync();

        await AudioAsync("Song.flac", AudioFormat.Flac);
        ScanResult result = await ScanAsync();

        result.Statistics.FilesAdded.Should().Be(1);
        result.Statistics.TracksAdded.Should().Be(0);
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await PrimaryAsync(db, "SONG")).Should().Equal("Song.flac");
    }

    [Test]
    public async Task Rescan_DeletedFilesAndFolders_RemoveTracksAlbumsAndArtists()
    {
        await AudioAsync("Keep/Album/01 - Stay.mp3");
        await AudioAsync("Keep/Album/02 - Go.mp3");
        await ImageAsync("Keep/Album/02 - Go.jpg", 50, 50);
        await AudioAsync("Gone/Record/01 - Old.mp3");
        await ScanAsync();

        File.Delete(_media.Combine("Keep/Album/02 - Go.mp3"));
        Directory.Delete(_media.Combine("Gone"), recursive: true);
        ScanResult result = await ScanAsync();

        result.Statistics.FilesRemoved.Should().Be(2);
        result.Statistics.TracksRemoved.Should().Be(2);
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.Tracks.Select(track => track.BaseName).ToListAsync()).Should().Equal("01 - Stay");
        (await db.Albums.Select(album => album.Title).ToListAsync()).Should().Equal("Album");
        (await db.Artists.Select(artist => artist.Name).ToListAsync()).Should().Equal("Keep");
        MediaFile picture = await db.MediaFiles.SingleAsync(file => file.FileName == "02 - Go.jpg");
        picture.TrackId.Should().BeNull();
        (await db.Covers.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Scan_EmptyMediaFolderWithALibrary_FailsAndRemovesNothing()
    {
        await AudioAsync("Band/Song.mp3");
        await ScanAsync();
        Directory.Delete(_media.Combine("Band"), recursive: true);

        Func<Task> act = () => ScanAsync();

        (await act.Should().ThrowAsync<JobFailedException>()).Which.Code.Should().Be(JobErrorCode.MediaFolderEmpty);
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.Tracks.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Scan_MissingMediaFolder_Fails()
    {
        await using ServiceProvider services = CreateServices(_media.Combine("missing"));

        Func<Task> act = () => ScanAsync(services: services);

        (await act.Should().ThrowAsync<JobFailedException>()).Which.Code
            .Should().Be(JobErrorCode.MediaFolderNotFound);
    }

    [Test]
    public async Task Scan_SeveralSources_KeepsTheFilesOfEachSourceApart()
    {
        using TemporaryDirectory videos = new();
        await AudioAsync("Band/Song.mp3");
        await TestMediaGenerator.CreateAudioAsync(videos.Combine("Band/Song.mp3"), AudioFormat.Mp3);
        await TestMediaGenerator.CreateVideoAsync(videos.Combine("Band/Song.mkv"), VideoFormat.Mkv, 320, 240);
        MediaSource second = await AddSourceAsync("Videos", videos.Path);

        ScanResult result = await ScanAsync();

        result.Statistics.IsPartial.Should().BeFalse();
        result.Statistics.TracksAdded.Should().Be(2);
        result.Issues.Should().BeEmpty();
        await using WaveMpDbContext db = _database.CreateDbContext();
        List<Track> tracks = await db.Tracks.Include(track => track.Files).ToListAsync();
        tracks.Should().HaveCount(2);
        Track first = tracks.Single(track => track.SourceId == MediaSource.DefaultId);
        first.HasVideo.Should().BeFalse();
        first.Files.Should().ContainSingle().Which.SourceId.Should().Be(MediaSource.DefaultId);
        Track other = tracks.Single(track => track.SourceId == second.Id);
        other.HasVideo.Should().BeTrue();
        other.Files.Should().HaveCount(2).And.OnlyContain(file => file.SourceId == second.Id);
        other.DirectoryPath.Should().Be("Band");
    }

    [Test]
    public async Task Scan_ScopeOfOneSource_LeavesTheOtherSourcesAlone()
    {
        using TemporaryDirectory videos = new();
        await AudioAsync("Band/Song.mp3");
        await TestMediaGenerator.CreateAudioAsync(videos.Combine("Clip.mp3"), AudioFormat.Mp3);
        MediaSource second = await AddSourceAsync("Videos", videos.Path);
        await ScanAsync();
        File.Delete(_media.Combine("Band/Song.mp3"));
        await TestMediaGenerator.CreateAudioAsync(videos.Combine("New.mp3"), AudioFormat.Mp3);

        ScanResult result = await ScanAsync(ScanScope.OfSources([second.Id]));

        result.Statistics.IsPartial.Should().BeTrue();
        result.Statistics.FilesAdded.Should().Be(1);
        result.Statistics.FilesRemoved.Should().Be(0);
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.Tracks.Select(track => track.BaseName).OrderBy(name => name).ToListAsync())
            .Should().Equal("Clip", "New", "Song");
    }

    [Test]
    public async Task Scan_SourceWithAMissingFolder_IsSkippedWithAnIssueAndTheOthersAreScanned()
    {
        await AudioAsync("Song.mp3");
        MediaSource missing = await AddSourceAsync("Gone", _media.Combine("../missing-" + Guid.NewGuid().ToString("N")));

        ScanResult result = await ScanAsync();

        result.Statistics.TracksAdded.Should().Be(1);
        result.Issues.Should().ContainSingle().Which.Should().Be(
            new ScanIssue(string.Empty, ScanIssueCode.SourceNotFound, missing.Id, "Gone"));
    }

    [Test]
    public async Task Scan_EmptySourceWithALibrary_IsSkippedAndKeepsItsTracks()
    {
        using TemporaryDirectory videos = new();
        await AudioAsync("Song.mp3");
        await TestMediaGenerator.CreateAudioAsync(videos.Combine("Clip.mp3"), AudioFormat.Mp3);
        MediaSource second = await AddSourceAsync("Videos", videos.Path);
        await ScanAsync();
        File.Delete(videos.Combine("Clip.mp3"));

        ScanResult result = await ScanAsync();

        result.Issues.Should().ContainSingle().Which.Code.Should().Be(ScanIssueCode.SourceEmpty);
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.Tracks.CountAsync(track => track.SourceId == second.Id)).Should().Be(1);
    }

    [Test]
    public async Task Scan_DeactivatedSource_IsNotScannedAndKeepsItsTracks()
    {
        using TemporaryDirectory videos = new();
        await AudioAsync("Song.mp3");
        await TestMediaGenerator.CreateAudioAsync(videos.Combine("Clip.mp3"), AudioFormat.Mp3);
        MediaSource second = await AddSourceAsync("Videos", videos.Path);
        await ScanAsync();
        Directory.Delete(videos.Path, recursive: true);
        await SetActiveAsync(second.Id, false);

        ScanResult result = await ScanAsync();

        result.Issues.Should().BeEmpty();
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.Tracks.CountAsync(track => track.SourceId == second.Id)).Should().Be(1);
    }

    [Test]
    public async Task Scan_NoActiveSource_FailsWithNoMediaSources()
    {
        await SetActiveAsync(MediaSource.DefaultId, false);

        Func<Task> act = () => ScanAsync();

        (await act.Should().ThrowAsync<JobFailedException>()).Which.Code.Should().Be(JobErrorCode.NoMediaSources);
    }

    [Test]
    public async Task Scan_AllSourcesFail_FailsWithTheFirstProblem()
    {
        await AddSourceAsync("Gone", _media.Combine("../missing-" + Guid.NewGuid().ToString("N")));
        await using ServiceProvider services = CreateServices(_media.Combine("missing"));

        Func<Task> act = () => ScanAsync(services: services);

        (await act.Should().ThrowAsync<JobFailedException>()).Which.Code
            .Should().Be(JobErrorCode.MediaFolderNotFound);
    }

    [Test]
    public async Task RefreshFolder_OtherSource_ReadsTheFolderOfThatSource()
    {
        using TemporaryDirectory videos = new();
        await AudioAsync("Band/Song.mp3");
        MediaSource second = await AddSourceAsync("Videos", videos.Path);
        await ScanAsync();
        await TestMediaGenerator.CreateAudioAsync(videos.Combine("Band/Song.mp3"), AudioFormat.Mp3);

        await using (AsyncServiceScope serviceScope = _services.CreateAsyncScope())
        {
            ILibraryScanner scanner = serviceScope.ServiceProvider.GetRequiredService<ILibraryScanner>();
            (await scanner.RefreshFolderAsync(second.Id, "Band", ["Band/Song.mp3"])).Should().BeTrue();
        }

        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.MediaFiles.Select(file => file.SourceId).ToListAsync())
            .Should().BeEquivalentTo([MediaSource.DefaultId, second.Id]);
    }

    [Test]
    public async Task Scan_ReadOnlyAudioAndVideo_AreNotWritable()
    {
        await AudioAsync("Locked.mp3");
        await AudioAsync("Open.mp3");
        await VideoAsync("Open.mkv", 320, 240);
        File.SetAttributes(_media.Combine("Locked.mp3"), FileAttributes.ReadOnly);

        await ScanAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Dictionary<string, bool> writable = await db.MediaFiles.ToDictionaryAsync(
            file => file.FileName,
            file => file.IsWritable);
        writable.Should().BeEquivalentTo(new Dictionary<string, bool>
        {
            ["Locked.mp3"] = false,
            ["Open.mp3"] = true,
            ["Open.mkv"] = false,
        });
    }

    [Test]
    public async Task Scan_HiddenAndSystemFolders_AreIgnored()
    {
        await AudioAsync("Visible.mp3");
        await AudioAsync(".hidden/Secret.mp3");
        await AudioAsync("Album/@eaDir/Thumb.mp3");

        ScanResult result = await ScanAsync();

        result.Statistics.FilesFound.Should().Be(1);
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.MediaFiles.Select(file => file.RelativePath).ToListAsync()).Should().Equal("Visible.mp3");
    }

    [Test]
    public async Task PartialScan_OnlyTouchesItsFolders()
    {
        await AudioAsync("One/a.mp3");
        await AudioAsync("Two/b.mp3");
        await AudioAsync("Three/c.mp3");
        await ScanAsync();

        await AudioAsync("One/new.mp3");
        await AudioAsync("Two/ignored.mp3");
        Directory.Delete(_media.Combine("Three"), recursive: true);
        ScanResult result = await ScanAsync(ScanScope.Of(MediaSource.DefaultId, ["One", "Three"]));

        result.Statistics.IsPartial.Should().BeTrue();
        result.Statistics.FilesAdded.Should().Be(1);
        result.Statistics.FilesRemoved.Should().Be(1);
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.MediaFiles.Select(file => file.RelativePath).OrderBy(path => path).ToListAsync())
            .Should().Equal("One/a.mp3", "One/new.mp3", "Two/b.mp3");
    }

    [Test]
    public async Task Scan_DamagedFile_IsReportedAndTheScanSucceeds()
    {
        TestMediaGenerator.CreateCorruptFile(_media.Combine("Broken.mp3"));
        await AudioAsync("Fine.mp3");

        ScanResult result = await ScanAsync();

        result.Issues.Should().ContainSingle().Which.Path.Should().Be("Broken.mp3");
        result.Issues[0].Code.Should().BeOneOf(ScanIssueCode.ProbeFailed, ScanIssueCode.FileUnreadable);
        result.Statistics.FilesFailed.Should().Be(1);
        result.Statistics.TracksTotal.Should().Be(2);
    }

    [Test]
    public async Task RefreshFolderAsync_ChangedFile_UpdatesTheTrack()
    {
        await AudioAsync("Band/Album/01 - One.mp3", tags: new TestTags { Title = "Before", Genre = "Rock" });
        await ScanAsync();

        await ChangeAudioAsync("Band/Album/01 - One.mp3", new TestTags { Title = "After", Genre = "Jazz" });
        bool changed = await RefreshAsync("Band/Album");

        changed.Should().BeTrue();
        await using WaveMpDbContext db = _database.CreateDbContext();
        Track track = await LoadTrackAsync(db, "01 - ONE");
        (track.Title, track.Genre).Should().Be(("After", "Jazz"));
        track.Files.Single().Tags.Title.Should().Be("After");
    }

    [Test]
    public async Task RefreshFolderAsync_FileWithTheSameSizeAndTime_IsReadAgainOnlyIfNamed()
    {
        string path = "Band/Album/01 - One.mp3";
        await AudioAsync(path, tags: new TestTags { Title = "Before" });
        DateTime modified = File.GetLastWriteTimeUtc(_media.Combine(path));
        await ScanAsync();

        // Tags written in place may keep the size (padding) and, on coarse file systems, the change time.
        await AudioAsync(path, tags: new TestTags { Title = "Behind" });
        File.SetLastWriteTimeUtc(_media.Combine(path), modified);
        bool unnamed = await RefreshAsync("Band/Album");
        bool named = await RefreshAsync("Band/Album", path);

        (unnamed, named).Should().Be((false, true));
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await LoadTrackAsync(db, "01 - ONE")).Title.Should().Be("Behind");
    }

    [Test]
    public async Task RefreshFolderAsync_LeavesOtherFoldersAndSubfoldersAlone()
    {
        await AudioAsync("Band/Album/01 - One.mp3", tags: new TestTags { Title = "One" });
        await AudioAsync("Band/Album/CD2/01 - Two.mp3", tags: new TestTags { Title = "Two" });
        await AudioAsync("Other/01 - Three.mp3", tags: new TestTags { Title = "Three" });
        await ScanAsync();

        await ChangeAudioAsync("Band/Album/01 - One.mp3", new TestTags { Title = "One changed" });
        await ChangeAudioAsync("Band/Album/CD2/01 - Two.mp3", new TestTags { Title = "Two changed" });
        await ChangeAudioAsync("Other/01 - Three.mp3", new TestTags { Title = "Three changed" });
        await AudioAsync("Other/02 - New.mp3");
        await RefreshAsync("Band/Album");

        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.Tracks.OrderBy(track => track.Title).Select(track => track.Title).ToListAsync())
            .Should().Equal("One changed", "Three", "Two");
    }

    [Test]
    public async Task RefreshFolderAsync_MediaFolder_ReadsTheFilesOfTheRoot()
    {
        await AudioAsync("Loose.mp3", tags: new TestTags { Title = "Loose" });
        await ScanAsync();

        await ChangeAudioAsync("Loose.mp3", new TestTags { Title = "Loose changed" });
        bool changed = await RefreshAsync(string.Empty);

        changed.Should().BeTrue();
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await LoadTrackAsync(db, "LOOSE")).Title.Should().Be("Loose changed");
    }

    [Test]
    public async Task RefreshFolderAsync_NewAlbum_RemovesTheOldAlbumAndSetsTheAlbumCover()
    {
        await AudioAsync("Band/Record/01 - One.mp3", tags: new TestTags { Artist = "Band", Album = "Old Name" });
        await ImageAsync("Band/Record/cover.jpg", 100, 100);
        await ScanAsync();

        await ChangeAudioAsync(
            "Band/Record/01 - One.mp3",
            new TestTags { Artist = "Other Band", Album = "New Name" });
        await RefreshAsync("Band/Record");

        await using WaveMpDbContext db = _database.CreateDbContext();
        Album album = await db.Albums
            .Include(item => item.AlbumArtist)
            .Include(item => item.Covers).ThenInclude(cover => cover.MediaFile)
            .SingleAsync();
        album.Title.Should().Be("New Name");
        album.AlbumArtist!.Name.Should().Be("Other Band");
        album.Covers.Should().ContainSingle().Which.MediaFile!.FileName.Should().Be("cover.jpg");
        (await db.Artists.Select(artist => artist.Name).ToListAsync()).Should().Equal("Other Band");
    }

    [Test]
    public async Task RefreshFolderAsync_MissingFolder_ChangesNothing()
    {
        await AudioAsync("Band/Album/01 - One.mp3");
        await ScanAsync();
        Directory.Delete(_media.Combine("Band/Album"), recursive: true);

        bool changed = await RefreshAsync("Band/Album");

        changed.Should().BeFalse();
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.Tracks.CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task RefreshFolderAsync_UnchangedFolder_ReturnsFalse()
    {
        await AudioAsync("Band/Album/01 - One.mp3");
        await ScanAsync();

        (await RefreshAsync("Band/Album")).Should().BeFalse();
    }

    [Test]
    public async Task RefreshFolderAsync_WhileTheLockIsHeld_Waits()
    {
        await AudioAsync("Band/Album/01 - One.mp3", tags: new TestTags { Title = "Before" });
        await ScanAsync();
        await ChangeAudioAsync("Band/Album/01 - One.mp3", new TestTags { Title = "After" });
        LibraryWriteLock writeLock = _services.GetRequiredService<LibraryWriteLock>();

        Task<bool> refresh;
        using (await writeLock.AcquireAsync())
        {
            refresh = RefreshAsync("Band/Album");
            await Task.Delay(200);
            refresh.IsCompleted.Should().BeFalse();
        }

        (await refresh.WaitAsync(TimeSpan.FromSeconds(30))).Should().BeTrue();
    }

    [Test]
    public async Task RefreshFolderAsync_DuringAScan_DoesNotChangeItsProgress()
    {
        await AudioAsync("Band/Album/01 - One.mp3", tags: new TestTags { Title = "Before" });
        await ScanAsync();
        await ChangeAudioAsync("Band/Album/01 - One.mp3", new TestTags { Title = "After" });
        IScanProgressTracker progress = _services.GetRequiredService<IScanProgressTracker>();
        var runId = Guid.NewGuid();
        progress.Start(runId);
        progress.Planned(3, 7);
        ScanProgress before = progress.Get(runId)!;

        (await RefreshAsync("Band/Album")).Should().BeTrue();

        progress.Get(runId).Should().Be(before);
        progress.Complete(runId);
    }

    private static (string Title, string? Artist, string? Album, int? Track, int? Disc) Describe(
        IEnumerable<Track> tracks,
        string directory,
        string baseName)
    {
        Track track = tracks.Single(item => item.DirectoryPath == directory && item.BaseName == baseName);
        return (track.Title, track.Artist?.Name, track.Album?.Title, track.TrackNumber, track.DiscNumber);
    }

    /// <summary>
    /// A library as grouping version 2 left it: a flagged compilation as an album per artist (one of them the artist
    /// of the folder, as album artist), each marked and with the cover of the folder, and an album without flag.
    /// Returns the ids of the tracks by their path and the id of the album without flag.
    /// </summary>
    private async Task<(Dictionary<string, Guid> TrackIds, Guid UnaffectedAlbumId)>
        CreateLibraryOfGroupingVersion2Async()
    {
        TestTags hits = new() { Album = "Hits", Compilation = "1" };
        await AudioAsync("Sampler/Hits/01 - A - One.mp3", tags: hits with { Title = "One" });
        await AudioAsync("Sampler/Hits/02 - B - Two.mp3", tags: hits with { Title = "Two", Artist = "Guest" });
        await AudioAsync("Sampler/Hits/03 - C - Three.mp3", tags: hits with { Title = "Three", Artist = "Other" });
        await ImageAsync("Sampler/Hits/cover.jpg", 100, 100);
        await AudioAsync(
            "Band/Record/01.mp3",
            tags: new TestTags { Title = "Four", Artist = "Band", Album = "Record" });
        await ScanAsync();

        // Without the stored flag, the current rules form exactly the albums of version 2.
        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            await db.MediaFiles.ExecuteUpdateAsync(setters =>
                setters.SetProperty(file => file.Tags.IsCompilation, false));
            await db.MediaSources.ExecuteUpdateAsync(setters =>
                setters.SetProperty(source => source.AlbumGroupingVersion, 0));
        }

        await ScanAsync();

        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            List<string> albumArtists = await db.Albums
                .Where(album => album.Title == "Hits")
                .Select(album => album.AlbumArtist!.Name)
                .ToListAsync();
            albumArtists.Should().BeEquivalentTo("Sampler", "Guest", "Other");
            await db.MediaFiles
                .Where(file => file.Kind == MediaFileKind.Audio && file.DirectoryPath == "Sampler/Hits")
                .ExecuteUpdateAsync(setters => setters.SetProperty(file => file.Tags.IsCompilation, true));
            await db.Albums
                .Where(album => album.Title == "Hits")
                .ExecuteUpdateAsync(setters => setters.SetProperty(album => album.IsCompilation, true));
            await db.MediaSources.ExecuteUpdateAsync(setters =>
                setters.SetProperty(source => source.AlbumGroupingVersion, 2));

            return (
                await db.Tracks.ToDictionaryAsync(
                    track => track.DirectoryPath + "/" + track.BaseName,
                    track => track.Id),
                (await db.Albums.SingleAsync(album => album.Title == "Record")).Id);
        }
    }

    /// <summary>
    /// Expects the library of <see cref="CreateLibraryOfGroupingVersion2Async"/> formed again by the rules of now:
    /// one compilation with mark and cover, nothing without tracks, and the ids that must stay. Returns the id of the
    /// compilation.
    /// </summary>
    private async Task<Guid> ExpectRegroupedCompilationAsync(
        Dictionary<string, Guid> trackIds,
        Guid unaffectedAlbumId)
    {
        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.Tracks.ToDictionaryAsync(track => track.DirectoryPath + "/" + track.BaseName, track => track.Id))
            .Should().BeEquivalentTo(trackIds);
        Album compilation = await LoadAlbumAsync(db, "Hits");
        (compilation.AlbumArtistId, compilation.Tracks.Count, compilation.IsCompilation)
            .Should().Be(((Guid?)null, 3, true));
        compilation.Covers.Should().ContainSingle()
            .Which.MediaFile!.RelativePath.Should().Be("Sampler/Hits/cover.jpg");
        Album record = await LoadAlbumAsync(db, "Record");
        (record.Id, record.IsCompilation).Should().Be((unaffectedAlbumId, false));
        (await db.Albums.CountAsync()).Should().Be(2);

        // The artist of the folder was only the album artist of an old album.
        (await db.Artists.Select(artist => artist.Name).ToListAsync())
            .Should().BeEquivalentTo("A", "Guest", "Other", "Band");
        (await db.MediaSources.Select(source => source.AlbumGroupingVersion).ToListAsync())
            .Should().Equal(ScannerVersions.AlbumGrouping);
        return compilation.Id;
    }

    private static Task<Track> LoadTrackAsync(WaveMpDbContext db, string normalizedBaseName)
    {
        return db.Tracks
            .Include(track => track.Files)
            .Include(track => track.Covers).ThenInclude(cover => cover.MediaFile)
            .SingleAsync(track => track.NormalizedBaseName == normalizedBaseName);
    }

    private static Task<Album> LoadAlbumAsync(WaveMpDbContext db, string title)
    {
        return db.Albums
            .Include(album => album.Tracks)
            .Include(album => album.AlbumArtist)
            .Include(album => album.Covers).ThenInclude(cover => cover.MediaFile)
            .SingleAsync(album => album.Title == title);
    }

    private static async Task<List<string>> PrimaryAsync(WaveMpDbContext db, string normalizedBaseName)
    {
        return await db.MediaFiles
            .Where(file => file.NormalizedBaseName == normalizedBaseName && file.IsPrimary)
            .Select(file => file.FileName)
            .ToListAsync();
    }

    private async Task<DateTimeOffset> FirstSeenAsync(string normalizedBaseName)
    {
        await using WaveMpDbContext db = _database.CreateDbContext();
        return await db.Tracks
            .Where(track => track.NormalizedBaseName == normalizedBaseName)
            .Select(track => track.FirstSeenAt)
            .SingleAsync();
    }

    private async Task<ScanResult> ScanAsync(ScanScope? scope = null, ServiceProvider? services = null)
    {
        await using AsyncServiceScope serviceScope = (services ?? _services).CreateAsyncScope();
        ILibraryScanner scanner = serviceScope.ServiceProvider.GetRequiredService<ILibraryScanner>();
        return await scanner.ScanAsync(scope ?? ScanScope.Full, Guid.NewGuid());
    }

    private async Task<bool> RefreshAsync(string folder, params string[] changedFiles)
    {
        await using AsyncServiceScope serviceScope = _services.CreateAsyncScope();
        ILibraryScanner scanner = serviceScope.ServiceProvider.GetRequiredService<ILibraryScanner>();
        return await scanner.RefreshFolderAsync(MediaSource.DefaultId, folder, changedFiles);
    }

    /// <summary>
    /// Writes the file again with other tags and a later change time, so a scan sees the change for sure.
    /// </summary>
    private async Task ChangeAudioAsync(string relativePath, TestTags tags)
    {
        await AudioAsync(relativePath, tags: tags);
        File.SetLastWriteTimeUtc(_media.Combine(relativePath), DateTime.UtcNow.AddMinutes(1));
    }

    private Task AudioAsync(
        string relativePath,
        AudioFormat format = AudioFormat.Mp3,
        TestTags? tags = null,
        int? bitrate = null,
        int? sampleRate = null,
        int? bitDepth = null)
    {
        return TestMediaGenerator.CreateAudioAsync(
            _media.Combine(relativePath),
            format,
            tags,
            bitrate,
            sampleRate,
            bitDepth);
    }

    /// <summary>
    /// Sets the ID3 frame TCMP with TagLib#, since ffmpeg writes the compilation flag of MP3 files only as a user text
    /// frame.
    /// </summary>
    private void SetTcmpFrame(string relativePath)
    {
        using var file = TagLib.File.Create(_media.Combine(relativePath));
        ((TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true)).IsCompilation = true;
        file.Save();
    }

    private Task VideoAsync(string relativePath, int width, int height)
    {
        VideoFormat format = relativePath.EndsWith(".mkv", StringComparison.Ordinal) ? VideoFormat.Mkv : VideoFormat.Mp4;
        return TestMediaGenerator.CreateVideoAsync(_media.Combine(relativePath), format, width, height);
    }

    private Task ImageAsync(string relativePath, int width, int height)
    {
        return TestMediaGenerator.CreateImageAsync(_media.Combine(relativePath), width, height);
    }

    /// <summary>
    /// Adds a source to the database and lets the catalog of <see cref="_services"/> see it.
    /// </summary>
    private async Task<MediaSource> AddSourceAsync(string name, string path)
    {
        MediaSource source = new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Path = Path.GetFullPath(path),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            db.MediaSources.Add(source);
            await db.SaveChangesAsync();
        }

        await _services.GetRequiredService<IMediaSourceCatalog>().RefreshAsync();
        return source;
    }

    private async Task SetActiveAsync(Guid sourceId, bool isActive)
    {
        await using (WaveMpDbContext db = _database.CreateDbContext())
        {
            await db.MediaSources
                .Where(source => source.Id == sourceId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(source => source.IsActive, isActive));
        }

        await _services.GetRequiredService<IMediaSourceCatalog>().RefreshAsync();
    }

    private static ServiceProvider CreateServices(string mediaPath)
    {
        Dictionary<string, string?> settings = TestDatabaseSettings.ToConfiguration(DatabaseName);
        settings["MEDIA_PATH"] = mediaPath;
        settings["Metadata:FfprobePath"] = FfmpegTool.FfprobePath;
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddWaveMpData();
        services.AddWaveMpSettings();
        services.AddWaveMpMetadata();
        services.AddWaveMpJobs();
        services.AddWaveMpScanning();
        return services.BuildServiceProvider();
    }
}
