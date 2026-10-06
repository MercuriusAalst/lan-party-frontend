namespace Mercurius.LAN.Web.DTOs.Matches;

public sealed class MatchOpponentProfileDTO
{
    public string Username { get; set; } = string.Empty;
    public string? Firstname { get; set; }
    public string? Lastname { get; set; }
    public string? DiscordId { get; set; }
    public string? SteamId { get; set; }
    public string? RiotId { get; set; }
}
