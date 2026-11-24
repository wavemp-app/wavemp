using System.Text;

namespace WaveMP.Core.Settings.Templates;

/// <summary>
/// Reads layout templates such as <c>{AlbumArtist}/{Album}/{TrackNumber:00} {Title}</c>:
/// <list type="bullet">
/// <item><c>/</c> separates folders; the last name is the file name without extension.</item>
/// <item><c>{Name}</c> is a value of the song (see <see cref="TemplateField"/>, without regard to case); numbers take a
/// format of 1 to 4 zeros that pads them, e.g. <c>{TrackNumber:00}</c>.</item>
/// <item><c>[...]</c> is an optional section within one name; it vanishes when one of its placeholders has no
/// value, e.g. <c>[{DiscNumber}-]{TrackNumber:00}</c>.</item>
/// <item><c>{{</c>, <c>}}</c>, <c>[[</c> and <c>]]</c> stand for the character itself.</item>
/// </list>
/// The file name must contain <c>{Title}</c>. Pure: rendering a template is the task of the organizer.
/// </summary>
public static class LayoutTemplateSyntax
{
    public const int MaxLength = 512;
    public const int MaxNumberWidth = 4;
    public const int MaxPlaceholderLength = 64;

    /// <summary>
    /// The template as it is stored: trimmed and normalized to NFC, with lone surrogates (which cannot be normalized)
    /// replaced by U+FFFD.
    /// </summary>
    public static string Normalize(string? template)
    {
        string text = (template ?? string.Empty).Trim();
        StringBuilder builder = new(text.Length);
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                builder.Append(character).Append(text[index + 1]);
                index++;
            }
            else
            {
                builder.Append(char.IsSurrogate(character) ? '�' : character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Whether a value is a number that takes a format.
    /// </summary>
    public static bool IsNumber(TemplateField field)
    {
        return field is TemplateField.Year or TemplateField.TrackNumber or TemplateField.DiscNumber;
    }

    /// <summary>
    /// Parses a template after normalizing it (see <see cref="Normalize"/>); positions of errors refer to the
    /// normalized text.
    /// </summary>
    public static LayoutTemplateParseResult Parse(string? template)
    {
        string text = Normalize(template);
        if (text.Length == 0)
        {
            return Failure([new LayoutTemplateError(LayoutTemplateErrorCode.Required)]);
        }

        if (text.Length > MaxLength)
        {
            return Failure([new LayoutTemplateError(LayoutTemplateErrorCode.TooLong)]);
        }

        List<LayoutTemplateError> errors = [];
        List<List<LayoutTemplatePart>> segments = [[]];
        List<int> segmentStarts = [0];
        List<LayoutTemplatePart>? section = null;
        int sectionStart = 0;
        StringBuilder literal = new();
        int index = 0;
        while (index < text.Length)
        {
            char character = text[index];
            if (character is '{' or '}' or '[' or ']' && index + 1 < text.Length && text[index + 1] == character)
            {
                literal.Append(character);
                index += 2;
                continue;
            }

            switch (character)
            {
                case '{':
                    int end = text.IndexOfAny(['{', '}'], index + 1);
                    if (end < 0 || text[end] == '{')
                    {
                        return Failure([new LayoutTemplateError(LayoutTemplateErrorCode.UnbalancedBrace, index)]);
                    }

                    Flush();
                    if (ParsePlaceholder(text[(index + 1)..end], index, errors) is { } placeholder)
                    {
                        (section ?? segments[^1]).Add(placeholder);
                    }

                    index = end + 1;
                    continue;
                case '}':
                    return Failure([new LayoutTemplateError(LayoutTemplateErrorCode.UnbalancedBrace, index)]);
                case '[':
                    if (section is not null)
                    {
                        return Failure([new LayoutTemplateError(LayoutTemplateErrorCode.NestedSection, index)]);
                    }

                    Flush();
                    section = [];
                    sectionStart = index;
                    break;
                case ']':
                    if (section is null)
                    {
                        return Failure([new LayoutTemplateError(LayoutTemplateErrorCode.UnbalancedSection, index)]);
                    }

                    Flush();
                    segments[^1].Add(new LayoutTemplateSection(section));
                    section = null;
                    break;
                case '/':
                    if (section is not null)
                    {
                        return Failure([new LayoutTemplateError(LayoutTemplateErrorCode.FolderInSection, index)]);
                    }

                    Flush();
                    segments.Add([]);
                    segmentStarts.Add(index + 1);
                    break;
                default:
                    literal.Append(character);
                    break;
            }

            index++;
        }

        if (section is not null)
        {
            return Failure([new LayoutTemplateError(LayoutTemplateErrorCode.UnbalancedSection, sectionStart)]);
        }

        Flush();
        CheckStructure(text, segments, segmentStarts, errors);
        return errors.Count > 0
            ? Failure(errors)
            : new LayoutTemplateParseResult(new LayoutTemplate([.. segments]), []);

        void Flush()
        {
            if (literal.Length > 0)
            {
                (section ?? segments[^1]).Add(new LayoutTemplateText(literal.ToString()));
                literal.Clear();
            }
        }
    }

    /// <summary>
    /// Whether a valid template lays the songs out in folders; <c>null</c> for an invalid template.
    /// </summary>
    public static bool? IsFolderBased(string? template)
    {
        return Parse(template).Template?.IsFolderBased;
    }

    private static LayoutTemplatePlaceholder? ParsePlaceholder(
        string content,
        int position,
        List<LayoutTemplateError> errors)
    {
        int colon = content.IndexOf(':', StringComparison.Ordinal);
        string name = colon < 0 ? content : content[..colon];
        if (name.Length == 0
            || !name.All(char.IsAsciiLetter)
            || !Enum.TryParse(name, ignoreCase: true, out TemplateField field))
        {
            errors.Add(new LayoutTemplateError(LayoutTemplateErrorCode.UnknownPlaceholder, position, Cut(content)));
            return null;
        }

        if (colon < 0)
        {
            return new LayoutTemplatePlaceholder(field);
        }

        string format = content[(colon + 1)..];
        if (!IsNumber(field) || format.Length is 0 or > MaxNumberWidth || format.Any(character => character != '0'))
        {
            errors.Add(new LayoutTemplateError(LayoutTemplateErrorCode.InvalidNumberFormat, position, Cut(name)));
            return null;
        }

        return new LayoutTemplatePlaceholder(field, format.Length);
    }

    private static void CheckStructure(
        string text,
        List<List<LayoutTemplatePart>> segments,
        List<int> segmentStarts,
        List<LayoutTemplateError> errors)
    {
        if (text[0] == '/')
        {
            errors.Add(new LayoutTemplateError(LayoutTemplateErrorCode.Rooted));
        }

        for (int segment = 0; segment < segments.Count - 1; segment++)
        {
            // Empty by its text: a name of unknown placeholders only has no parts either, but is reported as such.
            List<LayoutTemplatePart> parts = segments[segment];
            bool isEmpty = segmentStarts[segment + 1] - 1 == segmentStarts[segment];
            if (segment > 0 && isEmpty)
            {
                errors.Add(new LayoutTemplateError(LayoutTemplateErrorCode.EmptyFolder, segmentStarts[segment]));
            }
            else if (parts.Count > 0
                && parts.All(part => part is LayoutTemplateText)
                && string.Concat(parts.Cast<LayoutTemplateText>().Select(part => part.Text)).Trim() is "." or "..")
            {
                errors.Add(new LayoutTemplateError(LayoutTemplateErrorCode.ParentFolder, segmentStarts[segment]));
            }
        }

        bool hasTitle = segments[^1]
            .SelectMany(part => part is LayoutTemplateSection section ? section.Parts : [part])
            .Any(part => part is LayoutTemplatePlaceholder { Field: TemplateField.Title });
        if (!hasTitle)
        {
            errors.Add(new LayoutTemplateError(LayoutTemplateErrorCode.MissingTitle, segmentStarts[^1]));
        }
    }

    private static string Cut(string text)
    {
        if (text.Length <= MaxPlaceholderLength)
        {
            return text;
        }

        int length = char.IsHighSurrogate(text[MaxPlaceholderLength - 1])
            ? MaxPlaceholderLength - 1
            : MaxPlaceholderLength;
        return text[..length];
    }

    private static LayoutTemplateParseResult Failure(IReadOnlyList<LayoutTemplateError> errors)
    {
        return new LayoutTemplateParseResult(null, errors);
    }
}
