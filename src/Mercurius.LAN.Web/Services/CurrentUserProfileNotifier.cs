namespace Mercurius.LAN.Web.Services;

public sealed class CurrentUserProfileNotifier : ICurrentUserProfileNotifier
{
    public event Func<Task>? Changed;

    public async Task NotifyChangedAsync()
    {
        var handlers = Changed;
        if(handlers is null)
            return;

        foreach(var handler in handlers.GetInvocationList().Cast<Func<Task>>())
            await handler();
    }
}
