using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.CMU14.Atmos;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Humans don't breathe by default. While one is on a map with <see cref="CMUBreathingRequiredMapComponent"/> (or in
/// its z-network) this gives them a respirator, so the map's air and their internals matter. Leaving takes it away
/// again. Only respirators this system added are ever removed.
/// </summary>
public sealed partial class CMUMapBreathingSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    /// <summary>Entity prototype holding the respirator settings to add.</summary>
    private static readonly EntProtoId RespiratorTemplate = "CMUMapRespiratorTemplate";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<EntityUid, bool> _mapRequiresBreathing = new();
    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;
        _mapRequiresBreathing.Clear();

        var query = EntityQueryEnumerator<CMUHumanMedicalComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var required = xform.MapUid is { } map && RequiresBreathing(map);
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
        }
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
