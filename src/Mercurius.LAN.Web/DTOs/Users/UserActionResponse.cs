namespace Mercurius.LAN.Web.DTOs.Users;

public record UserActionResponse(string Message, bool DeletionPending = false);
