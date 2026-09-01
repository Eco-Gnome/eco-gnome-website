using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace ecocraft.Services;

// Clé partagée entre le CircuitHandler et le ContextService d'un même circuit (tous deux Scoped).
// Le ContextService ne connaît pas l'Id du circuit Blazor, mais partage ce scope.
public sealed class CircuitSession
{
    public Guid Key { get; } = Guid.NewGuid();
}

public sealed record LiveCircuitSnapshot(
    int ConnectedCircuits,
    int OpenCircuits,
    int UniqueUsers,
    int UniqueClients);

// Compteur live des circuits Blazor (singleton). Rien n'est persisté : les IP ne sont jamais
// stockées, seul un hash SHA-256 salé au démarrage vit en RAM le temps du circuit.
public sealed class LiveCircuitTracker
{
    private sealed class Entry
    {
        public bool Connected;
        public Guid? UserId;
        public string? ClientHash;
    }

    private readonly ConcurrentDictionary<Guid, Entry> _circuits = new();
    private readonly byte[] _salt = RandomNumberGenerator.GetBytes(16);

    public void Register(Guid key) => _circuits.TryAdd(key, new Entry());

    public void Remove(Guid key) => _circuits.TryRemove(key, out _);

    public void SetConnected(Guid key, bool connected)
    {
        if (_circuits.TryGetValue(key, out var entry))
        {
            entry.Connected = connected;
        }
    }

    public void SetUser(Guid key, Guid userId)
    {
        if (_circuits.TryGetValue(key, out var entry))
        {
            entry.UserId = userId;
        }
    }

    public void SetClient(Guid key, string? clientAddress)
    {
        if (string.IsNullOrWhiteSpace(clientAddress) || !_circuits.TryGetValue(key, out var entry))
        {
            return;
        }

        var bytes = SHA256.HashData([.. _salt, .. Encoding.UTF8.GetBytes(clientAddress)]);
        entry.ClientHash = Convert.ToHexString(bytes);
    }

    public LiveCircuitSnapshot GetSnapshot()
    {
        var entries = _circuits.Values.ToList();
        var connected = entries.Where(e => e.Connected).ToList();

        return new LiveCircuitSnapshot(
            ConnectedCircuits: connected.Count,
            OpenCircuits: entries.Count,
            UniqueUsers: connected.Where(e => e.UserId is not null).Select(e => e.UserId!.Value).Distinct().Count(),
            UniqueClients: connected.Where(e => e.ClientHash is not null).Select(e => e.ClientHash!).Distinct().Count());
    }
}

public sealed class LiveCircuitHandler(
    LiveCircuitTracker tracker,
    CircuitSession session,
    IHttpContextAccessor httpContextAccessor) : CircuitHandler
{
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        tracker.Register(session.Key);
        tracker.SetClient(session.Key, ResolveClientAddress());
        return Task.CompletedTask;
    }

    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        tracker.SetConnected(session.Key, true);
        return Task.CompletedTask;
    }

    public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        tracker.SetConnected(session.Key, false);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        tracker.Remove(session.Key);
        return Task.CompletedTask;
    }

    // Derrière le reverse proxy (127.0.0.1:3030), la vraie IP est dans X-Forwarded-For / X-Real-IP.
    private string? ResolveClientAddress()
    {
        var http = httpContextAccessor.HttpContext;
        if (http is null)
        {
            return null;
        }

        var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        var realIp = http.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp.Trim();
        }

        return http.Connection.RemoteIpAddress?.ToString();
    }
}
