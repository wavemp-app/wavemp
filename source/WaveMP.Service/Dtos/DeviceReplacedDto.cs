namespace WaveMP.Service.Dtos;

/// <summary>
/// A newer connection registered the same device (e.g. another tab of the same browser). This connection no longer
/// takes part: it gets no more messages of the profile and its calls answer <c>NotRegistered</c>. It may register again
/// to take part once more. Has no members.
/// </summary>
public sealed class DeviceReplacedDto
{
}
