using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Atmos;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Connects internals for mobs that spawn with <see cref="CMUSpawnWithInternalsComponent"/>, so they start out
/// breathing from their tank.
/// </summary>
public sealed partial class CMUSpawnWithInternalsSystem : EntitySystem
{
    [Dependency] private SharedGasTankSystem _gasTank = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextTry;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextTry)
            return;

        _nextTry = now + RetryInterval;

        var query = EntityQueryEnumerator<CMUSpawnWithInternalsComponent, InternalsComponent>();
        while (query.MoveNext(out var uid, out var spawn, out var internals))
        {
            if (_internals.AreInternalsWorking(internals) ||
                _internals.FindBestGasTank(uid) is { } tank && _gasTank.ConnectToInternals(tank, uid))
            {
                RemCompDeferred<CMUSpawnWithInternalsComponent>(uid);
                continue;
            }

            if (--spawn.Attempts <= 0)
                RemCompDeferred<CMUSpawnWithInternalsComponent>(uid);
        }
    }
}
