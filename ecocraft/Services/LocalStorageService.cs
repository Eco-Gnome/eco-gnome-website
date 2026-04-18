using Microsoft.JSInterop;

namespace ecocraft.Services;

public class LocalStorageService
{
    private readonly IJSRuntime _jsRuntime;

    public LocalStorageService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task AddItem(string key, string value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (Exception ex) when (IsJsRuntimeUnavailable(ex))
        {
            // Circuit disconnected/disposed: no-op.
        }
    }

    public async Task RemoveItem(string key)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (Exception ex) when (IsJsRuntimeUnavailable(ex))
        {
            // Circuit disconnected/disposed: no-op.
        }
    }

    public async Task<string> GetItem(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
        }
        catch (Exception ex) when (IsJsRuntimeUnavailable(ex))
        {
            // Circuit disconnected/disposed: treat as key not found.
            return string.Empty;
        }
    }

    private static bool IsJsRuntimeUnavailable(Exception ex)
    {
        return ex is JSDisconnectedException
            or ObjectDisposedException
            or TaskCanceledException
            or OperationCanceledException;
    }
}
