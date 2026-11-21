using System.Text.Json.Serialization;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Validations;

namespace WaveMP.Core.Themes.Storage;

/// <summary>
/// An uploaded theme as it is installed in the folder of the themes (<c>/data/themes/{id}</c>, described by its
/// <c>theme.json</c>).
/// </summary>
public sealed record InstalledTheme
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Author { get; init; }

    public required string Version { get; init; }

    /// <summary>
    /// The version of the theme API the theme is written for.
    /// </summary>
    public required int ThemeApi { get; init; }

    /// <summary>
    /// The variants of the theme, the first one is shown where the appearance of a profile has no say.
    /// </summary>
    public required IReadOnlyList<ThemeVariant> Variants { get; init; }

    /// <summary>
    /// Name of the CSS file (see <see cref="ThemeFiles"/>).
    /// </summary>
    public required string Stylesheet { get; init; }

    /// <summary>
    /// Name of the preview picture of each variant.
    /// </summary>
    public required IReadOnlyDictionary<ThemeVariant, string> Previews { get; init; }

    /// <summary>
    /// Names of all files of the theme.
    /// </summary>
    public required IReadOnlyList<string> Files { get; init; }

    /// <summary>
    /// Size of all files of the theme in bytes.
    /// </summary>
    public required long SizeBytes { get; init; }

    /// <summary>
    /// When this version of the theme was installed.
    /// </summary>
    public required DateTimeOffset InstalledAt { get; init; }

    /// <summary>
    /// The path in the uploaded package of each file by its installed name, so problems found later name the file as
    /// the author knows it. Empty for themes installed before WaveMP kept it.
    /// </summary>
    public IReadOnlyDictionary<string, string> Sources { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// What the rules of this version of WaveMP reject in the installed theme, found when the themes are read on
    /// start (see <see cref="InstalledThemeCheck"/>). Empty for a theme that passes them; otherwise the theme is
    /// outdated or blocked (see <see cref="Status"/>) until an admin uploads a new version. Not stored in
    /// <c>theme.json</c>.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ThemeError> Problems { get; init; } = [];

    /// <summary>
    /// Whether the theme passes the rules of this version (see <see cref="Problems"/>): a theme that fails a security
    /// rule is blocked, one that fails only other rules is outdated.
    /// </summary>
    [JsonIgnore]
    public InstalledThemeStatus Status => Problems.Count == 0
        ? InstalledThemeStatus.Current
        : Problems.Any(problem => problem.Code.IsSecurityRule())
            ? InstalledThemeStatus.Blocked
            : InstalledThemeStatus.Outdated;

    /// <summary>
    /// Whether the theme fails a security rule; its files are not served then.
    /// </summary>
    [JsonIgnore]
    public bool IsBlocked => Status == InstalledThemeStatus.Blocked;
}
