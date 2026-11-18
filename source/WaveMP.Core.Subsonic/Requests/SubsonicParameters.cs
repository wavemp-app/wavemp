using System.Globalization;

namespace WaveMP.Core.Subsonic.Requests;

/// <summary>
/// The parameters of a request to the Subsonic API, from the query and (OpenSubsonic <c>formPost</c>) from a form
/// body. Names are case-sensitive like in the specification; a parameter may repeat (e.g. <c>id</c> of
/// <c>star</c>).
/// </summary>
public sealed class SubsonicParameters(IReadOnlyDictionary<string, IReadOnlyList<string>> values)
{
    public static SubsonicParameters Empty { get; } = new(new Dictionary<string, IReadOnlyList<string>>());

    /// <summary>
    /// The first value of the parameter; <c>null</c> if it is missing.
    /// </summary>
    public string? Get(string name)
    {
        return values.TryGetValue(name, out IReadOnlyList<string>? list) && list.Count > 0 ? list[0] : null;
    }

    /// <summary>
    /// All values of the parameter in their order; empty if it is missing.
    /// </summary>
    public IReadOnlyList<string> GetAll(string name)
    {
        return values.TryGetValue(name, out IReadOnlyList<string>? list) ? list : [];
    }

    public bool Has(string name)
    {
        return Get(name) is not null;
    }

    /// <summary>
    /// The first value as a whole number; <c>null</c> if it is missing or no number.
    /// </summary>
    public int? GetInt(string name)
    {
        return int.TryParse(Get(name), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value)
            ? value
            : null;
    }

    /// <summary>
    /// The first value as a whole number; <c>null</c> if it is missing or no number.
    /// </summary>
    public long? GetLong(string name)
    {
        return long.TryParse(Get(name), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)
            ? value
            : null;
    }

    /// <summary>
    /// The first value as <c>true</c> or <c>false</c>; <paramref name="fallback"/> if it is missing or neither.
    /// </summary>
    public bool GetBool(string name, bool fallback)
    {
        return bool.TryParse(Get(name), out bool value) ? value : fallback;
    }

    /// <summary>
    /// The value as id of WaveMP; <c>null</c> if it is missing or no id.
    /// </summary>
    public Guid? GetId(string name)
    {
        return SubsonicIds.Parse(Get(name));
    }
}
