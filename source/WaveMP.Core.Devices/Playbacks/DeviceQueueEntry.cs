using WaveMP.Data.Projections;

namespace WaveMP.Core.Devices.Playbacks;

/// <summary>
/// An entry of the live queue.
/// </summary>
/// <param name="Position">Its position in the live queue; commands name entries by it.</param>
/// <param name="TrackId">The song of the entry.</param>
/// <param name="Autoplay">Whether autoplay added the entry after the list had ended.</param>
/// <param name="Track">The song with the favorite and rating of the profile; <c>null</c> if it left the library.
/// </param>
public sealed record DeviceQueueEntry(int Position, Guid TrackId, bool Autoplay, TrackRow? Track);
