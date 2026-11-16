using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace WaveMP.Service.OpenApiTransformers;

/// <summary>
/// The XML documentation file of an assembly (GenerateDocumentationFile), for comments the XML comment generator of the
/// OpenAPI document does not apply correctly.
/// </summary>
internal sealed class XmlDocumentation
{
    private readonly Dictionary<string, XElement> _members;

    public XmlDocumentation(XDocument document)
    {
        _members = document.Root?.Element("members")?.Elements("member")
            .Where(member => member.Attribute("name") is not null)
            .GroupBy(member => member.Attribute("name")!.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal) ?? [];
    }

    /// <summary>
    /// Reads the documentation file next to the assembly; without one, the documentation is empty.
    /// </summary>
    public static XmlDocumentation Load(Assembly assembly)
    {
        string file = Path.ChangeExtension(assembly.Location, ".xml");
        return File.Exists(file) ? new XmlDocumentation(XDocument.Load(file)) : new XmlDocumentation(new XDocument());
    }

    /// <summary>
    /// Returns the text of the <c>param</c> comment of a method parameter, with code (<c>c</c>) in backticks like the
    /// XML comment generator writes it, or null if the method or the parameter has no comment. Overloads are told apart
    /// by their number of parameters.
    /// </summary>
    public string? FindParameter(MethodInfo method, string parameterName)
    {
        string type = (method.DeclaringType?.FullName ?? string.Empty).Replace('+', '.');
        string prefix = $"M:{type}.{method.Name}";
        int parameterCount = method.GetParameters().Length;
        List<XElement> candidates = [.. _members
            .Where(member => IsMethod(member.Key, prefix, parameterCount))
            .Select(member => member.Value)];
        if (candidates.Count != 1)
        {
            return null;
        }

        XElement? parameter = candidates[0].Elements("param")
            .FirstOrDefault(element => element.Attribute("name")?.Value == parameterName);
        if (parameter is null)
        {
            return null;
        }

        string text = Render(parameter).Trim();
        return text.Length == 0 ? null : text;
    }

    private static bool IsMethod(string id, string prefix, int parameterCount)
    {
        if (parameterCount == 0)
        {
            return id == prefix;
        }

        if (!id.StartsWith(prefix + "(", StringComparison.Ordinal) || !id.EndsWith(')'))
        {
            return false;
        }

        // Commas inside generic arguments ({...}) do not separate parameters.
        string parameters = id[(prefix.Length + 1)..^1];
        int count = 1;
        int depth = 0;
        foreach (char character in parameters)
        {
            switch (character)
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    break;
                case ',' when depth == 0:
                    count++;
                    break;
            }
        }

        return count == parameterCount;
    }

    private static string Render(XElement element)
    {
        StringBuilder text = new();
        foreach (XNode node in element.Nodes())
        {
            switch (node)
            {
                case XText content:
                    text.Append(content.Value);
                    break;
                case XElement { Name.LocalName: "c" } code:
                    text.Append('`').Append(code.Value).Append('`');
                    break;
                case XElement other:
                    text.Append(Render(other));
                    break;
            }
        }

        return text.ToString();
    }
}
