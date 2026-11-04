using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Options;
using Microsoft.AspNetCore.WebUtilities;

namespace Mercurius.LAN.Web.Components.Pages;

internal static class TicketRegistrationUrlBuilder
{
    public static string? GetValidUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? value
            : null;

    public static string AddProfileValues(string url, RegistrationFormOptions options, UserProfileDTO? profile)
    {
        var existingKeys = QueryHelpers.ParseQuery(new Uri(url).Query).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var values = new Dictionary<string, string?>();
        AddPrefill(values, existingKeys, options.FirstNameEntryId, profile?.Firstname);
        AddPrefill(values, existingKeys, options.LastNameEntryId, profile?.Lastname);
        AddPrefill(values, existingKeys, options.UsernameEntryId, profile?.Username);
        return values.Count == 0 ? url : QueryHelpers.AddQueryString(url, values);
    }

    private static void AddPrefill(
        IDictionary<string, string?> values,
        ISet<string> existingKeys,
        string? entryId,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(entryId) ||
            entryId.Any(character => !char.IsAsciiDigit(character)) ||
            string.IsNullOrWhiteSpace(value))
            return;

        var key = $"entry.{entryId}";
        if (existingKeys.Add(key))
            values[key] = value;
    }
}
