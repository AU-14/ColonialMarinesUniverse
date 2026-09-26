using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Evacuation;

namespace Content.Shared.CMU14.Hijack;

/// <summary>Shared queries also enforce launch restrictions on predicted client UI actions.</summary>
public abstract class CMUShipHijackSystem : EntitySystem
{
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public bool TryGetShip(EntityUid reference, out Entity<CMUShipHijackComponent> ship)
    {
        ship = default;
        if (!TryComp(reference, out TransformComponent? transform) || transform.MapUid is not { } map)
            return false;

        var query = EntityQueryEnumerator<CMUShipHijackComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.ShipMaps.Count > 0
                    ? component.ShipMaps.Contains(map)
                    : _zLevels.IsSameZNetwork(map, uid))
            {
                ship = (uid, component);
                return true;
            }
        }

        return false;
    }

    public bool CanLaunch(EntityUid reference, bool lifeboat = false)
    {
        if (!TryGetShip(reference, out var ship) || ship.Comp.Stage == CMUShipHijackStage.Idle)
            return true;
        if (ship.Comp.InFTL || ship.Comp.Stage == CMUShipHijackStage.Destroyed)
            return false;
        if (ship.Comp.Stage == CMUShipHijackStage.GroundCrash && (!ship.Comp.GroundImpacted || lifeboat))
            return false;
        return TryComp(ship, out EvacuationProgressComponent? progress) &&
               (progress.Progress >= CMUHijackMath.EarlyLaunchProgress || ship.Comp.SelfDestructUnlocked);
    }

    public bool CanArrive(EntityUid destination)
        => !TryGetShip(destination, out var ship) || !ship.Comp.InFTL &&
           ship.Comp.Stage is not (CMUShipHijackStage.GroundCrash or CMUShipHijackStage.Detonating or CMUShipHijackStage.Destroyed);

    public bool HasDetonationInProgress()
    {
        var query = EntityQueryEnumerator<CMUShipHijackComponent>();
        while (query.MoveNext(out var ship))
        {
            if (ship.Stage == CMUShipHijackStage.Detonating)
                return true;
        }
        return false;
    }
}
