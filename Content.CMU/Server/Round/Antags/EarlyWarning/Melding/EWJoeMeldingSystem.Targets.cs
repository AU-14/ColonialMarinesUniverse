using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Melding;

/// <summary>
/// Victim selection: a living, player-controlled synthetic humanoid on the Joe's planet that is not
/// another antag, a threat, a drone or already melded; random among them. Playtest 4: organics are
/// never melded (the organic fallback and its protected-role and open-sky rules are gone).
/// </summary>
public sealed partial class EWJoeMeldingSystem
{
    /// <summary>Picks the victim for the next melding, or null when nobody is eligible.</summary>
    public EntityUid? PickMeldTarget(Entity<EWJoeComponent> joe)
    {
        var mapId = Transform(joe).MapID;
        if (mapId == MapId.Nullspace)
            return null;

        var maps = _zLevels.GetAllNetworkMapIds(mapId);
        var candidates = new List<EntityUid>();

        var query = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (IsEligible(joe, uid, maps))
                candidates.Add(uid);
        }

        return Rank(candidates);
    }

    /// <summary>
    /// Whether <paramref name="uid"/> can be melded by <paramref name="joe"/>. <paramref name="maps"/>
    /// is the Joe's z-level network; null skips the map check. <paramref name="pendingOk"/> is set
    /// when re-validating a running meld, whose victim carries the pending marker by definition.
    /// </summary>
    public bool IsEligible(Entity<EWJoeComponent> joe, EntityUid uid, HashSet<MapId>? maps, bool pendingOk = false)
    {
        if (uid == joe.Owner || !HasComp<HumanoidProfileComponent>(uid) || _mobState.IsDead(uid))
            return false;

        if (HasComp<EWJoeComponent>(uid) || HasComp<RiverMeldedComponent>(uid))
            return false;

        if (!pendingOk && HasComp<RiverMeldingComponent>(uid))
            return false;

        if (EWJoeTargetRules.IsNonHumanBody(EntityManager, uid))
            return false;

        // playtest 4: the river takes synthetics only
        if (!IsSynthetic(uid))
            return false;

        if (maps != null && !maps.Contains(Transform(uid).MapID))
            return false;

        // a player who is here: a mind with a connected session
        if (!_mind.TryGetMind(uid, out var mindId, out var mind) || !_player.TryGetSessionById(mind.UserId, out _))
            return false;

        if (_role.MindIsExclusiveAntagonist(mindId))
            return false;

        return true;
    }

    /// <summary>A random eligible synthetic, or null when there is none.</summary>
    public EntityUid? Rank(IReadOnlyList<EntityUid> candidates)
    {
        return candidates.Count == 0 ? null : _random.Pick(candidates);
    }

    public bool IsSynthetic(EntityUid uid)
    {
        return HasComp<SynthComponent>(uid);
    }
}
