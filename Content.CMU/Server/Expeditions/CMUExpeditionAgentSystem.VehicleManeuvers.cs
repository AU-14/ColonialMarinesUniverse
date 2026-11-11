using System.Linq;
using System.Numerics;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool ApproachVehicleShot(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (!agent.AntiVehicle || now < agent.NextVehicleApproach || agent.Target is not { } target ||
            !Visible(uid, target, agent.FireRange) || agent.Action != null ||
            agent.Treatment != null || agent.RushTarget != null || agent.FlareItem != null || agent.PendingWeapon != null ||
            CommittedMovement(agent) || HasCoverCommitment(uid, agent, now) || now - agent.LastHit < TimeSpan.FromSeconds(1))
            return false;
        agent.NextVehicleApproach = now + TimeSpan.FromSeconds(3);
        var launcher = CarriedWeapons(uid).FirstOrDefault(weapon =>
            TryComp<CMUExpeditionWeaponRoleComponent>(weapon, out var role) && role.Rocket && WeaponAmmo(weapon) > 0);
        if (launcher == default || !TryComp<GunComponent>(launcher, out var rocket))
            return false;
        var start = Transform(uid).Coordinates;
        var aim = Transform(target).Coordinates;
        if (!RocketOpportunity(uid, agent, target, aim, out agent.RocketDecision))
            return false;
        if (SafeShot(uid, agent, (launcher, rocket), aim, out agent.RocketDecision))
        {
            agent.RocketDecision = "rocket-ready";
            return false;
        }
        // Search a short, reachable bound to an actual safe launch position. Never chase an
        // unseen contact or override a covering commitment simply to spend a rocket.
        var currentExposure = ExposureScore(uid, agent, start);
        var currentDistance = Vector2.Distance(_transform.ToMapCoordinates(start).Position, _transform.ToMapCoordinates(aim).Position);
        var approaching = ArmedVehicle(target) && agent.RocketDecision == "rocket-out-of-range";
        EntityCoordinates? chosen = null;
        var best = float.MinValue;
        foreach (var candidate in NearbySquadPositions(start, 3))
        {
            if (_transform.InRange(start, candidate, 0.9f) || !ValidOrderPoint(uid, candidate) ||
                !_transform.InRange(start, candidate, 3) || Reserved(uid, candidate) || GrenadeDanger(candidate) ||
                !TraversablePassage(uid, start, candidate, allowVault: false) || !KnownDangerPassage(uid, agent, start, candidate) ||
                MeleeClearance(agent, candidate) < agent.MeleeStandoffRange || CrossesCoveringFire(uid, agent, start, candidate))
                continue;
            var exposure = ExposureScore(uid, agent, candidate);
            if (exposure > currentExposure + 0.25f)
                continue;
            var ready = SafeShot(uid, agent, (launcher, rocket), aim, out var reason, candidate);
            var distance = Vector2.Distance(_transform.ToMapCoordinates(candidate).Position, _transform.ToMapCoordinates(aim).Position);
            // Distant armor may require several covered bounds. An out-of-range stance
            // only authorizes this safe movement step; blast and backblast are checked
            // again before drawing the launcher from an actual firing position.
            if (!ready && (!approaching || reason != "rocket-out-of-range" || distance > currentDistance - 0.75f))
                continue;
            var score = (ready ? 10 : 0) + currentDistance - distance - exposure * 2;
            if (score <= best)
                continue;
            best = score;
            chosen = candidate;
        }
        if (chosen is not { } destination)
        {
            agent.RocketDecision = "rocket-no-safe-position";
            return false;
        }
        if (!TryReserveManeuver(uid, agent, now, destination))
            return false;
        ClearCover(agent);
        if (!BeginMove(uid, agent, destination, CMUExpeditionAgentState.Reposition, now))
        {
            ReleaseManeuver(uid, agent);
            return false;
        }
        agent.RocketDecision = approaching ? "closing-to-rocket-range" : "moving-to-rocket-lane";
        agent.WeaponDecision = "moving-to-rocket-lane";
        return true;
    }
}
