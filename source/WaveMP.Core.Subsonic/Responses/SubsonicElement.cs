using System.Globalization;

namespace WaveMP.Core.Subsonic.Responses;

/// <summary>
/// An element of a Subsonic response, written as XML or JSON. Attributes become XML attributes and JSON properties,
/// child elements XML elements and JSON objects. Children added as a list are always a JSON array, also with one item
/// or none, as the Subsonic clients expect; other children with the same name become an array only when they repeat.
/// </summary>
public sealed class SubsonicElement(string name)
{
    private readonly List<KeyValuePair<string, object>> _attributes = [];
    private readonly List<SubsonicElement> _children = [];
    private readonly HashSet<string> _lists = new(StringComparer.Ordinal);
    private readonly List<string> _emptyLists = [];
    private readonly List<KeyValuePair<string, IReadOnlyList<object>>> _values = [];

    public string Name { get; } = name;

    /// <summary>
    /// The attributes in their order; values are <see cref="string"/>, <see cref="bool"/>, <see cref="int"/>,
    /// <see cref="long"/> or <see cref="double"/>.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, object>> Attributes => _attributes;

    public IReadOnlyList<SubsonicElement> Children => _children;

    /// <summary>
    /// Text content of the element (JSON: the property <c>value</c>); <c>null</c> without text.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Names of the children that are lists, whether they have items or not.
    /// </summary>
    public IReadOnlySet<string> Lists => _lists;

    /// <summary>
    /// Lists without items, in the order they were added.
    /// </summary>
    public IReadOnlyList<string> EmptyLists => _emptyLists;

    /// <summary>
    /// Lists of plain values (XML: an element with the value as text per item, JSON: an array of the values).
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, IReadOnlyList<object>>> Values => _values;

    /// <summary>
    /// Adds a text attribute; <c>null</c> adds nothing.
    /// </summary>
    public SubsonicElement Set(string attribute, string? value)
    {
        if (value is not null)
        {
            _attributes.Add(new KeyValuePair<string, object>(attribute, value));
        }

        return this;
    }

    public SubsonicElement Set(string attribute, bool value)
    {
        _attributes.Add(new KeyValuePair<string, object>(attribute, value));
        return this;
    }

    /// <summary>
    /// Adds a number attribute; <c>null</c> adds nothing.
    /// </summary>
    public SubsonicElement Set(string attribute, int? value)
    {
        if (value is { } number)
        {
            _attributes.Add(new KeyValuePair<string, object>(attribute, number));
        }

        return this;
    }

    /// <summary>
    /// Adds a number attribute; <c>null</c> adds nothing.
    /// </summary>
    public SubsonicElement Set(string attribute, long? value)
    {
        if (value is { } number)
        {
            _attributes.Add(new KeyValuePair<string, object>(attribute, number));
        }

        return this;
    }

    /// <summary>
    /// Adds a time attribute in the format of the Subsonic API (ISO 8601 in UTC with milliseconds); <c>null</c> adds
    /// nothing.
    /// </summary>
    public SubsonicElement Set(string attribute, DateTimeOffset? value)
    {
        return value is { } time
            ? Set(attribute, time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture))
            : this;
    }

    /// <summary>
    /// Adds a list of numbers, e.g. the versions of an OpenSubsonic extension.
    /// </summary>
    public SubsonicElement AddValues(string name, IEnumerable<int> values)
    {
        _values.Add(new KeyValuePair<string, IReadOnlyList<object>>(name, [.. values.Cast<object>()]));
        return this;
    }

    /// <summary>
    /// Adds a child element.
    /// </summary>
    public SubsonicElement Add(SubsonicElement child)
    {
        _children.Add(child);
        return this;
    }

    /// <summary>
    /// Adds the items of a list, each an element named <paramref name="itemName"/>. In JSON they are always an array.
    /// </summary>
    public SubsonicElement AddList(string itemName, IEnumerable<SubsonicElement> items)
    {
        _lists.Add(itemName);
        int before = _children.Count;
        foreach (SubsonicElement item in items)
        {
            if (!string.Equals(item.Name, itemName, StringComparison.Ordinal))
            {
                throw new ArgumentException($"The item {item.Name} is not named {itemName}.", nameof(items));
            }

            _children.Add(item);
        }

        if (_children.Count == before && !_emptyLists.Contains(itemName))
        {
            _emptyLists.Add(itemName);
        }

        return this;
    }
}
