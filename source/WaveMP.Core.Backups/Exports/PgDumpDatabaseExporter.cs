using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using WaveMP.Core.Backups.Options;
using WaveMP.Core.Backups.Services;
using WaveMP.Data.Options;

namespace WaveMP.Core.Backups.Exports;

/// <summary>
/// Exports the database with pg_dump as an external process, in its compressed custom format.
/// </summary>
internal sealed class PgDumpDatabaseExporter(
    IOptions<BackupOptions> options,
    IOptions<DatabaseOptions> database) : IDatabaseExporter
{
    /// <summary>
    /// How much of the error output of pg_dump is kept for the log.
    /// </summary>
    internal const int MaxErrorOutputLength = 2000;

    public async Task ExportAsync(string path, CancellationToken cancellationToken = default)
    {
        BackupOptions backup = options.Value;
        DatabaseOptions connection = database.Value;
        ProcessStartInfo startInfo = new(backup.PgDumpPath)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        };
        foreach (string argument in PgDumpArguments.Build(connection, path))
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment[PgDumpArguments.PasswordVariable] = connection.Password;

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception exception) when (exception is Win32Exception or FileNotFoundException)
        {
            throw new BackupFailedException(
                BackupError.PgDumpUnavailable,
                $"pg_dump cannot be started from '{backup.PgDumpPath}': {exception.Message}",
                exception);
        }

        Task<string> errorOutput = process.StandardError.ReadToEndAsync(CancellationToken.None);
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(backup.Timeout);
        try
        {
            await process.WaitForExitAsync(limit.Token);
        }
        catch (OperationCanceledException)
        {
            Kill(process);
            cancellationToken.ThrowIfCancellationRequested();
            throw new BackupFailedException(
                BackupError.ExportFailed,
                $"pg_dump took longer than {backup.Timeout} and was stopped.");
        }

        await standardOutput;
        string error = await errorOutput;
        if (process.ExitCode != 0)
        {
            throw new BackupFailedException(
                BackupError.ExportFailed,
                $"pg_dump ended with exit code {process.ExitCode}: {Shorten(error)}");
        }
    }

    internal static string Shorten(string errorOutput)
    {
        string text = errorOutput.Trim();
        return text.Length <= MaxErrorOutputLength ? text : text[..MaxErrorOutputLength];
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // The process has already ended.
        }
    }
}
