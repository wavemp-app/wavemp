namespace WaveMP.Core.Subsonic.Responses;

/// <summary>
/// The format of the answers, chosen by the parameter <c>f</c>.
/// </summary>
public enum SubsonicFormat
{
    Xml,
    Json,

    /// <summary>
    /// JSON wrapped in a call of the function named by the parameter <c>callback</c>.
    /// </summary>
    JsonP,
}
