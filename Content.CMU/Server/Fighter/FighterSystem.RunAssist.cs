using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.Fighter;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    public bool TryPrepareRun(EntityUid? user)
    {
        if (!TryGetSeat(user, out var seat, out var aircraft) || !seat.Comp.Pilot ||
            !TryComp(aircraft, out FighterWeaponsComponent? weapons) || seat.Comp.Target is not { } target ||
            !TryGetEntity(target, out var targetUid) || targetUid == null ||
            !TryGetTarget(targetUid.Value, aircraft, weapons, out var flare) ||
            !FighterRunAssist.TryPlan(aircraft.Comp, weapons, flare)) return false;
        CancelQueuedFire(seat);
        Dirty(aircraft);
        return true;
    }

    public bool TryQueueFire(EntityUid? user)
    {
        if (!TryGetSeat(user, out var seat, out var aircraft) || !seat.Comp.Pilot) return false;
        if (seat.Comp.FireQueued)
        {
            CancelQueuedFire(seat);
            return true;
        }
        if (!TryComp(aircraft, out FighterWeaponsComponent? weapons) ||
            seat.Comp.WeaponSlot < 0 || seat.Comp.WeaponSlot >= weapons.Hardpoints.Count ||
            !TryComp(weapons.Hardpoints[seat.Comp.WeaponSlot], out FighterHardpointComponent? point) ||
            seat.Comp.Target is not { } id || !TryGetEntity(id, out var target) || target == null ||
            !TryGetTarget(target.Value, aircraft, weapons, out var flare) || point.Ammo is not { } ammo ||
            !FighterRunAssist.CanQueue(aircraft.Comp, seat.Comp, GetWeaponStatus((weapons.Hardpoints[seat.Comp.WeaponSlot], point)), flare)) return false;
        seat.Comp.FireQueued = true;
        seat.Comp.QueuedTarget = id;
        seat.Comp.QueuedSlot = seat.Comp.WeaponSlot;
        seat.Comp.QueuedAmmo = ammo;
        seat.Comp.QueuedPass = aircraft.Comp.PassNumber + (aircraft.Comp.Flying ? 0 : 1);
        Dirty(seat);
        return true;
    }

    private void CancelQueuedFire(Entity<FighterSeatComponent> seat)
    {
        if (!seat.Comp.FireQueued) return;
        seat.Comp.FireQueued = false;
        seat.Comp.QueuedTarget = null;
        seat.Comp.QueuedAmmo = null;
        Dirty(seat);
    }

    private void UpdateQueuedFire(Entity<FighterSeatComponent> seat, Entity<FighterAircraftComponent> aircraft, FighterWeaponsComponent weapons)
    {
        if (!seat.Comp.FireQueued) return;
        if (seat.Comp.Occupant is not { } user || !TryGetSeat(user, out _, out _) || !seat.Comp.Pilot ||
            aircraft.Comp.ForcedRetreat || aircraft.Comp.Phase == FighterPhase.Return ||
            !aircraft.Comp.Flying && aircraft.Comp.PassNumber == seat.Comp.QueuedPass ||
            seat.Comp.Target != seat.Comp.QueuedTarget || seat.Comp.WeaponSlot != seat.Comp.QueuedSlot ||
            aircraft.Comp.PassNumber > seat.Comp.QueuedPass ||
            seat.Comp.QueuedSlot < 0 || seat.Comp.QueuedSlot >= weapons.Hardpoints.Count ||
            !TryComp(weapons.Hardpoints[seat.Comp.QueuedSlot], out FighterHardpointComponent? point) ||
            point.Ammo != seat.Comp.QueuedAmmo || point.Ammo is not { } ammo ||
            !TryComp(ammo, out DropshipAmmoComponent? payload) || payload.Rounds < payload.RoundsPerShot ||
            seat.Comp.Target is not { } id || !TryGetEntity(id, out var target) || target == null ||
            !TryGetTarget(target.Value, aircraft, weapons, out var flare) || !flare.CanStrike || flare.Laser)
        {
            CancelQueuedFire(seat);
            return;
        }
        if (!aircraft.Comp.Flying || aircraft.Comp.PassNumber != seat.Comp.QueuedPass) return;
        var status = FighterWeapons.Status(aircraft.Comp, weapons, seat.Comp,
            GetWeaponStatus((weapons.Hardpoints[seat.Comp.QueuedSlot], point)), flare, _timing.CurTime);
        if (status != FighterFireStatus.Ready) return;
        // Consume the authorization before releasing so a single queue cannot repeat.
        CancelQueuedFire(seat);
        TryFire(user, out _);
    }
}
