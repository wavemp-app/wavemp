using WaveMP.Core.Metadata.Sources;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;

namespace WaveMP.Core.Subsonic.Browsing;

/// <summary>
/// The music folders of the Subsonic API: one per active media source, with the name of the source and an id derived
/// from the id of the source (<see cref="SubsonicIds.MusicFolder"/>). The parameter <c>musicFolderId</c> narrows the
/// lists to the tracks of one source.
/// </summary>
internal sealed class SubsonicMusicFolders(IMediaSourceCatalog catalog)
{
    public const string Parameter = "musicFolderId";

    /// <summary>
    /// The music folders, sorted by name. Deactivated sources are left out; their tracks stay in the lists without
    /// <c>musicFolderId</c>.
    /// </summary>
    public IReadOnlyList<SubsonicElement> List()
    {
        return [.. catalog.Sources
            .Where(source => source.IsActive)
            .Select(source => new SubsonicElement("musicFolder")
                .Set("id", SubsonicIds.MusicFolder(source.Id))
                .Set("name", source.Name))];
    }

    /// <summary>
    /// The filter the request asks for with <c>musicFolderId</c>: no source without the parameter (or with an empty
    /// one), otherwise the source of the folder, also a deactivated one; <c>null</c> if no source has a folder with
    /// this id.
    /// </summary>
    public SubsonicMusicFolderFilter? Resolve(SubsonicParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.Get(Parameter)))
        {
            return SubsonicMusicFolderFilter.All;
        }

        if (parameters.GetInt(Parameter) is not { } id)
        {
            return null;
        }

        return catalog.Sources.FirstOrDefault(source => SubsonicIds.MusicFolder(source.Id) == id) is { } match
            ? new SubsonicMusicFolderFilter(match.Id)
            : null;
    }
}

/// <summary>
/// The media source a list of the Subsonic API is narrowed to.
/// </summary>
/// <param name="SourceId">Only tracks of this source; <c>null</c> for the whole library.</param>
internal sealed record SubsonicMusicFolderFilter(Guid? SourceId)
{
    public static SubsonicMusicFolderFilter All { get; } = new((Guid?)null);
}
