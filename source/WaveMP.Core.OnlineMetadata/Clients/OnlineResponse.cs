using System.Diagnostics.CodeAnalysis;

namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// The answer of an online service: the content, or why there is none.
/// </summary>
/// <param name="Status">The kind of answer.</param>
/// <param name="Value">The content; only with <see cref="OnlineStatus.Found"/>.</param>
public sealed record OnlineResponse<T>(OnlineStatus Status, T? Value)
{
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsFound => Status == OnlineStatus.Found && Value is not null;

    public static OnlineResponse<T> Found(T value)
    {
        return new OnlineResponse<T>(OnlineStatus.Found, value);
    }

    public static OnlineResponse<T> Failed(OnlineStatus status)
    {
        return new OnlineResponse<T>(status, default);
    }
}
