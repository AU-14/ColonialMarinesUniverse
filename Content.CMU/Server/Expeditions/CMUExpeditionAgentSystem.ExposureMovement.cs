using System.Numerics;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.Movement.Components;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool UpdateExposedMovement(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        var start = Transform(uid).Coordinates;
        if (agent.ExposedStepUntil != TimeSpan.Zero)
        {
            if (now >= agent.ExposedStepUntil || agent.SpacingDestination is not { } active ||
                agent.RushTarget != null || agent.Target is not { } target ||
                !Visible(uid, target, WeaponFireRange(uid, agent)) || !TrafficPassageClear(uid, start, active) ||
                !KnownDangerPassage(uid, agent, start, active) || _transform.InRange(start, active, 0.3f))
            {
                StopSpacing(uid, agent);
                agent.ExposureMovementDecision = "settling-to-fire";
                return false;
            }
            // The existing spacing controller preserves the destination while ordinary
            // gun control keeps shooting. A new aim every tick would throw away each volley.
            KeepCombatSpacing(uid, agent, now);
            return true;
        }
        if (now < agent.NextExposedStep || agent.SpacingDestination != null || now < agent.SpacingUntil ||
            agent.Target is not { } enemy || agent.RushTarget != null || agent.ContactFromRadio ||
            agent.State is not (CMUExpeditionAgentState.Guard or CMUExpeditionAgentState.Watch or
                CMUExpeditionAgentState.Aim or CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.HoldAngle or CMUExpeditionAgentState.Recover) ||
            agent.Action != null || agent.Treatment != null || agent.PendingWeapon != null || agent.AimedWeapon != null ||
            agent.FlareItem != null || agent.WorkItem != null || agent.PreparingWork || agent.ScavengeTarget != null ||
            agent.HoldPosition || agent.Entrench || agent.CoveringShooter != null || agent.LastDamage >= agent.EmergencyHealDamage ||
            !Visible(uid, enemy, WeaponFireRange(uid, agent)) || IsMeleeThreat(enemy) ||
            !_guns.TryGetGun(uid, out var gun) || WeaponAmmo(gun) <= 0 || agent.LastFiredWeapon != gun.Owner ||
            TryComp<CMUExpeditionWeaponRoleComponent>(gun, out var role) && role.Rocket ||
            !TryAimPoint(uid, agent, gun, out var aim) || !SafeShot(uid, agent, gun, aim))
            return false;

        var underFire = now < agent.IncomingFireUntil || now < agent.SuppressedUntil || now - agent.LastHit < TimeSpan.FromSeconds(2);
        var support = agent.CombatRole is CMUExpeditionCombatRole.Support or CMUExpeditionCombatRole.Marksman;
        if (!CMUCombatMovementPolicy.ShouldStrafe(underFire, agent.CoverAnchor != null || ShelteredFromKnownThreats(uid, agent, start),
                HasCoverCommitment(uid, agent, now), support, (now - agent.FirstContact).TotalSeconds, (now - agent.LastShotAt).TotalSeconds))
            return false;
        // A short firing step is bounded even when no covering shooter has a clear angle.
        // Permit only one such mover locally, so the whole line does not strafe together.
        var members = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (members.MoveNext(out var other, out var buddy))
            if (LocalSquadMember(uid, agent, other, buddy) && buddy.ExposedStepUntil > now)
                return false;

        agent.NextExposedStep = now + TimeSpan.FromSeconds(1);
        if (NearbyUsefulCover(uid, agent, start))
        {
            agent.ExposureMovementDecision = "keeping-nearby-cover";
            return false;
        }
        var speed = TryComp<MovementSpeedModifierComponent>(uid, out var movement) ? movement.CurrentSprintSpeed : 2f;
        if (speed < 0.9f)
            return false;
        var toEnemy = _transform.ToCoordinates(start.EntityId, _transform.ToMapCoordinates(aim)).Position - start.Position;
        var distance = toEnemy.Length();
        if (distance < 0.1f)
            return false;
        var forward = toEnemy / distance;
        var side = new Vector2(-forward.Y, forward.X) * (uid.Id % 2 == 0 ? 1 : -1);
        var exposure = ExposureScore(uid, agent, start);
        var crowding = FriendlyCrowding(uid, start);
        var options = new List<CMUCombatStepOption>();
        var stride = Math.Clamp(speed * 0.75f, 1.05f, 1.6f);
        foreach (var length in new[] { stride, Math.Max(0.95f, stride * 0.7f) })
        foreach (var direction in new[] { side, -side, Vector2.Normalize(side - forward * 0.65f),
                     Vector2.Normalize(-side - forward * 0.65f), Vector2.Normalize(side + forward * 0.65f),
                     Vector2.Normalize(-side + forward * 0.65f), -forward })
        {
            var offset = direction * length;
            var candidate = start.Offset(offset);
            var middle = start.Offset(offset * 0.5f);
            var safe = agent.Home is { } home && _transform.InRange(home, candidate, agent.LeashRange) &&
                !(agent.LastExposedStepOrigin is { } previous && Exists(previous.EntityId) && now < agent.AvoidExposedStepOriginUntil &&
                    _transform.InRange(previous, candidate, 0.65f)) &&
                !(agent.FailedPosition is { } failed && now < agent.AvoidPositionUntil && _transform.InRange(failed, candidate, 0.8f)) &&
                !Reserved(uid, candidate) && TraversablePassage(uid, start, candidate, allowVault: false) &&
                TrafficPassageClear(uid, start, candidate) && KnownDangerPassage(uid, agent, start, candidate) &&
                MeleeClearance(agent, candidate) >= Math.Min(agent.MeleeStandoffRange, MeleeClearance(agent, start)) &&
                ExposureScore(uid, agent, middle) <= exposure + 0.25f &&
                !CrossesCoveringFire(uid, agent, start, candidate) &&
                SafeShot(uid, agent, gun, aim, middle) && SafeShot(uid, agent, gun, aim, candidate);
            options.Add(new CMUCombatStepOption(offset, (toEnemy - offset).Length(),
                safe ? ExposureScore(uid, agent, candidate) : exposure,
                safe ? FriendlyCrowding(uid, candidate) : crowding, safe));
        }
        var selected = CMUCombatMovementPolicy.SelectStep(options, forward, distance, agent.PreferredFireRange,
            Math.Min(distance, agent.MinimumFireRange), WeaponFireRange(uid, agent) - 0.25f, exposure, crowding);
        if (selected < 0)
        {
            agent.ExposureMovementDecision = "no-safe-firing-step";
            return false;
        }
        var destination = start.Offset(options[selected].Offset);
        var state = agent.State;
        var fireAt = agent.FireAt;
        var resumeVolley = agent.ResumeVolley;
        BeginCombatSpacing(uid, agent, now);
        // Preserve both unfinished volleys and their pauses; changing position alone does
        // not grant a fresh shot budget or skip a previously scheduled recovery.
        agent.State = state;
        agent.FireAt = fireAt;
        agent.ResumeVolley = resumeVolley;
        var duration = Math.Clamp(options[selected].Offset.Length() / speed + 0.35f, 0.75f, 1.6f);
        agent.SpacingDestination = destination;
        agent.SpacingUntil = agent.SpacingMoveUntil = agent.ExposedStepUntil = now + TimeSpan.FromSeconds(duration);
        agent.NextExposedStep = agent.ExposedStepUntil + TimeSpan.FromSeconds(CMUCombatMovementPolicy.SettleSeconds(underFire, support) + uid.Id % 4 * 0.15);
        agent.LastExposedStepOrigin = start;
        agent.AvoidExposedStepOriginUntil = now + TimeSpan.FromSeconds(8);
        agent.ExposedSteps++;
        agent.SpacingDecision = agent.ExposureMovementDecision = "exposed-firing-step";
        agent.FightingPosition = null;
        Move(uid, destination, precise: true, validated: true);
        return true;
    }

    private bool NearbyUsefulCover(EntityUid uid, CMUExpeditionAgentComponent agent, EntityCoordinates start)
    {
        foreach (var distance in new[] { 1.25f, 2f })
        for (var direction = 0; direction < 8; direction++)
        {
            var angle = direction * MathF.Tau / 8;
            var point = start.Offset(new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance);
            if (!Reserved(uid, point) && TraversablePassage(uid, start, point, allowVault: false) &&
                KnownDangerPassage(uid, agent, start, point) && ShelteredFromKnownThreats(uid, agent, point))
                return true;
        }
        return false;
    }

    private bool CrossesCoveringFire(EntityUid uid, CMUExpeditionAgentComponent agent,
        EntityCoordinates start, EntityCoordinates destination)
    {
        var from = _transform.ToMapCoordinates(start).Position;
        var to = _transform.ToMapCoordinates(destination).Position;
        var members = EntityQueryEnumerator<CMUExpeditionAgentComponent>();
        while (members.MoveNext(out var other, out var buddy))
        {
            if (!LocalSquadMember(uid, agent, other, buddy) || !CoveringFireReady(other, buddy, out var target))
                continue;
            var origin = _transform.GetWorldPosition(other);
            var end = _transform.GetWorldPosition(target);
            if (CorridorsConflict(from, to, origin, end) &&
                SegmentDistance(to, origin, end) < SegmentDistance(from, origin, end) + 0.4f)
                return true;
        }
        return false;
    }
}
