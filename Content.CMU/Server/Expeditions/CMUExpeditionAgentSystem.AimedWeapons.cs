using Content.Shared._RMC14.Weapons.Ranged.AimedShot;
using Content.Shared.CombatMode;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private SharedRMCAimedShotSystem _aimedWeapons = default!;

    private void CancelAimedWeapon(CMUExpeditionAgentComponent agent)
    {
        if (agent.AimedWeapon is { } weapon && Exists(weapon))
            _aimedWeapons.CancelControlledAimedShot(weapon);
        agent.AimedWeapon = null;
        agent.AimedTarget = null;
    }

    private bool MaintainAimedWeapon(EntityUid uid, CMUExpeditionAgentComponent agent, TimeSpan now)
    {
        if (agent.AimedWeapon is not { } weapon)
            return false;
        if (!_guns.TryGetGun(uid, out var gun) || gun.Owner != weapon ||
            !TryComp<AimedShotComponent>(weapon, out var aimed) || aimed.Targets.Count == 0 ||
            agent.Target is not { } target || target != agent.AimedTarget || !Visible(uid, target, agent.FireRange) ||
            !AcceptOrderedContact(uid, agent, target) || !_mobs.IsAlive(target) ||
            !SafeShot(uid, agent, gun, Transform(target).Coordinates) || agent.RushTarget != null ||
            agent.LastHit > agent.AimedStarted || agent.State != CMUExpeditionAgentState.Engage ||
            agent.Action != null || agent.Treatment != null || agent.PendingWeapon != null ||
            agent.SpacingDestination != null || GrenadeDanger(Transform(uid).Coordinates) || now >= agent.AimedUntil)
        {
            CancelAimedWeapon(agent);
            return false;
        }
        _steering.Unregister(uid);
        return true;
    }

    private bool StartAimedWeapon(EntityUid uid, CMUExpeditionAgentComponent agent, Entity<GunComponent> gun)
    {
        var now = _timing.CurTime;
        if (now < agent.NextAimedShot || agent.FiringAtFlash || agent.RushTarget != null ||
            agent.SpacingDestination != null || now - agent.LastHit < TimeSpan.FromSeconds(3) ||
            agent.VisibleThreats.Count > 1 || agent.Target is not { } target ||
            _transform.InRange(uid, target, 5) || !TryComp<AimedShotComponent>(gun, out var aimed))
            return false;
        agent.NextAimedShot = now + TimeSpan.FromSeconds(8);
        if (TryComp<CombatModeComponent>(uid, out var combat))
            _combat.SetInCombatMode(uid, true, combat);
        if (!_aimedWeapons.TryStartControlledAimedShot(gun, uid, target))
            return false;
        agent.AimedWeapon = gun;
        agent.AimedTarget = target;
        agent.AimedStarted = now;
        agent.AimedUntil = now + TimeSpan.FromSeconds(aimed.AimDuration + agent.FireRange * aimed.AimDistanceDifficulty + 2);
        agent.BurstEnd = agent.AimedUntil;
        agent.WeaponDecision = "native-aimed-shot";
        _steering.Unregister(uid);
        return true;
    }
}
