using System;
using System.Threading.Tasks;
using Npgsql;

namespace Hali.Infrastructure.Data;

/// <summary>
/// Owns the per-DbContext <see cref="NpgsqlDataSource"/> instances.
/// Registered as a singleton in DI so the host disposes the underlying
/// connection pools on shutdown — directly registering data sources keyed
/// by type would require keyed services, so a single owning holder keeps
/// the wiring simple while still giving DI ownership.
/// </summary>
public sealed class HaliDataSources : IAsyncDisposable, IDisposable
{
    public NpgsqlDataSource Auth { get; }
    public NpgsqlDataSource Signals { get; }
    public NpgsqlDataSource Clusters { get; }
    public NpgsqlDataSource Participation { get; }
    public NpgsqlDataSource Advisories { get; }
    public NpgsqlDataSource Notifications { get; }

    public HaliDataSources(
        NpgsqlDataSource auth,
        NpgsqlDataSource signals,
        NpgsqlDataSource clusters,
        NpgsqlDataSource participation,
        NpgsqlDataSource advisories,
        NpgsqlDataSource notifications)
    {
        Auth = auth;
        Signals = signals;
        Clusters = clusters;
        Participation = participation;
        Advisories = advisories;
        Notifications = notifications;
    }

    public void Dispose()
    {
        Auth.Dispose();
        Signals.Dispose();
        Clusters.Dispose();
        Participation.Dispose();
        Advisories.Dispose();
        Notifications.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await Auth.DisposeAsync();
        await Signals.DisposeAsync();
        await Clusters.DisposeAsync();
        await Participation.DisposeAsync();
        await Advisories.DisposeAsync();
        await Notifications.DisposeAsync();
    }
}
