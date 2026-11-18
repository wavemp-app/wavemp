using System.Text;
using AwesomeAssertions;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Manifests;
using WaveMP.Core.Themes.Validations;
using WaveMP.Testing.Themes;

namespace WaveMP.Core.Themes.Tests.Manifests;

[TestFixture]
public class ThemeManifestReaderTests
{
    private static (ThemeManifest? Manifest, List<ThemeError> Errors) Read(string json)
    {
        List<ThemeError> errors = [];
        ThemeManifest? manifest = ThemeManifestReader.Read(Encoding.UTF8.GetBytes(json), errors);
        return (manifest, errors);
    }

    private static string Replace(string member, string value)
    {
        string json = ThemePackageBuilder.Manifest();
        int start = json.IndexOf($"\"{member}\":", StringComparison.Ordinal);
        int end = json.IndexOf('\n', start);
        string line = json[start..end];
        string ending = line.EndsWith(',') ? "," : string.Empty;
        return json.Replace(line, $"\"{member}\": {value}{ending}", StringComparison.Ordinal);
    }

    [Test]
    public void Read_ValidManifest_ReturnsIt()
    {
        (ThemeManifest? manifest, List<ThemeError> errors) = Read(ThemePackageBuilder.Manifest());

        errors.Should().BeEmpty();
        manifest.Should().BeEquivalentTo(new ThemeManifest
        {
            Id = "aurora",
            Name = "Aurora",
            Author = "Test Author",
            Version = "1.0.0",
            ThemeApi = ThemeApiVersions.Current,
            Variants = [ThemeVariant.Light, ThemeVariant.Dark],
            Previews = new Dictionary<ThemeVariant, string>
            {
                [ThemeVariant.Light] = "preview-light.png",
                [ThemeVariant.Dark] = "preview-dark.png",
            },
        });
    }

    [Test]
    public void Read_CommentsAndTrailingCommasAndOtherMembers_AreAccepted()
    {
        string json = ThemePackageBuilder.Manifest().Replace(
            "\"id\"",
            "// The theme\n\"$schema\": \"x\", \"description\": \"own\", \"id\"",
            StringComparison.Ordinal).Replace(
            "\"preview-dark.png\" }",
            "\"preview-dark.png\", },",
            StringComparison.Ordinal);

        json.Should().Contain("},");
        Read(json).Errors.Should().BeEmpty();
    }

    [TestCase("")]
    [TestCase("[]")]
    [TestCase("{")]
    [TestCase("\"aurora\"")]
    public void Read_NoJsonObject_IsRejected(string json)
    {
        Read(json).Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.ManifestInvalidJson);
    }

    [Test]
    public void Read_DuplicateMember_IsRejected()
    {
        string json = ThemePackageBuilder.Manifest().Replace(
            "\"name\"",
            "\"id\": \"midnight\", \"name\"",
            StringComparison.Ordinal);

        Read(json).Errors.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCode.ManifestInvalidJson);
    }

    [TestCase("id")]
    [TestCase("name")]
    [TestCase("author")]
    [TestCase("version")]
    [TestCase("themeApi")]
    [TestCase("variants")]
    [TestCase("previews")]
    public void Read_MissingMember_IsRejected(string member)
    {
        (ThemeManifest? manifest, List<ThemeError> errors) = Read(Replace(member, "null"));

        manifest.Should().BeNull();
        errors.Should().ContainSingle().Which.Should().Be(
            new ThemeError(ThemeErrorCode.ManifestMissingField, "manifest.json", Field: member));
    }

    [TestCase("id", "\"Aurora\"")]
    [TestCase("id", "\"aurora theme\"")]
    [TestCase("id", "\"../aurora\"")]
    [TestCase("id", "\"aurora-\"")]
    [TestCase("id", "\"a-very-long-theme-id-that-is-longer-than-forty\"")]
    [TestCase("id", "5")]
    [TestCase("name", "\"Aurora\\u0000\"")]
    [TestCase("name", "\"Aur\\u202Eora\"")]
    [TestCase("name", "\"An overly long name of a theme that nobody can read in a picker\"")]
    [TestCase("author", "[\"a\"]")]
    [TestCase("version", "\"1.0 beta\"")]
    [TestCase("version", "\"-1\"")]
    [TestCase("themeApi", "\"5\"")]
    [TestCase("themeApi", "5.5")]
    [TestCase("variants", "[]")]
    [TestCase("variants", "[\"light\", \"light\"]")]
    [TestCase("variants", "[\"sepia\"]")]
    [TestCase("variants", "\"light\"")]
    [TestCase("previews", "\"preview.png\"")]
    public void Read_InvalidMember_IsRejected(string member, string value)
    {
        (ThemeManifest? manifest, List<ThemeError> errors) = Read(Replace(member, value));

        manifest.Should().BeNull();
        ThemeError error = errors.Should().ContainSingle().Subject;
        error.Code.Should().Be(ThemeErrorCode.ManifestInvalidField);
        error.Field.Should().Be(member);
    }

    [TestCase("midnight")]
    [TestCase("clear-glass")]
    public void Read_IdOfAShippedTheme_IsRejected(string id)
    {
        Read(ThemePackageBuilder.Manifest(id)).Errors.Should().ContainSingle().Which.Code
            .Should().Be(ThemeErrorCode.ManifestReservedId);
    }

    [TestCase(ThemeApiVersions.Oldest - 1)]
    [TestCase(ThemeApiVersions.Current + 1)]
    public void Read_UnsupportedThemeApiVersion_IsRejectedWithTheSupportedRange(int version)
    {
        Read(ThemePackageBuilder.Manifest(themeApi: version)).Errors.Should().ContainSingle().Which.Should().Be(
            new ThemeError(
                ThemeErrorCode.UnsupportedApiVersion,
                "manifest.json",
                Field: "themeApi",
                Min: ThemeApiVersions.Oldest,
                Max: ThemeApiVersions.Current));
    }

    [TestCase("{ \"light\": \"preview-light.png\" }", "previews.dark", ThemeErrorCode.ManifestMissingField)]
    [TestCase("{ \"light\": \"../x.png\", \"dark\": \"preview-dark.png\" }", "previews.light", ThemeErrorCode.ManifestInvalidField)]
    public void Read_PreviewOfAVariant_IsChecked(string previews, string field, ThemeErrorCode code)
    {
        (_, List<ThemeError> errors) = Read(Replace("previews", previews));

        errors.Should().ContainSingle().Which.Should().Be(new ThemeError(code, "manifest.json", Field: field));
    }

    [Test]
    public void Read_SingleVariant_NeedsOnlyItsPreview()
    {
        const string Json =
            """
            {
              "id": "night", "name": "Night", "author": "A", "version": "2", "themeApi": 5,
              "variants": ["dark"], "previews": { "dark": "preview.webp" }
            }
            """;

        (ThemeManifest? manifest, List<ThemeError> errors) = Read(Json);

        errors.Should().BeEmpty();
        manifest!.Variants.Should().Equal(ThemeVariant.Dark);
        manifest.Previews.Should().ContainSingle().Which.Value.Should().Be("preview.webp");
    }
}
