using WaveMP.Core.Organizing.Previews;
using WaveMP.Core.Organizing.Templates;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Templates;
using WaveMP.Core.Settings.Validations;
using WaveMP.Service.Dtos;

namespace WaveMP.Service.Mappings;

public static class OrganizingMappingExtensions
{
    public static OrganizingSettingsDto ToDto(this OrganizingSettings settings)
    {
        return new OrganizingSettingsDto
        {
            LayoutTemplate = settings.LayoutTemplate,
            IsFolderBased = LayoutTemplateSyntax.IsFolderBased(settings.LayoutTemplate) == true,
        };
    }

    public static UpdateOrganizingSettingsCommand ToCommand(this UpdateOrganizingSettingsDto request)
    {
        return new UpdateOrganizingSettingsCommand(request.LayoutTemplate);
    }

    /// <summary>
    /// The JSON name of the field in <see cref="UpdateOrganizingSettingsDto"/>, the key of its validation errors.
    /// </summary>
    public static string ToJsonName(this OrganizingSettingsField field)
    {
        return field switch
        {
            OrganizingSettingsField.LayoutTemplate => "layoutTemplate",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown field."),
        };
    }

    public static TemplatePreviewRequest ToRequest(this PreviewLayoutTemplateDto request)
    {
        return new TemplatePreviewRequest(
            request.Template,
            request.TrackIds,
            request.Samples?.Select(sample => new TemplateSample(
                new TemplateValues(
                    sample.Title,
                    sample.Artist,
                    sample.AlbumArtist,
                    sample.Album,
                    sample.Year,
                    sample.TrackNumber,
                    sample.DiscNumber,
                    sample.Genre,
                    sample.IsCompilation),
                sample.Extension)).ToList());
    }

    public static LayoutTemplatePreviewDto ToDto(this TemplatePreviewResult result)
    {
        return new LayoutTemplatePreviewDto
        {
            IsValid = result.IsValid,
            IsFolderBased = result.IsFolderBased,
            Errors = [.. result.Errors.Select(ToDto)],
            Paths = [.. result.Paths.Select(path => new LayoutTemplatePathDto
            {
                TrackId = path.TrackId,
                Folders = path.Path.Folders,
                BaseName = path.Path.BaseName,
                Extension = path.Extension,
                IsTooLong = path.Path.IsTooLong,
            })],
        };
    }

    public static LayoutTemplateErrorDto ToDto(this LayoutTemplateError error)
    {
        return new LayoutTemplateErrorDto
        {
            Code = error.Code.ToDto(),
            Position = error.Position,
            Placeholder = error.Placeholder,
            Max = error.Code == LayoutTemplateErrorCode.TooLong ? LayoutTemplateSyntax.MaxLength : null,
        };
    }

    public static LayoutTemplateErrorCodeDto ToDto(this LayoutTemplateErrorCode code)
    {
        return code switch
        {
            LayoutTemplateErrorCode.Required => LayoutTemplateErrorCodeDto.Required,
            LayoutTemplateErrorCode.TooLong => LayoutTemplateErrorCodeDto.MaxLength,
            LayoutTemplateErrorCode.UnknownPlaceholder => LayoutTemplateErrorCodeDto.UnknownPlaceholder,
            LayoutTemplateErrorCode.UnbalancedBrace => LayoutTemplateErrorCodeDto.UnbalancedBrace,
            LayoutTemplateErrorCode.UnbalancedSection => LayoutTemplateErrorCodeDto.UnbalancedSection,
            LayoutTemplateErrorCode.NestedSection => LayoutTemplateErrorCodeDto.NestedSection,
            LayoutTemplateErrorCode.InvalidNumberFormat => LayoutTemplateErrorCodeDto.InvalidNumberFormat,
            LayoutTemplateErrorCode.MissingTitle => LayoutTemplateErrorCodeDto.MissingTitle,
            LayoutTemplateErrorCode.Rooted => LayoutTemplateErrorCodeDto.Rooted,
            LayoutTemplateErrorCode.EmptyFolder => LayoutTemplateErrorCodeDto.EmptyFolder,
            LayoutTemplateErrorCode.ParentFolder => LayoutTemplateErrorCodeDto.ParentFolder,
            LayoutTemplateErrorCode.FolderInSection => LayoutTemplateErrorCodeDto.FolderInSection,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown code."),
        };
    }
}
