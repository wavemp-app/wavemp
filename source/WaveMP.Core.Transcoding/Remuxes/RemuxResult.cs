using System.Diagnostics.CodeAnalysis;

namespace WaveMP.Core.Transcoding.Remuxes;

/// <summary>
/// The remuxed video of a track, or why there is none.
/// </summary>
/// <param name="Status">Whether the file is ready.</param>
/// <param name="Stream">The file, opened for reading; set if ready. The receiver disposes it.</param>
/// <param name="ETag">Strong entity tag (without quotes) of the remuxed file; set if ready.</param>
/// <param name="LastModified">Time of the last change of the source file; set if ready.</param>
public sealed record RemuxResult(RemuxStatus Status, Stream? Stream, string? ETag, DateTimeOffset? LastModified)
{
    [MemberNotNullWhen(true, nameof(Stream), nameof(ETag), nameof(LastModified))]
    public bool IsReady => Status == RemuxStatus.Ready && Stream is not null && ETag is not null
        && LastModified is not null;

    public static RemuxResult Of(RemuxStatus status)
    {
        return new RemuxResult(status, null, null, null);
    }
}
