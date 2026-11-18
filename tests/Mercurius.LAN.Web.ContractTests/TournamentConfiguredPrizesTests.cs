using Mercurius.LAN.Web.Extensions;
using Mercurius.LAN.Web.Models.Tournaments;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentConfiguredPrizesTests
{
    [Fact]
    public void ConfiguredPrizesKeepPlaceOrderSkipBlankPlacesAndTrim()
    {
        var tournament = new Tournament { FirstPlacePrize = "Keyboard", SecondPlacePrize = "   ", ThirdPlacePrize = " Mouse " };

        Assert.Equal([(1, "Keyboard"), (3, "Mouse")], tournament.GetConfiguredPrizes());
    }

    [Fact]
    public void TournamentWithoutPrizesHasNoConfiguredPrizes()
    {
        Assert.Empty(new Tournament().GetConfiguredPrizes());
    }
}
