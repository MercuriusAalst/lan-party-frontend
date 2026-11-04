using Mercurius.LAN.Web.Components.Pages;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Options;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Claims;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public class TicketRegistrationUrlBuilderTests
{
    private const string TicketUrl = "https://docs.google.com/forms/d/e/example/viewform?usp=pp_url&entry.327008568=Basic+%E2%80%93+%E2%82%AC+10";

    [Fact]
    public void AddProfileValuesPreservesTicketAndEncodesUnicodeAndReservedCharacters()
    {
        var options = new RegistrationFormOptions
        {
            FirstNameEntryId = "171194938",
            LastNameEntryId = "727955101",
            UsernameEntryId = "106529928"
        };
        var profile = new UserProfileDTO
        {
            Firstname = "Zoë + Kai",
            Lastname = "O'Neil & Sons",
            Username = "player?one=value"
        };

        var result = TicketRegistrationUrlBuilder.AddProfileValues(TicketUrl, options, profile);
        var query = QueryHelpers.ParseQuery(new Uri(result).Query);

        Assert.Equal("Basic – € 10", query["entry.327008568"].ToString());
        Assert.Equal("Zoë + Kai", query["entry.171194938"].ToString());
        Assert.Equal("O'Neil & Sons", query["entry.727955101"].ToString());
        Assert.Equal("player?one=value", query["entry.106529928"].ToString());
        Assert.False(query.Keys.Any(key => key.Contains("email", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void AddProfileValuesOmitsMissingInvalidAndTicketCollidingMappings()
    {
        var options = new RegistrationFormOptions
        {
            FirstNameEntryId = "327008568",
            LastNameEntryId = "727955101&entry.327008568=wrong",
            UsernameEntryId = "106529928"
        };
        var profile = new UserProfileDTO { Firstname = "Ada", Lastname = "Lovelace", Username = " " };

        var result = TicketRegistrationUrlBuilder.AddProfileValues(TicketUrl, options, profile);
        var query = QueryHelpers.ParseQuery(new Uri(result).Query);

        Assert.Single(query["entry.327008568"]);
        Assert.Equal("Basic – € 10", query["entry.327008568"].ToString());
        Assert.False(query.ContainsKey("entry.727955101&entry.327008568=wrong"));
        Assert.False(query.ContainsKey("entry.106529928"));
    }

    [Fact]
    public void IsSameAuthenticatedUserRejectsAnonymousAndChangedIdentities()
    {
        var userA = AuthenticatedUser("user-a");
        var userB = AuthenticatedUser("user-b");
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.True(TicketRegistrationUrlBuilder.IsSameAuthenticatedUser(userA, AuthenticatedUser("user-a")));
        Assert.False(TicketRegistrationUrlBuilder.IsSameAuthenticatedUser(userA, userB));
        Assert.False(TicketRegistrationUrlBuilder.IsSameAuthenticatedUser(userA, anonymous));
        Assert.False(TicketRegistrationUrlBuilder.IsSameAuthenticatedUser(anonymous, userB));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://example.test/form")]
    public void GetValidUrlRejectsMissingOrNonHttpsUrls(string? value) =>
        Assert.Null(TicketRegistrationUrlBuilder.GetValidUrl(value));

    private static ClaimsPrincipal AuthenticatedUser(string id) => new(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "test"));
}
