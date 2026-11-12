using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Mercurius.LAN.Web.Components.Shared;

/// <summary>
/// Focus-trap lifecycle for a modal rendered by a component: activates <c>activateTeamModalFocusTrap</c>
/// (wwwroot/app.js) while the modal is open and releases it when it closes or the component is disposed.
/// </summary>
internal sealed class ModalFocusTrap(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _trap;

    /// <summary>Call from <c>OnAfterRenderAsync</c> with the modal's current open state.</summary>
    public async Task SyncAsync(bool isOpen, ElementReference dialog)
    {
        if(isOpen && _trap is null)
            _trap = await jsRuntime.InvokeAsync<IJSObjectReference>("activateTeamModalFocusTrap", dialog);
        else if(!isOpen && _trap is not null)
            await DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        var trap = _trap;
        _trap = null;
        if(trap is null)
            return;

        try
        {
            await trap.InvokeVoidAsync("dispose");
            await trap.DisposeAsync();
        }
        catch(JSDisconnectedException)
        {
        }
    }
}
