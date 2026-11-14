using Blazored.Toast.Services;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Options;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using Refit;
using System.Globalization;

namespace Mercurius.LAN.Web.Components.Pages;

public partial class Info
{
    private ContactMessage _contactModel = new();
    private bool _isSendingContact;
    private UserProfileDTO? _currentUserProfile;
    private string? _currentUserEmail;

    private static readonly IReadOnlyDictionary<string, string> ValidationFieldLabelKeys = new Dictionary<string, string>
    {
        [nameof(ContactMessage.Name)] = "General.Info.Name",
        [nameof(ContactMessage.Contact)] = "General.Info.EmailOrDiscord",
        [nameof(ContactMessage.Message)] = "General.Info.Message"
    };

    [Inject] private IOptions<LanEventOptions> EventOptions { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;
    [Inject] private IUserClient UserClient { get; set; } = null!;
    [Inject] private IContactEmailService ContactEmailService { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;

    private LanEventOptions Event => EventOptions.Value;
    private string? GeneralRegistrationUrl => TicketRegistrationUrlBuilder.GetValidUrl(Event.Registration?.GeneralUrl);
    private string? GetTicketRegistrationUrl(TicketOption ticket)
    {
        var ticketUrl = TicketRegistrationUrlBuilder.GetValidUrl(ticket.RegistrationUrl);
        return ticketUrl is null
            ? null
            : TicketRegistrationUrlBuilder.AddProfileValues(ticketUrl, Event.Registration, _currentUserProfile, _currentUserEmail);
    }

    private string GamerTicketPrice => Event.Tickets
        .FirstOrDefault(ticket => string.Equals(ticket.Kind, "gamer", StringComparison.OrdinalIgnoreCase))
        ?.Price is { } price ? GetTicketPrice(price) : "10 EUR";

    private string GetPackingItemName(PackingItemOptions item) => item.Name switch
    {
        "Computer" => Localization["General.Info.Packing.Computer"],
        "Max 2 monitors" => Localization["General.Info.Packing.Max2Monitors"],
        "Power cables" => Localization["General.Info.Packing.PowerCables"],
        "Power strip" => Localization["General.Info.Packing.PowerStrip"],
        "Headset" => Localization["General.Info.Packing.Headset"],
        "Mouse" => Localization["General.Info.Packing.Mouse"],
        "Keyboard" => Localization["General.Info.Packing.Keyboard"],
        "Mousepad" => Localization["General.Info.Packing.Mousepad"],
        "Phone + charger" => Localization["General.Info.Packing.PhoneCharger"],
        "ID" => Localization["General.Info.Packing.Id"],
        "Gaming chair" => Localization["General.Info.Packing.GamingChair"],
        "Console" => Localization["General.Info.Packing.Console"],
        _ => item.Name
    };

    private string GetPackingItemGroup(PackingItemOptions item) => item.Group switch
    {
        "Gaming essential" => Localization["General.Info.PackingGroup.GamingEssential"],
        "Basic" => Localization["General.Info.PackingGroup.Basic"],
        "Optional" => Localization["General.Info.PackingGroup.Optional"],
        _ => item.Group
    };

    private string GetTicketName(TicketOption ticket) =>
        string.Equals(ticket.Kind, "chair", StringComparison.OrdinalIgnoreCase) && ticket.Name == "Comfort"
            ? Localization["General.Info.Ticket.GamerChair"]
            : string.Equals(ticket.Kind, "gamer", StringComparison.OrdinalIgnoreCase) && ticket.Name == "Basic"
                ? Localization["General.Info.Ticket.Gamer"]
                : string.Equals(ticket.Kind, "visitor", StringComparison.OrdinalIgnoreCase) && ticket.Name == "Fun"
                    ? Localization["General.Info.Ticket.Visitor"]
                    : ticket.Name;

    private string GetTicketDescription(TicketOption ticket) =>
        string.Equals(ticket.Kind, "chair", StringComparison.OrdinalIgnoreCase) && ticket.Description == "Access to all competitions + a gaming chair"
            ? Localization["General.Info.Ticket.GamerChairDescription"]
            : string.Equals(ticket.Kind, "gamer", StringComparison.OrdinalIgnoreCase) && ticket.Description == "Access to all competitions"
                ? Localization["General.Info.Ticket.GamerDescription"]
                : string.Equals(ticket.Kind, "visitor", StringComparison.OrdinalIgnoreCase) && ticket.Description == "Access to the fun and console tournaments"
                    ? Localization["General.Info.Ticket.VisitorDescription"]
                    : ticket.Description;

    private string GetTicketPrice(string price) => price == "Free"
        ? Localization["General.Info.Ticket.Free"]
        : price;

    protected override async Task OnInitializedAsync()
    {
        var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        if (authenticationState.User.Identity?.IsAuthenticated != true)
            return;

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var profile = await UserClient.GetCurrentUserProfileAsync(timeout.Token);
            _currentUserProfile = profile.User;
            _currentUserEmail = profile.Email;
        }
        catch (ApiException)
        {
            _currentUserProfile = null;
            _currentUserEmail = null;
        }
        catch (HttpRequestException)
        {
            _currentUserProfile = null;
            _currentUserEmail = null;
        }
        catch (OperationCanceledException)
        {
            _currentUserProfile = null;
            _currentUserEmail = null;
        }
    }

    private string GetMenuSectionName(string name) => name switch
    {
        "Drinks" => Localization["General.Info.MenuSection.Drinks"],
        "Snacks" => Localization["General.Info.MenuSection.Snacks"],
        _ => name
    };

    private string GetMenuItemName(string name) => name switch
    {
        "Water (still/sparkling)" => Localization["General.Info.MenuItem.Water"],
        "Fuze Tea" => Localization["General.Info.MenuItem.FuzeTea"],
        "Cola (zero)" => Localization["General.Info.MenuItem.ColaZero"],
        "Monster Energy" => Localization["General.Info.MenuItem.MonsterEnergy"],
        "Beer" => Localization["General.Info.MenuItem.Beer"],
        "Bell For (6.2°)" => Localization["General.Info.MenuItem.BellFor"],
        "Croque Monsieur" => Localization["General.Info.MenuItem.CroqueMonsieur"],
        "Curryworst" => Localization["General.Info.MenuItem.Curryworst"],
        "Bitterballen (6 pcs)" => Localization["General.Info.MenuItem.Bitterballen"],
        "Chicken nuggets (6 pcs)" => Localization["General.Info.MenuItem.ChickenNuggets"],
        _ => name
    };

    private string GetSauceName(string name) => name switch
    {
        "Ketchup (tomato / curry)" => Localization["General.Info.Sauce.Ketchup"],
        "Mayonnaise" => Localization["General.Info.Sauce.Mayonnaise"],
        "Andalouse" => Localization["General.Info.Sauce.Andalouse"],
        _ => name
    };

    private string GetSocialLinkLabel(string label) => label switch
    {
        "Discord" => Localization["General.Info.Social.Discord"],
        "Facebook" => Localization["General.Info.Social.Facebook"],
        "Instagram" => Localization["General.Info.Social.Instagram"],
        _ => label
    };

    private string GetSocialLinkAriaLabel(string ariaLabel) => ariaLabel switch
    {
        "Join the Mercurius Aalst Discord server" => Localization["General.Info.Social.DiscordAria"],
        "Open the Mercurius Aalst Facebook page" => Localization["General.Info.Social.FacebookAria"],
        "Open the mercurius.aalst Instagram profile" => Localization["General.Info.Social.InstagramAria"],
        _ => ariaLabel
    };

    private async Task SendContactAsync()
    {
        if(_isSendingContact)
            return;

        _isSendingContact = true;
        try
        {
            await ContactEmailService.SendAsync(_contactModel);
            ToastService.ShowSuccess(Localization["General.Info.ContactSuccess"]);
            _contactModel = new ContactMessage();
        }
        catch(InvalidOperationException exception)
        {
            ToastService.ShowError(exception.Message);
        }
        catch(Exception)
        {
            ToastService.ShowError(Localization["General.Info.ContactFailure"]);
        }
        finally
        {
            _isSendingContact = false;
        }
    }

    private static string FormatMenuPrice(decimal price) => price % 1 == 0
        ? $"{price.ToString("0", CultureInfo.InvariantCulture)} EUR"
        : $"{price.ToString("0.00", CultureInfo.InvariantCulture)} EUR";
}
