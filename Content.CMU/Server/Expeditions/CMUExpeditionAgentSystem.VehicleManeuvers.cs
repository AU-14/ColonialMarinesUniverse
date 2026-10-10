using System.Linq;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private bool ApproachVehicleShot(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (!agent.AntiVehicle || now < agent.NextVehicleApproach || agent.Target is not { } target ||
            !ArmedVehicle(target) || !Visible(uid, target, agent.FireRange) || agent.Action != null ||
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
        if (SafeShot(uid, agent, rocket, aim))
            return false;
        // Search a short, reachable bound to an actual safe launch position. Never chase an
        // unseen vehicle or override a covering commitment simply to spend a rocket.
        var currentExposure = ExposureScore(uid, agent, start);
        foreach (var candidate in NearbySquadPositions(start, 3))
        {
            if (_transform.InRange(start, candidate, 0.9f) || !ValidOrderPoint(uid, candidate) ||
                !_transform.InRange(start, candidate, 3.25f))
                continue;
            if (!SafeShot(uid, agent, rocket, aim, candidate) || !TraversablePassage(uid, start, candidate) ||
                ExposureScore(uid, agent, candidate) > currentExposure + 1 ||
                !TryReserveManeuver(uid, agent, now))
                continue;
            ClearCover(agent);
            agent.WeaponDecision = "moving-to-rocket-lane";
            BeginMove(uid, agent, candidate, CMUExpeditionAgentState.Reposition, now);
            return true;
        }
        return false;
    }
}
