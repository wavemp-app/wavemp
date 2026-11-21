namespace WaveMP.Core.Themes.Storage;

/// <summary>
/// Whether an installed theme passes the rules of this version of WaveMP, which are applied again on every start.
/// </summary>
public enum InstalledThemeStatus
{
    /// <summary>
    /// The theme passes the rules.
    /// </summary>
    Current,

    /// <summary>
    /// The theme fails rules that are no security rules, e.g. it is written for an older theme API version. It is
    /// still served as it is until an admin uploads a new version.
    /// </summary>
    Outdated,

    /// <summary>
    /// The theme fails at least one security rule (see
    /// <see cref="Validations.ThemeErrorCodeExtensions.IsSecurityRule"/>). Its files are no longer served, and the
    /// profiles that chose it see the default theme until an admin uploads a new version.
    /// </summary>
    Blocked,
}
