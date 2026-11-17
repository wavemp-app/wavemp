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
    public void ToOptionDto_EveryRule_MapsBothWays(CoverPriority? rule, CoverRuleOptionDto option)
    {
        rule.ToOptionDto().Should().Be(option);
        option.ToRule().Should().Be(rule);
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
    public void LibrarySettings_CoverPriority_IsMappedInBothDirections()
    {
        LibrarySettingsDto dto = new LibrarySettings { CoverPriority = CoverPriority.TrackFirst }.ToDto();
        UpdateLibrarySettingsCommand command =
            new UpdateLibrarySettingsDto { CoverPriority = CoverPriorityDto.AlbumFirst }.ToCommand();
        UpdateLibrarySettingsCommand unchanged = new UpdateLibrarySettingsDto().ToCommand();

        dto.CoverPriority.Should().Be(CoverPriorityDto.TrackFirst);
        command.CoverPriority.Should().Be(CoverPriority.AlbumFirst);
        unchanged.CoverPriority.Should().BeNull();
    }
}
