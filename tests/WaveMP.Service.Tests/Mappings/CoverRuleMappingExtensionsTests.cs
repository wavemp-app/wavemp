using AwesomeAssertions;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Settings.Groups;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class CoverRuleMappingExtensionsTests
{
    [Test]
    public void ToDto_EveryPriority_MapsBothWays()
    {
        foreach (CoverPriority priority in Enum.GetValues<CoverPriority>())
        {
            priority.ToDto().ToEntity().Should().Be(priority);
            priority.ToDto().ToString().Should().Be(priority.ToString());
        }
    }

    [TestCase(null, CoverRuleOptionDto.Inherit)]
    [TestCase(CoverPriority.AlbumFirst, CoverRuleOptionDto.AlbumFirst)]
    [TestCase(CoverPriority.TrackFirst, CoverRuleOptionDto.TrackFirst)]
    [TestCase(CoverPriority.TrackOnly, CoverRuleOptionDto.TrackOnly)]
    public void ToOptionDto_EveryRule_MapsBothWays(CoverPriority? rule, CoverRuleOptionDto option)
    {
        rule.ToOptionDto().Should().Be(option);
        option.ToRule().Should().Be(rule);
    }

    [Test]
    public void ToDto_EveryFallback_MapsBothWays()
    {
        foreach (AlbumCoverFallback fallback in Enum.GetValues<AlbumCoverFallback>())
        {
            fallback.ToDto().ToEntity().Should().Be(fallback);
            fallback.ToDto().ToString().Should().Be(fallback.ToString());
        }
    }

    [TestCase(null, AlbumCoverFallbackOptionDto.Inherit)]
    [TestCase(AlbumCoverFallback.ArtistPicture, AlbumCoverFallbackOptionDto.ArtistPicture)]
    [TestCase(AlbumCoverFallback.None, AlbumCoverFallbackOptionDto.None)]
    public void ToOptionDto_EveryFallback_MapsBothWays(
        AlbumCoverFallback? fallback,
        AlbumCoverFallbackOptionDto option)
    {
        fallback.ToOptionDto().Should().Be(option);
        option.ToFallback().Should().Be(fallback);
    }

    [Test]
    public void ToDto_FallbackResult_NamesTheInheritedAndEffectiveFallback()
    {
        AlbumCoverFallbackRuleDto inherited =
            new AlbumCoverFallbackResult(true, null, AlbumCoverFallback.ArtistPicture).ToDto();
        AlbumCoverFallbackRuleDto own =
            new AlbumCoverFallbackResult(true, AlbumCoverFallback.None, AlbumCoverFallback.ArtistPicture).ToDto();

        inherited.Fallback.Should().Be(AlbumCoverFallbackOptionDto.Inherit);
        inherited.InheritedFallback.Should().Be(AlbumCoverFallbackDto.ArtistPicture);
        inherited.EffectiveFallback.Should().Be(AlbumCoverFallbackDto.ArtistPicture);
        own.Fallback.Should().Be(AlbumCoverFallbackOptionDto.None);
        own.EffectiveFallback.Should().Be(AlbumCoverFallbackDto.None);
    }

    [Test]
    public void ToRule_UndefinedOption_Throws()
    {
        Action map = () => ((CoverRuleOptionDto)99).ToRule();

        map.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Test]
    public void ToDto_Result_ContainsTheRuleTheInheritedAndTheEffectivePriority()
    {
        CoverRuleDto inherited = new CoverRuleResult(true, null, CoverPriority.TrackFirst).ToDto();
        CoverRuleDto own = new CoverRuleResult(true, CoverPriority.AlbumFirst, CoverPriority.TrackFirst).ToDto();

        inherited.Rule.Should().Be(CoverRuleOptionDto.Inherit);
        inherited.InheritedPriority.Should().Be(CoverPriorityDto.TrackFirst);
        inherited.EffectivePriority.Should().Be(CoverPriorityDto.TrackFirst);
        own.Rule.Should().Be(CoverRuleOptionDto.AlbumFirst);
        own.InheritedPriority.Should().Be(CoverPriorityDto.TrackFirst);
        own.EffectivePriority.Should().Be(CoverPriorityDto.AlbumFirst);
    }

    [Test]
    public void LibrarySettings_CoverRules_AreMappedInBothDirections()
    {
        LibrarySettingsDto dto = new LibrarySettings
        {
            TrackCoverPriority = CoverPriority.TrackOnly,
            AlbumCoverFallback = AlbumCoverFallback.None,
        }.ToDto();
        UpdateLibrarySettingsCommand command = new UpdateLibrarySettingsDto
        {
            CoverPriority = CoverPriorityDto.AlbumFirst,
            AlbumCoverFallback = AlbumCoverFallbackDto.ArtistPicture,
        }.ToCommand();
        UpdateLibrarySettingsCommand unchanged = new UpdateLibrarySettingsDto().ToCommand();

        dto.CoverPriority.Should().Be(CoverPriorityDto.TrackOnly);
        dto.AlbumCoverFallback.Should().Be(AlbumCoverFallbackDto.None);
        command.TrackCoverPriority.Should().Be(CoverPriority.AlbumFirst);
        command.AlbumCoverFallback.Should().Be(AlbumCoverFallback.ArtistPicture);
        unchanged.TrackCoverPriority.Should().BeNull();
        unchanged.AlbumCoverFallback.Should().BeNull();
    }
}
