using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Atmos;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

public sealed class CMUDayNightAtmosphereSystem : EntitySystem
{
    // Cadence between mixture updates. Relaxation smooths the steps further,
    // so this only bounds overlay refresh traffic, not smoothness.
    private const float UpdateIntervalSeconds = 30f;

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly CMUNetworkMapAtmosphereSystem _networkAtmos = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CMUSharedZLevelsSystem _zLevels = default!;

    private EntityQuery<MapAtmosphereComponent> _mapAtmosQuery = default!;
    private EntityQuery<CMUDayNightAtmosphereComponent> _dayNightQuery = default!;

    public override void Initialize()
    {
        _mapAtmosQuery = GetEntityQuery<MapAtmosphereComponent>();
        _dayNightQuery = GetEntityQuery<CMUDayNightAtmosphereComponent>();

        SubscribeLocalEvent<CMUDayNightAtmosphereComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnZNetworkUpdated);
    }

    private void OnMapInit(Entity<CMUDayNightAtmosphereComponent> ent, ref MapInitEvent args)
    {
        if (!_mapAtmosQuery.TryComp(ent.Owner, out _))
        {
            Log.Warning($"Day-night atmosphere on {ToPrettyString(ent.Owner)} without a MapAtmosphere; removing it.");
            RemCompDeferred<CMUDayNightAtmosphereComponent>(ent.Owner);
            return;
        }

        // One clock per network: a duplicate component would fight this one
        // every beat, so the first to init keeps it (see Initialized).
        foreach (var member in _zLevels.GetAllNetworkMaps(ent.Owner))
        {
            if (member != ent.Owner
                && _dayNightQuery.TryComp(member, out var other)
                && other.Initialized)
            {
                Log.Warning($"Day-night atmosphere on {ToPrettyString(ent.Owner)} duplicates the one on {ToPrettyString(member)}; removing it.");
                RemCompDeferred<CMUDayNightAtmosphereComponent>(ent.Owner);
                return;
            }
        }

        StartCycle((ent.Owner, ent.Comp));
    }

    private void OnZNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
    {
        // Inheritance first: event subscription order between systems is not
        // a contract, so the day-night capture drives it itself.
        _networkAtmos.EnsureNetworkInherited(args.Network);

        // Rebuild captures: decks join or leave the network (z-level building),
        // and only authored moles matter, so recapturing a driven mixture is
        // lossless.
        var members = new List<EntityUid>();
        foreach (var member in args.Network.Comp.ZLevels.Values)
        {
            if (member is { } map && !TerminatingOrDeleted(map))
                members.Add(map);
        }

        foreach (var map in members)
        {
            if (!_dayNightQuery.TryComp(map, out var comp))
                continue;

            CaptureBaselines(map, comp, members);
        }
    }

    private void StartCycle(Entity<CMUDayNightAtmosphereComponent> ent)
    {
        // Inheritance before capture, so gap decks already carry the base
        // declaration when baselines are taken. Idempotent either way.
        if (_zLevels.TryGetZNetwork(ent.Owner, out var network))
            _networkAtmos.EnsureNetworkInherited(network.Value);

        CaptureBaselines(ent.Owner, ent.Comp, _zLevels.GetAllNetworkMaps(ent.Owner));
        ent.Comp.CycleStart = _timing.CurTime + ent.Comp.PhaseOffset;
        ent.Comp.NextUpdate = _timing.CurTime;
        ent.Comp.Initialized = true;
    }

    private void CaptureBaselines(EntityUid owner, CMUDayNightAtmosphereComponent comp, List<EntityUid> members)
    {
        comp.Baselines.Clear();
        foreach (var member in members)
        {
            if (TerminatingOrDeleted(member)
                || !_mapAtmosQuery.TryComp(member, out var mapAtmos)
                || mapAtmos.Space)
                continue;

            // A space member keeps its flag and its vacuum mixture; writing a
            // cycling temperature into it only churns every grid's map tiles.
            comp.Baselines[member] = new GasMixture(mapAtmos.Mixture);
        }

        if (comp.Baselines.Count == 0)
            Log.Warning($"Day-night atmosphere on {ToPrettyString(owner)} found no non-space member map with a MapAtmosphere.");
    }

    public override void Update(float frameTime)
    {
        if (!_cfg.GetCVar(CCVars.CMUDayNightAtmosTemp))
            return;

        var query = EntityQueryEnumerator<CMUDayNightAtmosphereComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Baselines.Count == 0)
                continue;

            var now = _timing.CurTime;
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + TimeSpan.FromSeconds(UpdateIntervalSeconds);

            // Sine phase: zero is mid-morning on the rise, quarter is the day
            // peak, three quarters the night trough. Negative phases from a
            // future-dated offset simply read as the previous cycle.
            var frac = (float) ((now - comp.CycleStart).TotalSeconds
                / Math.Max(comp.CycleDuration.TotalSeconds, 1.0)) % 1f;
            var average = (comp.DayTemperature + comp.NightTemperature) * 0.5f;
            var amplitude = (comp.DayTemperature - comp.NightTemperature) * 0.5f;
            var temperature = average + amplitude * MathF.Sin(2f * MathF.PI * frac);

            foreach (var (map, baseline) in comp.Baselines)
            {
                if (TerminatingOrDeleted(map))
                    continue;

                _atmosphere.SetMapGasMixture(map, new GasMixture(baseline)
                {
                    Temperature = temperature,
                });
            }
        }
    }
}
