using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Storage;

/// <summary>
/// Keeps the installed themes in <see cref="ThemeOptions.RootPath"/>: <c>{id}/theme.json</c> and the files in
/// <c>{id}/files/</c>. A theme is first written completely into a staging folder and then moved into place, so a
/// theme is never half installed; a replaced theme is moved aside and removed afterwards. Remains of an interrupted
/// installation are removed on start, and the installed themes are checked again with the rules of this version
/// (<see cref="InstalledThemeCheck"/>). Installations run one after another.
/// </summary>
internal sealed class ThemeStore(IOptions<ThemeOptions> options, TimeProvider time, ILogger<ThemeStore> logger)
    : IThemeStore, IDisposable
{
    internal const string DescriptionFileName = "theme.json";
    internal const string FilesFolderName = "files";
    private const string StagingPrefix = ".staging-";
    private const string TrashPrefix = ".trash-";

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile bool _isAvailable;
    private ImmutableDictionary<string, InstalledTheme> _themes = [];

    public bool IsAvailable => _isAvailable;

    private string RootPath => options.Value.RootPath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(RootPath);
            foreach (string folder in Directory.EnumerateDirectories(RootPath))
            {
                string name = Path.GetFileName(folder);
                if (name.StartsWith(StagingPrefix, StringComparison.Ordinal) ||
                    name.StartsWith(TrashPrefix, StringComparison.Ordinal))
                {
                    TryDeleteFolder(folder);
                }
            }

            _isAvailable = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _isAvailable = false;
            logger.LogWarning(
                exception,
                "The folder {Folder} of the themes cannot be written; themes cannot be installed",
                RootPath);
        }

        _themes = await LoadAsync(cancellationToken);
    }

    public IReadOnlyList<InstalledTheme> List()
    {
        return [.. _themes.Values
            .OrderBy(theme => theme.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(theme => theme.Id, StringComparer.Ordinal)];
    }

    public InstalledTheme? Find(string id)
    {
        return _themes.GetValueOrDefault(id);
    }

    public async Task<ThemeStoreResult> SaveAsync(
        ValidatedTheme theme,
        bool replace,
        CancellationToken cancellationToken = default)
    {
        string id = theme.Manifest.Id;
        if (!ThemeIdRules.IsValid(id))
        {
            throw new ArgumentException("The theme has no valid id.", nameof(theme));
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            bool exists = _themes.ContainsKey(id);
            if (exists && !replace)
            {
                return new ThemeStoreResult(ThemeStoreStatus.AlreadyInstalled);
            }

            if (!exists && replace)
            {
                return new ThemeStoreResult(ThemeStoreStatus.NotFound);
            }

            if (!_isAvailable)
            {
                return new ThemeStoreResult(ThemeStoreStatus.Unavailable);
            }

            InstalledTheme installed = Describe(theme);
            string staging = Path.Combine(RootPath, StagingPrefix + Guid.NewGuid().ToString("N"));
            try
            {
                await WriteAsync(staging, theme, installed, cancellationToken);
                MoveIntoPlace(staging, FolderOf(id));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "Cannot install the theme {ThemeId}", id);
                TryDeleteFolder(staging);
                return new ThemeStoreResult(ThemeStoreStatus.Unavailable);
            }

            _themes = _themes.SetItem(id, installed);
            return new ThemeStoreResult(ThemeStoreStatus.Saved, installed);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!_themes.ContainsKey(id))
            {
                return false;
            }

            string folder = FolderOf(id);
            string trash = Path.Combine(RootPath, TrashPrefix + Guid.NewGuid().ToString("N"));
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Move(folder, trash);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Without its description the theme is gone after the next start anyway.
                logger.LogWarning(exception, "Cannot remove the folder of the theme {ThemeId}", id);
                TryDeleteFile(Path.Combine(folder, DescriptionFileName));
            }

            _themes = _themes.Remove(id);
            TryDeleteFolder(trash);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Stream? OpenFile(string id, string name)
    {
        if (!ThemeFiles.IsValidName(name) || Find(id) is not { } theme || !theme.Files.Contains(name))
        {
            return null;
        }

        try
        {
            // Delete: a replacement may remove the file while it is delivered.
            return new FileStream(
                Path.Combine(FolderOf(id), FilesFolderName, name),
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                bufferSize: 1,
                useAsync: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
    }

    private InstalledTheme Describe(ValidatedTheme theme)
    {
        return new InstalledTheme
        {
            Id = theme.Manifest.Id,
            Name = theme.Manifest.Name,
            Author = theme.Manifest.Author,
            Version = theme.Manifest.Version,
            ThemeApi = theme.Manifest.ThemeApi,
            Variants = theme.Manifest.Variants,
            Stylesheet = theme.Stylesheet.Name,
            Previews = theme.Previews,
            Files = [.. theme.Files.Select(file => file.Name)],
            SizeBytes = theme.Files.Sum(file => (long)file.Content.Length),
            InstalledAt = time.GetUtcNow(),
            Sources = theme.Sources,
        };
    }

    private static async Task WriteAsync(
        string folder,
        ValidatedTheme theme,
        InstalledTheme installed,
        CancellationToken cancellationToken)
    {
        string files = Path.Combine(folder, FilesFolderName);
        Directory.CreateDirectory(files);
        foreach (ValidatedThemeFile file in theme.Files)
        {
            await File.WriteAllBytesAsync(Path.Combine(files, file.Name), file.Content, cancellationToken);
        }

        await File.WriteAllBytesAsync(
            Path.Combine(folder, DescriptionFileName),
            JsonSerializer.SerializeToUtf8Bytes(installed, _json),
            cancellationToken);
    }

    /// <summary>
    /// Moves the staging folder to the folder of the theme; an installed version is moved aside first and removed
    /// afterwards.
    /// </summary>
    private void MoveIntoPlace(string staging, string target)
    {
        string? trash = null;
        if (Directory.Exists(target))
        {
            trash = Path.Combine(RootPath, TrashPrefix + Guid.NewGuid().ToString("N"));
            Directory.Move(target, trash);
        }

        try
        {
            Directory.Move(staging, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException && trash is not null)
        {
            // Put the installed version back.
            Directory.Move(trash, target);
            throw;
        }

        if (trash is not null)
        {
            TryDeleteFolder(trash);
        }
    }

    private async Task<ImmutableDictionary<string, InstalledTheme>> LoadAsync(CancellationToken cancellationToken)
    {
        ImmutableDictionary<string, InstalledTheme>.Builder themes =
            ImmutableDictionary.CreateBuilder<string, InstalledTheme>(StringComparer.Ordinal);
        if (!Directory.Exists(RootPath))
        {
            return themes.ToImmutable();
        }

        foreach (string folder in Directory.EnumerateDirectories(RootPath))
        {
            string id = Path.GetFileName(folder);
            if (!ThemeIdRules.IsValid(id))
            {
                continue;
            }

            InstalledTheme? theme = await ReadDescriptionAsync(folder, cancellationToken);
            if (theme is null || theme.Id != id || !IsComplete(theme))
            {
                logger.LogWarning("The theme in {Folder} is incomplete and is not offered", folder);
                continue;
            }

            if (BuiltInThemes.Ids.Contains(id))
            {
                logger.LogWarning(
                    "The uploaded theme {ThemeId} has the id of a theme shipped with WaveMP and is not offered",
                    id);
                continue;
            }

            themes[id] = await CheckAsync(theme, cancellationToken);
        }

        return themes.ToImmutable();
    }

    /// <summary>
    /// Checks an installed theme again with the rules of this version (see <see cref="InstalledThemeCheck"/>). An
    /// outdated theme is still offered; the admins see its problems and can upload a new version.
    /// </summary>
    private async Task<InstalledTheme> CheckAsync(InstalledTheme theme, CancellationToken cancellationToken)
    {
        string folder = Path.Combine(FolderOf(theme.Id), FilesFolderName);
        Dictionary<string, byte[]> files = new(StringComparer.Ordinal);
        try
        {
            foreach (string name in theme.Files)
            {
                files[name] = await File.ReadAllBytesAsync(Path.Combine(folder, name), cancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Served as it is; a file that cannot be read now is answered with 404 then.
            logger.LogWarning(exception, "Cannot read the files of the theme {ThemeId} to check them", theme.Id);
            return theme;
        }

        IReadOnlyList<ThemeError> problems = InstalledThemeCheck.Run(theme, files);
        if (problems.Count == 0)
        {
            return theme;
        }

        logger.LogWarning(
            "The theme {ThemeId} {Version} does not pass the checks of this version of WaveMP ({Problems}); it is " +
            "offered as it is until an admin uploads a new version",
            theme.Id,
            theme.Version,
            string.Join(", ", problems.Select(problem => problem.Code).Distinct()));
        return theme with { Problems = problems };
    }

    private async Task<InstalledTheme?> ReadDescriptionAsync(string folder, CancellationToken cancellationToken)
    {
        try
        {
            await using FileStream stream = File.OpenRead(Path.Combine(folder, DescriptionFileName));
            return await JsonSerializer.DeserializeAsync<InstalledTheme>(stream, _json, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogDebug(exception, "Cannot read the description of the theme in {Folder}", folder);
            return null;
        }
    }

    private bool IsComplete(InstalledTheme theme)
    {
        string files = Path.Combine(FolderOf(theme.Id), FilesFolderName);
        return theme.Variants.Count > 0 &&
            theme.Files.All(name => ThemeFiles.IsValidName(name) && File.Exists(Path.Combine(files, name))) &&
            theme.Files.Contains(theme.Stylesheet) &&
            theme.Variants.All(variant => theme.Previews.TryGetValue(variant, out string? preview) &&
                theme.Files.Contains(preview));
    }

    private string FolderOf(string id)
    {
        return Path.Combine(RootPath, id);
    }

    private void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Removed on the next start.
            logger.LogDebug(exception, "Cannot remove the folder {Folder}", folder);
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(exception, "Cannot remove the file {Path}", path);
        }
    }
}
