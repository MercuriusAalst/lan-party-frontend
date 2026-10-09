using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mercurius.LAN.Web.Components.Shared;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Localization;
using Mercurius.LAN.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class AdminPickerDropdownBehaviorTests
{
    private static readonly Guid AssignedId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task WhileTheListIsStillLoading_TheAssignedAdminStaysSelectable()
    {
        var pending = new TaskCompletionSource<List<PublicUserDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateTournamentService(_ => pending.Task);

        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(service)
            .AddSingleton<ILocalizationService>(TestLocalizationService.Instance)
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        // RenderComponentAsync waits for the pending load, so inspect the initial render instead.
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.BeginRenderingComponent<AdminPicker>(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(AdminPicker.Id)] = "adminPicker",
                [nameof(AdminPicker.Value)] = AssignedId,
                [nameof(AdminPicker.SelectedAdmin)] = Selected(AssignedId, "assigned-admin")
            })));

        var loadingHtml = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("aria-busy=\"true\"", loadingHtml);
        Assert.Contains($"option value=\"{AssignedId}\"", loadingHtml);
        Assert.DoesNotContain($"option value=\"{OtherId}\"", loadingHtml);

        pending.SetResult([]);
        await renderer.Dispatcher.InvokeAsync(() => root.QuiescenceTask);
        var loadedHtml = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        Assert.Contains("aria-busy=\"false\"", loadedHtml);
        Assert.Contains($"option value=\"{AssignedId}\"", loadedHtml);
    }

    [Fact]
    public async Task AfterLoading_TheAssignedAdminIsAnOptionAndTheSelectKeepsItsValue()
    {
        var admins = new List<PublicUserDTO> { Selected(OtherId, "other-admin"), Selected(AssignedId, "assigned-admin") };
        var service = CreateTournamentService(_ => Task.FromResult(admins));

        var html = await RenderAsync(service, AssignedId, Selected(AssignedId, "assigned-admin"));

        Assert.Contains($"option value=\"{AssignedId}\"", html);
        Assert.Contains($"option value=\"{OtherId}\"", html);
        Assert.Single(Regex.Matches(html, $"option value=\"{AssignedId}\""));
        Assert.Contains($"select id=\"adminPicker\"", html);
        Assert.Contains(AssignedId.ToString(), SelectTag(html));
        Assert.Contains("aria-busy=\"false\"", html);
    }

    [Fact]
    public async Task WhenTheAssignedAdminIsNotInTheLoadedList_ItIsStillOfferedOnce()
    {
        var admins = new List<PublicUserDTO> { Selected(OtherId, "other-admin") };
        var service = CreateTournamentService(_ => Task.FromResult(admins));

        var html = await RenderAsync(service, AssignedId, Selected(AssignedId, "assigned-admin"));

        Assert.Single(Regex.Matches(html, $"option value=\"{AssignedId}\""));
        Assert.Contains("assigned-admin", html);
        Assert.Contains($"option value=\"{OtherId}\"", html);
    }

    [Fact]
    public async Task WhenTheClearedContactIsNotInTheLoadedList_ItIsNoLongerOffered()
    {
        var admins = new List<PublicUserDTO> { Selected(OtherId, "other-admin") };
        var service = CreateTournamentService(_ => Task.FromResult(admins));

        var html = await RenderAsync(service, null, Selected(AssignedId, "assigned-admin"));

        Assert.DoesNotContain($"option value=\"{AssignedId}\"", html);
        Assert.Contains($"option value=\"{OtherId}\"", html);
    }

    [Fact]
    public async Task WhenTheListFails_ThePickerShowsAnErrorStateAndNoAdminOptions()
    {
        var service = CreateTournamentService(_ => Task.FromException<List<PublicUserDTO>>(new HttpRequestException("boom")));

        var html = await RenderAsync(service, AssignedId, Selected(AssignedId, "assigned-admin"));

        Assert.Contains("role=\"alert\"", html);
        Assert.DoesNotContain($"option value=\"{OtherId}\"", html);
    }

    [Fact]
    public async Task WhenTheListIsEmpty_ThePickerShowsOnlyTheClearChoice()
    {
        var service = CreateTournamentService(_ => Task.FromResult(new List<PublicUserDTO>()));

        var html = await RenderAsync(service, null, null);

        Assert.Contains("role=\"status\"", html);
        Assert.DoesNotContain("option value=\"2", html);
        Assert.DoesNotContain("aria-busy=\"true\"", html);
    }

    [Fact]
    public async Task ChangingTheSelectionToTheClearChoiceRaisesANullValue()
    {
        var component = new AdminPicker();
        SetProperty(component, "TournamentService", CreateTournamentService(_ => Task.FromResult(new List<PublicUserDTO>())));
        SetProperty(component, "Localization", TestLocalizationService.Instance);
        Guid? raised = AssignedId;
        SetProperty(component, "ValueChanged", EventCallback.Factory.Create<Guid?>(this, value => raised = value));

        await InvokeAsync(component, "OnChangeAsync", new ChangeEventArgs { Value = string.Empty });
        Assert.Null(raised);

        await InvokeAsync(component, "OnChangeAsync", new ChangeEventArgs { Value = OtherId.ToString() });
        Assert.Equal(OtherId, raised);
    }

    [Fact]
    public void EveryLocalizedKeyUsedByThePickerExistsInEveryLocaleFile()
    {
        var razor = File.ReadAllText(SourcePath("src", "Mercurius.LAN.Web", "Components", "Shared", "AdminPicker.razor"));
        var keys = Regex.Matches(razor, "Localization\\[\"([^\"]+)\"\\]")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(keys);

        foreach(var locale in new[] { "translations.en-US.json", "translations.nl-BE.json" })
        {
            using var document = JsonDocument.Parse(
                File.ReadAllText(SourcePath("src", "Mercurius.LAN.Web", "wwwroot", "locales", locale)));
            foreach(var key in keys)
                Assert.True(document.RootElement.TryGetProperty(key, out _), $"{locale} is missing '{key}'.");
        }
    }

    [Fact]
    public async Task WhenTheLoadIsCanceledWithoutCancellingThePicker_TheErrorStateIsShown()
    {
        // An HttpClient timeout cancels its own token, not the picker's, so it is a load failure.
        var service = CreateTournamentService(_ => Task.FromException<List<PublicUserDTO>>(
            new TaskCanceledException("The request timed out.")));

        var html = await RenderAsync(service, AssignedId, Selected(AssignedId, "assigned-admin"));

        Assert.Contains("role=\"alert\"", html);
        Assert.DoesNotContain("role=\"status\"", html);
        Assert.Contains($"option value=\"{AssignedId}\"", html);
        Assert.Contains("aria-busy=\"false\"", html);
    }

    [Fact]
    public async Task WhenTheListFails_TheErrorStateOffersAnInPlaceRetry()
    {
        var service = CreateTournamentService(_ => Task.FromException<List<PublicUserDTO>>(new HttpRequestException("boom")));

        var html = await RenderAsync(service, AssignedId, Selected(AssignedId, "assigned-admin"));

        var alert = AlertBlock(html);
        Assert.Contains("type=\"button\"", alert);
        Assert.Contains("Try again", alert);
        Assert.DoesNotContain("spinner-border", html);
    }

    [Fact]
    public async Task WhenARetryIsPending_ThePickerReportsLoadingInsteadOfTheError()
    {
        var pending = new TaskCompletionSource<List<PublicUserDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateTournamentService(_ => Task.FromException<List<PublicUserDTO>>(new HttpRequestException("boom")));
        var component = CreateComponent(service, AssignedId, Selected(AssignedId, "assigned-admin"));

        await InvokeTaskAsync(component, "OnInitializedAsync");
        Assert.NotNull(GetField<string?>(component, "_loadError"));

        SetProperty(component, "TournamentService", CreateTournamentService(_ => pending.Task));
        var retry = InvokeTaskAsync(component, "LoadAdminsAsync");

        Assert.False(retry.IsCompleted);
        Assert.True(GetField<bool>(component, "_isLoading"));
        Assert.Null(GetField<string?>(component, "_loadError"));

        pending.SetResult([Selected(OtherId, "other-admin")]);
        await retry;

        Assert.False(GetField<bool>(component, "_isLoading"));
        Assert.Null(GetField<string?>(component, "_loadError"));
        Assert.Single(GetField<List<PublicUserDTO>>(component, "_choices"));
    }

    [Fact]
    public async Task WhileALoadIsInFlight_ASecondRetryIsIgnored()
    {
        var pending = new TaskCompletionSource<List<PublicUserDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var service = CreateTournamentService(_ => ++calls switch
        {
            1 => Task.FromException<List<PublicUserDTO>>(new HttpRequestException("boom")),
            2 => pending.Task,
            _ => Task.FromResult(new List<PublicUserDTO>())
        });
        var component = CreateComponent(service, AssignedId, Selected(AssignedId, "assigned-admin"));

        await InvokeTaskAsync(component, "OnInitializedAsync");
        var retry = InvokeTaskAsync(component, "LoadAdminsAsync");
        var overlappingRetry = InvokeTaskAsync(component, "LoadAdminsAsync");

        Assert.False(retry.IsCompleted);
        Assert.True(overlappingRetry.IsCompleted);
        Assert.Equal(2, calls);

        pending.SetResult([Selected(OtherId, "other-admin")]);
        await retry;

        Assert.Equal(2, calls);
        Assert.False(GetField<bool>(component, "_isLoading"));
        Assert.Null(GetField<string?>(component, "_loadError"));
        Assert.Single(GetField<List<PublicUserDTO>>(component, "_choices"));
    }

    [Fact]
    public async Task WhenARetrySucceeds_TheErrorIsClearedAndTheListIsNotDuplicated()
    {
        var admins = new List<PublicUserDTO> { Selected(OtherId, "other-admin"), Selected(AssignedId, "assigned-admin") };
        var calls = 0;
        var service = CreateTournamentService(_ => ++calls == 1
            ? Task.FromException<List<PublicUserDTO>>(new HttpRequestException("boom"))
            : Task.FromResult(admins));
        var component = CreateComponent(service, AssignedId, Selected(AssignedId, "assigned-admin"));

        await InvokeTaskAsync(component, "OnInitializedAsync");
        Assert.NotNull(GetField<string?>(component, "_loadError"));
        Assert.Empty(GetField<List<PublicUserDTO>>(component, "_choices"));

        await InvokeTaskAsync(component, "LoadAdminsAsync");
        Assert.Null(GetField<string?>(component, "_loadError"));
        Assert.Equal(2, GetField<List<PublicUserDTO>>(component, "_choices").Count);
        Assert.Equal(AssignedId, component.Value);

        await InvokeTaskAsync(component, "LoadAdminsAsync");
        Assert.Equal(2, GetField<List<PublicUserDTO>>(component, "_choices").Count);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task WhenThePickerIsDisposedWhileLoading_TheCancellationIsBenign()
    {
        var pending = new TaskCompletionSource<List<PublicUserDTO>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var component = CreateComponent(CreateTournamentService(token =>
        {
            token.Register(() => pending.TrySetCanceled(token));
            return pending.Task;
        }), null, null);

        var load = InvokeTaskAsync(component, "OnInitializedAsync");
        component.Dispose();
        await load;

        Assert.Null(GetField<string?>(component, "_loadError"));
        Assert.False(GetField<bool>(component, "_isLoading"));
    }

    private static AdminPicker CreateComponent(ITournamentService service, Guid? value, PublicUserDTO? selectedAdmin)
    {
        var component = new AdminPicker();
        SetProperty(component, "TournamentService", service);
        SetProperty(component, "Localization", TestLocalizationService.Instance);
        SetProperty(component, "ValueChanged", EventCallback.Factory.Create<Guid?>(component, _ => { }));
        SetProperty(component, "Value", value);
        SetProperty(component, "SelectedAdmin", selectedAdmin);
        return component;
    }

    private static string AlertBlock(string html)
    {
        var start = html.IndexOf("role=\"alert\"", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected an alert state in the rendered picker.");
        var end = html.IndexOf("</div>", start, StringComparison.Ordinal);
        return html[start..end];
    }

    private static Task InvokeTaskAsync(AdminPicker component, string methodName)
    {
        var method = typeof(AdminPicker).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(AdminPicker).FullName, methodName);
        return (Task)method.Invoke(component, null)!;
    }

    private static T GetField<T>(AdminPicker component, string fieldName)
    {
        var field = typeof(AdminPicker).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AdminPicker).FullName, fieldName);
        return (T)field.GetValue(component)!;
    }

    private static string SelectTag(string html)
    {
        var start = html.IndexOf("<select", StringComparison.Ordinal);
        var end = html.IndexOf('>', start);
        return html[start..end];
    }

    private static PublicUserDTO Selected(Guid id, string username) =>
        new() { Id = id, Username = username, DisplayName = username };

    private static async Task<string> RenderAsync(ITournamentService service, Guid? value, PublicUserDTO? selectedAdmin)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<ITournamentService>(service)
            .AddSingleton<ILocalizationService>(TestLocalizationService.Instance)
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<AdminPicker>(ParameterView.FromDictionary(
                new Dictionary<string, object?>
                {
                    [nameof(AdminPicker.Id)] = "adminPicker",
                    [nameof(AdminPicker.Value)] = value,
                    [nameof(AdminPicker.SelectedAdmin)] = selectedAdmin
                }));
            return component.ToHtmlString();
        });
    }

    private static async Task InvokeAsync(AdminPicker component, string methodName, ChangeEventArgs args)
    {
        var method = typeof(AdminPicker).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(typeof(AdminPicker).FullName, methodName);
        await (Task)method.Invoke(component, [args])!;
    }

    private static void SetProperty(object instance, string propertyName, object? value)
    {
        var property = instance.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
        property.SetValue(instance, value);
    }

    private static ITournamentService CreateTournamentService(Func<CancellationToken, Task<List<PublicUserDTO>>> loadAdmins)
    {
        var proxy = DispatchProxy.Create<ITournamentService, ServiceProxy>();
        ((ServiceProxy)(object)proxy).Handler = (method, args) =>
            method.Name == nameof(ITournamentService.GetAllAdminUsersAsync)
                ? loadAdmins(args is { Length: > 0 } && args[0] is CancellationToken token ? token : CancellationToken.None)
                : throw new NotSupportedException(method.Name);
        return proxy;
    }

    private static string SourcePath(params string[] segments)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(SourceFile())!);
        while(directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine([directory!.FullName, .. segments]);
    }

    private static string SourceFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    private class ServiceProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = (_, _) => null;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod ?? throw new InvalidOperationException("Missing proxy method."), args);
    }
}
