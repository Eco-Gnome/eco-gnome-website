namespace ecocraft.Services;

using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class JSInteropService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly List<Func<IJSRuntime, Task>> _pendingActions = new();

    public JSInteropService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Ajoute une action JS à exécuter après le prochain rendu.
    /// </summary>
    public void EnqueueAction(Func<IJSRuntime, Task> action)
    {
        _pendingActions.Add(action);
    }

    /// <summary>
    /// Exécute les actions JS en attente après le rendu.
    /// </summary>
    public async Task ExecutePendingActionsAsync()
    {
        while (_pendingActions.Count > 0)
        {
            var action = _pendingActions[0];
            _pendingActions.RemoveAt(0);
            await action(_jsRuntime);
        }
    }

    public Task<T> ExecuteActionAsync<T>(Func<IJSRuntime, Task<T>> action)
    {
        return action(_jsRuntime);
    }
}
