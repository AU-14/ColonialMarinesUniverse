using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private AttachableHolderSystem _weaponAttachments = default!;
    [Dependency] private GunIFFSystem _weaponIff = default!;

    private void ConfigureEquipment(EntityUid uid, CMUExpeditionAgentComponent agent, Entity<GunComponent> gun)
    {
        // The AI's volley controller limits rounds. Select an actual supported native mode;
        // bipod-granted modes appear only after its deployment do-after succeeds.
        if ((gun.Comp.AvailableModes & SelectiveFire.FullAuto) != 0 && gun.Comp.SelectedMode != SelectiveFire.FullAuto)
            _guns.SelectFire(gun, gun.Comp, SelectiveFire.FullAuto, uid);
        if (_timing.CurTime < agent.NextEquipmentAction || !TryComp<AttachableHolderComponent>(gun, out var holder))
            return;
        agent.NextEquipmentAction = _timing.CurTime + TimeSpan.FromSeconds(5);
        if (CommittedMovement(agent) || agent.SpacingDestination != null || agent.Action != null || agent.Treatment != null ||
            agent.PendingWeapon != null || agent.RushTarget != null || agent.FlareItem != null || agent.ScavengeTarget != null ||
            agent.State is not (CMUExpeditionAgentState.Engage or CMUExpeditionAgentState.HoldAngle) ||
            _timing.CurTime - agent.LastHit < TimeSpan.FromSeconds(2) || agent.Target is not { } target ||
            !Visible(uid, target, agent.FireRange) || _transform.InRange(uid, target, 5) ||
            !SafeShot(uid, agent, gun.Comp, Transform(target).Coordinates))
            return;
        foreach (var slot in holder.Slots.Keys)
        {
            if (!_weaponAttachments.TryGetAttachable((gun, holder), slot, out var attachment) ||
                !TryComp<AttachableToggleableComponent>(attachment, out var toggle) || toggle.Active ||
                toggle.InstantToggle != AttachableInstantToggleConditions.Brace)
                continue;
            // Native deployment validates ownership/wielding, braces on suitable surfaces,
            // takes its normal time on the ground, and breaks when moving or turning away.
            var deploy = new AttachableToggleStartedEvent(gun, uid, slot);
            RaiseLocalEvent(attachment, ref deploy);
            agent.BipodsDeployed++;
            agent.WeaponDecision = "deploying-bipod";
        }
    }

    private bool IffPassesFriendly(EntityUid user, EntityUid weapon, EntityUid friendly)
    {
        if (!TryComp<CMUExpeditionWeaponRoleComponent>(weapon, out var role) || role.Rocket)
            return false;
        TryComp<GunIFFComponent>(weapon, out var gunIff);
        TryComp<GunAttachableIFFComponent>(weapon, out var attachmentIff);
        // An interlock prevents the trigger instead of allowing a projectile to pass through.
        // Match the final native AmmoShot handler, including attachment overrides and toggles.
        if (gunIff is { Enabled: false } || gunIff?.PreventFriendlyFire == true ||
            attachmentIff?.PreventFriendlyFire == true || gunIff == null && attachmentIff == null)
            return false;
        var source = attachmentIff != null || gunIff?.Intrinsic != true ? user : weapon;
        var factions = new HashSet<EntProtoId<IFFFactionComponent>>();
        if (!_weaponIff.TryGetFactions((source, null), factions))
            return false;
        foreach (var faction in factions)
            if (_weaponIff.IsInFaction(friendly, faction))
                return true;
        return false;
    }
}
