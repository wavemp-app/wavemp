namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// Why a field of a smart playlist was rejected.
/// </summary>
/// <param name="Field">Path of the field as in the request, e.g. <c>name</c>, <c>limit</c> or <c>rules[2].number</c>.
/// </param>
/// <param name="Code">The reason.</param>
/// <param name="Min">Smallest allowed value of <see cref="SmartPlaylistErrorCode.OutOfRange"/>.</param>
/// <param name="Max">Largest allowed value, length or number of items.</param>
public sealed record SmartPlaylistError(string Field, SmartPlaylistErrorCode Code, int? Min = null, int? Max = null);
