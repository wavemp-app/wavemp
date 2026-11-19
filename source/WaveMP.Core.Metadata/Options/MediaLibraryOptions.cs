namespace WaveMP.Core.Metadata.Options;

/// <summary>
/// The folder of the first media source as long as the admin set no path of its own, read from the environment
/// variable <c>MEDIA_PATH</c> (default <c>/media</c>, the mount point of the container). Relative paths are resolved
/// against the content root.
/// </summary>
public sealed class MediaLibraryOptions
{
    public const string PathVariable = "MEDIA_PATH";
    public const string DefaultPath = "/media";

    /// <summary>
    /// Absolute path of the media folder.
    /// </summary>
    public string RootPath { get; set; } = DefaultPath;
}
