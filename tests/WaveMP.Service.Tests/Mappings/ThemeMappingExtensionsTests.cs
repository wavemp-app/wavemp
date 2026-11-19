using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Storage;
using WaveMP.Core.Themes.Validations;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public partial class ThemeMappingExtensionsTests
{
    [Test]
    public void ToDto_InstalledTheme_NamesItsFilesByAddress()
    {
        DateTimeOffset installedAt = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);
        InstalledTheme theme = new()
        {
            Id = "aurora",
            Name = "Aurora",
            Author = "Author",
            Version = "1.0.0",
            ThemeApi = 5,
            Variants = [ThemeVariant.Dark, ThemeVariant.Light],
            Stylesheet = "0123456789abcdef0123456789abcdef.css",
            Previews = new Dictionary<ThemeVariant, string>
            {
                [ThemeVariant.Light] = "11111111111111111111111111111111.png",
                [ThemeVariant.Dark] = "22222222222222222222222222222222.svg",
            },
            Files = [],
            SizeBytes = 2048,
            InstalledAt = installedAt,
        };

        theme.ToDto().Should().BeEquivalentTo(new ThemeDto
        {
            Id = "aurora",
            Name = "Aurora",
            Author = "Author",
            Version = "1.0.0",
            ThemeApi = 5,
            Variants = [ThemeVariantDto.Dark, ThemeVariantDto.Light],
            StylesheetUrl = "/api/v1/themes/aurora/files/0123456789abcdef0123456789abcdef.css",
            Previews =
            [
                new ThemePreviewDto
                {
                    Variant = ThemeVariantDto.Dark,
                    Url = "/api/v1/themes/aurora/files/22222222222222222222222222222222.svg",
                },
                new ThemePreviewDto
                {
                    Variant = ThemeVariantDto.Light,
                    Url = "/api/v1/themes/aurora/files/11111111111111111111111111111111.png",
                },
            ],
            SizeBytes = 2048,
            InstalledAt = installedAt,
            Status = ThemeStatusDto.Current,
            Problems = [],
        }, options => options.WithStrictOrdering());
    }

    [Test]
    public void ToDto_ThemeWithProblems_IsOutdatedAndNamesThem()
    {
        InstalledTheme theme = new()
        {
            Id = "aurora",
            Name = "Aurora",
            Author = "Author",
            Version = "1.0.0",
            ThemeApi = 4,
            Variants = [ThemeVariant.Light],
            Stylesheet = "0123456789abcdef0123456789abcdef.css",
            Previews = new Dictionary<ThemeVariant, string>(),
            Files = [],
            SizeBytes = 2048,
            InstalledAt = DateTimeOffset.UnixEpoch,
            Problems =
            [
                new ThemeError(ThemeErrorCode.UnsupportedApiVersion, "manifest.json", Field: "themeApi", Min: 5, Max: 5),
                new ThemeError(ThemeErrorCode.SvgNotCleaned, "images/glass.svg"),
            ],
        };

        ThemeDto dto = theme.ToDto();

        dto.Status.Should().Be(ThemeStatusDto.Outdated);
        dto.Problems.Select(problem => (problem.Code, problem.Path)).Should().Equal(
            (ThemeErrorCodeDto.UnsupportedApiVersion, "manifest.json"),
            (ThemeErrorCodeDto.SvgNotCleaned, "images/glass.svg"));
    }

    [Test]
    public void ToDto_EveryErrorCode_HasItsOwnCode()
    {
        string[] codes = [.. Enum.GetValues<ThemeErrorCode>()
            .Select(code => JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(code.ToDto()))!)];

        codes.Should().OnlyHaveUniqueItems().And.AllSatisfy(code => CodePattern().IsMatch(code).Should().BeTrue(code));
        Enum.GetValues<ThemeErrorCodeDto>().Should().HaveSameCount(Enum.GetValues<ThemeErrorCode>());
    }

    [Test]
    public void ToDto_Error_KeepsItsDetails()
    {
        ThemeError error = new(ThemeErrorCode.UnsupportedApiVersion, "manifest.json", 3, "themeApi", null, 5, 6);

        JsonSerializer.Serialize(error.ToDto()).Should().Be(
            """{"Code":"manifest.unsupported_api_version","Path":"manifest.json","Line":3,"Field":"themeApi","Min":5,"Max":6}""");
    }

    [GeneratedRegex(@"\A[a-z]+\.[a-z_]+\z")]
    private static partial Regex CodePattern();
}
