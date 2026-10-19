namespace Mercurius.LAN.Web.DTOs.Users;

public sealed class UserSearchResponseDTO
{
    public IReadOnlyList<UserSearchResultDTO> Results { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

public sealed class UserSearchResultDTO
{
    public Guid Id { get; init; }
    public string Type { get; init; } = "user";
    public string Username { get; init; } = string.Empty;
    public string DisplayLabel { get; init; } = string.Empty;
    public string? SupportingText { get; init; }
    public string? ProfilePictureUrl { get; init; }
}
