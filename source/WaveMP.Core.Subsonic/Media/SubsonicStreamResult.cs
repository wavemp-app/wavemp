using System.Diagnostics.CodeAnalysis;
using WaveMP.Core.Streaming.Sources;
using WaveMP.Core.Subsonic.Responses;

namespace WaveMP.Core.Subsonic.Media;

/// <summary>
/// The file a request of <c>stream</c> or <c>download</c> delivers and how, or the error code why there is none.
/// </summary>
/// <param name="Error">The error; <c>null</c> if the file can be delivered.</param>
/// <param name="Source">The file, as found on the disk right now.</param>
/// <param name="Plan">Directly or transcoded; <c>download</c> always delivers directly.</param>
public sealed record SubsonicStreamResult(SubsonicErrorCode? Error, StreamSource? Source, SubsonicStreamPlan Plan)
{
    [MemberNotNullWhen(true, nameof(Source))]
    public bool IsFound => Error is null && Source is not null;

    public static SubsonicStreamResult Failed(SubsonicErrorCode error)
    {
        return new SubsonicStreamResult(error, null, SubsonicStreamPlan.Direct);
    }
}
