using Content.Shared.CMU14.Hijack;
using Robust.Shared.Audio;

namespace Content.Shared._RMC14.Evacuation;

// CMU14: ship flight objectives and manual reactor overloads.
public abstract partial class SharedEvacuationSystem
{
    private bool IsOnEvacuatingShip(EntityUid? map, EntityUid reference)
    {
        if (map is not { } target)
            return false;
        if (_shipHijack.TryGetShip(reference, out var ship) && ship.Comp.ShipMaps.Count > 0)
            return ship.Comp.ShipMaps.Contains(target);
        return Transform(reference).MapUid is { } referenceMap &&
            _zLevels.IsSameZNetwork(target, referenceMap);
    }

    private void ToggleHijackEvacuation(Entity<CMUShipHijackComponent> ship, SoundSpecifier? start, SoundSpecifier? cancel)
    {
        var progress = EnsureComp<EvacuationProgressComponent>(ship);
        if (!progress.Enabled && (ship.Comp.InFTL ||
            ship.Comp.Stage == CMUShipHijackStage.GroundCrash && !ship.Comp.GroundImpacted))
        {
            _marineAnnounce.AnnounceARESStaging(null, Loc.GetString("cmu-hijack-launch-unavailable"),
                faction: progress.VictimFaction);
            return;
        }

        progress.Enabled = !progress.Enabled;
        progress.EnabledAt = progress.Enabled ? _timing.CurTime : null;
        // Evacuation never starts, aborts, or resets the reactor meltdown.
        progress.SelfDestructAt = null;
        Dirty(ship.Owner, progress);
        _marineAnnounce.AnnounceARESStaging(null,
            Loc.GetString(progress.Enabled ? "cmu-hijack-evacuation-started" : "cmu-hijack-evacuation-cancelled"),
            progress.Enabled ? start : cancel, faction: progress.VictimFaction);
        if (progress.Enabled)
        {
            var ev = new EvacuationEnabledEvent(ship);
            RaiseLocalEvent(ship, ref ev, true);
        }
        else
        {
            var ev = new EvacuationDisabledEvent(ship);
            RaiseLocalEvent(ship, ref ev, true);
        }
    }
}
