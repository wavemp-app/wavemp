using WaveMP.Core.Settings.Paths;

namespace WaveMP.Service.Keys;

/// <summary>
/// The keys of ASP.NET Core Data Protection. They lie in the folder <c>keys</c> of the data folder (<c>DATA_PATH</c>,
/// default <c>/data</c>), so they survive restarts and updates of the container and do not depend on a home folder of
/// the user that runs WaveMP. The key files are stored unencrypted: the folder is only accessible to this user, and
/// the data folder belongs into the backup of the user anyway.
/// </summary>
public static class DataProtectionKeys
{
    /// <summary>
    /// Name of the folder of the keys in the data folder.
    /// </summary>
    public const string FolderName = "keys";

    /// <summary>
    /// Name of the application for Data Protection. Protected data stays readable after a change of the content root
    /// (another folder of the published service), which the default name would otherwise depend on.
    /// </summary>
    public const string ApplicationName = "WaveMP";

    // Read, write and list for the user that runs WaveMP only (700): the keys are stored unencrypted.
    private const UnixFileMode FolderMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <summary>
    /// The absolute path of the folder of the keys.
    /// </summary>
    public static string ResolveFolder(IConfiguration configuration, IHostEnvironment? environment = null)
    {
        return DataPaths.Resolve(configuration, environment, FolderName);
    }

    /// <summary>
    /// Creates the folder of the keys, accessible to the current user only, if it does not exist yet; an existing
    /// folder keeps its permissions. A folder that cannot be created does not stop the start: Data Protection then
    /// logs the error when it stores a key, and the reason is logged here as well.
    /// </summary>
    /// <returns><c>true</c> if the folder exists afterwards.</returns>
    public static bool PrepareFolder(string folder, ILogger logger)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(folder);
            }
            else
            {
                Directory.CreateDirectory(folder, FolderMode);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                exception,
                "The folder {KeyFolder} of the Data Protection keys cannot be created; "
                + "check that the data folder can be written",
                folder);
            return false;
        }
    }
}
