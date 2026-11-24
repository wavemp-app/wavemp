using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// State of an organize operation: Planning while the preview is computed, Previewed until it is applied or expires,
/// Queued and Running while files move, then Completed, Stopped (an item failed), Canceled, Failed (nothing changed),
/// Interrupted (the server stopped; the interrupted item was put back) or Expired (the preview was not applied in
/// time).
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<OrganizeOperationStatusDto>))]
public enum OrganizeOperationStatusDto
{
    Planning,

    Previewed,

    Queued,

    Running,

    Completed,

    Stopped,

    Canceled,

    Failed,

    Interrupted,

    Expired,
}
