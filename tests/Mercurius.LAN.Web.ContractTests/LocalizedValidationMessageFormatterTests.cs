using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Tournaments;
using Mercurius.LAN.Web.Localization;
using Mercurius.LAN.Web.Models.Tournaments;
using Microsoft.JSInterop;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class LocalizedValidationMessageFormatterTests
{
    private static readonly IReadOnlyDictionary<string, string> FieldLabels = new Dictionary<string, string>
    {
        [nameof(ContactForm.Message)] = "contact.message",
        [nameof(RangeForm.Duration)] = "tournament.duration",
        [nameof(MinimumValueForm.Duration)] = "tournament.duration"
    };

    private static readonly IReadOnlyDictionary<string, string> ValidationMessages = new Dictionary<string, string>
    {
        ["Team tournaments require a team size between 1 and 50."] = "form.teamSizeRange"
    };

    [Fact]
    public void FormatsDutchRequiredAndMinimumLengthMessages()
    {
        var formatter = CreateFormatter();
        var model = new ContactForm();

        var requiredResults = Validate(model);
        Assert.Equal("Bericht is verplicht.", formatter.Format(model, Assert.Single(requiredResults), FieldLabels));

        model.Message = "kort";
        var minimumLengthResults = Validate(model);
        Assert.Equal("Bericht moet minstens 10 tekens bevatten.", formatter.Format(model, Assert.Single(minimumLengthResults), FieldLabels));

        model.Message = new string('x', 4001);
        var maximumLengthResults = Validate(model);
        Assert.Equal("Bericht mag maximaal 4000 tekens bevatten.", formatter.Format(model, Assert.Single(maximumLengthResults), FieldLabels));
    }

    [Fact]
    public void FormatsDutchRangeAndMinimumValueMessages()
    {
        var formatter = CreateFormatter();
        var model = new RangeForm { Duration = 0 };

        var rangeResults = Validate(model);
        Assert.Equal("Duur moet tussen 1 en 1440 liggen.", formatter.Format(model, Assert.Single(rangeResults), FieldLabels));

        var minimumValueModel = new MinimumValueForm { Duration = 0 };
        var minimumValueResults = Validate(minimumValueModel);
        Assert.Equal("Duur moet minstens 1 zijn.", formatter.Format(minimumValueModel, Assert.Single(minimumValueResults), FieldLabels));
    }

    [Fact]
    public void FormatsDutchCustomTeamSizeMessage()
    {
        var formatter = CreateFormatter();
        var model = new CreateTournamentDTO
        {
            AssignedAdminUserId = Guid.Parse("41111111-1111-1111-1111-111111111121"),
            ParticipationMode = ParticipationMode.Team,
            TeamSize = 0
        };
        var result = Assert.Single(model.Validate(new ValidationContext(model)));

        Assert.Equal(
            "Teamtoernooien vereisen een teamgrootte tussen 1 en 50.",
            formatter.Format(model, result, validationMessageKeys: ValidationMessages));
    }

    private static LocalizedValidationMessageFormatter CreateFormatter() =>
        new(new TestLocalizationService(new Dictionary<string, string>
        {
            ["form.requiredField"] = "{0} is verplicht.",
            ["form.minLengthField"] = "{0} moet minstens {1} tekens bevatten.",
            ["form.maxLengthField"] = "{0} mag maximaal {1} tekens bevatten.",
            ["form.rangeField"] = "{0} moet tussen {1} en {2} liggen.",
            ["form.minValueField"] = "{0} moet minstens {1} zijn.",
            ["form.teamSizeRange"] = "Teamtoernooien vereisen een teamgrootte tussen 1 en 50.",
            ["form.invalid"] = "Vul een geldige waarde in.",
            ["contact.message"] = "Bericht",
            ["tournament.duration"] = "Duur"
        }));

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private sealed class ContactForm
    {
        [Required]
        [StringLength(4000, MinimumLength = 10)]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class RangeForm
    {
        [Range(1, 1440)]
        public int Duration { get; set; }
    }

    private sealed class MinimumValueForm
    {
        [Range(1, int.MaxValue)]
        public int Duration { get; set; }
    }

    private sealed class TestLocalizationService(IReadOnlyDictionary<string, string> resources) : ILocalizationService
    {
        public string CurrentLanguage => ILocalizationService.DutchLanguage;
        public CultureInfo Culture => CultureInfo.GetCultureInfo(CurrentLanguage);
        public IReadOnlyList<string> SupportedLanguages => [ILocalizationService.DefaultLanguage, ILocalizationService.DutchLanguage];
        public string this[string key] => resources[key];

        public string Get(string key, params object?[] arguments) =>
            arguments.Length == 0 ? this[key] : string.Format(Culture, this[key], arguments);

        public string FormatDate(DateTime value) => value.ToString("d", Culture);
        public string FormatDate(DateTimeOffset value) => value.ToString("d", Culture);
        public string FormatDateTime(DateTime value) => value.ToString("g", Culture);
        public string FormatDateTime(DateTimeOffset value) => value.ToString("g", Culture);
        public string FormatTime(DateTime value) => value.ToString("t", Culture);
        public string FormatTime(DateTimeOffset value) => value.ToString("t", Culture);
        public Task<bool> InitializeAsync(IJSRuntime jsRuntime, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SetLanguageAsync(IJSRuntime jsRuntime, string language, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
