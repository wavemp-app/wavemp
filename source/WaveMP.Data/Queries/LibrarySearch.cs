using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using WaveMP.Data.Entities;
using WaveMP.Data.Texts;

namespace WaveMP.Data.Queries;

/// <summary>
/// The tolerant search of the library, shared by the lists of artists, albums and tracks and so by the search of the
/// API, of the web app and of the Subsonic API. A name matches a search text when it contains the text (without regard
/// to case), when its search key (see <see cref="SearchKeys"/>: the normalized name and its symbol variant) contains
/// the normalized text or its symbol variant, or when it is similar to the text (pg_trgm word similarity on the name
/// and on the search key, so typos still match). Artists also match by their aliases (see <see cref="ArtistAlias"/>).
/// The relevance ranks exact matches first, then names that start with the text, then names that contain it, then
/// artists found by an alias, then similar names; the similarity orders the matches of each rank.
/// </summary>
internal sealed class LibrarySearch
{
    /// <summary>
    /// Relevance of an exact match, before the similarity is added.
    /// </summary>
    internal const double ExactRank = 8;

    /// <summary>
    /// Relevance of a name that starts with the text.
    /// </summary>
    internal const double StartRank = 6;

    /// <summary>
    /// Relevance of a name that contains the text.
    /// </summary>
    internal const double ContainsRank = 4;

    /// <summary>
    /// Relevance of an artist found by one of its aliases.
    /// </summary>
    internal const double AliasRank = 2;

    private readonly string _pattern;
    private readonly string _exactPattern;
    private readonly string _startPattern;
    private readonly string _variant;
    private readonly string[] _keys;

    private LibrarySearch(string text)
    {
        Text = text;
        _pattern = LikePatterns.Contains(text);
        _exactPattern = LikePatterns.Escape(text);
        _startPattern = LikePatterns.Escape(text) + "%";
        string key = NameKeys.Of(text) ?? text;
        _variant = SearchKeys.Variant(text) ?? key;
        _keys = string.Equals(key, _variant, StringComparison.Ordinal) ? [key] : [key, _variant];
    }

    /// <summary>
    /// The search text as given (already normalized by the caller).
    /// </summary>
    public string Text { get; }

    public static LibrarySearch Of(string text)
    {
        return new LibrarySearch(text);
    }

    /// <summary>
    /// Whether a name with its search key matches the text, containing it or similar to it.
    /// </summary>
    public Expression<Func<T, bool>> Matches<T>(
        Expression<Func<T, string>> name,
        Expression<Func<T, string>> searchKey)
    {
        string text = Text;
        string pattern = _pattern;
        string variant = _variant;
        Expression<Func<string, string, bool>> matches = (value, key) =>
            EF.Functions.ILike(value, pattern, LikePatterns.EscapeCharacter)
            || EF.Functions.TrigramsAreWordSimilar(text, value)
            || EF.Functions.TrigramsAreWordSimilar(variant, key);
        return Bind(Or(matches, KeyContains()), name, searchKey);
    }

    /// <summary>
    /// The relevance of a name with its search key; higher is better (see the ranks of this class).
    /// </summary>
    public Expression<Func<T, double>> Score<T>(
        Expression<Func<T, string>> name,
        Expression<Func<T, string>> searchKey)
    {
        return Bind(TextScore(), name, searchKey);
    }

    /// <summary>
    /// Whether an artist matches by its name or by one of its aliases that an admin did not remove.
    /// </summary>
    public Expression<Func<Artist, bool>> ArtistMatches()
    {
        Expression<Func<Artist, bool>> byName = Matches<Artist>(artist => artist.Name, artist => artist.SearchKey);
        Expression<Func<ArtistAlias, bool>> byAlias = Matches<ArtistAlias>(
            alias => alias.Name,
            alias => alias.SearchKey);
        Expression<Func<Artist, bool>> anyAlias = artist =>
            artist.Aliases.Any(alias => !alias.IsRemoved && Placeholder.Of(alias));
        return byName.OrElse(Inline(anyAlias, byAlias));
    }

    /// <summary>
    /// The relevance of an artist: the rank of its name, otherwise <see cref="AliasRank"/> when an alias contains the
    /// text, plus the best similarity of its name and its aliases.
    /// </summary>
    public Expression<Func<Artist, double>> ArtistScore()
    {
        Expression<Func<string, string, double>> rank = TextRank();
        Expression<Func<string, string, double>> similarity = Similarity();
        Expression<Func<string, string, bool>> aliasContains = Or(
            (value, key) => EF.Functions.ILike(value, _pattern, LikePatterns.EscapeCharacter),
            KeyContains());

        Expression<Func<Artist, double>> score = artist =>
            (Placeholder.Rank(artist.Name, artist.SearchKey) > 0
                ? Placeholder.Rank(artist.Name, artist.SearchKey)
                : artist.Aliases.Any(alias =>
                    !alias.IsRemoved && Placeholder.Contains(alias.Name, alias.SearchKey))
                    ? AliasRank
                    : 0)
            + Math.Max(
                Placeholder.Similarity(artist.Name, artist.SearchKey),
                artist.Aliases
                    .Where(alias => !alias.IsRemoved)
                    .Select(alias => (double?)Placeholder.Similarity(alias.Name, alias.SearchKey))
                    .Max() ?? 0);
        return (Expression<Func<Artist, double>>)new PlaceholderInliner(rank, similarity, aliasContains)
            .Visit(score);
    }

