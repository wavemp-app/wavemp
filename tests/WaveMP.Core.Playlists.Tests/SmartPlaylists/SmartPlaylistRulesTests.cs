using AwesomeAssertions;
using WaveMP.Core.Playlists.SmartPlaylists;
using WaveMP.Data.Entities;
using WaveMP.Data.Queries;

namespace WaveMP.Core.Playlists.Tests.SmartPlaylists;

[TestFixture]
public class SmartPlaylistRulesTests
{
    [Test]
    public void OperatorsOf_EveryField_HasOperatorsOfItsKind()
    {
        foreach (SmartPlaylistField field in Enum.GetValues<SmartPlaylistField>())
        {
            SmartPlaylistRules.OperatorsOf(field).Should().NotBeEmpty();
        }

        SmartPlaylistRules.OperatorsOf(SmartPlaylistField.Genre).Should().Contain(SmartPlaylistOperator.Contains);
        SmartPlaylistRules.OperatorsOf(SmartPlaylistField.Year).Should().Contain(SmartPlaylistOperator.Between);
        SmartPlaylistRules.OperatorsOf(SmartPlaylistField.LastPlayed)
            .Should().Equal(SmartPlaylistOperator.InLast, SmartPlaylistOperator.NotInLast);
        SmartPlaylistRules.OperatorsOf(SmartPlaylistField.HasVideo)
            .Should().Equal(SmartPlaylistOperator.IsTrue, SmartPlaylistOperator.IsFalse);
    }

    [Test]
    public void Validate_ValidDefinition_NormalizesNameAndTextsAndDropsValuesTheOperatorDoesNotNeed()
    {
        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(Definition(
            "  Road   Trip ",
            new SmartPlaylistRule(SmartPlaylistField.Genre, SmartPlaylistOperator.Contains, "  Rock ", 3, 4),
            new SmartPlaylistRule(SmartPlaylistField.Year, SmartPlaylistOperator.AtLeast, "x", 1990, 2000),
            new SmartPlaylistRule(SmartPlaylistField.Year, SmartPlaylistOperator.Between, null, 1990, 1999),
            new SmartPlaylistRule(SmartPlaylistField.Favorite, SmartPlaylistOperator.IsTrue, "x", 1, 2)));

        validation.IsValid.Should().BeTrue();
        validation.Name.Should().Be("Road Trip");
        validation.Criteria.Rules.Should().Equal(
            new SmartPlaylistRule(SmartPlaylistField.Genre, SmartPlaylistOperator.Contains, "Rock"),
            new SmartPlaylistRule(SmartPlaylistField.Year, SmartPlaylistOperator.AtLeast, Number: 1990),
            new SmartPlaylistRule(SmartPlaylistField.Year, SmartPlaylistOperator.Between, Number: 1990, NumberTo: 1999),
            new SmartPlaylistRule(SmartPlaylistField.Favorite, SmartPlaylistOperator.IsTrue));
    }

    [TestCase(null)]
    [TestCase("   ")]
    public void Validate_MissingName_ReportsTheName(string? name)
    {
        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(Definition(name));

        validation.Errors.Should().Equal(new SmartPlaylistError("name", SmartPlaylistErrorCode.Required));
    }

    [Test]
    public void Validate_OperatorOfAnotherKind_ReportsTheOperator()
    {
        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(Definition(
            "Mixed",
            new SmartPlaylistRule(SmartPlaylistField.Rating, SmartPlaylistOperator.Contains, "5"),
            new SmartPlaylistRule(SmartPlaylistField.Artist, SmartPlaylistOperator.InLast, Number: 3)));

        validation.Errors.Should().Equal(
            new SmartPlaylistError("rules[0].operator", SmartPlaylistErrorCode.InvalidValue),
            new SmartPlaylistError("rules[1].operator", SmartPlaylistErrorCode.InvalidValue));
    }

    [Test]
    public void Validate_MissingValues_ReportsEachOfThem()
    {
        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(Definition(
            "Empty",
            new SmartPlaylistRule(SmartPlaylistField.Album, SmartPlaylistOperator.Is, "  "),
            new SmartPlaylistRule(SmartPlaylistField.PlayCount, SmartPlaylistOperator.AtMost),
            new SmartPlaylistRule(SmartPlaylistField.Duration, SmartPlaylistOperator.Between, Number: 60),
            new SmartPlaylistRule(SmartPlaylistField.DateAdded, SmartPlaylistOperator.InLast)));

        validation.Errors.Should().Equal(
            new SmartPlaylistError("rules[0].text", SmartPlaylistErrorCode.Required),
            new SmartPlaylistError("rules[1].number", SmartPlaylistErrorCode.Required),
            new SmartPlaylistError("rules[2].numberTo", SmartPlaylistErrorCode.Required),
            new SmartPlaylistError("rules[3].number", SmartPlaylistErrorCode.Required));
    }

