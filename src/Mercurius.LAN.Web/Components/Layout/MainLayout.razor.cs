using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Mercurius.LAN.Web.Localization;

namespace Mercurius.LAN.Web.Components.Layout;

public partial class MainLayout : IDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;

    private bool _sidebarOpen = false;
    private ElementReference _menuToggle;
    private bool _isDarkMode;
    private bool _themeInitialized;
    private bool _languageInitialized;
    private string _themePreference = "system";

    private string ThemeToggleLabel => _isDarkMode ? Localization["theme.switchLight"] : Localization["theme.switchDark"];

    private static readonly MudBlazor.MudTheme SiteTheme = new()
    {
        PaletteLight = new MudBlazor.PaletteLight
        {
            Primary = "#06752a",
            Info = "#1d64a5",
            Success = "#18794e",
            Warning = "#9a5b00",
            Error = "#b42318",
            Dark = "#20252b",
            TextPrimary = "#23282e",
            TextSecondary = "#58616b",
            TextDisabled = "#7c8792",
            ActionDefault = "#58616b",
            ActionDisabled = "#aeb8c4",
            ActionDisabledBackground = "#e9edf1",
            Background = "#f3f5f7",
            BackgroundGray = "#eef1f4",
            Surface = "#fffefa",
            DrawerBackground = "#fffefa",
            DrawerText = "#23282e",
            DrawerIcon = "#58616b",
            AppbarBackground = "#fbfcfa",
            AppbarText = "#23282e",
            LinesDefault = "#d7dde4",
            LinesInputs = "#aeb8c4",
            Divider = "#d7dde4",
            DividerLight = "#eef1f4",
            Skeleton = "#e3e7eb"
        },
        PaletteDark = new MudBlazor.PaletteDark
        {
            Primary = "#06752a",
            Info = "#9cc4e5",
            Success = "#8bd6ad",
            Warning = "#f2c36e",
            Error = "#ff9f9f",
            Dark = "#171b20",
            TextPrimary = "#f1f3f5",
            TextSecondary = "#b7c0c9",
            TextDisabled = "#7b8792",
            ActionDefault = "#b7c0c9",
            ActionDisabled = "#6f7b87",
            ActionDisabledBackground = "#303841",
            Background = "#171b20",
            BackgroundGray = "#20252b",
            Surface = "#20252b",
            DrawerBackground = "#20252b",
            DrawerText = "#f1f3f5",
            DrawerIcon = "#b7c0c9",
            AppbarBackground = "#171b20",
            AppbarText = "#f1f3f5",
            LinesDefault = "#38414b",
            LinesInputs = "#4c5865",
            Divider = "#38414b",
            DividerLight = "#303841",
            Skeleton = "#303841"
        }
    };

    private bool IsHomePage
    {
        get
        {
            var relativePath = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
            var routeOnly = relativePath.Split('?', '#')[0].TrimEnd('/');
            return string.IsNullOrEmpty(routeOnly);
        }
    }

    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += HandleLocationChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _themeInitialized)
            return;

        try
        {
            var storedPreference = await JsRuntime.InvokeAsync<string?>("lanTheme.getStored");
            if (storedPreference is "light" or "dark")
            {
                _themePreference = storedPreference;
                _isDarkMode = storedPreference == "dark";
            }
            else
            {
                _themePreference = "system";
                _isDarkMode = await JsRuntime.InvokeAsync<bool>("lanTheme.getSystem");
            }

            await JsRuntime.InvokeVoidAsync("lanTheme.apply", _isDarkMode ? "dark" : "light");
        }
        catch (JSException)
        {
            // A browser without client-side storage still receives a usable light theme.
            _themePreference = "system";
            _isDarkMode = false;
        }

        _themeInitialized = true;
        if(!_languageInitialized)
        {
            var languageChanged = await Localization.InitializeAsync(JsRuntime);
            _languageInitialized = true;
            if(languageChanged)
            {
                NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
                return;
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task ChangeLanguageAsync(string language)
    {
        if(!await Localization.SetLanguageAsync(JsRuntime, language))
            return;

        NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    }

    private async Task ToggleThemeAsync()
    {
        _isDarkMode = !_isDarkMode;
        _themePreference = _isDarkMode ? "dark" : "light";
        await JsRuntime.InvokeVoidAsync("lanTheme.set", _themePreference);
    }

    private void ToggleSidebar() => _sidebarOpen = !_sidebarOpen;

    private async Task HandleHeaderKeyDown(KeyboardEventArgs args)
    {
        if (!_sidebarOpen || !string.Equals(args.Key, "Escape", StringComparison.Ordinal))
            return;

        _sidebarOpen = false;
        await _menuToggle.FocusAsync();
    }

    private Task HandleMenuEscape() => HandleHeaderKeyDown(new KeyboardEventArgs { Key = "Escape" });
 
    private void HandleOutsideClick()
    {
        _sidebarOpen = false;
    }

    private void OnNavigationSelected()
    {
        if(_sidebarOpen)
            _sidebarOpen = !_sidebarOpen;
    }

    private void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if(!_sidebarOpen)
            return;

        _sidebarOpen = false;
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= HandleLocationChanged;
    }
}
