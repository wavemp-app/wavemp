using WaveMP.Core.Subsonic.Requests;

namespace WaveMP.Core.Subsonic.Tests;

internal static class TestParameters
{
    /// <summary>
    /// Parameters of a request; names may repeat.
    /// </summary>
    public static SubsonicParameters Of(params (string Name, string Value)[] values)
    {
        return new SubsonicParameters(values
            .GroupBy(value => value.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(value => value.Value)],
                StringComparer.Ordinal));
    }
}
