using System.Text;
using WaveMP.Core.Playlists.Names;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// The rules of smart playlists: which operators fit which field, the ranges of the values and the check of a smart
/// playlist entered by a user. A rule keeps only the values its operator needs; other values are dropped.
/// </summary>
public static class SmartPlaylistRules
{
    /// <summary>
    /// Largest number of rules of a smart playlist.
    /// </summary>
    public const int MaxRules = 20;

    /// <summary>
    /// Longest text of a rule (artist, album, genre).
    /// </summary>
    public const int TextMaxLength = 200;

    /// <summary>
    /// Largest limit of a smart playlist; the same as the largest number of entries of a playlist by default.
    /// </summary>
    public const int MaxLimit = 10_000;

    private static readonly IReadOnlyList<SmartPlaylistOperator> _textOperators =
    [
        SmartPlaylistOperator.Is,
        SmartPlaylistOperator.IsNot,
        SmartPlaylistOperator.Contains,
        SmartPlaylistOperator.DoesNotContain,
    ];

    private static readonly IReadOnlyList<SmartPlaylistOperator> _numberOperators =
    [
        SmartPlaylistOperator.Is,
        SmartPlaylistOperator.IsNot,
        SmartPlaylistOperator.AtLeast,
        SmartPlaylistOperator.AtMost,
        SmartPlaylistOperator.Between,
    ];

    private static readonly IReadOnlyList<SmartPlaylistOperator> _dateOperators =
    [
        SmartPlaylistOperator.InLast,
        SmartPlaylistOperator.NotInLast,
    ];

    private static readonly IReadOnlyList<SmartPlaylistOperator> _flagOperators =
    [
        SmartPlaylistOperator.IsTrue,
        SmartPlaylistOperator.IsFalse,
    ];

    /// <summary>
    /// The kind of value of a field.
    /// </summary>
    public static SmartPlaylistFieldKind KindOf(SmartPlaylistField field)
    {
        return field switch
        {
            SmartPlaylistField.Artist or SmartPlaylistField.Album or SmartPlaylistField.Genre
                => SmartPlaylistFieldKind.Text,
            SmartPlaylistField.Year or SmartPlaylistField.Rating or SmartPlaylistField.PlayCount
                or SmartPlaylistField.Duration => SmartPlaylistFieldKind.Number,
            SmartPlaylistField.LastPlayed or SmartPlaylistField.DateAdded => SmartPlaylistFieldKind.Date,
            SmartPlaylistField.Favorite or SmartPlaylistField.HasAudio or SmartPlaylistField.HasVideo
                => SmartPlaylistFieldKind.Flag,
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field."),
        };
    }

    /// <summary>
    /// The operators that fit a field.
    /// </summary>
    public static IReadOnlyList<SmartPlaylistOperator> OperatorsOf(SmartPlaylistField field)
    {
        return KindOf(field) switch
        {
            SmartPlaylistFieldKind.Text => _textOperators,
            SmartPlaylistFieldKind.Number => _numberOperators,
            SmartPlaylistFieldKind.Date => _dateOperators,
            _ => _flagOperators,
        };
    }

