namespace WaveMP.Core.Settings.Groups;

/// <summary>
/// Changes the settings of the online search for album info. Every <c>null</c> value keeps the current value.
/// </summary>
public sealed record UpdateOnlineMetadataSettingsCommand(
    bool? Enabled = null,
    string? MusicBrainzUrl = null,
    string? CoverArtArchiveUrl = null,
    int? MusicBrainzRequestIntervalMilliseconds = null);
