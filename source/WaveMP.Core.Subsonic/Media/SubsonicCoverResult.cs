using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Subsonic.Responses;

namespace WaveMP.Core.Subsonic.Media;

/// <summary>
/// The picture <c>getCoverArt</c> delivers, or the error code why there is none. The receiver disposes the content
/// of the picture.
/// </summary>
public sealed record SubsonicCoverResult(SubsonicErrorCode? Error, CoverDelivery? Delivery);
