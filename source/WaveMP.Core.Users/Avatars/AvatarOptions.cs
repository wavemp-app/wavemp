using WaveMP.Core.Settings.Paths;

namespace WaveMP.Core.Users.Avatars;

/// <summary>
/// Settings of the avatars (configuration section <c>Avatars</c>, e.g. <c>Avatars__MaxUploadBytes</c>). Uploaded
/// avatars lie in the folder <c>avatars</c> of the data folder (see <see cref="DataPaths"/>).
/// </summary>
public sealed class AvatarOptions
{
    public const string SectionName = "Avatars";
    public const string FolderName = "avatars";

    /// <summary>
    /// Absolute path of the folder of the uploaded avatars, set from the data folder.
    /// </summary>
    public string RootPath { get; set; } = DataPaths.DefaultDataPath + "/" + FolderName;

    /// <summary>
    /// Largest uploaded picture in bytes.
    /// </summary>
    public long MaxUploadBytes { get; set; } = 10 * 1024 * 1024;
}
