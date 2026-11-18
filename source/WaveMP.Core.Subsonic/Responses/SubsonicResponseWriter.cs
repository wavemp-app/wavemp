using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;

namespace WaveMP.Core.Subsonic.Responses;

/// <summary>
/// Writes the answers of the Subsonic API as XML, JSON or JSONP: the element <c>subsonic-response</c> with status,
/// protocol version, the OpenSubsonic markers and the content or the error. Errors carry their code and the message
/// the Subsonic specification defines for it, no text of WaveMP.
/// </summary>
public static partial class SubsonicResponseWriter
{
    /// <summary>
    /// The version of the Subsonic API that WaveMP implements.
    /// </summary>
    public const string ApiVersion = "1.16.1";

    /// <summary>
    /// The name of the server for the OpenSubsonic field <c>type</c>.
    /// </summary>
    public const string ServerType = "wavemp";

    public const string XmlNamespace = "http://subsonic.org/restapi";

    public const string XmlContentType = "text/xml; charset=utf-8";
    public const string JsonContentType = "application/json; charset=utf-8";
    public const string JsonPContentType = "application/javascript; charset=utf-8";

    private const string RootName = "subsonic-response";
    private const string TextProperty = "value";

    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// The message of each code, as the Subsonic specification words it.
    /// </summary>
    public static string MessageOf(SubsonicErrorCode code)
    {
        return code switch
        {
            SubsonicErrorCode.MissingParameter => "Required parameter is missing.",
            SubsonicErrorCode.ClientMustUpgrade => "Incompatible Subsonic REST protocol version. Client must upgrade.",
            SubsonicErrorCode.ServerMustUpgrade => "Incompatible Subsonic REST protocol version. Server must upgrade.",
            SubsonicErrorCode.WrongCredentials => "Wrong username or password.",
            SubsonicErrorCode.TokenNotSupported => "Token authentication not supported for LDAP users.",
            SubsonicErrorCode.AuthenticationNotSupported => "Provided authentication mechanism not supported.",
            SubsonicErrorCode.ConflictingAuthentication => "Multiple conflicting authentication mechanisms provided.",
            SubsonicErrorCode.InvalidApiKey => "Invalid API key.",
            SubsonicErrorCode.NotAuthorized => "User is not authorized for the given operation.",
            SubsonicErrorCode.NotFound => "The requested data was not found.",
            _ => "A generic error.",
        };
    }

