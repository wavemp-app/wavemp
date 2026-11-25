using Microsoft.AspNetCore.SignalR;
using WaveMP.Core.Users.Sessions;
using WaveMP.Service.Extensions;

namespace WaveMP.Service.Hubs;

/// <summary>
/// Checks the session of the connection before every call: a connection outlives the request that authenticated it, so
/// a session that ended in the meantime (profile switched or deleted, PIN set, expired) closes the connection instead
/// of running the call.
/// </summary>
public sealed class DeviceHubSessionFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        HubCallerContext context = invocationContext.Context;
        ISessionService sessions = invocationContext.ServiceProvider.GetRequiredService<ISessionService>();
        ActiveSession? session = context.User is { } user
            ? await sessions.GetActiveAsync(user.GetSessionId(), context.ConnectionAborted)
            : null;
        if (session is null || session.User.Id != context.User!.GetUserId())
        {
            context.Abort();
            throw new HubException(DeviceHubDefaults.SessionEndedError);
        }

        return await next(invocationContext);
    }
}
