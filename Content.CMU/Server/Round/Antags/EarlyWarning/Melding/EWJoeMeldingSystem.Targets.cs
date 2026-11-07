using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.CMU14.Threats.Mobs.WorkingJoe;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Melding;

/// <summary>
/// Victim selection: a living, player-controlled synthetic humanoid on the Joe's planet that is not
/// another antag, a threat, a drone or already melded. Playtest 4: organics are never melded (the
/// organic fallback and its protected-role and open-sky rules are gone). N7: synthetics that are not
/// Working Joes are preferred, then Working Joes; random within a tier.
/// </summary>
public sealed partial class EWJoeMeldingSystem
{
    /// <summary>The first gate a body fails, in the order <see cref="IsEligible"/> checks them.</summary>
    private enum MeldGate : byte
    {
        Eligible,
        Self,
        NotHumanoid,
        Dead,
        EarlyWarningJoe,
        Melded,
        Melding,
        NonHumanBody,
        NotSynthetic,
        OffNetwork,
        NoSession,
        ExclusiveAntag,
    }

    /// <summary>Synthetic bodies turned away by a search, counted per gate, for the admin log.</summary>
    private sealed class MeldRejections
    {
        public bool JoeInNullspace;
        public int Dead;
        public int OffNetwork;
        public int NoSession;
        public int ExclusiveAntag;
        public int MeldingOrMelded;
        public int Other;

        public void Count(MeldGate gate)
        {
            switch (gate)
            {
                case MeldGate.Eligible:
                case MeldGate.Self:
                    return;
                case MeldGate.Dead:
                    Dead++;
                    return;
                case MeldGate.OffNetwork:
                    OffNetwork++;
                    return;
                case MeldGate.NoSession:
                    NoSession++;
                    return;
                case MeldGate.ExclusiveAntag:
                    ExclusiveAntag++;
                    return;
                case MeldGate.Melded:
                case MeldGate.Melding:
                    MeldingOrMelded++;
                    return;
                default:
                    // another Early Warning Joe, a drone android or a threat body
                    Other++;
                    return;
            }
        }

        public override string ToString()
        {
            if (JoeInNullspace)
                return "the Joe is in nullspace";

            return $"synthetic bodies rejected: dead {Dead}, off-network {OffNetwork}, no session {NoSession}, "
                + $"exclusive antag {ExclusiveAntag}, melding/melded {MeldingOrMelded}, other (Joes, drones, threats) {Other}";
        }
    }

    /// <summary>Picks the victim for the next melding, or null when nobody is eligible.</summary>
    public EntityUid? PickMeldTarget(Entity<EWJoeComponent> joe)
    {
        return PickMeldTarget(joe, null);
    }

    private EntityUid? PickMeldTarget(Entity<EWJoeComponent> joe, MeldRejections? rejections)
    {
        var mapId = Transform(joe).MapID;
        if (mapId == MapId.Nullspace)
        {
            if (rejections != null)
                rejections.JoeInNullspace = true;

            return null;
        }

        var maps = _zLevels.GetAllNetworkMapIds(mapId);
        var candidates = new List<EntityUid>();

        var query = EntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            var gate = CheckEligible(joe, uid, maps, false);
            if (gate == MeldGate.Eligible)
            {
                candidates.Add(uid);
                continue;
            }

            if (rejections != null && IsSynthetic(uid))
                rejections.Count(gate);
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
        return CheckEligible(joe, uid, maps, pendingOk) == MeldGate.Eligible;
    }

    /// <summary>The first gate <paramref name="uid"/> fails, or <see cref="MeldGate.Eligible"/>.</summary>
    private MeldGate CheckEligible(Entity<EWJoeComponent> joe, EntityUid uid, HashSet<MapId>? maps, bool pendingOk)
    {
        if (uid == joe.Owner)
            return MeldGate.Self;

        if (!HasComp<HumanoidProfileComponent>(uid))
            return MeldGate.NotHumanoid;

        if (_mobState.IsDead(uid))
            return MeldGate.Dead;

        if (HasComp<EWJoeComponent>(uid))
            return MeldGate.EarlyWarningJoe;

        if (HasComp<RiverMeldedComponent>(uid))
            return MeldGate.Melded;

        if (!pendingOk && HasComp<RiverMeldingComponent>(uid))
            return MeldGate.Melding;

        if (EWJoeTargetRules.IsNonHumanBody(EntityManager, uid))
            return MeldGate.NonHumanBody;

        // playtest 4: the river takes synthetics only
        if (!IsSynthetic(uid))
            return MeldGate.NotSynthetic;

        // planet only (N7 decision): the z-network gate stays
        if (maps != null && !maps.Contains(Transform(uid).MapID))
            return MeldGate.OffNetwork;

        // a player who is here: a mind with a connected session
        if (!_mind.TryGetMind(uid, out var mindId, out var mind) || !_player.TryGetSessionById(mind.UserId, out _))
            return MeldGate.NoSession;

        if (_role.MindIsExclusiveAntagonist(mindId))
            return MeldGate.ExclusiveAntag;

        return MeldGate.Eligible;
    }

    /// <summary>
    /// N7: an eligible synthetic that is not a Working Joe first (the colony or aux synthetic), then a
    /// Working Joe; random within the tier. Null when there is none. Every candidate is already on the
    /// Joe's planet network.
    /// </summary>
    public EntityUid? Rank(IReadOnlyList<EntityUid> candidates)
    {
        if (candidates.Count == 0)
            return null;

        var preferred = new List<EntityUid>();
        foreach (var uid in candidates)
        {
            if (!IsWorkingJoe(uid))
                preferred.Add(uid);
        }

        return preferred.Count > 0 ? _random.Pick(preferred) : _random.Pick(candidates);
    }

    public bool IsSynthetic(EntityUid uid)
    {
        return HasComp<SynthComponent>(uid);
    }

    /// <summary>A Working Joe body: every Working Joe mob carries the Working Joe voice (<c>WorkingJoe/base.yml</c>).</summary>
    public bool IsWorkingJoe(EntityUid uid)
    {
        return HasComp<WorkingJoeVoiceComponent>(uid);
    }
}