    [Test]
    public void Validate_NumbersOutOfRange_ReportsTheRangeOfTheField()
    {
        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(Definition(
            "Range",
            new SmartPlaylistRule(SmartPlaylistField.Rating, SmartPlaylistOperator.AtLeast, Number: 6),
            new SmartPlaylistRule(SmartPlaylistField.Year, SmartPlaylistOperator.Between, Number: 2000, NumberTo: 1999),
            new SmartPlaylistRule(SmartPlaylistField.LastPlayed, SmartPlaylistOperator.InLast, Number: 0),
            new SmartPlaylistRule(SmartPlaylistField.PlayCount, SmartPlaylistOperator.Is, Number: -1)));

        validation.Errors.Should().Equal(
            new SmartPlaylistError("rules[0].number", SmartPlaylistErrorCode.OutOfRange, 0, 5),
            new SmartPlaylistError("rules[1].numberTo", SmartPlaylistErrorCode.OutOfRange, 2000, 9999),
            new SmartPlaylistError("rules[2].number", SmartPlaylistErrorCode.OutOfRange, 1, 36_500),
            new SmartPlaylistError("rules[3].number", SmartPlaylistErrorCode.OutOfRange, 0, 1_000_000));
    }

    [Test]
    public void Validate_InvalidTexts_ReportsLengthAndControlCharacters()
    {
        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(Definition(
            "Texts",
            new SmartPlaylistRule(
                SmartPlaylistField.Artist,
                SmartPlaylistOperator.Is,
                new string('a', SmartPlaylistRules.TextMaxLength + 1)),
            new SmartPlaylistRule(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "Ro\u0000ck"),
            new SmartPlaylistRule(SmartPlaylistField.Genre, SmartPlaylistOperator.Is, "\ud800")));

        validation.Errors.Should().Equal(
            new SmartPlaylistError(
                "rules[0].text",
                SmartPlaylistErrorCode.MaxLength,
                Max: SmartPlaylistRules.TextMaxLength),
            new SmartPlaylistError("rules[1].text", SmartPlaylistErrorCode.InvalidValue),
            new SmartPlaylistError("rules[2].text", SmartPlaylistErrorCode.InvalidValue));
    }

    [Test]
    public void Validate_TooManyRules_ReportsTheRules()
    {
        SmartPlaylistRule rule = new(SmartPlaylistField.HasAudio, SmartPlaylistOperator.IsTrue);

        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(Definition(
            "Long",
            [.. Enumerable.Repeat(rule, SmartPlaylistRules.MaxRules + 1)]));

        validation.Errors.Should().Equal(
            new SmartPlaylistError("rules", SmartPlaylistErrorCode.MaxItems, Max: SmartPlaylistRules.MaxRules));
    }

    [TestCase(0)]
    [TestCase(SmartPlaylistRules.MaxLimit + 1)]
    public void Validate_LimitOutOfRange_ReportsTheLimit(int limit)
    {
        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(Definition("Limited") with { Limit = limit });

        validation.Errors.Should().Equal(
            new SmartPlaylistError("limit", SmartPlaylistErrorCode.OutOfRange, 1, SmartPlaylistRules.MaxLimit));
    }

    [Test]
    public void Validate_UndefinedEnums_ReportsThem()
    {
        SmartPlaylistValidation validation = SmartPlaylistRules.Validate(
            Definition("Odd", new SmartPlaylistRule((SmartPlaylistField)99, SmartPlaylistOperator.Is, "x")) with
            {
                Match = (SmartPlaylistMatch)9,
                Sort = (SmartPlaylistSortField)99,
                Direction = (SortDirection)9,
            });

        validation.Errors.Select(error => error.Field).Should().Equal(
            "match",
            "sort",
            "direction",
            "rules[0].field");
    }

    [Test]
    public void Validate_WithoutRules_IsValid()
    {
        SmartPlaylistRules.Validate(Definition("Everything")).IsValid.Should().BeTrue();
    }

    private static SmartPlaylistDefinition Definition(string? name, params SmartPlaylistRule[] rules)
    {
        return new SmartPlaylistDefinition(
            name,
            SmartPlaylistMatch.All,
            rules,
            SmartPlaylistSortField.Artist,
            SortDirection.Asc);
    }
}
