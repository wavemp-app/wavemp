using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Whether an uploaded theme passes the checks of this version of the server. Installed themes are checked again on
/// every start, because a newer version may reject more in theme packages or no longer support an old theme API
/// version.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ThemeStatusDto>))]
public enum ThemeStatusDto
{
    /// <summary>
    /// The theme passes the checks.
    /// </summary>
    Current,

    /// <summary>
    /// The theme no longer passes the checks, but breaks no security rule (e.g. it is written for an older theme API
    /// version); <see cref="ThemeDto.Problems"/> says why. It is still offered as it is, an admin should upload a new
    /// version.
    /// </summary>
    Outdated,

    /// <summary>
    /// The theme breaks a security rule of this version, an unsafe construct in its CSS or SVG files:
    /// <c>css.invalid_encoding</c>, <c>css.syntax_error</c>, <c>css.import</c>, <c>css.at_rule_not_allowed</c>,
    /// <c>css.external_url</c>, <c>css.absolute_url</c>, <c>css.invalid_url</c>, <c>svg.invalid</c>,
    /// <c>svg.entity</c>, <c>svg.script</c>, <c>svg.event_handler</c>, <c>svg.foreign_object</c>,
    /// <c>svg.external_reference</c> or <c>svg.not_cleaned</c> (<see cref="ThemeDto.Problems"/> lists all problems).
    /// Its files are no longer served (<c>404</c>); clients neither offer nor apply it, and profiles that chose it see
    /// the default theme until an admin uploads a new version.
    /// </summary>
    Blocked,
}
