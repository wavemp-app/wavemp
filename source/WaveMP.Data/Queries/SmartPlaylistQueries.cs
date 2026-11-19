using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Data.Queries;

/// <summary>
/// Translates the rules of a smart playlist into a query of the tracks: every rule becomes a condition (with the
/// rating, favorites and play history of the owner as subqueries), the conditions are combined with AND (all) or OR
/// (any), and the sort key becomes an ORDER BY. The database computes the list on every request.
/// </summary>
internal static class SmartPlaylistQueries
{
    /// <summary>
    /// Only the tracks that meet the rules of the criteria; all tracks without rules.
    /// </summary>
    public static IQueryable<Track> Filter(
        IQueryable<Track> tracks,
        WaveMpDbContext dbContext,
        Guid userId,
        SmartPlaylistCriteria criteria,
        DateTimeOffset now)
    {
        if (criteria.Rules.Count == 0)
        {
            return tracks;
        }

        ParameterExpression track = Expression.Parameter(typeof(Track), "track");
        Expression? body = null;
        foreach (SmartPlaylistRule rule in criteria.Rules)
        {
            Expression<Func<Track, bool>> condition = Condition(rule, dbContext, userId, now);
            Expression next = ParameterReplacer.Replace(condition.Body, condition.Parameters[0], track);
            body = body is null
                ? next
                : criteria.Match == SmartPlaylistMatch.Any
                    ? Expression.OrElse(body, next)
                    : Expression.AndAlso(body, next);
        }

        return tracks.Where(Expression.Lambda<Func<Track, bool>>(body!, track));
    }

    /// <summary>
    /// Sorts by the key of the criteria; tracks without a value come last in both directions, ties are sorted by
    /// album, disc and track number, title and id, so the order is stable for paging.
    /// </summary>
    public static IOrderedQueryable<Track> Sort(
        IQueryable<Track> tracks,
        WaveMpDbContext dbContext,
        Guid userId,
        SmartPlaylistCriteria criteria)
    {
        SortDirection direction = criteria.Direction;
        IQueryable<TrackRating> ratings = dbContext.TrackRatings.Where(rating => rating.UserId == userId);
        IQueryable<PlayHistoryEntry> plays = dbContext.PlayHistory.Where(entry => entry.UserId == userId);
        IOrderedQueryable<Track> sorted = criteria.Sort switch
        {
            SmartPlaylistSortField.Title => tracks.OrderBy(track => track.Title, direction),
            SmartPlaylistSortField.Artist => tracks
                .OrderBy(track => track.Artist == null)
                .ThenBy(track => track.Artist!.NormalizedName, direction)
                .ThenBy(track => track.Album!.NormalizedTitle),
            SmartPlaylistSortField.Album => tracks
                .OrderBy(track => track.Album == null)
                .ThenBy(track => track.Album!.NormalizedTitle, direction),
            SmartPlaylistSortField.Year => tracks
                .OrderBy(track => track.Year == null)
                .ThenBy(track => track.Year, direction),
            SmartPlaylistSortField.Genre => tracks
                .OrderBy(track => track.Genre == null)
                .ThenBy(track => track.Genre!.ToUpper(), direction),
            SmartPlaylistSortField.Rating => tracks.OrderBy(
                track => ratings
                    .Where(rating => rating.TrackId == track.Id)
                    .Select(rating => (int?)rating.Rating)
                    .FirstOrDefault() ?? 0,
                direction),
            SmartPlaylistSortField.PlayCount => tracks.OrderBy(
                track => plays.Count(entry => entry.TrackId == track.Id),
                direction),
            SmartPlaylistSortField.LastPlayed => tracks
                .OrderBy(track => plays
                    .Where(entry => entry.TrackId == track.Id)
                    .Max(entry => (DateTimeOffset?)entry.PlayedAt) == null)
                .ThenBy(
                    track => plays
                        .Where(entry => entry.TrackId == track.Id)
                        .Max(entry => (DateTimeOffset?)entry.PlayedAt),
                    direction),
            SmartPlaylistSortField.DateAdded => tracks.OrderBy(track => track.FirstSeenAt, direction),
            SmartPlaylistSortField.Duration => tracks
                .OrderBy(track => track.Duration == null)
                .ThenBy(track => track.Duration, direction),
            _ => throw new ArgumentOutOfRangeException(nameof(criteria), criteria.Sort, "Unknown sort key."),
        };

        return sorted
            .ThenBy(track => track.AlbumId)
            .ThenBy(track => track.DiscNumber)
            .ThenBy(track => track.TrackNumber)
            .ThenBy(track => track.Title)
            .ThenBy(track => track.Id);
    }

