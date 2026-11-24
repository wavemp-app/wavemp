using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Templates;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Scanning.Writers;

/// <summary>
/// The title cleaning the layout template asks for: a file-based template (e.g. <c>{Artist} - {Title}</c>) repeats the
/// artist in every file name, so a leading artist is removed loosely; a folder-based one only removes a prefix that
/// equals the artist tag exactly.
/// </summary>
public static class TitleCleaningModes
{
    public static TitleCleaningMode Of(OrganizingSettings settings)
    {
        return LayoutTemplateSyntax.IsFolderBased(settings.LayoutTemplate) == false
            ? TitleCleaningMode.LooseArtist
            : TitleCleaningMode.ExactArtistTag;
    }
}