    /// <summary>
    /// The rank of a name with its search key (exact, start, contains or none) plus its similarity.
    /// </summary>
    private Expression<Func<string, string, double>> TextScore()
    {
        Expression<Func<string, string, double>> rank = TextRank();
        Expression<Func<string, string, double>> similarity = Similarity();
        Expression<Func<string, string, double>> score = (value, key) =>
            Placeholder.Rank(value, key) + Placeholder.Similarity(value, key);
        return (Expression<Func<string, string, double>>)new PlaceholderInliner(rank, similarity, null)
            .Visit(score);
    }

    private Expression<Func<string, string, double>> TextRank()
    {
        string exact = _exactPattern;
        string start = _startPattern;
        string pattern = _pattern;
        Expression<Func<string, string, bool>> keyExact = Or(_keys.Select(key => KeyExact(key)));
        Expression<Func<string, string, bool>> keyStart = Or(_keys.Select(key => KeyStart(key)));
        Expression<Func<string, string, double>> rank = (value, key) =>
            EF.Functions.ILike(value, exact, LikePatterns.EscapeCharacter) || Placeholder.Exact(value, key)
                ? ExactRank
                : EF.Functions.ILike(value, start, LikePatterns.EscapeCharacter) || Placeholder.Start(value, key)
                    ? StartRank
                    : EF.Functions.ILike(value, pattern, LikePatterns.EscapeCharacter)
                        || Placeholder.Contains(value, key)
                        ? ContainsRank
                        : 0;
        return (Expression<Func<string, string, double>>)new PlaceholderInliner(
            keyExact: keyExact,
            keyStart: keyStart,
            contains: KeyContains()).Visit(rank);
    }

    private Expression<Func<string, string, double>> Similarity()
    {
        string text = Text;
        string variant = _variant;
        return (value, key) => Math.Max(
            EF.Functions.TrigramsWordSimilarity(text, value),
            EF.Functions.TrigramsWordSimilarity(variant, key));
    }

    /// <summary>
    /// Whether one part of the search key (the normalized name or its symbol variant) contains the normalized text or
    /// its symbol variant.
    /// </summary>
    private Expression<Func<string, string, bool>> KeyContains()
    {
        return Or(_keys.Select(key =>
        {
            string pattern = LikePatterns.Contains(key);
            return (Expression<Func<string, string, bool>>)((value, searchKey) =>
                EF.Functions.Like(searchKey, pattern, LikePatterns.EscapeCharacter));
        }));
    }

    /// <summary>
    /// Whether a part of the search key equals the key; the parts are apart by a space.
    /// </summary>
    private static Expression<Func<string, string, bool>> KeyExact(string key)
    {
        string first = LikePatterns.Escape(key) + " %";
        string last = "% " + LikePatterns.Escape(key);
        return (value, searchKey) =>
            searchKey == key
            || EF.Functions.Like(searchKey, first, LikePatterns.EscapeCharacter)
            || EF.Functions.Like(searchKey, last, LikePatterns.EscapeCharacter);
    }

    /// <summary>
    /// Whether a part of the search key starts with the key.
    /// </summary>
    private static Expression<Func<string, string, bool>> KeyStart(string key)
    {
        string start = LikePatterns.Escape(key) + "%";
        string second = "% " + LikePatterns.Escape(key) + "%";
        return (value, searchKey) =>
            EF.Functions.Like(searchKey, start, LikePatterns.EscapeCharacter)
            || EF.Functions.Like(searchKey, second, LikePatterns.EscapeCharacter);
    }

    private static Expression<Func<string, string, bool>> Or(
        params IEnumerable<Expression<Func<string, string, bool>>> parts)
    {
        Expression<Func<string, string, bool>>[] all = [.. parts];
        ParameterExpression value = Expression.Parameter(typeof(string), "value");
        ParameterExpression key = Expression.Parameter(typeof(string), "key");
        Expression body = all
            .Select(part => Replace(Replace(part.Body, part.Parameters[0], value), part.Parameters[1], key))
            .Aggregate(Expression.OrElse);
        return Expression.Lambda<Func<string, string, bool>>(body, value, key);
    }