    /// <summary>
    /// The condition of one rule. Negations are the complement of their positive operator, so they also match tracks
    /// without a value.
    /// </summary>
    /// <exception cref="ArgumentException">The operator does not fit the field, or a value is missing.</exception>
    public static Expression<Func<Track, bool>> Condition(
        SmartPlaylistRule rule,
        WaveMpDbContext dbContext,
        Guid userId,
        DateTimeOffset now)
    {
        Expression<Func<Track, bool>> positive = rule.Field switch
        {
            SmartPlaylistField.Artist or SmartPlaylistField.Album or SmartPlaylistField.Genre => Text(rule),
            SmartPlaylistField.Year => Range(track => track.Year, rule),
            SmartPlaylistField.Rating => Range(
                track => (int?)(dbContext.TrackRatings
                    .Where(rating => rating.UserId == userId && rating.TrackId == track.Id)
                    .Select(rating => (int?)rating.Rating)
                    .FirstOrDefault() ?? 0),
                rule),
            SmartPlaylistField.PlayCount => Range(
                track => (int?)dbContext.PlayHistory.Count(entry => entry.UserId == userId && entry.TrackId == track.Id),
                rule),
            SmartPlaylistField.Duration => Duration(rule),
            SmartPlaylistField.LastPlayed => Within(
                track => dbContext.PlayHistory
                    .Where(entry => entry.UserId == userId && entry.TrackId == track.Id)
                    .Max(entry => (DateTimeOffset?)entry.PlayedAt),
                rule,
                now),
            SmartPlaylistField.DateAdded => Within(track => (DateTimeOffset?)track.FirstSeenAt, rule, now),
            SmartPlaylistField.Favorite => Flag(
                track => dbContext.Favorites.Any(favorite =>
                    favorite.UserId == userId && favorite.TrackId == track.Id),
                rule),
            SmartPlaylistField.HasAudio => Flag(track => track.HasAudio, rule),
            SmartPlaylistField.HasVideo => Flag(track => track.HasVideo, rule),
            _ => throw new ArgumentException($"Unknown field {rule.Field}.", nameof(rule)),
        };

        return IsNegation(rule.Operator)
            ? Expression.Lambda<Func<Track, bool>>(Expression.Not(positive.Body), positive.Parameters)
            : positive;
    }

    private static bool IsNegation(SmartPlaylistOperator op)
    {
        return op is SmartPlaylistOperator.IsNot
            or SmartPlaylistOperator.DoesNotContain
            or SmartPlaylistOperator.NotInLast
            or SmartPlaylistOperator.IsFalse;
    }

    /// <summary>
    /// Artist (of the track or the album), album or genre equal to the text or containing it, without regard to case.
    /// </summary>
    private static Expression<Func<Track, bool>> Text(SmartPlaylistRule rule)
    {
        string text = rule.Text ?? throw new ArgumentException("The rule needs a text.", nameof(rule));
        string pattern = rule.Operator switch
        {
            SmartPlaylistOperator.Is or SmartPlaylistOperator.IsNot => LikePatterns.Escape(text),
            SmartPlaylistOperator.Contains or SmartPlaylistOperator.DoesNotContain => LikePatterns.Contains(text),
            _ => throw UnsupportedOperator(rule),
        };

        return rule.Field switch
        {
            SmartPlaylistField.Artist => track =>
                (track.Artist != null
                    && EF.Functions.ILike(track.Artist.Name, pattern, LikePatterns.EscapeCharacter))
                || (track.Album != null
                    && track.Album.AlbumArtist != null
                    && EF.Functions.ILike(track.Album.AlbumArtist.Name, pattern, LikePatterns.EscapeCharacter)),
            SmartPlaylistField.Album => track =>
                track.Album != null && EF.Functions.ILike(track.Album.Title, pattern, LikePatterns.EscapeCharacter),
            _ => track =>
                track.Genre != null && EF.Functions.ILike(track.Genre, pattern, LikePatterns.EscapeCharacter),
        };
    }

    /// <summary>
    /// A number in the range of the operator: equal (Is, IsNot), at least, at most or between, bounds included.
    /// </summary>
    private static Expression<Func<Track, bool>> Range(Expression<Func<Track, int?>> selector, SmartPlaylistRule rule)
    {
        (int? lower, int? upper) = Bounds(rule);
        return InRange(selector, lower, upper, upperExclusive: false);
    }

