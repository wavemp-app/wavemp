using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.Library.Options;

/// <summary>
/// Where the covers from the online search lie that could not be written into the media folder: the folder
/// <c>online-covers</c> of the data folder (see <see cref="DataPaths"/>). Unlike the cache, the folder holds covers the
/// admin chose and belongs into backups.
/// </summary>
public sealed class OnlineCoverOptions
{
    public const string FolderName = "online-covers";

    /// <summary>
    /// Absolute path of the folder, set from the data folder.
    /// </summary>
    public string RootPath { get; set; } = DataPaths.DefaultDataPath + "/" + FolderName;
}
