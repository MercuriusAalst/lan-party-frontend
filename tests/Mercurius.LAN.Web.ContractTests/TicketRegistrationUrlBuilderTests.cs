using Mercurius.LAN.Web.Components.Pages;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Options;
using Microsoft.AspNetCore.WebUtilities;
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
            UsernameEntryId = "106529928",
            EmailEntryId = "793859948"
        };
        var profile = new UserProfileDTO
        {
            Firstname = "Zoë + Kai",
            Lastname = "O'Neil & Sons",
            Username = "player?one=value"
        };

        var result = TicketRegistrationUrlBuilder.AddProfileValues(TicketUrl, options, profile, "zoe+lan@example.test");
        var query = QueryHelpers.ParseQuery(new Uri(result).Query);

        Assert.Equal("Basic – € 10", query["entry.327008568"].ToString());
        Assert.Equal("Zoë + Kai", query["entry.171194938"].ToString());
        Assert.Equal("O'Neil & Sons", query["entry.727955101"].ToString());
        Assert.Equal("player?one=value", query["entry.106529928"].ToString());
        Assert.Equal("zoe+lan@example.test", query["entry.793859948"].ToString());
    }

    [Fact]
    public void AddProfileValuesOmitsMissingInvalidAndTicketCollidingMappings()
    {
        var options = new RegistrationFormOptions
        {
            FirstNameEntryId = "327008568",
            LastNameEntryId = "727955101&entry.327008568=wrong",
            UsernameEntryId = "106529928",
            EmailEntryId = "327008568"
        };
        var profile = new UserProfileDTO { Firstname = "Ada", Lastname = "Lovelace", Username = " " };

        var result = TicketRegistrationUrlBuilder.AddProfileValues(TicketUrl, options, profile, "collision@example.test");
        var query = QueryHelpers.ParseQuery(new Uri(result).Query);

        Assert.Single(query["entry.327008568"]);
        Assert.Equal("Basic – € 10", query["entry.327008568"].ToString());
        Assert.False(query.ContainsKey("entry.727955101&entry.327008568=wrong"));
        Assert.False(query.ContainsKey("entry.106529928"));
    }

    [Fact]
    public void AddProfileValuesOmitsEmailWhenTheCurrentApplicationProfileHasNoEmail()
    {
        var options = new RegistrationFormOptions { EmailEntryId = "793859948" };

        var result = TicketRegistrationUrlBuilder.AddProfileValues(TicketUrl, options, new UserProfileDTO(), email: null);
        var query = QueryHelpers.ParseQuery(new Uri(result).Query);

        Assert.False(query.ContainsKey("entry.793859948"));
    }

    [Fact]
    public void AddProfileValuesReturnsTicketUrlWhenRegistrationOptionsAreNull() =>
        Assert.Equal(TicketUrl, TicketRegistrationUrlBuilder.AddProfileValues(TicketUrl, null, new UserProfileDTO()));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://example.test/form")]
    public void GetValidUrlRejectsMissingOrNonHttpsUrls(string? value) =>
        Assert.Null(TicketRegistrationUrlBuilder.GetValidUrl(value));
}
