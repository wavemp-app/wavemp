namespace WaveMP.Core.Subsonic.Requests;

/// <summary>
/// A cover id of the Subsonic API.
/// </summary>
/// <param name="Owner">What the cover belongs to.</param>
/// <param name="Id">Id of the owner.</param>
public sealed record SubsonicCoverId(SubsonicCoverOwner Owner, Guid Id);
