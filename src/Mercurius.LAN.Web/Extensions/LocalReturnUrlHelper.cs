namespace Mercurius.LAN.Web.Extensions;

internal static class LocalReturnUrlHelper
{
    private static readonly string[] ProtectedRoutePaths =
    [
        "/profile",
        "/complete-profile",
        "/users",
        "/teams/manage",
        "/admin/sponsors",
        "/admin/featured-tournaments",
        "/account/logout"
    ];

    public static string GetSafeLocalReturnUrl(string? returnUrl)
    {
        if(string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        if(returnUrl.Any(char.IsControl))
            return "/";

        if(!Uri.TryCreate(returnUrl, UriKind.Relative, out _))
            return "/";

        if(!returnUrl.StartsWith("/", StringComparison.Ordinal) ||
           returnUrl.StartsWith("//", StringComparison.Ordinal) ||
           returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }

    public static string GetSafeLogoutReturnUrl(string? returnUrl)
    {
        var safeReturnUrl = GetSafeLocalReturnUrl(returnUrl);
        var path = safeReturnUrl.Split(['?', '#'], 2)[0];

        var canonicalPath = GetUnambiguousPath(path);
        if(canonicalPath is null)
            return "/";

        var normalizedPath = canonicalPath.TrimEnd('/');

        return ProtectedRoutePaths.Any(protectedPath =>
            string.Equals(normalizedPath, protectedPath, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(protectedPath + "/", StringComparison.OrdinalIgnoreCase))
            ? "/"
            : safeReturnUrl;
    }

    private static string? GetUnambiguousPath(string path)
    {
        if(path.Contains('\\') || path.Split('/').Any(segment => segment is "." or ".."))
            return null;

        for(var index = 0; index < path.Length; index++)
        {
            if(path[index] != '%')
                continue;

            if(index + 2 >= path.Length ||
               !Uri.IsHexDigit(path[index + 1]) ||
               !Uri.IsHexDigit(path[index + 2]))
            {
                return null;
            }

            var decoded = Convert.ToByte(path.Substring(index + 1, 2), 16);
            if(decoded <= 0x1F || decoded == 0x7F ||
               decoded is (byte)'.' or (byte)'/' or (byte)'\\' or (byte)'%' or (byte)'?' or (byte)'#')
            {
                return null;
            }

            index += 2;
        }

        var unescapedPath = Uri.UnescapeDataString(path);
        return unescapedPath.Contains('%') || unescapedPath.Any(char.IsControl)
            ? null
            : unescapedPath;
    }
}
