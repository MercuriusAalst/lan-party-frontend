using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class TournamentParticipantDialogStateTests
{
    [Fact]
    public async Task PublicParticipantCardShowsUsernameWithoutPopupInteractionOrOtherProfileFields()
    {
        var participant = ParticipantViewModel.FromUser(new PublicUserDTO
        {
            Id = Guid.NewGuid(),
            Username = "public-player",
            Firstname = "HiddenFirst",
            Lastname = "HiddenLast",
            DiscordId = "hidden-discord",
            SteamId = "hidden-steam",
            RiotId = "hidden-riot",
            DisplayName = "Hidden Display Name"
        });

        var html = await RenderAsync<ParticipantCardComponent>(new Dictionary<string, object?>
        {
            [nameof(ParticipantCardComponent.Participant)] = participant
        });

        Assert.Contains("public-player", html);
        Assert.DoesNotContain("Hidden", html);
        Assert.DoesNotContain("role=\"button\"", html);
        Assert.DoesNotContain("aria-haspopup", html);
    }

    [Fact]
    public async Task ParticipantDetailsAreNotShownWithoutAnAuthorizedMatchCallback()
    {
        var participant = ParticipantViewModel.FromUser(new PublicUserDTO
        {
            Id = Guid.NewGuid(),
            Username = "public-player",
            Firstname = "HiddenFirst",
            Lastname = "HiddenLast",
            DiscordId = "hidden-discord",
            SteamId = "hidden-steam",
            RiotId = "hidden-riot"
        });

        var html = await RenderAsync<ParticipantComponent>(new Dictionary<string, object?>
        {
            [nameof(ParticipantComponent.Participant)] = participant,
            [nameof(ParticipantComponent.CanViewUserDetails)] = true
        });

        Assert.Contains("public-player", html);
        Assert.DoesNotContain("Hidden", html);
        Assert.DoesNotContain("<button", html);
    }

    private static async Task<string> RenderAsync<TComponent>(Dictionary<string, object?> parameters)
        where TComponent : IComponent
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ILocalizationService>(TestLocalizationService.Instance)
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<TComponent>(ParameterView.FromDictionary(parameters));
            return component.ToHtmlString();
        });
    }
}
