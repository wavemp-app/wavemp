namespace WaveMP.Core.Scanning.Sources;

/// <summary>
/// Checks and compares the folders of media sources.
/// </summary>
public static class MediaSourcePaths
{
    private static readonly StringComparison _comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// The absolute path in its normal form (no <c>.</c> or <c>..</c>, no separator at the end); <c>false</c> for
    /// empty and relative paths.
    /// </summary>
    public static bool TryNormalize(string? value, out string path)
    {
        path = string.Empty;
        string trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Contains('\0', StringComparison.Ordinal)
            || !Path.IsPathFullyQualified(trimmed))
        {
            return false;
        }

        try
        {
            path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trimmed));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException
            or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether two absolute folders are the same, or one lies in the other; a separator at the end does not count.
    /// </summary>
    public static bool Overlap(string first, string second)
    {
        first = Path.TrimEndingDirectorySeparator(first);
        second = Path.TrimEndingDirectorySeparator(second);
        return string.Equals(first, second, _comparison) || IsInside(first, second) || IsInside(second, first);
    }

    private static bool IsInside(string path, string folder)
    {
        string prefix = Path.EndsInDirectorySeparator(folder) ? folder : folder + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, _comparison);
    }
}
