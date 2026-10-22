using Content.Shared.CMU14.Fighter;

namespace Content.Server.CMU14.Fighter;

public sealed partial class FighterSystem
{
    private void OnPartTerminating(Entity<CMUFighterPartOwnerComponent> part, ref EntityTerminatingEvent args)
    {
        var owner = part.Comp.Aircraft;
        if (TerminatingOrDeleted(owner) || !TryComp(owner, out FighterAircraftComponent? aircraft))
            return;

        if (aircraft.FrontSeat == part.Owner) aircraft.FrontSeat = null;
        if (aircraft.RearSeat == part.Owner) aircraft.RearSeat = null;
        if (aircraft.Hull == part.Owner) aircraft.Hull = null;
        if (aircraft.Canopy == part.Owner) aircraft.Canopy = null;
        Dirty(owner, aircraft);

        if (aircraft.GroundEntity is { } hull && !TerminatingOrDeleted(hull) &&
            TryComp(hull, out FighterGroundComponent? ground))
        {
            if (ground.FrontSeat == part.Owner) ground.FrontSeat = null;
            if (ground.RearSeat == part.Owner) ground.RearSeat = null;
            if (ground.Canopy == part.Owner) ground.Canopy = null;
            Dirty(hull, ground);
        }

        if (!TryComp(owner, out FighterWeaponsComponent? weapons))
            return;

        var slot = weapons.Hardpoints.IndexOf(part.Owner);
        if (slot < 0)
            return;

        // Slot numbers are used by targeting and queued fire. Leave a network-safe
        // empty entry instead of shifting every subsequent pylon down one slot.
        weapons.Hardpoints[slot] = EntityUid.Invalid;
        weapons.Loadout.RemoveAll(status => status.Slot == slot);
        Dirty(owner, weapons);
    }
}