    /// <summary>
    /// The duration in whole seconds in the range of the operator: a track of 3:00.4 is 180 seconds long.
    /// </summary>
    private static Expression<Func<Track, bool>> Duration(SmartPlaylistRule rule)
    {
        (int? lower, int? upper) = Bounds(rule);
        return InRange<TimeSpan>(
            track => track.Duration,
            lower is { } from ? TimeSpan.FromSeconds(from) : null,
            upper is { } to ? TimeSpan.FromSeconds(to + 1L) : null,
            upperExclusive: true);
    }

    /// <summary>
    /// A time within the last days of the rule; tracks without a time (never played) are not within.
    /// </summary>
    private static Expression<Func<Track, bool>> Within(
        Expression<Func<Track, DateTimeOffset?>> selector,
        SmartPlaylistRule rule,
        DateTimeOffset now)
    {
        if (rule.Operator is not (SmartPlaylistOperator.InLast or SmartPlaylistOperator.NotInLast))
        {
            throw UnsupportedOperator(rule);
        }

        int days = rule.Number ?? throw new ArgumentException("The rule needs a number of days.", nameof(rule));
        return InRange<DateTimeOffset>(selector, now.AddDays(-days), null, upperExclusive: false);
    }

    private static Expression<Func<Track, bool>> Flag(Expression<Func<Track, bool>> condition, SmartPlaylistRule rule)
    {
        return rule.Operator is SmartPlaylistOperator.IsTrue or SmartPlaylistOperator.IsFalse
            ? condition
            : throw UnsupportedOperator(rule);
    }

    /// <summary>
    /// Lower and upper bound (both included) of a number rule; <c>null</c> where the range is open.
    /// </summary>
    private static (int? Lower, int? Upper) Bounds(SmartPlaylistRule rule)
    {
        int number = rule.Number ?? throw new ArgumentException("The rule needs a number.", nameof(rule));
        return rule.Operator switch
        {
            SmartPlaylistOperator.Is or SmartPlaylistOperator.IsNot => (number, number),
            SmartPlaylistOperator.AtLeast => (number, null),
            SmartPlaylistOperator.AtMost => (null, number),
            SmartPlaylistOperator.Between => (
                number,
                rule.NumberTo ?? throw new ArgumentException("The rule needs a second number.", nameof(rule))),
            _ => throw UnsupportedOperator(rule),
        };
    }

    /// <summary>
    /// <c>lower &lt;= value</c> and <c>value &lt;= upper</c> (or <c>&lt; upper</c>); a value that is not set is in no
    /// range. The bounds are parameters of the query.
    /// </summary>
    private static Expression<Func<Track, bool>> InRange<T>(
        Expression<Func<Track, T?>> selector,
        T? lower,
        T? upper,
        bool upperExclusive)
        where T : struct
    {
        Expression value = selector.Body;
        Expression? condition = null;
        if (lower is not null)
        {
            condition = Expression.GreaterThanOrEqual(value, Captured(lower));
        }

        if (upper is not null)
        {
            Expression below = upperExclusive
                ? Expression.LessThan(value, Captured(upper))
                : Expression.LessThanOrEqual(value, Captured(upper));
            condition = condition is null ? below : Expression.AndAlso(condition, below);
        }

        // A value without bounds: every track that has the value.
        condition ??= Expression.NotEqual(value, Expression.Constant(null, typeof(T?)));
        return Expression.Lambda<Func<Track, bool>>(condition, selector.Parameters);
    }

    /// <summary>
    /// The value as access to a captured variable, which EF Core sends as a parameter instead of a constant.
    /// </summary>
    private static Expression Captured<T>(T value)
    {
        Expression<Func<T>> accessor = () => value;
        return accessor.Body;
    }

    private static ArgumentException UnsupportedOperator(SmartPlaylistRule rule)
    {
        return new ArgumentException($"The operator {rule.Operator} does not fit the field {rule.Field}.", nameof(rule));
    }

    /// <summary>
    /// Puts one parameter in place of another, so the conditions of all rules share the parameter of the query.
    /// </summary>
    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        public static Expression Replace(Expression body, ParameterExpression from, ParameterExpression to)
        {
            return new ParameterReplacer(from, to).Visit(body);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == from ? to : base.VisitParameter(node);
        }
    }
}
