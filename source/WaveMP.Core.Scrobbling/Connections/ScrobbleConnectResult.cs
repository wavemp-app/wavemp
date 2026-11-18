using System.Diagnostics.CodeAnalysis;

namespace WaveMP.Core.Scrobbling.Connections;

/// <summary>
/// The connected service, or why it was not connected.
/// </summary>
public sealed record ScrobbleConnectResult(ScrobbleConnectStatus Status, ScrobbleServiceState? State)
{
    [MemberNotNullWhen(true, nameof(State))]
    public bool IsConnected => Status == ScrobbleConnectStatus.Connected && State is not null;
}