    /// <summary>
    /// The best of several weighted scores of an entity, e.g. the title of a track and, a little lower, its artist;
    /// a score of a missing related entity (<c>NULL</c> in the database) is left out.
    /// </summary>
    public static Expression<Func<T, double>> Best<T>(
        Expression<Func<T, double>> first,
        params IEnumerable<(Expression<Func<T, double>> Score, double Weight)> others)
    {
        ParameterExpression entity = first.Parameters[0];
        MethodInfo max = typeof(Math).GetMethod(nameof(Math.Max), [typeof(double), typeof(double)])!;
        Expression body = first.Body;
        foreach ((Expression<Func<T, double>> score, double weight) in others)
        {
            Expression weighted = Expression.Multiply(
                Expression.Constant(weight),
                Replace(score.Body, score.Parameters[0], entity));
            body = Expression.Call(max, body, weighted);
        }

        return Expression.Lambda<Func<T, double>>(body, entity);
    }

    /// <summary>
    /// Applies an expression over an entity to a related entity, e.g. the score of an artist to the album artist of
    /// an album.
    /// </summary>
    public static Expression<Func<T, TResult>> Apply<T, TRelated, TResult>(
        Expression<Func<TRelated, TResult>> expression,
        Expression<Func<T, TRelated>> related)
    {
        Expression body = Replace(expression.Body, expression.Parameters[0], related.Body);
        return Expression.Lambda<Func<T, TResult>>(body, related.Parameters[0]);
    }

    /// <summary>
    /// Applies an expression over a name and a search key to the name and the search key of an entity.
    /// </summary>
    private static Expression<Func<T, TResult>> Bind<T, TResult>(
        Expression<Func<string, string, TResult>> expression,
        Expression<Func<T, string>> name,
        Expression<Func<T, string>> searchKey)
    {
        ParameterExpression entity = name.Parameters[0];
        Expression body = Replace(expression.Body, expression.Parameters[0], name.Body);
        body = Replace(body, expression.Parameters[1], Replace(searchKey.Body, searchKey.Parameters[0], entity));
        return Expression.Lambda<Func<T, TResult>>(body, entity);
    }

    /// <summary>
    /// Puts the predicate of an alias in the place of <see cref="Placeholder.Of"/>.
    /// </summary>
    private static Expression<Func<Artist, bool>> Inline(
        Expression<Func<Artist, bool>> outer,
        Expression<Func<ArtistAlias, bool>> predicate)
    {
        return (Expression<Func<Artist, bool>>)new PlaceholderInliner(aliasPredicate: predicate).Visit(outer);
    }

    private static Expression Replace(Expression tree, Expression original, Expression replacement)
    {
        return ReplacingExpressionVisitor.Replace(original, replacement, tree);
    }

    /// <summary>
    /// Marks in an expression where the parts of the search go; <see cref="PlaceholderInliner"/> replaces every call
    /// before the expression reaches the database, so the methods never run.
    /// </summary>
    private static class Placeholder
    {
        public static bool Of(ArtistAlias alias) => throw new InvalidOperationException();

        public static double Rank(string value, string key) => throw new InvalidOperationException();

        public static double Similarity(string value, string key) => throw new InvalidOperationException();

        public static bool Contains(string value, string key) => throw new InvalidOperationException();

        public static bool Exact(string value, string key) => throw new InvalidOperationException();

        public static bool Start(string value, string key) => throw new InvalidOperationException();
    }

    /// <summary>
    /// Replaces the calls of <see cref="Placeholder"/> by the bodies of the given expressions.
    /// </summary>
    private sealed class PlaceholderInliner(
        Expression<Func<string, string, double>>? rank = null,
        Expression<Func<string, string, double>>? similarity = null,
        Expression<Func<string, string, bool>>? contains = null,
        Expression<Func<string, string, bool>>? keyExact = null,
        Expression<Func<string, string, bool>>? keyStart = null,
        Expression<Func<ArtistAlias, bool>>? aliasPredicate = null) : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.DeclaringType != typeof(Placeholder))
            {
                return base.VisitMethodCall(node);
            }

            LambdaExpression lambda = node.Method.Name switch
            {
                nameof(Placeholder.Of) => (LambdaExpression?)aliasPredicate,
                nameof(Placeholder.Rank) => rank,
                nameof(Placeholder.Similarity) => similarity,
                nameof(Placeholder.Contains) => contains,
                nameof(Placeholder.Exact) => keyExact,
                nameof(Placeholder.Start) => keyStart,
                _ => null,
            } ?? throw new InvalidOperationException($"No expression for {node.Method.Name}.");
            Expression[] arguments = [.. node.Arguments.Select(Visit)!];
            Expression body = lambda.Body;
            for (int index = 0; index < arguments.Length; index++)
            {
                body = Replace(body, lambda.Parameters[index], arguments[index]);
            }

            return Visit(body);
        }
    }
}
