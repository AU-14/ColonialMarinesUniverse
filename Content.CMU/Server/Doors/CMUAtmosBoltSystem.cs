using Content.Shared.CMU14.Doors;
using Content.Shared.CMU14.Ship;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Doors;

/// <summary>
///     Bolts a shut <see cref="CMUAtmosBoltComponent"/> airlock while the air beside it is unsafe, by the same test a
///     self-sealing firelock uses, and lifts the bolts once it's safe. If someone lifts them by hand in the meantime,
///     the door leaves them up until the air recovers. It only does this once a hijacked dropship has crashed into the
///     ship (<see cref="CMUShipCrashedMapComponent"/>), so doors next to space don't bolt themselves before then.
///     Examining a door bolted this way shows the same pressure warning a firelock does.
/// </summary>
public sealed partial class CMUAtmosBoltSystem : EntitySystem
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.5);

    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private CMUAtmosHazardSystem _hazard = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextCheck;

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckInterval;

        var query = EntityQueryEnumerator<CMUAtmosBoltComponent, DoorComponent, DoorBoltComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var atmosBolt, out var door, out var bolts, out var xform))
        {
            if (xform.MapUid is not { } map || !HasComp<CMUShipCrashedMapComponent>(map))
            {
                SetBoltedByAtmos((uid, atmosBolt), false);
                continue;
            }

            if (atmosBolt.BoltedByAtmos && !bolts.BoltsDown)
            {
                // Someone lifted the bolts by hand.
                SetBoltedByAtmos((uid, atmosBolt), false);
                atmosBolt.Overridden = true;
            }

            // Only a shut door holds anything back; an open one bolts once it closes.
            if (door.State != DoorState.Closed && !atmosBolt.BoltedByAtmos)
                continue;

            var hazardous = _hazard.IsHazardous(uid, xform, atmosBolt.LowPressure, atmosBolt.PressureDifference, false);
            if (!hazardous)
            {
                atmosBolt.Overridden = false;
                if (atmosBolt.BoltedByAtmos)
                {
                    SetBoltedByAtmos((uid, atmosBolt), false);
                    _door.SetBoltsDown((uid, bolts), false);
                }

                continue;
            }

            if (atmosBolt.BoltedByAtmos || atmosBolt.Overridden || bolts.BoltsDown)
                continue;

            if (_door.TrySetBoltDown((uid, bolts), true))
                SetBoltedByAtmos((uid, atmosBolt), true);
        }
    }

    private void SetBoltedByAtmos(Entity<CMUAtmosBoltComponent> ent, bool bolted)
    {
        if (ent.Comp.BoltedByAtmos == bolted)
            return;

        ent.Comp.BoltedByAtmos = bolted;
        Dirty(ent);
    }
}
