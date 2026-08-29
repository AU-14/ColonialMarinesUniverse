using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Pain.Penalties;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;

namespace Content.Server._CMU14.Medical.Injuries.Pain.Penalties;

public sealed partial class CMUMedicalSpeedSystem : SharedCMUMedicalSpeedSystem
{
    [Dependency] private AttachableHolderSystem _attachableHolder = default!;
    [Dependency] private CMGunSystem _cmGun = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private readonly HashSet<EntityUid> _refreshedGuns = new();
    private readonly HashSet<EntityUid> _queuedRefreshGuns = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUMedicalGunAimPenaltyComponent, GotUnequippedHandEvent>(OnGunUnequipped);
        SubscribeLocalEvent<CMUMedicalGunAimPenaltyComponent, HandSelectedEvent>(OnGunSelected);
        SubscribeLocalEvent<DeferredMedicalGunRefreshEvent>(OnDeferredGunRefresh);
    }

    protected override void RefreshAimDependentWeapons(EntityUid body)
    {
        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        _refreshedGuns.Clear();
        foreach (var held in _hands.EnumerateHeld((body, hands)))
        {
            if (TryComp(held, out GunComponent? heldGun))
                RefreshHeldGun((held, heldGun));

            if (_attachableHolder.TryGetSupercedingGun((held, null), out var attachableGun))
                RefreshHeldGun(attachableGun);
        }

        _refreshedGuns.Clear();
    }

    private void RefreshHeldGun(Entity<GunComponent> gun)
    {
        if (_refreshedGuns.Add(gun.Owner))
            SetPenalty(gun, true, false);
    }

    internal void OnGunEquipped(Entity<GunComponent> gun, ref GotEquippedHandEvent args)
    {
        SetPenalty(gun, HasComp<CMUHumanMedicalComponent>(args.User), false);
    }

    private void OnGunUnequipped(Entity<CMUMedicalGunAimPenaltyComponent> gun, ref GotUnequippedHandEvent args)
    {
        RemComp<CMUMedicalGunAimPenaltyComponent>(gun.Owner);
        _gun.RefreshModifiers(gun.Owner);
    }

    private void OnGunSelected(Entity<CMUMedicalGunAimPenaltyComponent> gun, ref HandSelectedEvent args)
    {
        if (!HasComp<CMUHumanMedicalComponent>(args.User))
            return;

        _gun.RefreshModifiers(gun.Owner);
    }

    internal void OnAttachableAltered(
        Entity<AttachableHolderComponent> holder,
        ref AttachableHolderAttachablesAlteredEvent args)
    {
        if (!TryComp(args.Attachable, out GunComponent? gunComp))
            return;

        var gun = new Entity<GunComponent>(args.Attachable, gunComp);
        switch (args.Alteration)
        {
            case AttachableAlteredType.Activated:
                SetPenalty(gun, IsHolderUserMedical(holder.Owner), true);
                break;
            case AttachableAlteredType.Attached:
                if (_attachableHolder.TryGetSupercedingGun(holder.AsNullable(), out var current) &&
                    current.Owner == gun.Owner)
                {
                    SetPenalty(gun, IsHolderUserMedical(holder.Owner), true);
                }

                break;
            case AttachableAlteredType.Deactivated:
            case AttachableAlteredType.Interrupted:
            case AttachableAlteredType.Detached:
            case AttachableAlteredType.DetachedDeactivated:
                SetPenalty(gun, false, true);
                break;
        }
    }

    internal void OnAttachableGunEquipped(
        Entity<GunComponent> gun,
        ref AttachableRelayedEvent<GotEquippedHandEvent> args)
    {
        var enabled = _attachableHolder.TryGetSupercedingGun((args.Holder, null), out var current) &&
            current.Owner == gun.Owner &&
            HasComp<CMUHumanMedicalComponent>(args.Args.User);
        SetPenalty(gun, enabled, true);
    }

    internal void OnAttachableGunUnequipped(
        Entity<GunComponent> gun,
        ref AttachableRelayedEvent<GotUnequippedHandEvent> args)
    {
        SetPenalty(gun, false, true);
    }

    private bool IsHolderUserMedical(EntityUid holder)
    {
        return _cmGun.TryGetGunUser(holder, out var user) &&
            HasComp<CMUHumanMedicalComponent>(user.Owner);
    }

    private void SetPenalty(Entity<GunComponent> gun, bool enabled, bool deferred)
    {
        var refresh = enabled;
        if (enabled)
            EnsureComp<CMUMedicalGunAimPenaltyComponent>(gun.Owner);
        else
            refresh = RemComp<CMUMedicalGunAimPenaltyComponent>(gun.Owner);

        if (!refresh)
            return;

        if (deferred)
            QueueGunRefresh(gun.Owner);
        else
            _gun.RefreshModifiers(gun.AsNullable());
    }

    private void QueueGunRefresh(EntityUid gun)
    {
        if (_queuedRefreshGuns.Add(gun))
            QueueLocalEvent(new DeferredMedicalGunRefreshEvent(gun));
    }

    private void OnDeferredGunRefresh(DeferredMedicalGunRefreshEvent args)
    {
        _queuedRefreshGuns.Remove(args.Gun);
        _gun.RefreshModifiers(args.Gun);
    }

    private sealed class DeferredMedicalGunRefreshEvent : EntityEventArgs
    {
        public readonly EntityUid Gun;

        public DeferredMedicalGunRefreshEvent(EntityUid gun)
        {
            Gun = gun;
        }
    }
}
