using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Which file a track uses in a category of the file selection. Automatic: no selection, the file of the highest
/// quality. Selected: the file the admin selected. Missing: the admin selected a file that is gone; the track falls
/// back to the automatic choice until the file is back, another file is selected or the selection is reset.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<FileSelectionStateDto>))]
public enum FileSelectionStateDto
{
    Automatic,
    Selected,
    Missing,
}
