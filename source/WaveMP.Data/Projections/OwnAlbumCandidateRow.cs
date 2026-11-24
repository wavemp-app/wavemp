namespace WaveMP.Data.Projections;

/// <summary>
/// A song that is to be added to an own album.
/// </summary>
/// <param name="TrackId">Id of the song.</param>
/// <param name="HasAudio">Whether it has an audio file, whose tags can be written.</param>
/// <param name="MemberOfOwnAlbumId">The own album the song is a member of, or <c>null</c>.</param>
public sealed record OwnAlbumCandidateRow(Guid TrackId, bool HasAudio, Guid? MemberOfOwnAlbumId);
