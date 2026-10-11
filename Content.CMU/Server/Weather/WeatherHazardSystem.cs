using System.Collections.Generic;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Radiation.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Weather;
using Content.Shared.Damage.Systems;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Weather;

/// <summary>
/// Applies <see cref="WeatherHazardComponent"/> data to mobs standing on tiles the weather can reach.
/// </summary>
public sealed class WeatherHazardSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RadiationSystem _radiation = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedWeatherSystem _weather = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private EntityQuery<MapGridComponent> _gridQuery = default!;
    private EntityQuery<FlammableComponent> _flammableQuery = default!;

    private readonly List<ActiveHazard> _dueHazards = new();

    private readonly record struct ActiveHazard(
        EntityUid MapUid,
        EntityUid WeatherEnt,
        WeatherHazardComponent Hazard,
        float Percent);

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _flammableQuery = GetEntityQuery<FlammableComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_cfg.GetCVar(CCVars.CMUWeatherHazards))
            return;

        _dueHazards.Clear();

        var weatherQuery = EntityQueryEnumerator<WeatherHazardComponent, StatusEffectComponent, TransformComponent>();
        while (weatherQuery.MoveNext(out var uid, out var hazard, out var status, out var xform))
        {
            if (_timing.CurTime < hazard.NextTickAt)
                continue;

            hazard.NextTickAt = _timing.CurTime + hazard.Interval;

            if (!HasComp<MapComponent>(xform.ParentUid))
                continue;

            var percent = _weather.GetWeatherPercent((uid, status));
            if (percent <= 0)
                continue;

            _dueHazards.Add(new ActiveHazard(xform.ParentUid, uid, hazard, percent));
        }

        if (_dueHazards.Count == 0)
            return;

        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        while (mobs.MoveNext(out var mob, out var mobState, out var xform))
        {
            if (mobState.CurrentState == MobState.Dead)
                continue;

            foreach (var active in _dueHazards)
            {
                if (active.MapUid == xform.MapUid)
                    ApplyToMob(mob, xform, active);
            }
        }
    }

    private void ApplyToMob(EntityUid mob, TransformComponent xform, ActiveHazard active)
    {
        var hazard = active.Hazard;

        if (xform.GridUid is not { } gridUid
            || _container.IsEntityInContainer(mob)
            || !_gridQuery.TryComp(gridUid, out var grid))
            return;

        var tile = _map.GetTileRef(gridUid, grid, xform.Coordinates);
        if (!_weather.CanWeatherAffect((gridUid, grid, CompOrNull<RoofComponent>(gridUid)), tile))
            return;

        if (hazard.Damage is { } damage)
            _damageable.TryChangeDamage(mob, damage * active.Percent, interruptsDoAfters: false, origin: active.WeatherEnt);

        if (hazard.RadsPerSecond > 0)
            _radiation.IrradiateEntity(mob, hazard.RadsPerSecond * active.Percent, (float) hazard.Interval.TotalSeconds);

        if (hazard.IgniteChance > 0
            && hazard.FireStacks > 0
            && _flammableQuery.HasComp(mob)
            && _random.Prob(hazard.IgniteChance * active.Percent))
            _flammable.AdjustFireStacks(mob, hazard.FireStacks, ignite: true);
    }
}
