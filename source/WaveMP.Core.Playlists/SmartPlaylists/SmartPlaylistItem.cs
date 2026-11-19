using WaveMP.Core.Playlists.Covers;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// A smart playlist with what it holds right now and its cover.
/// </summary>
/// <param name="Playlist">The smart playlist with its rules.</param>
/// <param name="Summary">Number and total duration of its tracks right now.</param>
/// <param name="Cover">The covers of up to four of its first tracks, from different albums, as for playlists without an
/// own cover.</param>
public sealed record SmartPlaylistItem(SmartPlaylist Playlist, SmartPlaylistSummary Summary, PlaylistCover Cover);
