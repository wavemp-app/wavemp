using WaveMP.Core.Playlists.SmartPlaylists;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class SmartPlaylistMappingExtensions
{
    public static SmartPlaylistDto ToDto(this SmartPlaylistItem item)
    {
        SmartPlaylist playlist = item.Playlist;
        return new SmartPlaylistDto
        {
            Id = playlist.Id,
            Name = playlist.Name,
            Match = playlist.Match.ToDto(),
            Rules = [.. playlist.Rules.Select(rule => rule.ToDto())],
            Sort = playlist.Sort.ToDto(),
            Direction = playlist.Direction.ToDto(),
            Limit = playlist.Limit,
            TrackCount = item.Summary.TrackCount,
            DurationMilliseconds = (long)Math.Round(item.Summary.DurationMilliseconds),
            Cover = item.Cover.ToDto(),
            CreatedAt = playlist.CreatedAt,
            UpdatedAt = playlist.UpdatedAt,
        };
    }

    public static SmartPlaylistTemplateDto ToDto(this SmartPlaylistTemplate template)
    {
        SmartPlaylistCriteria criteria = template.Criteria;
        return new SmartPlaylistTemplateDto
        {
            Id = template.Id.ToDto(),
            Match = criteria.Match.ToDto(),
            Rules = [.. criteria.Rules.Select(rule => rule.ToDto())],
            Sort = criteria.Sort.ToDto(),
            Direction = criteria.Direction.ToDto(),
            Limit = criteria.Limit,
        };
    }

    public static SmartPlaylistRuleDto ToDto(this SmartPlaylistRule rule)
    {
        return new SmartPlaylistRuleDto
        {
            Field = rule.Field.ToDto(),
            Operator = rule.Operator.ToDto(),
            Text = rule.Text,
            ReferenceId = rule.ReferenceId,
            Number = rule.Number,
            NumberTo = rule.NumberTo,
        };
    }

    /// <summary>
    /// The smart playlist of a request, with the defaults of missing values. Field and operator of the rules are
    /// required by the validation of the request.
    /// </summary>
    public static SmartPlaylistDefinition ToDefinition(this SaveSmartPlaylistDto request)
    {
        return new SmartPlaylistDefinition(
            request.Name,
            (request.Match ?? SmartPlaylistMatchDto.All).ToEntity(),
            [.. request.Rules.Select(rule => new SmartPlaylistRule(
                rule.Field!.Value.ToEntity(),
                rule.Operator!.Value.ToEntity(),
                rule.Text,
                rule.Number,
                rule.NumberTo,
                rule.ReferenceId))],
            (request.Sort ?? SmartPlaylistTrackSortDto.Artist).ToEntity(),
            (request.Direction ?? SortDirectionDto.Asc).ToEntity(),
            request.Limit);
    }

    public static ValidationErrorCodeDto ToDto(this SmartPlaylistErrorCode code)
    {
        return code switch
        {
            SmartPlaylistErrorCode.Required => ValidationErrorCodeDto.Required,
            SmartPlaylistErrorCode.InvalidValue => ValidationErrorCodeDto.InvalidValue,
            SmartPlaylistErrorCode.OutOfRange => ValidationErrorCodeDto.OutOfRange,
            SmartPlaylistErrorCode.MaxLength => ValidationErrorCodeDto.MaxLength,
            SmartPlaylistErrorCode.MaxItems => ValidationErrorCodeDto.MaxItems,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown error code."),
        };
    }

    public static SmartPlaylistTemplateIdDto ToDto(this SmartPlaylistTemplateId id)
    {
        return id switch
        {
            SmartPlaylistTemplateId.FourStarsAndMore => SmartPlaylistTemplateIdDto.FourStarsAndMore,
            SmartPlaylistTemplateId.NeverPlayed => SmartPlaylistTemplateIdDto.NeverPlayed,
            SmartPlaylistTemplateId.RecentlyAdded => SmartPlaylistTemplateIdDto.RecentlyAdded,
            SmartPlaylistTemplateId.MostPlayed => SmartPlaylistTemplateIdDto.MostPlayed,
            SmartPlaylistTemplateId.Favorites => SmartPlaylistTemplateIdDto.Favorites,
            _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown template."),
        };
    }

    public static SmartPlaylistMatchDto ToDto(this SmartPlaylistMatch match)
    {
        return match switch
        {
            SmartPlaylistMatch.All => SmartPlaylistMatchDto.All,
            SmartPlaylistMatch.Any => SmartPlaylistMatchDto.Any,
            _ => throw new ArgumentOutOfRangeException(nameof(match), match, "Unknown match."),
        };
    }

    public static SmartPlaylistMatch ToEntity(this SmartPlaylistMatchDto match)
    {
        return match switch
        {
            SmartPlaylistMatchDto.All => SmartPlaylistMatch.All,
            SmartPlaylistMatchDto.Any => SmartPlaylistMatch.Any,
            _ => throw new ArgumentOutOfRangeException(nameof(match), match, "Unknown match."),
        };
    }

    public static SmartPlaylistFieldDto ToDto(this SmartPlaylistField field)
    {
        return field switch
        {
            SmartPlaylistField.Artist => SmartPlaylistFieldDto.Artist,
            SmartPlaylistField.Album => SmartPlaylistFieldDto.Album,
            SmartPlaylistField.Genre => SmartPlaylistFieldDto.Genre,
            SmartPlaylistField.Year => SmartPlaylistFieldDto.Year,
            SmartPlaylistField.Rating => SmartPlaylistFieldDto.Rating,
            SmartPlaylistField.Favorite => SmartPlaylistFieldDto.Favorite,
            SmartPlaylistField.PlayCount => SmartPlaylistFieldDto.PlayCount,
            SmartPlaylistField.LastPlayed => SmartPlaylistFieldDto.LastPlayed,
            SmartPlaylistField.DateAdded => SmartPlaylistFieldDto.DateAdded,
            SmartPlaylistField.Duration => SmartPlaylistFieldDto.Duration,
            SmartPlaylistField.HasAudio => SmartPlaylistFieldDto.HasAudio,
            SmartPlaylistField.HasVideo => SmartPlaylistFieldDto.HasVideo,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field."),
        };
    }

    public static SmartPlaylistField ToEntity(this SmartPlaylistFieldDto field)
    {
        return field switch
        {
            SmartPlaylistFieldDto.Artist => SmartPlaylistField.Artist,
            SmartPlaylistFieldDto.Album => SmartPlaylistField.Album,
            SmartPlaylistFieldDto.Genre => SmartPlaylistField.Genre,
            SmartPlaylistFieldDto.Year => SmartPlaylistField.Year,
            SmartPlaylistFieldDto.Rating => SmartPlaylistField.Rating,
            SmartPlaylistFieldDto.Favorite => SmartPlaylistField.Favorite,
            SmartPlaylistFieldDto.PlayCount => SmartPlaylistField.PlayCount,
            SmartPlaylistFieldDto.LastPlayed => SmartPlaylistField.LastPlayed,
            SmartPlaylistFieldDto.DateAdded => SmartPlaylistField.DateAdded,
            SmartPlaylistFieldDto.Duration => SmartPlaylistField.Duration,
            SmartPlaylistFieldDto.HasAudio => SmartPlaylistField.HasAudio,
            SmartPlaylistFieldDto.HasVideo => SmartPlaylistField.HasVideo,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field."),
        };
    }

    public static SmartPlaylistOperatorDto ToDto(this SmartPlaylistOperator op)
    {
        return op switch
        {
            SmartPlaylistOperator.Is => SmartPlaylistOperatorDto.Is,
            SmartPlaylistOperator.IsNot => SmartPlaylistOperatorDto.IsNot,
            SmartPlaylistOperator.Contains => SmartPlaylistOperatorDto.Contains,
            SmartPlaylistOperator.DoesNotContain => SmartPlaylistOperatorDto.DoesNotContain,
            SmartPlaylistOperator.AtLeast => SmartPlaylistOperatorDto.AtLeast,
            SmartPlaylistOperator.AtMost => SmartPlaylistOperatorDto.AtMost,
            SmartPlaylistOperator.Between => SmartPlaylistOperatorDto.Between,
            SmartPlaylistOperator.InLast => SmartPlaylistOperatorDto.InLast,
            SmartPlaylistOperator.NotInLast => SmartPlaylistOperatorDto.NotInLast,
            SmartPlaylistOperator.IsTrue => SmartPlaylistOperatorDto.IsTrue,
            SmartPlaylistOperator.IsFalse => SmartPlaylistOperatorDto.IsFalse,
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown operator."),
        };
    }

    public static SmartPlaylistOperator ToEntity(this SmartPlaylistOperatorDto op)
    {
        return op switch
        {
            SmartPlaylistOperatorDto.Is => SmartPlaylistOperator.Is,
            SmartPlaylistOperatorDto.IsNot => SmartPlaylistOperator.IsNot,
            SmartPlaylistOperatorDto.Contains => SmartPlaylistOperator.Contains,
            SmartPlaylistOperatorDto.DoesNotContain => SmartPlaylistOperator.DoesNotContain,
            SmartPlaylistOperatorDto.AtLeast => SmartPlaylistOperator.AtLeast,
            SmartPlaylistOperatorDto.AtMost => SmartPlaylistOperator.AtMost,
            SmartPlaylistOperatorDto.Between => SmartPlaylistOperator.Between,
            SmartPlaylistOperatorDto.InLast => SmartPlaylistOperator.InLast,
            SmartPlaylistOperatorDto.NotInLast => SmartPlaylistOperator.NotInLast,
            SmartPlaylistOperatorDto.IsTrue => SmartPlaylistOperator.IsTrue,
            SmartPlaylistOperatorDto.IsFalse => SmartPlaylistOperator.IsFalse,
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown operator."),
        };
    }

    public static SmartPlaylistTrackSortDto ToDto(this SmartPlaylistSortField sort)
    {
        return sort switch
        {
            SmartPlaylistSortField.Title => SmartPlaylistTrackSortDto.Title,
            SmartPlaylistSortField.Artist => SmartPlaylistTrackSortDto.Artist,
            SmartPlaylistSortField.Album => SmartPlaylistTrackSortDto.Album,
            SmartPlaylistSortField.Year => SmartPlaylistTrackSortDto.Year,
            SmartPlaylistSortField.Genre => SmartPlaylistTrackSortDto.Genre,
            SmartPlaylistSortField.Rating => SmartPlaylistTrackSortDto.Rating,
            SmartPlaylistSortField.PlayCount => SmartPlaylistTrackSortDto.PlayCount,
            SmartPlaylistSortField.LastPlayed => SmartPlaylistTrackSortDto.LastPlayed,
            SmartPlaylistSortField.DateAdded => SmartPlaylistTrackSortDto.DateAdded,
            SmartPlaylistSortField.Duration => SmartPlaylistTrackSortDto.Duration,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown sort key."),
        };
    }

    public static SmartPlaylistSortField ToEntity(this SmartPlaylistTrackSortDto sort)
    {
        return sort switch
        {
            SmartPlaylistTrackSortDto.Title => SmartPlaylistSortField.Title,
            SmartPlaylistTrackSortDto.Artist => SmartPlaylistSortField.Artist,
            SmartPlaylistTrackSortDto.Album => SmartPlaylistSortField.Album,
            SmartPlaylistTrackSortDto.Year => SmartPlaylistSortField.Year,
            SmartPlaylistTrackSortDto.Genre => SmartPlaylistSortField.Genre,
            SmartPlaylistTrackSortDto.Rating => SmartPlaylistSortField.Rating,
            SmartPlaylistTrackSortDto.PlayCount => SmartPlaylistSortField.PlayCount,
            SmartPlaylistTrackSortDto.LastPlayed => SmartPlaylistSortField.LastPlayed,
            SmartPlaylistTrackSortDto.DateAdded => SmartPlaylistSortField.DateAdded,
            SmartPlaylistTrackSortDto.Duration => SmartPlaylistSortField.Duration,
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Unknown sort key."),
        };
    }
}
