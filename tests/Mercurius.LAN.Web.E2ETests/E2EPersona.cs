namespace Mercurius.LAN.Web.E2ETests;

public sealed record E2EPersona(
    Guid? UserId,
    string Subject,
    string Username,
    string Name,
    string Email,
    bool IsAdmin,
    bool EmailVerified,
    bool HasPasswordResetIdentity,
    IReadOnlyList<string> Roles);
