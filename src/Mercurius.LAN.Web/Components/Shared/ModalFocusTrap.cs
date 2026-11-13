using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Mercurius.LAN.Web.Components.Shared;

internal sealed class ModalFocusTrap(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _trap;

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
