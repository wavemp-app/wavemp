using System.Globalization;
using WaveMP.Data.Options;

namespace WaveMP.Core.Backups.Exports;

/// <summary>
/// The command line of pg_dump for an export of the WaveMP database.
/// </summary>
public static class PgDumpArguments
{
    /// <summary>
    /// Environment variable pg_dump reads the password from, so it never appears on the command line.
    /// </summary>
    public const string PasswordVariable = "PGPASSWORD";

    /// <summary>
    /// The arguments: the custom format of pg_dump, which is compressed and restored with pg_restore, the connection
    /// of <paramref name="database"/> without a password prompt and the target file. The password is passed in
    /// <see cref="PasswordVariable"/>.
    /// </summary>
    public static IReadOnlyList<string> Build(DatabaseOptions database, string path)
    {
        return
        [
            "--format=custom",
            "--no-password",
            "--host=" + database.Host,
            "--port=" + database.Port.ToString(CultureInfo.InvariantCulture),
            "--username=" + database.User,
            "--dbname=" + database.Name,
            "--file=" + path,
        ];
    }
}
