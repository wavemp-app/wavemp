using WaveMP.Core.Library.Covers;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class CoverRuleMappingExtensions
{
    public static CoverRuleDto ToDto(this CoverRuleResult result)
    {
        return new CoverRuleDto
        {
            Rule = result.Rule.ToOptionDto(),
            InheritedPriority = result.Inherited.ToDto(),
            EffectivePriority = result.Effective.ToDto(),
        };
    }

    public static CoverPriorityDto ToDto(this CoverPriority priority)
    {
        return priority switch
        {
            CoverPriority.AlbumFirst => CoverPriorityDto.AlbumFirst,
            CoverPriority.TrackFirst => CoverPriorityDto.TrackFirst,
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown cover priority."),
        };
    }

    public static CoverPriority ToEntity(this CoverPriorityDto priority)
    {
        return priority switch
        {
            CoverPriorityDto.AlbumFirst => CoverPriority.AlbumFirst,
            CoverPriorityDto.TrackFirst => CoverPriority.TrackFirst,
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown cover priority."),
        };
    }

    /// <summary>
    /// The rule of an album or track as option; <c>null</c> (no rule of its own) is
    /// <see cref="CoverRuleOptionDto.Inherit"/>.
    /// </summary>
    public static CoverRuleOptionDto ToOptionDto(this CoverPriority? rule)
    {
        return rule is { } priority
            ? priority == CoverPriority.AlbumFirst ? CoverRuleOptionDto.AlbumFirst : CoverRuleOptionDto.TrackFirst
            : CoverRuleOptionDto.Inherit;
    }

    /// <summary>
    /// The rule of an option; <see cref="CoverRuleOptionDto.Inherit"/> is <c>null</c> (no rule of its own).
    /// </summary>
    public static CoverPriority? ToRule(this CoverRuleOptionDto option)
    {
        return option switch
        {
            CoverRuleOptionDto.Inherit => null,
            CoverRuleOptionDto.AlbumFirst => CoverPriority.AlbumFirst,
            CoverRuleOptionDto.TrackFirst => CoverPriority.TrackFirst,
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown cover rule."),
        };
    }
}
