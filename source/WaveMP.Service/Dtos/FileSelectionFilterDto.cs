using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Which tracks the overview of the file selection lists. SeveralAudio, SeveralVideo and SeveralCovers: more than one
/// file of the category; Choices: more than one file in any category; Selected: at least one selection of the admin
/// (also a missing one); Missing: a selection whose file is gone.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<FileSelectionFilterDto>))]
public enum FileSelectionFilterDto
{
    All,
    SeveralAudio,
    SeveralVideo,
    SeveralCovers,
    Choices,
    Selected,
    Missing,
}
