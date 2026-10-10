using System.Numerics;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    // A state label is not covering fire. This predicate is shared by manoeuvres and medics.
    // A recent successful shot proves native readiness; permit ordinary gaps between bursts.
    private bool CoveringFireReady(EntityUid uid, CMUExpeditionAgentComponent agent, out EntityUid target)
    {
        target = default;
        var now = _timing.CurTime;
        if (!_mobs.IsAlive(uid) || HasComp<ActorComponent>(uid) || HasComp<KnockedDownComponent>(uid) ||
            HasComp<StunnedComponent>(uid) || _standing.IsDown(uid) || agent.Target is not { } contact ||
            agent.Action != null || agent.PendingWeapon != null || agent.Treatment != null || agent.WorkItem != null ||
            agent.PreparingWork || agent.RushTarget != null || agent.SpacingDestination != null ||
            agent.CoveringShooter != null || agent.LastDamage >= agent.EmergencyHealDamage ||
            GrenadeDanger(Transform(uid).Coordinates) ||
            agent.State is not (CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or
                CMUExpeditionAgentState.HoldAngle or CMUExpeditionAgentState.Recover) ||
            !_guns.TryGetGun(uid, out var gun) || WeaponAmmo(gun) <= 0 ||
            TryComp<CMUExpeditionWeaponRoleComponent>(gun, out var role) && role.Rocket ||
            HasComp<GunRequiresWieldComponent>(gun) && (!TryComp<WieldableComponent>(gun, out var wield) || !wield.Wielded) ||
            agent.LastFiredWeapon != gun.Owner || now - agent.LastShotAt > TimeSpan.FromSeconds(1.5) ||
            !Visible(uid, contact, WeaponFireRange(uid, agent)) || !TryAimPoint(uid, agent, gun, out var point) ||
            !SafeShot(uid, agent, gun, point))
            return false;
        target = contact;
        return true;
    }

    private bool LocalSquadMember(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid other,
        CMUExpeditionAgentComponent buddy) => other != uid && CoordinatedSquadMember(uid, agent, other, buddy) &&
        _mobs.IsAlive(other) && !HasComp<ActorComponent>(other) &&
        _transform.InRange(Transform(uid).Coordinates, Transform(other).Coordinates, 14);

    private bool TryReserveManeuver(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (HasCoverCommitment(uid, agent, now))
            return false;
        if (agent.CoveringShooter != null)
            return ManeuverSupported(uid, agent, now);
        if (agent.Target == null || now >= agent.ForgetAt)
            return true;
        var members = 0;
        EntityUid? chosen = null;
        CMUExpeditionAgentComponent? covering = null;
        var best = float.MinValue;
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
        {
            if (!LocalSquadMember(uid, agent, other, buddy) || !SharedEngagement(agent, buddy))
                continue;
            members++;
            if (buddy.CoveringFor != null && now < buddy.CoveringUntil || now < buddy.MedicalCoverUntil ||
                !CoveringFireReady(other, buddy, out _))
                continue;
            var score = (buddy.CombatRole == CMUExpeditionCombatRole.Support ? 4 : 0) +
                (buddy.CombatRole == CMUExpeditionCombatRole.Marksman ? 2 : 0) - buddy.Stress;
            if (score < best || score == best && chosen is { } incumbent && other.CompareTo(incumbent) > 0)
                continue;
            chosen = other;
            covering = buddy;
            best = score;
        }
        // A lone guard still has to fight. Nearby squadmates, however, must not all move at once.
        if (members == 0)
            return true;
        if (covering == null)
        {
            agent.SquadDecision = "holding-for-covering-fire";
            return false;
        }
        agent.CoveringShooter = chosen;
        agent.ManeuverUntil = now + TimeSpan.FromSeconds(6);
        covering.CoveringFor = uid;
        covering.CoveringUntil = now + TimeSpan.FromSeconds(1);
        covering.SquadDecision = "covering-mover";
        agent.SquadDecision = "moving-under-cover";
        agent.CoveredMoves++;
        return true;
    }

    private bool ManeuverSupported(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.CoveringShooter is not { } shooter)
            return true;
        if (now < agent.ManeuverUntil && TryComp<CMUExpeditionAgentComponent>(shooter, out var buddy) &&
            buddy.CoveringFor == uid && LocalSquadMember(uid, agent, shooter, buddy) &&
            CoveringFireReady(shooter, buddy, out _) && SharedEngagement(agent, buddy))
        {
            buddy.CoveringUntil = now + TimeSpan.FromSeconds(1);
            return true;
        }
        ReleaseManeuver(uid, agent);
        agent.SquadDecision = "support-lost-returning-fire";
        agent.InterruptedMoves++;
        return false;
    }

    private void ReleaseManeuver(EntityUid uid, CMUExpeditionAgentComponent agent)
    {
        if (agent.CoveringShooter is { } shooter && TryComp<CMUExpeditionAgentComponent>(shooter, out var buddy) &&
            buddy.CoveringFor == uid)
        {
            buddy.CoveringFor = null;
            buddy.CoveringUntil = TimeSpan.Zero;
        }
        agent.CoveringShooter = null;
        agent.ManeuverUntil = TimeSpan.Zero;
    }

    private bool HasCoverCommitment(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.CoveringFor is { } mover && now < agent.CoveringUntil &&
            TryComp<CMUExpeditionAgentComponent>(mover, out var buddy) && buddy.CoveringShooter == uid &&
            LocalSquadMember(uid, agent, mover, buddy) && CoveringFireReady(uid, agent, out _))
            return true;
        agent.CoveringFor = null;
        agent.CoveringUntil = TimeSpan.Zero;
        return now < agent.MedicalCoverUntil && CoveringFireReady(uid, agent, out _);
    }

    private bool HoldCoveringFire(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (!HasCoverCommitment(uid, agent, now))
            return false;
        agent.ContactDestination = null;
        ClearTraffic(agent);
        _steering.Unregister(uid);
        if (agent.State is not (CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.HoldAngle))
            Aim(agent, now, true);
        return true;
    }

    private bool KeepFightingPosition(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.Target == null || agent.RushTarget != null || GrenadeDanger(Transform(uid).Coordinates) ||
            agent.LastDamage >= agent.RetreatDamage || now - agent.LastHit < TimeSpan.FromSeconds(0.75) ||
            !_guns.TryGetGun(uid, out var gun) || WeaponAmmo(gun) == 0 ||
            !TryAimPoint(uid, agent, gun, out var point) || !SafeShot(uid, agent, gun, point))
            return false;
        if (agent.FightingPosition is not { } previous || !_transform.InRange(previous, Transform(uid).Coordinates, 0.6f))
        {
            agent.FightingPosition = Transform(uid).Coordinates;
            agent.PositionCommittedUntil = now + agent.PositionCommitDuration;
        }
        return true;
    }

    private bool ContinueContactMovement(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.ContactDestination is not { } destination)
            return false;
        var start = Transform(uid).Coordinates;
        if (now >= agent.ContactMoveUntil || agent.Target == null || agent.RushTarget != null ||
            agent.Action != null || agent.State is not (CMUExpeditionAgentState.Guard or CMUExpeditionAgentState.Investigate) ||
            now - agent.LastHit < TimeSpan.FromSeconds(0.4) || agent.LastDamage >= agent.RetreatDamage ||
            GrenadeDanger(start) || GrenadeDanger(destination) || !TraversablePassage(uid, start, destination) ||
            MeleeClearance(agent, destination) < agent.MeleeStandoffRange ||
            ExposureScore(uid, agent, destination) > ExposureScore(uid, agent, start) + 0.5f ||
            _transform.InRange(start, destination, ArrivalRange))
        {
            agent.ContactDestination = null;
            _steering.Unregister(uid);
            return false;
        }
        // Finish only a short, still-safe leg. The original order route is retained for resumption.
        // Support weapons settle first so the remaining members can acquire covering shooters.
        if (agent.CombatRole is CMUExpeditionCombatRole.Support or CMUExpeditionCombatRole.Marksman &&
            _guns.TryGetGun(uid, out var gun) && TryAimPoint(uid, agent, gun, out var point) && SafeShot(uid, agent, gun, point))
        {
            agent.ContactDestination = null;
            _steering.Unregister(uid);
            return false;
        }
        agent.SquadDecision = "contact-during-movement";
        Move(uid, destination, validated: true);
        return true;
    }

    // Select only the closest viable responder(s). Existing assignments count first, so a
    // sequential entity update cannot make every rifle turn away from the original attack.
    private bool AssignFlankResponse(EntityUid uid, CMUExpeditionAgentComponent agent, EntityUid threat, TimeSpan now)
    {
        var members = new List<(EntityUid Uid, CMUExpeditionAgentComponent Agent)> { (uid, agent) };
        var query = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (query.MoveNext(out var other, out var buddy))
            if (LocalSquadMember(uid, agent, other, buddy))
                members.Add((other, buddy));
        var slots = members.Count >= 6 ? 2 : 1;
        var responders = 0;
        var ranking = new List<(EntityUid Uid, float Score)>();
        foreach (var (member, buddy) in members)
        {
            if (buddy.Action != null || buddy.PendingWeapon != null || buddy.RushTarget != null ||
                buddy.Treatment != null || !Visible(member, threat, WeaponFireRange(member, buddy)) ||
                !_guns.TryGetGun(member, out var gun) || WeaponAmmo(gun) == 0 ||
                !SafeShot(member, buddy, gun, Transform(threat).Coordinates))
                continue;
            if (buddy.Target == threat && (now < buddy.FlankAssignmentUntil || CoveringFireReady(member, buddy, out _)))
            {
                if (member == uid)
                    return true;
                responders++;
                continue;
            }
            if (HasCoverCommitment(member, buddy, now))
                continue;
            var score = Vector2.Distance(_transform.GetWorldPosition(member), _transform.GetWorldPosition(threat)) +
                (buddy.CombatRole == CMUExpeditionCombatRole.Flanker ? -3 : 0) +
                (buddy.CombatRole == CMUExpeditionCombatRole.Support ? 3 : 0);
            ranking.Add((member, score));
        }
        ranking.Sort((a, b) => a.Score == b.Score ? a.Uid.CompareTo(b.Uid) : a.Score.CompareTo(b.Score));
        for (var i = 0; i < Math.Min(slots - responders, ranking.Count); i++)
        {
            if (ranking[i].Uid != uid)
                continue;
            agent.FlankAssignment = threat;
            agent.FlankAssignmentUntil = now + TimeSpan.FromSeconds(3);
            agent.SquadDecision = "covering-flank";
            return true;
        }
        return false;
    }
}
