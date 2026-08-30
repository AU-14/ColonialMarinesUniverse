using System.Collections.Generic;
using Content.Shared.CMU14.Traits.PanicProne;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Events;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server.CMU14.Traits.PanicProne;

public sealed partial class PanicGunSystem : PanicSystem
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

        SubscribeLocalEvent<PanicGunAimPenaltyComponent, GotUnequippedHandEvent>(OnGunUnequipped);
        SubscribeLocalEvent<PanicGunAimPenaltyComponent, HandSelectedEvent>(OnGunSelected);
        SubscribeLocalEvent<DeferredPanicGunRefreshEvent>(OnDeferredGunRefresh);
    }

    protected override void RefreshAimDependentWeapons(EntityUid body)
    {
        if (!TryComp<HandsComponent>(body, out var hands))
            return;

        var peaked = TryComp<PanicComponent>(body, out var panic) && panic.Peaked;

        _refreshedGuns.Clear();
        foreach (var held in _hands.EnumerateHeld((body, hands)))
        {
            if (TryComp(held, out GunComponent? heldGun))
                RefreshHeldGun((held, heldGun), peaked);

            if (_attachableHolder.TryGetSupercedingGun((held, null), out var attachableGun))
                RefreshHeldGun(attachableGun, peaked);
        }

        _refreshedGuns.Clear();
    }

    private void RefreshHeldGun(Entity<GunComponent> gun, bool enabled)
    {
        if (_refreshedGuns.Add(gun.Owner))
            SetPenalty(gun, enabled, false);
    }

    internal void OnGunEquipped(Entity<GunComponent> gun, ref GotEquippedHandEvent args)
    {
        SetPenalty(gun, IsPeaked(args.User), false);
    }

    private void OnGunUnequipped(Entity<PanicGunAimPenaltyComponent> gun, ref GotUnequippedHandEvent args)
    {
        RemComp<PanicGunAimPenaltyComponent>(gun.Owner);
        _gun.RefreshModifiers(gun.Owner);
    }

    private void OnGunSelected(Entity<PanicGunAimPenaltyComponent> gun, ref HandSelectedEvent args)
    {
        if (!TryComp<PanicComponent>(args.User, out var panic) || !panic.Peaked)
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
                SetPenalty(gun, IsHolderUserPeaked(holder.Owner), true);
                break;
            case AttachableAlteredType.Attached:
                if (_attachableHolder.TryGetSupercedingGun(holder.AsNullable(), out var current) &&
                    current.Owner == gun.Owner)
                {
                    SetPenalty(gun, IsHolderUserPeaked(holder.Owner), true);
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
            IsPeaked(args.Args.User);
        SetPenalty(gun, enabled, true);
    }

    internal void OnAttachableGunUnequipped(
        Entity<GunComponent> gun,
        ref AttachableRelayedEvent<GotUnequippedHandEvent> args)
    {
        SetPenalty(gun, false, true);
    }

    private bool IsHolderUserPeaked(EntityUid holder)
    {
        return _cmGun.TryGetGunUser(holder, out var user) && IsPeaked(user.Owner);
    }

    private bool IsPeaked(EntityUid user)
    {
        return TryComp<PanicComponent>(user, out var panic) && panic.Peaked;
    }

    private void SetPenalty(Entity<GunComponent> gun, bool enabled, bool deferred)
    {
        var refresh = enabled;
        if (enabled)
            EnsureComp<PanicGunAimPenaltyComponent>(gun.Owner);
        else
            refresh = RemComp<PanicGunAimPenaltyComponent>(gun.Owner);

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
            QueueLocalEvent(new DeferredPanicGunRefreshEvent(gun));
    }

    private void OnDeferredGunRefresh(DeferredPanicGunRefreshEvent args)
    {
        _queuedRefreshGuns.Remove(args.Gun);
        _gun.RefreshModifiers(args.Gun);
    }

    private sealed class DeferredPanicGunRefreshEvent : EntityEventArgs
    {
        public readonly EntityUid Gun;

        public DeferredPanicGunRefreshEvent(EntityUid gun)
        {
            Gun = gun;
        }
    }
}
