using AwesomeAssertions;
using WaveMP.Core.Metadata.Artists;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Data.Entities;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Metadata.Tests.Tags;

/// <summary>
/// Reads and writes several artists in synthetic media files created with ffmpeg; the multi-values are set with
/// TagLib# the way other taggers write them.
/// </summary>
[TestFixture]
[Category("Media")]
public class ArtistTagTests
{
    private static readonly TestTags _tags = new()
    {
        Title = "Song",
        Artist = "Old Artist",
        AlbumArtist = "Old Album Artist",
        Album = "Album",
    };

    private TemporaryDirectory _directory = null!;
    private TagLibTagReader _reader = null!;
    private TagLibTagWriter _writer = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = new TemporaryDirectory();
        _reader = new TagLibTagReader();
        _writer = new TagLibTagWriter();
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Dispose();
    }

    [Test]
    public async Task Read_Id3v24WithSeveralArtists_TakesOverTheValues()
    {
        string path = await CreateAsync(AudioFormat.Mp3, "mp3");
        using (var file = TagLib.File.Create(path))
        {
            var id3 = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true);
            id3.Version = 4;
            id3.Performers = ["Simon & Garfunkel", "AC/DC"];
            id3.AlbumArtists = ["Earth, Wind & Fire", "K/DA"];
            file.Save();
        }

        MediaTags tags = _reader.Read(path).Tags;

        tags.ArtistValues.Should().Equal("Simon & Garfunkel", "AC/DC");
        tags.Artist.Should().Be("Simon & Garfunkel; AC/DC");
        tags.AlbumArtistValues.Should().Equal("Earth, Wind & Fire", "K/DA");
    }

    [Test]
    public async Task Read_Id3v23WithSlash_KeepsTheNameWhole()
    {
        string path = await CreateAsync(AudioFormat.Mp3, "mp3", _tags with { Artist = "AC/DC" });

        MediaTags tags = _reader.Read(path).Tags;

        tags.Artist.Should().Be("AC/DC");
        tags.ArtistValues.Should().BeEmpty();
    }

    [Test]
    public async Task Read_Id3ArtistsFrame_TakesOverTheValues()
    {
        string path = await CreateAsync(AudioFormat.Mp3, "mp3", _tags with { Artist = "Bob Sinclar feat. K.Flay" });
        using (var file = TagLib.File.Create(path))
        {
            var id3 = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true);
            var frame = TagLib.Id3v2.UserTextInformationFrame.Get(id3, "ARTISTS", true);
            frame.Text = ["Bob Sinclar", "K.Flay"];
            file.Save();
        }

        MediaTags tags = _reader.Read(path).Tags;

        tags.ArtistValues.Should().Equal("Bob Sinclar", "K.Flay");
        ArtistNames.Resolve(tags.ArtistValues, tags.Artist, []).Should().Equal(
            new ArtistName("Bob Sinclar"),
            new ArtistName("K.Flay", ArtistRole.Featured));
    }

    [Test]
    public async Task Read_FlacArtistsFields_TakesOverTheValuesAndTheMusicBrainzArtists()
    {
        string path = await CreateAsync(AudioFormat.Flac, "flac", _tags with { Artist = "Bob Sinclar feat. K.Flay" });
        using (var file = TagLib.File.Create(path))
        {
            var xiph = (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true);
            xiph.SetField("ARTISTS", ["Bob Sinclar", "K.Flay"]);
            xiph.SetField(
                "MUSICBRAINZ_ARTISTID",
                ["5b11f4ce-a62d-471e-81fc-a69a8278c7da", "a74b1b7f-71a5-4011-9441-d0b5e4122711"]);
            file.Save();
        }

        MediaTags tags = _reader.Read(path).Tags;

        tags.Artist.Should().Be("Bob Sinclar feat. K.Flay");
        tags.ArtistValues.Should().Equal("Bob Sinclar", "K.Flay");
        tags.MusicBrainzArtistIds.Should().Equal(
            "5b11f4ce-a62d-471e-81fc-a69a8278c7da",
            "a74b1b7f-71a5-4011-9441-d0b5e4122711");
    }

    [Test]
    public async Task Read_Mp4AlbumArtistsBoxes_TakesOverTheValues()
    {
        string path = await CreateAsync(AudioFormat.M4a, "m4a");
        using (var file = TagLib.File.Create(path))
        {
            var apple = (TagLib.Mpeg4.AppleTag)file.GetTag(TagLib.TagTypes.Apple, true);
            apple.SetDashBoxes("com.apple.iTunes", "ALBUMARTISTS", ["Simon & Garfunkel", "Beyoncé"]);
            file.Save();
        }

        MediaTags tags = _reader.Read(path).Tags;

        tags.AlbumArtistValues.Should().Equal("Simon & Garfunkel", "Beyoncé");
    }

    [Test]
    public async Task Read_SingleText_HasNoValues()
    {
        string path = await CreateAsync(AudioFormat.Flac, "flac", _tags with { Artist = "Simon & Garfunkel" });

        MediaTags tags = _reader.Read(path).Tags;

        tags.Artist.Should().Be("Simon & Garfunkel");
        tags.ArtistValues.Should().BeEmpty();
    }

    [TestCase(AudioFormat.Mp3, "mp3")]
    [TestCase(AudioFormat.Flac, "flac")]
    [TestCase(AudioFormat.M4a, "m4a")]
    [TestCase(AudioFormat.Opus, "opus")]
    public async Task Write_ArtistCredits_WritesTheValuesAndAReadableText(AudioFormat format, string extension)
    {
        string path = await CreateAsync(format, extension);

        TagWriteStatus status = _writer.Write(path, MediaFileKind.Audio, new TagChanges
        {
            ArtistCredits = new TagChange<IReadOnlyList<ArtistName>>(
            [
                new ArtistName("Bob Sinclar"),
                new ArtistName("Simon & Garfunkel"),
                new ArtistName("K.Flay", ArtistRole.Featured),
            ]),
            AlbumArtistCredits = new TagChange<IReadOnlyList<string>>(["Bob Sinclar", "AC/DC"]),
        });

        status.Should().Be(TagWriteStatus.Written);
        MediaTags tags = _reader.Read(path).Tags;
        tags.Artist.Should().Be("Bob Sinclar; Simon & Garfunkel feat. K.Flay");
        tags.ArtistValues.Should().Equal("Bob Sinclar", "Simon & Garfunkel", "K.Flay");
        tags.AlbumArtist.Should().Be("Bob Sinclar; AC/DC");
        tags.AlbumArtistValues.Should().Equal("Bob Sinclar", "AC/DC");
        ArtistNames.Resolve(tags.ArtistValues, tags.Artist, []).Should().Equal(
            new ArtistName("Bob Sinclar"),
            new ArtistName("Simon & Garfunkel"),
            new ArtistName("K.Flay", ArtistRole.Featured));
    }

    [TestCase(AudioFormat.Mp3, "mp3")]
    [TestCase(AudioFormat.Flac, "flac")]
    [TestCase(AudioFormat.M4a, "m4a")]
    public async Task Write_OneConfirmedArtist_MarksTheNameAsOneArtist(AudioFormat format, string extension)
    {
        string path = await CreateAsync(format, extension, _tags with { Artist = "Simon & Garfunkel" });

        _writer.Write(path, MediaFileKind.Audio, new TagChanges
        {
            ArtistCredits = new TagChange<IReadOnlyList<ArtistName>>([new ArtistName("Simon & Garfunkel")]),
        });

        MediaTags tags = _reader.Read(path).Tags;
        tags.Artist.Should().Be("Simon & Garfunkel");
        tags.ArtistValues.Should().Equal("Simon & Garfunkel");
    }

    [TestCase(AudioFormat.Mp3, "mp3")]
    [TestCase(AudioFormat.Flac, "flac")]
    [TestCase(AudioFormat.M4a, "m4a")]
    public async Task Write_SingleArtistText_RemovesTheValues(AudioFormat format, string extension)
    {
        string path = await CreateAsync(format, extension);
        _writer.Write(path, MediaFileKind.Audio, new TagChanges
        {
            ArtistCredits = new TagChange<IReadOnlyList<ArtistName>>([new ArtistName("A"), new ArtistName("B")]),
        });

        _writer.Write(path, MediaFileKind.Audio, new TagChanges { Artist = new TagChange<string?>("Solo") });

        MediaTags tags = _reader.Read(path).Tags;
        tags.Artist.Should().Be("Solo");
        tags.ArtistValues.Should().BeEmpty();
    }

    [Test]
    public async Task Write_EmptyArtistCredits_RemovesTheArtists()
    {
        string path = await CreateAsync(AudioFormat.Flac, "flac");
        _writer.Write(path, MediaFileKind.Audio, new TagChanges
        {
            ArtistCredits = new TagChange<IReadOnlyList<ArtistName>>([new ArtistName("A"), new ArtistName("B")]),
        });

        _writer.Write(path, MediaFileKind.Audio, new TagChanges
        {
            ArtistCredits = new TagChange<IReadOnlyList<ArtistName>>([]),
        });

        MediaTags tags = _reader.Read(path).Tags;
        tags.Artist.Should().BeNull();
        tags.ArtistValues.Should().BeEmpty();
    }

    private async Task<string> CreateAsync(AudioFormat format, string extension, TestTags? tags = null)
    {
        string path = _directory.Combine($"song.{extension}");
        await TestMediaGenerator.CreateAudioAsync(path, format, tags ?? _tags);
        return path;
    }
}
