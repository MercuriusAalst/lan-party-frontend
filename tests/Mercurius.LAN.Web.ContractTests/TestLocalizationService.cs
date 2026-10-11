using System.Globalization;
using Mercurius.LAN.Web.Localization;
using Microsoft.JSInterop;

namespace Mercurius.LAN.Web.ContractTests;

internal sealed class TestLocalizationService : ILocalizationService
{
    private static readonly IReadOnlyDictionary<string, string> Resources =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["General.Profile.LoadError"] = "Your profile could not be loaded right now.",
            ["General.Profile.RequestFailed"] = "Request failed.",
            ["General.Profile.Saved"] = "Profile saved.",
            ["General.Profile.UsernameUnavailable"] = "Username is unavailable.",
            ["Feature.tournaments.loadingFailed"] = "Loading failed.",
            ["Feature.tournaments.registrationChangeFailed"] = "The registration change could not be saved.",
            ["Feature.tournaments.registrationIdentityMissing"] = "This registration has no removable participant.",
            ["Feature.tournaments.registrationRemoved"] = "Registration removed.",
            ["Feature.tournaments.savedDetailsUnavailable"] = "Saved, but updated tournament details are temporarily unavailable.",
            ["Feature.tournaments.savedDetailsUnavailableWithReason"] = "Saved, but updated tournament details are unavailable: {0}",
            ["nav.teamInviteTitle"] = "Team invitation",
            ["nav.teamInviteMessage"] = "You have been invited to join {0}.",
            ["nav.rosterSelectionTitle"] = "Tournament roster invitation",
            ["nav.rosterSelectionMessage"] = "{0} selected you for {1}.",
            ["status.tryAgain"] = "Try again"
        };

    public static TestLocalizationService Instance { get; } = new();

    public string CurrentLanguage => ILocalizationService.DefaultLanguage;

    public CultureInfo Culture => CultureInfo.GetCultureInfo(CurrentLanguage);

    public IReadOnlyList<string> SupportedLanguages =>
        [ILocalizationService.DefaultLanguage, ILocalizationService.DutchLanguage];

    public string this[string key] => Resources.TryGetValue(key, out var value) ? value : key;

    public string Get(string key, params object?[] arguments) =>
        arguments.Length == 0 ? this[key] : string.Format(Culture, this[key], arguments);

    public string FormatDate(DateTime value) => value.ToString("d", Culture);

    public string FormatDate(DateTimeOffset value) => value.ToString("d", Culture);

    public string FormatDateTime(DateTime value) => value.ToString("g", Culture);

    public string FormatDateTime(DateTimeOffset value) => value.ToString("g", Culture);

    public string FormatTime(DateTime value) => value.ToString("t", Culture);

    public string FormatTime(DateTimeOffset value) => value.ToString("t", Culture);

    public Task<bool> InitializeAsync(IJSRuntime jsRuntime, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task<bool> SetLanguageAsync(
        IJSRuntime jsRuntime,
        string language,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
