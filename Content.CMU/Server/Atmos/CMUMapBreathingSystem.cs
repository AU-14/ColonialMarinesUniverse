using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.CMU14.Atmos;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Ship;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Humans don't breathe by default. While one is on a map with <see cref="CMUBreathingRequiredMapComponent"/> (or in
/// its z-network) this gives them a respirator, so the map's air and their internals matter. Leaving takes it away
/// again. Only respirators this system added are ever removed.
/// Anyone breathing through internals connected to an empty tank also gets one, so the empty tank suffocates them.
/// Maps with <see cref="CMUPressureRequiredMapComponent"/> do the same for pressure damage.
/// </summary>
public sealed partial class CMUMapBreathingSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InternalsSystem _internals = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    /// <summary>Entity prototype holding the respirator settings to add.</summary>
    private static readonly EntProtoId RespiratorTemplate = "CMUMapRespiratorTemplate";

    /// <summary>Entity prototype holding the pressure damage settings to add.</summary>
    private static readonly EntProtoId BarotraumaTemplate = "CMUMapBarotraumaTemplate";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<EntityUid, bool> _mapRequiresBreathing = new();
    private readonly Dictionary<EntityUid, bool> _mapRequiresPressure = new();
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;
        _mapRequiresBreathing.Clear();
        _mapRequiresPressure.Clear();

        var query = EntityQueryEnumerator<CMUHumanMedicalComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var required = xform.MapUid is { } map && RequiresBreathing(map) || IsBreathingFromEmptyTank(uid);
            var granted = HasComp<CMUMapGrantedRespiratorComponent>(uid);

            if (required && !granted && !HasComp<RespiratorComponent>(uid))
            {
                EntityManager.AddComponents(uid, _prototypes.Index(RespiratorTemplate), removeExisting: false);
                EnsureComp<CMUMapGrantedRespiratorComponent>(uid);
            }
            else if (!required && granted)
            {
                // Clear any suffocation alert before the respirator that would have cleared it goes away.
                var ev = new StopSuffocatingEvent();
                RaiseLocalEvent(uid, ref ev);
                RemComp<RespiratorComponent>(uid);
                RemComp<CMUMapGrantedRespiratorComponent>(uid);
            }

            var pressure = xform.MapUid is { } pressureMap && RequiresPressure(pressureMap);
            var pressureGranted = HasComp<CMUMapGrantedBarotraumaComponent>(uid);
            if (pressure && !pressureGranted && !HasComp<BarotraumaComponent>(uid))
            {
                EntityManager.AddComponents(uid, _prototypes.Index(BarotraumaTemplate), removeExisting: false);
                EnsureComp<CMUMapGrantedBarotraumaComponent>(uid);
            }
            else if (!pressure && pressureGranted)
            {
                RemComp<BarotraumaComponent>(uid);
                RemComp<CMUMapGrantedBarotraumaComponent>(uid);
            }
        }
    }

    private bool RequiresPressure(EntityUid map)
    {
        if (_mapRequiresPressure.TryGetValue(map, out var cached))
            return cached;

        var required = false;
        foreach (var member in _zLevels.GetAllNetworkMaps(map))
        {
            if (HasComp<CMUPressureRequiredMapComponent>(member))
            {
                required = true;
                break;
            }
        }

        _mapRequiresPressure[map] = required;
        return required;
    }

    private bool IsBreathingFromEmptyTank(EntityUid uid)
    {
        return TryComp<InternalsComponent>(uid, out var internals)
               && _internals.AreInternalsWorking(internals)
               && TryComp<GasTankComponent>(internals.GasTankEntity, out var tank)
               && tank.Air.TotalMoles <= Atmospherics.GasMinMoles;
    }

    private bool RequiresBreathing(EntityUid map)
    {
        if (_mapRequiresBreathing.TryGetValue(map, out var cached))
            return cached;

        var required = false;
        foreach (var member in _zLevels.GetAllNetworkMaps(map))
        {
            if (HasComp<CMUBreathingRequiredMapComponent>(member))
            {
                required = true;
                break;
            }
        }

        _mapRequiresBreathing[map] = required;
        return required;
    }
}

/// <summary>Marks a respirator as one <see cref="CMUMapBreathingSystem"/> added, so it's the only kind it removes.</summary>
[RegisterComponent]
public sealed partial class CMUMapGrantedRespiratorComponent : Component;

/// <summary>Marks pressure damage as something <see cref="CMUMapBreathingSystem"/> added, so it's the only kind it removes.</summary>
[RegisterComponent]
public sealed partial class CMUMapGrantedBarotraumaComponent : Component;
