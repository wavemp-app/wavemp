using System.Globalization;
using System.Text.RegularExpressions;

namespace WaveMP.Core.Backups.Files;

/// <summary>
/// File names of the exports: <c>wavemp-&lt;UTC time&gt;-&lt;trigger&gt;.dump</c>, e.g.
/// <c>wavemp-20261116T030000Z-schedule.dump</c>, with a number before the extension if the second is already taken
/// (<c>...-manual-2.dump</c>). The time and the trigger are read back from the name, so the folder needs no index and
/// a restore of the database never disagrees with it. Other files in the folder are ignored.
/// </summary>
public static partial class BackupFileNames
{
    public const string Extension = ".dump";

    /// <summary>
    /// Ending of a file that pg_dump is still writing.
    /// </summary>
    public const string PartialExtension = ".partial";

    private const string Prefix = "wavemp-";
    private const string TimeFormat = "yyyyMMdd'T'HHmmss'Z'";

    /// <summary>
    /// The id (file name without extension) of an export started at <paramref name="createdAt"/>; a
    /// <paramref name="sequence"/> above 1 is added to tell exports of the same second apart.
    /// </summary>
    public static string CreateId(DateTimeOffset createdAt, BackupTrigger trigger, int sequence = 1)
    {
        string time = createdAt.ToUniversalTime().ToString(TimeFormat, CultureInfo.InvariantCulture);
        string id = $"{Prefix}{time}-{TriggerName(trigger)}";
        return sequence > 1 ? string.Create(CultureInfo.InvariantCulture, $"{id}-{sequence}") : id;
    }

    /// <summary>
    /// Reads time and trigger from the id of an export. Returns <c>false</c> for every other text, also for paths, so
    /// an id from a request can never name a file outside the folder.
    /// </summary>
    public static bool TryParseId(
        string? id,
        out DateTimeOffset createdAt,
        out BackupTrigger trigger,
        out int sequence)
    {
        createdAt = default;
        trigger = default;
        sequence = 1;
        if (id is null || IdPattern().Match(id) is not { Success: true } match)
        {
            return false;
        }

        if (!DateTimeOffset.TryParseExact(
                match.Groups["time"].Value,
                TimeFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out createdAt)
            || !TryParseTrigger(match.Groups["trigger"].Value, out trigger))
        {
            return false;
        }

        if (match.Groups["sequence"].Success)
        {
            sequence = int.Parse(match.Groups["sequence"].Value, CultureInfo.InvariantCulture);
        }

        return true;
    }

    /// <summary>
    /// The id of an export file name, or <c>null</c> if the name is not one of an export.
    /// </summary>
    public static string? IdOfFileName(string fileName)
    {
        if (!fileName.EndsWith(Extension, StringComparison.Ordinal))
        {
            return null;
        }

        string id = fileName[..^Extension.Length];
        return TryParseId(id, out _, out _, out _) ? id : null;
    }

    private static string TriggerName(BackupTrigger trigger)
    {
        return trigger switch
        {
            BackupTrigger.Schedule => "schedule",
            BackupTrigger.Manual => "manual",
            BackupTrigger.Migration => "migration",
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, "Unknown trigger."),
        };
    }

    private static bool TryParseTrigger(string name, out BackupTrigger trigger)
    {
        BackupTrigger? parsed = name switch
        {
            "schedule" => BackupTrigger.Schedule,
            "manual" => BackupTrigger.Manual,
            "migration" => BackupTrigger.Migration,
            _ => null,
        };
        trigger = parsed.GetValueOrDefault();
        return parsed.HasValue;
    }

    [GeneratedRegex(
        "^wavemp-(?<time>[0-9]{8}T[0-9]{6}Z)-(?<trigger>schedule|manual|migration)(?:-(?<sequence>[2-9]|[1-9][0-9]{1,2}))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
