using Content.Server.CMU14.Medical.Injuries.Pain.Penalties;
using Content.Server.CMU14.Traits.PanicProne;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared.Hands;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.CMU14.Weapons.Ranged;

/// <summary>
/// Relays the shared gun lifecycle events to each CMU gun aim penalty system.
/// </summary>
public sealed class CMUGunAimPenaltyLifecycleSystem : EntitySystem
{
    [Dependency] private readonly PanicGunSystem _panic = default!;
    [Dependency] private readonly CMUMedicalSpeedSystem _medical = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, GotEquippedHandEvent>(OnGunEquipped);
        SubscribeLocalEvent<AttachableHolderComponent, AttachableHolderAttachablesAlteredEvent>(OnAttachableAltered);
        SubscribeLocalEvent<GunComponent, AttachableRelayedEvent<GotEquippedHandEvent>>(OnAttachableGunEquipped);
        SubscribeLocalEvent<GunComponent, AttachableRelayedEvent<GotUnequippedHandEvent>>(OnAttachableGunUnequipped);
    }

    private void OnGunEquipped(Entity<GunComponent> gun, ref GotEquippedHandEvent args)
    {
        _panic.OnGunEquipped(gun, ref args);
        _medical.OnGunEquipped(gun, ref args);
    }

    private void OnAttachableAltered(
        Entity<AttachableHolderComponent> holder,
        ref AttachableHolderAttachablesAlteredEvent args)
    {
        _panic.OnAttachableAltered(holder, ref args);
        _medical.OnAttachableAltered(holder, ref args);
    }

    private void OnAttachableGunEquipped(
        Entity<GunComponent> gun,
        ref AttachableRelayedEvent<GotEquippedHandEvent> args)
    {
        _panic.OnAttachableGunEquipped(gun, ref args);
        _medical.OnAttachableGunEquipped(gun, ref args);
    }

    private void OnAttachableGunUnequipped(
        Entity<GunComponent> gun,
        ref AttachableRelayedEvent<GotUnequippedHandEvent> args)
    {
        _panic.OnAttachableGunUnequipped(gun, ref args);
        _medical.OnAttachableGunUnequipped(gun, ref args);
    }
}