    /// <summary>
    /// The format of the parameter <c>f</c>: <c>json</c>, <c>jsonp</c>, otherwise XML.
    /// </summary>
    public static SubsonicFormat ParseFormat(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "json" => SubsonicFormat.Json,
            "jsonp" => SubsonicFormat.JsonP,
            _ => SubsonicFormat.Xml,
        };
    }

    /// <summary>
    /// Writes the answer. JSONP needs a callback that is a plain JavaScript name (e.g. <c>handle</c> or
    /// <c>app.handle</c>); with another one, the answer is plain JSON.
    /// </summary>
    /// <returns>The body and its content type.</returns>
    public static (byte[] Body, string ContentType) Write(
        SubsonicResponse response,
        SubsonicFormat format,
        string serverVersion,
        string? callback = null)
    {
        SubsonicElement root = CreateRoot(response, serverVersion);
        if (format == SubsonicFormat.Xml)
        {
            return (WriteXml(root), XmlContentType);
        }

        byte[] json = WriteJson(root);
        if (format == SubsonicFormat.JsonP && callback is not null && CallbackRegex().IsMatch(callback))
        {
            byte[] prefix = _utf8.GetBytes(callback + "(");
            byte[] suffix = _utf8.GetBytes(");");
            return ([.. prefix, .. json, .. suffix], JsonPContentType);
        }

        return (json, JsonContentType);
    }

    private static SubsonicElement CreateRoot(SubsonicResponse response, string serverVersion)
    {
        SubsonicElement root = new SubsonicElement(RootName)
            .Set("status", response.IsOk ? "ok" : "failed")
            .Set("version", ApiVersion)
            .Set("type", ServerType)
            .Set("serverVersion", serverVersion)
            .Set("openSubsonic", true);
        if (!response.IsOk)
        {
            root.Add(new SubsonicElement("error")
                .Set("code", (int)response.Error.Value)
                .Set("message", MessageOf(response.Error.Value)));
        }
        else if (response.Content is { } content)
        {
            root.Add(content);
        }
        else if (response.RootList is { } list)
        {
            root.AddList(list.ItemName, list.Items);
        }

        return root;
    }

    private static byte[] WriteXml(SubsonicElement root)
    {
        using MemoryStream stream = new();
        XmlWriterSettings settings = new() { Encoding = _utf8, Indent = false };
        using (var writer = XmlWriter.Create(stream, settings))
        {
            writer.WriteStartDocument();
            WriteXmlElement(writer, root, XmlNamespace);
            writer.WriteEndDocument();
        }

        return stream.ToArray();
    }

    private static void WriteXmlElement(XmlWriter writer, SubsonicElement element, string ns)
    {
        writer.WriteStartElement(element.Name, ns);
        foreach ((string name, object value) in element.Attributes)
        {
            writer.WriteAttributeString(name, FormatXmlValue(value));
        }

        if (element.Text is { } text)
        {
            writer.WriteString(text);
        }

        foreach ((string name, IReadOnlyList<object> values) in element.Values)
        {
            foreach (object value in values)
            {
                writer.WriteElementString(name, ns, FormatXmlValue(value));
            }
        }

        foreach (SubsonicElement child in element.Children)
        {
            WriteXmlElement(writer, child, ns);
        }

        writer.WriteEndElement();
    }

    private static string FormatXmlValue(object value)
    {
        return value switch
        {
            bool flag => flag ? "true" : "false",
            IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static byte[] WriteJson(SubsonicElement root)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName(root.Name);
            WriteJsonObject(writer, root);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteJsonObject(Utf8JsonWriter writer, SubsonicElement element)
    {
        writer.WriteStartObject();
        foreach ((string name, object value) in element.Attributes)
        {
            writer.WritePropertyName(name);
            WriteJsonValue(writer, value);
        }

        if (element.Text is { } text)
        {
            writer.WriteString(TextProperty, text);
        }

        foreach ((string name, IReadOnlyList<object> values) in element.Values)
        {
            writer.WritePropertyName(name);
            writer.WriteStartArray();
            foreach (object value in values)
            {
                WriteJsonValue(writer, value);
            }

            writer.WriteEndArray();
        }

        // Children grouped by name in the order of their first appearance.
        List<string> names = [];
        Dictionary<string, List<SubsonicElement>> groups = new(StringComparer.Ordinal);
        foreach (SubsonicElement child in element.Children)
        {
            if (!groups.TryGetValue(child.Name, out List<SubsonicElement>? group))
            {
                group = [];
                groups[child.Name] = group;
                names.Add(child.Name);
            }

            group.Add(child);
        }

        foreach (string name in names)
        {
            List<SubsonicElement> group = groups[name];
            writer.WritePropertyName(name);
            if (group.Count == 1 && !element.Lists.Contains(name))
            {
                WriteJsonObject(writer, group[0]);
                continue;
            }

            writer.WriteStartArray();
            foreach (SubsonicElement item in group)
            {
                WriteJsonObject(writer, item);
            }

            writer.WriteEndArray();
        }

        foreach (string name in element.EmptyLists.Where(name => !groups.ContainsKey(name)))
        {
            writer.WritePropertyName(name);
            writer.WriteStartArray();
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case bool flag:
                writer.WriteBooleanValue(flag);
                break;
            case int number:
                writer.WriteNumberValue(number);
                break;
            case long number:
                writer.WriteNumberValue(number);
                break;
            case double number:
                writer.WriteNumberValue(number);
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }

    [GeneratedRegex("^[A-Za-z_$][A-Za-z0-9_$]*(\\.[A-Za-z_$][A-Za-z0-9_$]*)*$")]
    private static partial Regex CallbackRegex();
}
