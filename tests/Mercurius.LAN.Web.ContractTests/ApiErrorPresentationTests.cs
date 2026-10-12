using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.APIClients;
using Mercurius.LAN.Web.Components.Pages.Admin;
using Mercurius.LAN.Web.Components.Pages.Tournaments.Leaderboard;
using Mercurius.LAN.Web.Components.Pages.Users;
using Mercurius.LAN.Web.DTOs.Sponsors;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Localization;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using Refit;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class ApiErrorPresentationTests
{
    [Fact]
    public async Task ProfileSave_ShowsTheParsedProblemDetailsMessageInsteadOfRawJson()
    {
        var page = new Profile();
        SetProperty(page, typeof(Profile), "Localization", FallbackLocalization.Instance);
        var toastProxy = DispatchProxy.Create<IToastService, RecordingToastProxy>();
        var toast = (RecordingToastProxy)(object)toastProxy;
        SetProperty(page, typeof(Profile), "ToastService", toastProxy);

        ReadField<UpdateUserProfileRequest>(page, typeof(Profile), "_model").Username = "NewName";
        SetField(page, typeof(Profile), "_originalUsername", "CaptainOne");

        var userClientProxy = DispatchProxy.Create<IUserClient, RecordingUserClientProxy>();
        var userClient = (RecordingUserClientProxy)(object)userClientProxy;
        userClient.UpdateException = await CreateApiException(
            HttpStatusCode.Conflict,
            """{"code":"username_taken","message":"That username is already taken."}""",
            "application/problem+json");
        SetProperty(page, typeof(Profile), "UserClient", userClientProxy);

        await InvokeAsync(page, typeof(Profile), "SaveAsync");

        Assert.Equal(new[] { "That username is already taken." }, toast.Errors);
    }

    [Fact]
    public async Task CompleteProfileSave_ShowsTheParsedValidationMessageInsteadOfRawJson()
    {
        var page = new CompleteProfile();
        SetProperty(page, typeof(CompleteProfile), "Localization", FallbackLocalization.Instance);
        var toastProxy = DispatchProxy.Create<IToastService, RecordingToastProxy>();
        var toast = (RecordingToastProxy)(object)toastProxy;
        SetProperty(page, typeof(CompleteProfile), "ToastService", toastProxy);

        var model = ReadField<CompleteUserProfileRequest>(page, typeof(CompleteProfile), "_model");
        model.Username = "NewName";
        model.Firstname = "Ada";
        model.Lastname = "Lovelace";
        var editContext = new EditContext(model);
        SetField(page, typeof(CompleteProfile), "_editContext", editContext);
        SetField(page, typeof(CompleteProfile), "_validationMessageStore", new ValidationMessageStore(editContext));

        var userClientProxy = DispatchProxy.Create<IUserClient, RecordingUserClientProxy>();
        var userClient = (RecordingUserClientProxy)(object)userClientProxy;
        userClient.CompleteException = await CreateApiException(
            HttpStatusCode.BadRequest,
            """{"type":"about:blank","title":"One or more validation errors occurred.","status":400,"errors":{"Username":["That username is already taken."]}}""",
            "application/problem+json");
        SetProperty(page, typeof(CompleteProfile), "UserClient", userClientProxy);

        await InvokeAsync(page, typeof(CompleteProfile), "SaveAsync");

        Assert.Equal(new[] { "That username is already taken." }, toast.Errors);
    }

    [Fact]
    public async Task SponsorSave_ShowsTheParsedValidationMessageInsteadOfRawJson()
    {
        var page = new SponsorManagement();
        SetProperty(page, typeof(SponsorManagement), "Localization", FallbackLocalization.Instance);
        var toastProxy = DispatchProxy.Create<IToastService, RecordingToastProxy>();
        var toast = (RecordingToastProxy)(object)toastProxy;
        SetProperty(page, typeof(SponsorManagement), "ToastService", toastProxy);
        SetField(page, typeof(SponsorManagement), "_selectedSponsor", new SponsorManagementDTO
        {
            Name = "Acme",
            InfoUrl = "https://acme.test"
        });

        var sponsorProxy = DispatchProxy.Create<ISponsorService, RecordingSponsorServiceProxy>();
        var sponsor = (RecordingSponsorServiceProxy)(object)sponsorProxy;
        sponsor.CreateException = await CreateApiException(
            HttpStatusCode.BadRequest,
            """{"type":"about:blank","title":"One or more validation errors occurred.","status":400,"errors":{"Name":["The name is already in use."]}}""",
            "application/problem+json");
        SetProperty(page, typeof(SponsorManagement), "SponsorService", sponsorProxy);

        await InvokeAsync(page, typeof(SponsorManagement), "HandleSubmit");

        Assert.Equal(new[] { "The name is already in use." }, toast.Errors);
    }

    [Fact]
    public async Task SponsorDelete_FallsBackToLocalizedCopyWhenTheBodyHasNoUserFacingMessage()
    {
        var page = new SponsorManagement();
        SetProperty(page, typeof(SponsorManagement), "Localization", FallbackLocalization.Instance);
        var toastProxy = DispatchProxy.Create<IToastService, RecordingToastProxy>();
        var toast = (RecordingToastProxy)(object)toastProxy;
        SetProperty(page, typeof(SponsorManagement), "ToastService", toastProxy);
        SetField(page, typeof(SponsorManagement), "_selectedSponsor", new SponsorManagementDTO
        {
            Id = 7,
            Name = "Acme",
            InfoUrl = "https://acme.test"
        });

        var sponsorProxy = DispatchProxy.Create<ISponsorService, RecordingSponsorServiceProxy>();
        var sponsor = (RecordingSponsorServiceProxy)(object)sponsorProxy;
        sponsor.DeleteException = await CreateApiException(
            HttpStatusCode.Conflict,
            """{"status":409,"traceId":"00-abc"}""",
            "application/problem+json");
        SetProperty(page, typeof(SponsorManagement), "SponsorService", sponsorProxy);

        await InvokeAsync(page, typeof(SponsorManagement), "DeleteSponsor");

        Assert.Equal(new[] { "common.unexpectedError" }, toast.Errors);
    }

    [Fact]
    public async Task LeaderboardApiErrorsUseParsedMessagesAndLocalizedFallbacks()
    {
        var dialog = new RecordLeaderboardAttemptDialog();
        SetProperty(dialog, typeof(RecordLeaderboardAttemptDialog), "Localization", FallbackLocalization.Instance);

        var problemJson = """{"status":500,"detail":"The leaderboard could not be updated."}""";
        var unstructuredProblemJson = """{"status":500,"traceId":"00-abc"}""";

        Assert.Equal(
            "The leaderboard could not be updated.",
            ResolveLeaderboardError(dialog, await CreateApiException(HttpStatusCode.InternalServerError, problemJson, "application/problem+json"), null));
        Assert.Equal(
            "Feature.leaderboard.submitFailed",
            ResolveLeaderboardError(dialog, await CreateApiException(HttpStatusCode.InternalServerError, unstructuredProblemJson, "application/problem+json"), null));
        Assert.Equal(
            "Feature.leaderboard.loadFailed",
            ResolveLeaderboardError(dialog, await CreateApiException(HttpStatusCode.InternalServerError, unstructuredProblemJson, "application/problem+json"), "Feature.leaderboard.loadFailed"));
        Assert.Equal(
            "Feature.leaderboard.unauthorized",
            ResolveLeaderboardError(dialog, await CreateApiException(HttpStatusCode.Forbidden, "{}", "application/problem+json"), null));
        Assert.Equal(
            "Feature.leaderboard.notFound",
            ResolveLeaderboardError(dialog, await CreateApiException(HttpStatusCode.NotFound, "{}", "application/problem+json"), null));

        var raw = await CreateApiException(HttpStatusCode.InternalServerError, problemJson, "application/problem+json");
        var parsed = ResolveLeaderboardError(dialog, raw, null);
        Assert.NotEqual(problemJson, parsed);
        Assert.DoesNotContain("{", parsed, StringComparison.Ordinal);

        dialog.Dispose();
    }

    private static string ResolveLeaderboardError(RecordLeaderboardAttemptDialog dialog, ApiException exception, string? fallback)
    {
        var method = typeof(RecordLeaderboardAttemptDialog)
            .GetMethod("ResolveApiError", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResolveApiError was not found.");

        return (string)(method.Invoke(dialog, [exception, fallback])
            ?? throw new InvalidOperationException("ResolveApiError did not return a message."));
    }

    private static async Task InvokeAsync(object instance, Type declaringType, string methodName, params object?[] arguments)
    {
        var method = declaringType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found on {declaringType.Name}.");

        if(method.Invoke(instance, arguments) is Task task)
            await task;
    }

    private static void SetField(object instance, Type declaringType, string name, object? value) =>
        (declaringType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{name}' was not found on {declaringType.Name}."))
        .SetValue(instance, value);

    private static T ReadField<T>(object instance, Type declaringType, string name) =>
        (T)(declaringType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(instance)
            ?? throw new InvalidOperationException($"Field '{name}' was not found on {declaringType.Name}."));

    private static void SetProperty(object instance, Type declaringType, string name, object? value) =>
        (declaringType.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Property '{name}' was not found on {declaringType.Name}."))
        .SetValue(instance, value);

    private static async Task<ApiException> CreateApiException(HttpStatusCode statusCode, string content, string mediaType)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.test/api");
        using var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, mediaType)
        };

        return await ApiException.Create(request, HttpMethod.Post, response, new RefitSettings(), innerException: null);
    }

    public class RecordingToastProxy : DispatchProxy
    {
        public List<string> Errors { get; } = [];
        public List<string> Warnings { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var message = args is { Length: > 0 } ? args[0] as string : null;
            if(message is not null)
            {
                if(targetMethod?.Name == nameof(IToastService.ShowError))
                    Errors.Add(message);
                else if(targetMethod?.Name == nameof(IToastService.ShowWarning))
                    Warnings.Add(message);
            }

            return null;
        }
    }

    public class RecordingUserClientProxy : DispatchProxy
    {
        public ApiException? UpdateException { get; set; }
        public ApiException? CompleteException { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IUserClient.CheckUsernameAvailabilityAsync) =>
                    Task.FromResult(new UsernameAvailabilityResponse { IsAvailable = true }),
                nameof(IUserClient.UpdateCurrentUserProfileAsync) when UpdateException is not null => throw UpdateException,
                nameof(IUserClient.UpdateCurrentUserProfileAsync) =>
                    Task.FromResult(new UserProfileDTO { Username = "NewName" }),
                nameof(IUserClient.CompleteCurrentUserProfileAsync) when CompleteException is not null => throw CompleteException,
                nameof(IUserClient.CompleteCurrentUserProfileAsync) =>
                    Task.FromResult(new UserProfileDTO { Username = "NewName" }),
                _ => throw new NotSupportedException($"Unexpected user client call: {targetMethod?.Name}")
            };
        }
    }

    public class RecordingSponsorServiceProxy : DispatchProxy
    {
        public ApiException? CreateException { get; set; }
        public ApiException? DeleteException { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(ISponsorService.CreateSponsorAsync) when CreateException is not null => throw CreateException,
                nameof(ISponsorService.DeleteSponsorAsync) when DeleteException is not null => throw DeleteException,
                _ => throw new NotSupportedException($"Unexpected sponsor service call: {targetMethod?.Name}")
            };
        }
    }

    private sealed class FallbackLocalization : ILocalizationService
    {
        public static FallbackLocalization Instance { get; } = new();

        public string CurrentLanguage => ILocalizationService.DefaultLanguage;

        public CultureInfo Culture => CultureInfo.GetCultureInfo(CurrentLanguage);

        public IReadOnlyList<string> SupportedLanguages =>
            [ILocalizationService.DefaultLanguage, ILocalizationService.DutchLanguage];

        // Unmapped keys resolve to the key itself so tests can assert the exact resource that is used.
        public string this[string key] => key;

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
}
