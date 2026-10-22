using System.Reflection;
using System.Text.Json;
using Blazored.Toast.Services;
using Mercurius.LAN.Web.Components.Auth;
using Mercurius.LAN.Web.DTOs.Users;
using Mercurius.LAN.Web.Localization;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class AccountDeletionBehaviorTests
{
    [Fact]
    public void UserActionResponseDefaultsPendingCleanupToFalse()
    {
        var legacy = JsonSerializer.Deserialize<UserActionResponse>(
            """{"message":"deleted"}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        var pending = JsonSerializer.Deserialize<UserActionResponse>(
            """{"message":"queued","deletionPending":true}""",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(legacy);
        Assert.False(legacy.DeletionPending);
        Assert.NotNull(pending);
        Assert.True(pending.DeletionPending);
    }

    [Fact]
    public void PendingDeletionShowsOnePostLogoutMessageAndRemovesItsQuery()
    {
        var navigation = new RecordingNavigationManager(
            "https://example.test/?campaign=lan&accountDeletion=pending#account");
        var toast = DispatchProxy.Create<IToastService, RecordingToastProxy>();
        var component = new TestAuthStatusToast();
        SetProperty(component, "Localization", TestLocalizationService.Instance);
        SetProperty(component, "NavigationManager", navigation);
        SetProperty(component, "ToastService", toast);

        component.HandleStatus();
        component.HandleStatus();

        var proxy = (RecordingToastProxy)(object)toast;
        Assert.Equal("auth.accountDeletionPending", Assert.Single(proxy.InfoMessages));
        Assert.Empty(proxy.ErrorMessages);
        Assert.Equal("https://example.test/?campaign=lan#account", Assert.Single(navigation.Navigations));
    }

    [Theory]
    [InlineData("cancelled", "auth.loginCancelled", false)]
    [InlineData("failed", "auth.loginFailed", true)]
    public void ExistingLoginStatusHandlingStillShowsItsMessage(string status, string message, bool isError)
    {
        var navigation = new RecordingNavigationManager(
            $"https://example.test/?login={status}&state=preserved");
        var toast = DispatchProxy.Create<IToastService, RecordingToastProxy>();
        var component = new TestAuthStatusToast();
        SetProperty(component, "Localization", TestLocalizationService.Instance);
        SetProperty(component, "NavigationManager", navigation);
        SetProperty(component, "ToastService", toast);

        component.HandleStatus();

        var proxy = (RecordingToastProxy)(object)toast;
        var messages = isError ? proxy.ErrorMessages : proxy.InfoMessages;
        Assert.Equal(message, Assert.Single(messages));
        Assert.Equal("https://example.test/?state=preserved", Assert.Single(navigation.Navigations));
    }

    private static void SetProperty(object instance, string propertyName, object? value)
    {
        PropertyInfo? property = null;
        for(var type = instance.GetType(); type is not null; type = type.BaseType)
        {
            property = type.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if(property is not null)
                break;
        }

        (property ?? throw new MissingMemberException(instance.GetType().FullName, propertyName))
            .SetValue(instance, value);
    }

    private sealed class TestAuthStatusToast : AuthStatusToast
    {
        public void HandleStatus() => base.OnAfterRender(firstRender: false);
    }

    private class RecordingToastProxy : DispatchProxy
    {
        public List<string> InfoMessages { get; } = [];
        public List<string> ErrorMessages { get; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var message = args is { Length: > 0 } ? args[0] as string : null;
            if(message is null)
                return null;

            if(targetMethod?.Name == nameof(IToastService.ShowInfo))
                InfoMessages.Add(message);
            else if(targetMethod?.Name == nameof(IToastService.ShowError))
                ErrorMessages.Add(message);

            return null;
        }
    }

    private sealed class RecordingNavigationManager : NavigationManager
    {
        public List<string> Navigations { get; } = [];

        public RecordingNavigationManager(string uri)
        {
            Initialize("https://example.test/", uri);
        }

        protected override void NavigateToCore(string uri, NavigationOptions options) =>
            Navigations.Add(uri);
    }
}
