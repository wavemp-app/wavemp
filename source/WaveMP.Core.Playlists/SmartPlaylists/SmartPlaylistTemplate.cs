using WaveMP.Data.Repositories;

namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// A built-in template: the rules, sort order and limit a new smart playlist can start with.
/// </summary>
/// <param name="Id">Id of the template.</param>
/// <param name="Criteria">Rules, sort order and limit of the template.</param>
public sealed record SmartPlaylistTemplate(SmartPlaylistTemplateId Id, SmartPlaylistCriteria Criteria);
