using Blazored.Toast.Services;
using Mercurius.LAN.Web.Options;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.Globalization;

namespace Mercurius.LAN.Web.Components.Pages;

public partial class Info : IAsyncDisposable
{
    private ContactMessage _contactModel = new();
    private bool _isSendingContact;
    private IJSObjectReference? _ticketModule;

    private static readonly IReadOnlyDictionary<string, string> ValidationFieldLabelKeys = new Dictionary<string, string>
    {
        [nameof(ContactMessage.Name)] = "General.Info.Name",
        [nameof(ContactMessage.Contact)] = "General.Info.EmailOrDiscord",
        [nameof(ContactMessage.Message)] = "General.Info.Message"
    };

    [Inject] private IOptions<LanEventOptions> EventOptions { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;
    [Inject] private IContactEmailService ContactEmailService { get; set; } = null!;
    [Inject] private IToastService ToastService { get; set; } = null!;

    private LanEventOptions Event => EventOptions.Value;
    private string? GeneralRegistrationUrl => TicketRegistrationUrlBuilder.GetValidUrl(Event.Registration.GeneralUrl);
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        try
        {
            _ticketModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/info-ticket-registration.js");
            await _ticketModule.InvokeVoidAsync("attach");
        }
        catch (JSException)
        {
            _ticketModule = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_ticketModule is not null)
        {
            try
            {
                await _ticketModule.InvokeVoidAsync("detach");
                await _ticketModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
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
        "Croque Monsieur" => Localization["General.Info.MenuItem.CroqueMonsieur"],
        "Fryer Snacks Party Mix (8pcs)" => Localization["General.Info.MenuItem.FryerSnacksPartyMix"],
        "Boulet" => Localization["General.Info.MenuItem.Boulet"],
        "Boulet Special" => Localization["General.Info.MenuItem.BouletSpecial"],
        "Curryworst" => Localization["General.Info.MenuItem.Curryworst"],
        "Curryworst special" => Localization["General.Info.MenuItem.CurryworstSpecial"],
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
