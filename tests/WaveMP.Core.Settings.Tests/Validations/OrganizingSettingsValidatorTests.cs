using AwesomeAssertions;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Templates;
using WaveMP.Core.Settings.Validations;

namespace WaveMP.Core.Settings.Tests.Validations;

[TestFixture]
public class OrganizingSettingsValidatorTests
{
    [Test]
    public void Validate_TheDefaults_AreValid()
    {
        OrganizingSettingsValidator.Validate(new OrganizingSettings()).Should().BeEmpty();
        new OrganizingSettings().LayoutTemplate.Should().Be("{AlbumArtist}/{Album}/{TrackNumber:00} {Title}");
    }

    [Test]
    public void Normalize_TrimsTheTemplate()
    {
        OrganizingSettings settings = OrganizingSettingsValidator.Normalize(
            new OrganizingSettings { LayoutTemplate = "  {Artist} - {Title}\t" });

        settings.LayoutTemplate.Should().Be("{Artist} - {Title}");
    }

    [Test]
    public void Validate_AnEmptyTemplate_IsRequired()
    {
        OrganizingSettingsValidator.Validate(new OrganizingSettings { LayoutTemplate = string.Empty })
            .Should().Equal(new OrganizingSettingsValidationError(
                OrganizingSettingsField.LayoutTemplate,
                SettingsErrorReason.Empty));
    }

    [Test]
    public void Validate_ATooLongTemplate_NamesTheLimit()
    {
        OrganizingSettings settings = new() { LayoutTemplate = "{Title}" + new string('x', 600) };

        OrganizingSettingsValidator.Validate(settings).Should().Equal(new OrganizingSettingsValidationError(
            OrganizingSettingsField.LayoutTemplate,
            SettingsErrorReason.TooLong,
            LayoutTemplateSyntax.MaxLength));
    }

    [Test]
    public void Validate_AnInvalidTemplate_CarriesItsProblems()
    {
        OrganizingSettingsValidationError error = OrganizingSettingsValidator
            .Validate(new OrganizingSettings { LayoutTemplate = "{Artist}/{Album}" })
            .Should().ContainSingle().Which;

        error.Field.Should().Be(OrganizingSettingsField.LayoutTemplate);
        error.Reason.Should().Be(SettingsErrorReason.Invalid);
        error.TemplateErrors.Should().Equal(new LayoutTemplateError(LayoutTemplateErrorCode.MissingTitle, 9));
    }
}
