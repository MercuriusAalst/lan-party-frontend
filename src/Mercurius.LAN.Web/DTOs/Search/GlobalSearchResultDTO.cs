namespace Mercurius.LAN.Web.DTOs.Search;

public class GlobalSearchResultDTO
{
    public GlobalSearchResultType Type { get; set; }
    public string DisplayLabel { get; set; } = string.Empty;
    public string? SupportingText { get; set; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public string? TeamName { get; set; }
    public Guid? TournamentId { get; set; }
    public string? ProfilePictureUrl { get; set; }
}
