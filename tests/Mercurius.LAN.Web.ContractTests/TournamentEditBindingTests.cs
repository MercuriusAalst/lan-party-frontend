using System.Globalization;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentEditBindingTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("nl-BE")]
    public void DateTimeLocalInputValueParsesInEverySupportedUiCulture(string cultureName)
    {
        Assert.True(BindConverter.TryConvertToDateTime(
            "2030-05-06T18:15",
            CultureInfo.GetCultureInfo(cultureName),
            "yyyy-MM-ddTHH:mm",
            out var result));

        Assert.Equal(new DateTime(2030, 5, 6, 18, 15, 0), result);
    }
}
