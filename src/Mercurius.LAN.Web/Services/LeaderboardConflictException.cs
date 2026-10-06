namespace Mercurius.LAN.Web.Services;

/// <summary>
/// Signals that a leaderboard attempt was changed by another writer since it was loaded.
/// The live API reports the same condition as an HTTP 409 conflict.
/// </summary>
public sealed class LeaderboardConflictException : InvalidOperationException
{
    public LeaderboardConflictException()
        : base("The leaderboard attempt changed since it was loaded.")
    {
    }
}
