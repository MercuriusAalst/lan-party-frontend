using Xunit;

namespace Mercurius.LAN.Web.ContractTests;

public sealed class NavigationDropdownContractTests
{
    [Fact]
    public void AdminMenuMarkupUsesControlledAccessibleMenuSemantics()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Layout/NavMenu.razor");

        Assert.Contains("id=\"@AdminMenuContainerElementId\"", markup);
        Assert.Contains("id=\"@AdminMenuTriggerElementId\"", markup);
        Assert.Contains("aria-haspopup=\"true\"", markup);
        Assert.Contains("aria-controls=\"@AdminMenuElementId\"", markup);
        Assert.Contains("role=\"menu\"", markup);
        Assert.Contains("role=\"menuitem\"", markup);
        Assert.Contains("@onclick=\"HandleNavigationClicked\"", markup);
    }

    [Fact]
    public void AdminMenuListenerDismissesOutsidePointerAndEscapeWithCleanup()
    {
        var script = ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/app.js");
        var code = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Layout/NavMenu.razor.cs");

        Assert.Contains("function addNavAdminMenuListener", script);
        Assert.Contains("document.addEventListener('pointerdown', listener, true)", script);
        Assert.Contains("document.addEventListener('keydown', listener, true)", script);
        Assert.Contains("event.key === 'Escape'", script);
        Assert.Contains("CloseAdminDropdown', true", script);
        Assert.Contains("CloseAdminDropdown', false", script);
        Assert.Contains("document.removeEventListener('pointerdown', listener, true)", script);
        Assert.Contains("document.removeEventListener('keydown', listener, true)", script);
        Assert.Contains("addNavAdminMenuListener", code);
        Assert.Contains("public async Task CloseAdminDropdown(bool restoreFocus = false)", code);
        Assert.Contains("await _adminMenuTrigger.FocusAsync()", code);
    }

    [Fact]
    public void InfoMenuDismissesWithoutBlockingOutsideTargetsAndRestoresFocusOnEscape()
    {
        var markup = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Layout/NavMenu.razor");
        var script = ReadRepositoryFile("src/Mercurius.LAN.Web/wwwroot/app.js");
        var code = ReadRepositoryFile("src/Mercurius.LAN.Web/Components/Layout/NavMenu.razor.cs");
        var infoListenerStart = script.IndexOf("function addNavInfoMenuListener", StringComparison.Ordinal);
        var infoListenerEnd = script.IndexOf("\nfunction ", infoListenerStart + 1, StringComparison.Ordinal);
        var infoListener = script[infoListenerStart..infoListenerEnd];

        Assert.Contains("id=\"info-nav-menu-container\"", markup);
        Assert.Contains("@onkeydown=\"HandleNavMenuKeyDown\"", markup);
        Assert.Contains("document.addEventListener('click', listener, true)", infoListener);
        Assert.DoesNotContain("pointerdown", infoListener);
        Assert.Contains("document.addEventListener('keydown', listener, true)", infoListener);
        Assert.Contains("element.closest('.brand-nav-shell')?.contains(event.target)", infoListener);
        Assert.Contains("CloseInfoDropdown', false", infoListener);
        Assert.Contains("document.removeEventListener('click', listener, true)", infoListener);
        Assert.Contains("document.removeEventListener('keydown', listener, true)", infoListener);
        Assert.Contains("private async Task HandleNavMenuKeyDown(KeyboardEventArgs args)", code);
        Assert.Contains("await CloseInfoDropdown(restoreFocus: true)", code);
        Assert.Contains("await OnEscape.InvokeAsync()", code);
        Assert.Contains("public async Task CloseInfoDropdown(bool restoreFocus = false)", code);
        Assert.Contains("await _infoMenuTrigger.FocusAsync()", code);
        Assert.Contains("_isDropdownVisible || _isNotificationMenuVisible", code);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory != null)
        {
            var repositoryPath = Path.Combine(directory.FullName, relativePath);
            if(File.Exists(repositoryPath))
                return File.ReadAllText(repositoryPath);

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not locate repository file '{relativePath}'.");
    }
}
