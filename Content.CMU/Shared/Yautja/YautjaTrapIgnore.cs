using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Xenonids.Construction.ResinHole;
using Content.Shared.CMU14.Explosion.BuryableMine;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.LandMines;

namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// CMU14: CM-SS13 Yautja do not set off traps. The exemption is keyed on the components that make an entity a trap
/// rather than on the trigger mechanism, so it follows the trap prototypes: any new mine, tripwire or hunting trap
/// built on one of these components is covered automatically, while step- and proximity-triggered entities that are
/// not traps - teleporters, slips, shards, chasms - carry none of them and keep reacting to a Yautja.
/// </summary>
public static class YautjaTrapIgnore
{
    public static bool IsTrap(IEntityManager entMan, EntityUid uid)
    {
        return entMan.HasComponent<LandMineComponent>(uid)
               || entMan.HasComponent<RMCLandmineComponent>(uid)
               || entMan.HasComponent<YautjaTrapComponent>(uid)
               || entMan.HasComponent<CMUBuryableMineComponent>(uid)
               || entMan.HasComponent<SapperTrapComponent>(uid)
               || entMan.HasComponent<SapperTripwireSegmentComponent>(uid)
               || entMan.HasComponent<XenoResinHoleComponent>(uid);
    }
}
