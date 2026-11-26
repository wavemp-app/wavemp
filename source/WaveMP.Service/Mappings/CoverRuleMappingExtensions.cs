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

    public static AlbumCoverFallbackRuleDto ToDto(this AlbumCoverFallbackResult result)
    {
        return new AlbumCoverFallbackRuleDto
        {
            Fallback = result.Fallback.ToOptionDto(),
            InheritedFallback = result.Inherited.ToDto(),
            EffectiveFallback = result.Effective.ToDto(),
        };
    }

    public static CoverPriorityDto ToDto(this CoverPriority priority)
    {
        return priority switch
        {
            CoverPriority.AlbumFirst => CoverPriorityDto.AlbumFirst,
            CoverPriority.TrackFirst => CoverPriorityDto.TrackFirst,
            CoverPriority.TrackOnly => CoverPriorityDto.TrackOnly,
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown cover priority."),
        };
    }

    public static CoverPriority ToEntity(this CoverPriorityDto priority)
    {
        return priority switch
        {
            CoverPriorityDto.AlbumFirst => CoverPriority.AlbumFirst,
            CoverPriorityDto.TrackFirst => CoverPriority.TrackFirst,
            CoverPriorityDto.TrackOnly => CoverPriority.TrackOnly,
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown cover priority."),
        };
    }

    public static AlbumCoverFallbackDto ToDto(this AlbumCoverFallback fallback)
    {
        return fallback switch
        {
            AlbumCoverFallback.ArtistPicture => AlbumCoverFallbackDto.ArtistPicture,
            AlbumCoverFallback.None => AlbumCoverFallbackDto.None,
            _ => throw new ArgumentOutOfRangeException(nameof(fallback), fallback, "Unknown album cover fallback."),
        };
    }

    public static AlbumCoverFallback ToEntity(this AlbumCoverFallbackDto fallback)
    {
        return fallback switch
        {
            AlbumCoverFallbackDto.ArtistPicture => AlbumCoverFallback.ArtistPicture,
            AlbumCoverFallbackDto.None => AlbumCoverFallback.None,
            _ => throw new ArgumentOutOfRangeException(nameof(fallback), fallback, "Unknown album cover fallback."),
        };
    }

    /// <summary>
    /// The rule of an album or track as option; <c>null</c> (no rule of its own) is
    /// <see cref="CoverRuleOptionDto.Inherit"/>.
    /// </summary>
    public static CoverRuleOptionDto ToOptionDto(this CoverPriority? rule)
    {
        return rule switch
        {
            null => CoverRuleOptionDto.Inherit,
            CoverPriority.AlbumFirst => CoverRuleOptionDto.AlbumFirst,
            CoverPriority.TrackFirst => CoverRuleOptionDto.TrackFirst,
            CoverPriority.TrackOnly => CoverRuleOptionDto.TrackOnly,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unknown cover priority."),
        };
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
            CoverRuleOptionDto.TrackOnly => CoverPriority.TrackOnly,
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown cover rule."),
        };
    }

    /// <summary>
    /// The exception of an album as option; <c>null</c> is <see cref="AlbumCoverFallbackOptionDto.Inherit"/>.
    /// </summary>
    public static AlbumCoverFallbackOptionDto ToOptionDto(this AlbumCoverFallback? fallback)
    {
        return fallback switch
        {
            null => AlbumCoverFallbackOptionDto.Inherit,
            AlbumCoverFallback.ArtistPicture => AlbumCoverFallbackOptionDto.ArtistPicture,
            AlbumCoverFallback.None => AlbumCoverFallbackOptionDto.None,
            _ => throw new ArgumentOutOfRangeException(nameof(fallback), fallback, "Unknown album cover fallback."),
        };
    }

    /// <summary>
    /// The exception of an option; <see cref="AlbumCoverFallbackOptionDto.Inherit"/> is <c>null</c>.
    /// </summary>
    public static AlbumCoverFallback? ToFallback(this AlbumCoverFallbackOptionDto option)
    {
        return option switch
        {
            AlbumCoverFallbackOptionDto.Inherit => null,
            AlbumCoverFallbackOptionDto.ArtistPicture => AlbumCoverFallback.ArtistPicture,
            AlbumCoverFallbackOptionDto.None => AlbumCoverFallback.None,
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown album cover fallback."),
        };
    }
}
