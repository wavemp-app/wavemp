using AwesomeAssertions;
using WaveMP.Core.Metadata.Paths;
using WaveMP.Core.Metadata.Resolutions;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Metadata.Tests.Resolutions;

[TestFixture]
public class TitleCleanerTests
{
    [TestCase(TitleCleaningMode.LooseArtist)]
    [TestCase(TitleCleaningMode.ExactArtistTag)]
    public void Clean_APrefixEqualToTheArtistTag_IsRemoved(TitleCleaningMode mode)
    {
        TitleCleaner.Clean("Alan Walker - Faded", "Alan Walker", null, mode).Should().Be("Faded");
    }

    [Test]
    public void Clean_WithoutCleaning_KeepsTheTitle()
    {
        TitleCleaner.Clean("Alan Walker - Faded", "Alan Walker", null, TitleCleaningMode.None)
            .Should().Be("Alan Walker - Faded");
    }

    [Test]
    public void Clean_FileBased_IgnoresCaseAndWhiteSpace()
    {
        TitleCleaner.Clean("ALAN  walker - Faded", "Alan Walker", null, TitleCleaningMode.LooseArtist)
            .Should().Be("Faded");
    }

    [Test]
    public void Clean_FolderBased_NeedsTheExactArtistTag()
    {
        TitleCleaner.Clean("ALAN walker - Faded", "Alan Walker", null, TitleCleaningMode.ExactArtistTag)
            .Should().Be("ALAN walker - Faded");
        TitleCleaner.Clean("Alan Walker - Faded", null, "Alan Walker", TitleCleaningMode.ExactArtistTag)
            .Should().Be("Alan Walker - Faded");
    }

    [Test]
    public void Clean_FileBasedWithoutArtistTag_ComparesTheArtistOfThePath()
    {
        TitleCleaner.Clean("Alan Walker - Faded", null, "alan walker", TitleCleaningMode.LooseArtist)
            .Should().Be("Faded");
    }

    [Test]
    public void Clean_FileBasedWithAnotherArtistTag_IgnoresTheArtistOfThePath()
    {
        TitleCleaner.Clean("Alan Walker - Faded", "Someone", "Alan Walker", TitleCleaningMode.LooseArtist)
            .Should().Be("Alan Walker - Faded");
    }

    [TestCase("Another Artist - Faded")]
    [TestCase("Faded - Alan Walker")]
    [TestCase("Alan Walker-Faded")]
    [TestCase("Alan Walker - ")]
    [TestCase("Alan Walker -  ")]
    [TestCase("Faded")]
    public void Clean_WithoutAMatchingPrefixOrRest_KeepsTheTitle(string title)
    {
        TitleCleaner.Clean(title, "Alan Walker", null, TitleCleaningMode.LooseArtist).Should().Be(title);
    }

    [Test]
    public void Clean_AnArtistWithTheSeparator_IsRemovedAsAWhole()
    {
        TitleCleaner.Clean("Up - Down - Song - Part 2", "Up - Down", null, TitleCleaningMode.ExactArtistTag)
            .Should().Be("Song - Part 2");
    }

    [Test]
    public void Resolve_WithTitleCleaning_CleansOnlyTitlesFromTags()
    {
        PathMetadata path = PathMetadataParser.Parse(["Alan Walker"], "Alan Walker - Faded");
        MediaTags tags = new() { Title = "Alan Walker - Faded", Artist = "Alan Walker" };

        ResolvedTrackMetadata cleaned = TrackMetadataResolver.Resolve(tags, path, TitleCleaningMode.LooseArtist);
        ResolvedTrackMetadata kept = TrackMetadataResolver.Resolve(tags, path);

        cleaned.Title.Should().Be("Faded");
        cleaned.TitleSource.Should().Be(TrackMetadataSource.Tags);
        cleaned.Artist.Should().Be("Alan Walker");
        kept.Title.Should().Be("Alan Walker - Faded");
    }
}
