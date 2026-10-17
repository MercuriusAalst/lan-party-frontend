namespace Mercurius.LAN.Web.Services;

/// <summary>
/// Signals that the authenticated player's own profile changed, so shared identity surfaces
/// reload the current profile instead of holding a stale reference.
/// </summary>
public interface ICurrentUserProfileNotifier
{
    event Func<Task>? Changed;

    Task NotifyChangedAsync();
}