    /// <summary>
    /// The allowed numbers of a number field (for duration in seconds) or the allowed number of days of a date field.
    /// </summary>
    public static (int Min, int Max) RangeOf(SmartPlaylistField field)
    {
        return field switch
        {
            SmartPlaylistField.Year => (0, 9999),
            SmartPlaylistField.Rating => (0, 5),
            SmartPlaylistField.PlayCount => (0, 1_000_000),
            // Up to 99:59:59.
            SmartPlaylistField.Duration => (0, 359_999),
            SmartPlaylistField.LastPlayed or SmartPlaylistField.DateAdded => (1, 36_500),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "The field has no number."),
        };
    }

    /// <summary>
    /// Checks a smart playlist entered by a user and returns it normalized: the name as for playlists, the texts of
    /// the rules trimmed, only the values each operator needs. Every rejected field is reported.
    /// </summary>
    public static SmartPlaylistValidation Validate(SmartPlaylistDefinition definition)
    {
        List<SmartPlaylistError> errors = [];
        string name = string.Empty;
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            errors.Add(new SmartPlaylistError("name", SmartPlaylistErrorCode.Required));
        }
        else if (!PlaylistNames.TryNormalize(definition.Name, out name))
        {
            errors.Add(new SmartPlaylistError("name", SmartPlaylistErrorCode.InvalidValue));
        }

        if (!Enum.IsDefined(definition.Match))
        {
            errors.Add(new SmartPlaylistError("match", SmartPlaylistErrorCode.InvalidValue));
        }

        if (!Enum.IsDefined(definition.Sort))
        {
            errors.Add(new SmartPlaylistError("sort", SmartPlaylistErrorCode.InvalidValue));
        }

        if (!Enum.IsDefined(definition.Direction))
        {
            errors.Add(new SmartPlaylistError("direction", SmartPlaylistErrorCode.InvalidValue));
        }

        if (definition.Limit is < 1 or > MaxLimit)
        {
            errors.Add(new SmartPlaylistError("limit", SmartPlaylistErrorCode.OutOfRange, 1, MaxLimit));
        }

        List<SmartPlaylistRule> rules = [];
        if (definition.Rules.Count > MaxRules)
        {
            errors.Add(new SmartPlaylistError("rules", SmartPlaylistErrorCode.MaxItems, Max: MaxRules));
        }
        else
        {
            for (int index = 0; index < definition.Rules.Count; index++)
            {
                SmartPlaylistRule? rule = Normalize(definition.Rules[index], $"rules[{index}]", errors);
                if (rule is not null)
                {
                    rules.Add(rule);
                }
            }
        }

        return new SmartPlaylistValidation(
            errors,
            name,
            new SmartPlaylistCriteria(
                definition.Match,
                rules,
                definition.Sort,
                definition.Direction,
                definition.Limit));
    }

    private static SmartPlaylistRule? Normalize(SmartPlaylistRule? rule, string path, List<SmartPlaylistError> errors)
    {
        if (rule is null)
        {
            errors.Add(new SmartPlaylistError(path, SmartPlaylistErrorCode.Required));
            return null;
        }

        if (!Enum.IsDefined(rule.Field))
        {
            errors.Add(new SmartPlaylistError(path + ".field", SmartPlaylistErrorCode.InvalidValue));
            return null;
        }

        if (!OperatorsOf(rule.Field).Contains(rule.Operator))
        {
            errors.Add(new SmartPlaylistError(path + ".operator", SmartPlaylistErrorCode.InvalidValue));
            return null;
        }

        int before = errors.Count;
        SmartPlaylistRule normalized = KindOf(rule.Field) switch
        {
            SmartPlaylistFieldKind.Text => new SmartPlaylistRule(
                rule.Field,
                rule.Operator,
                Text: NormalizeText(rule.Text, path + ".text", errors),
                ReferenceId: rule.CanReference() ? rule.ReferenceId : null),
            SmartPlaylistFieldKind.Flag => new SmartPlaylistRule(rule.Field, rule.Operator),
            _ => NormalizeNumbers(rule, path, errors),
        };
        return errors.Count == before ? normalized : null;
    }

    private static string? NormalizeText(string? text, string path, List<SmartPlaylistError> errors)
    {
        string trimmed = (text ?? string.Empty).Trim();
        try
        {
            trimmed = trimmed.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            // E.g. a lone surrogate, which cannot be stored as UTF-8.
            errors.Add(new SmartPlaylistError(path, SmartPlaylistErrorCode.InvalidValue));
            return null;
        }

        if (trimmed.Length == 0)
        {
            errors.Add(new SmartPlaylistError(path, SmartPlaylistErrorCode.Required));
            return null;
        }

        if (trimmed.Length > TextMaxLength || trimmed.Any(char.IsControl))
        {
            errors.Add(trimmed.Length > TextMaxLength
                ? new SmartPlaylistError(path, SmartPlaylistErrorCode.MaxLength, Max: TextMaxLength)
                : new SmartPlaylistError(path, SmartPlaylistErrorCode.InvalidValue));
            return null;
        }

        return trimmed;
    }

    private static SmartPlaylistRule NormalizeNumbers(
        SmartPlaylistRule rule,
        string path,
        List<SmartPlaylistError> errors)
    {
        (int min, int max) = RangeOf(rule.Field);
        int? number = CheckNumber(rule.Number, min, max, path + ".number", errors);
        int? numberTo = null;
        if (rule.Operator == SmartPlaylistOperator.Between)
        {
            numberTo = CheckNumber(rule.NumberTo, number ?? min, max, path + ".numberTo", errors);
        }

        return new SmartPlaylistRule(rule.Field, rule.Operator, Number: number, NumberTo: numberTo);
    }

    private static int? CheckNumber(int? value, int min, int max, string path, List<SmartPlaylistError> errors)
    {
        if (value is not { } number)
        {
            errors.Add(new SmartPlaylistError(path, SmartPlaylistErrorCode.Required));
            return null;
        }

        if (number < min || number > max)
        {
            errors.Add(new SmartPlaylistError(path, SmartPlaylistErrorCode.OutOfRange, min, max));
            return null;
        }

        return number;
    }
}
