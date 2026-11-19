using System.Diagnostics.CodeAnalysis;

namespace WaveMP.Core.Scanning.Sources;

/// <summary>
/// Result of creating or changing a media source: the saved source or the reason why nothing was saved.
/// </summary>
public sealed record MediaSourceResult(MediaSourceOverview? Source, MediaSourceError? Error)
{
    [MemberNotNullWhen(true, nameof(Source))]
    public bool IsSuccess => Source is not null;

    public static MediaSourceResult Success(MediaSourceOverview source)
    {
        return new MediaSourceResult(source, null);
    }

    public static MediaSourceResult Failure(MediaSourceError error)
    {
        return new MediaSourceResult(null, error);
    }
}
