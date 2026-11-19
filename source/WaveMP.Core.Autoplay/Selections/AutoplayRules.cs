namespace WaveMP.Core.Autoplay.Selections;

/// <summary>
/// Rules of the choice by artists: how long played songs rest, how much favorites and ratings weigh and how many
/// tracks are considered.
/// </summary>
public static class AutoplayRules
{
    /// <summary>
    /// Songs the user played within this time are chosen only when no other matching song is left.
    /// </summary>
    public static readonly TimeSpan RecentlyPlayedWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Weight of a song without favorite and rating (or with 2 or 3 stars).
    /// </summary>
    public const double BaseWeight = 1.0;

    /// <summary>
    /// Factor of a favorite song.
    /// </summary>
    public const double FavoriteFactor = 1.5;

    /// <summary>
    /// Factor of a song with 4 stars.
    /// </summary>
    public const double FourStarsFactor = 1.25;

    /// <summary>
    /// Factor of a song with 5 stars.
    /// </summary>
    public const double FiveStarsFactor = 1.5;

    /// <summary>
    /// Songs with this rating (or less) are never chosen.
    /// </summary>
    public const int NeverRating = 1;

    /// <summary>
    /// Most songs of the artists of a list that are considered; a random selection if they have more.
    /// </summary>
    public const int MaxArtistCandidates = 2000;

    /// <summary>
    /// Most songs of the same genres and similar decades that are considered; a random selection if there are more.
    /// </summary>
    public const int MaxFallbackCandidates = 500;

    /// <summary>
    /// Years a decade of the list reaches to each side: songs of the decade before and after are similar.
    /// </summary>
    public const int DecadeReach = 10;

    /// <summary>
    /// The weight of a song for the user: favorites and well-rated songs come a bit more often, songs with 1 star never
    /// (0).
    /// </summary>
    public static double WeightOf(bool isFavorite, int rating)
    {
        if (rating is > 0 and <= NeverRating)
        {
            return 0;
        }

        double weight = BaseWeight;
        if (isFavorite)
        {
            weight *= FavoriteFactor;
        }

        return rating switch
        {
            >= 5 => weight * FiveStarsFactor,
            4 => weight * FourStarsFactor,
            _ => weight,
        };
    }

    /// <summary>
    /// The first year of the decade of a year, e.g. 1990 for 1997.
    /// </summary>
    public static int DecadeOf(int year)
    {
        return (int)Math.Floor(year / 10.0) * 10;
    }

    /// <summary>
    /// The decades similar to the years of a list: the decade of each year and the decades next to it.
    /// </summary>
    public static IReadOnlySet<int> SimilarDecades(IEnumerable<int> years)
    {
        HashSet<int> decades = [];
        foreach (int decade in years.Select(DecadeOf))
        {
            for (int offset = -DecadeReach; offset <= DecadeReach; offset += 10)
            {
                decades.Add(decade + offset);
            }
        }

        return decades;
    }
}
