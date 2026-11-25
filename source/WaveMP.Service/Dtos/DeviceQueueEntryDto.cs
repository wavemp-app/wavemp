namespace WaveMP.Service.Dtos;

/// <summary>
/// An entry of the live queue.
/// </summary>
public sealed class DeviceQueueEntryDto
{
    /// <summary>
    /// Its position in the live queue; commands name entries by it.
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// Id of the song.
    /// </summary>
    public required Guid TrackId { get; init; }

    /// <summary>
    /// Whether autoplay added the entry after the list had ended.
    /// </summary>
    public required bool Autoplay { get; init; }

    /// <summary>
    /// The song with the favorite and rating of the profile; null if it left the library.
    /// </summary>
    public required TrackDto? Track { get; init; }
}
