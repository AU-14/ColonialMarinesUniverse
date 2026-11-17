using System.Numerics;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.CMU14.Backpack;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Server.CMU14.Backpack;

public sealed class CMUBackpackRocketAttachmentSystem : EntitySystem
{
    private const string BackSlot = "back";
    private const string RocketSlot = "cmu-aslot-backpack";
    private static readonly TimeSpan DetachDelay = TimeSpan.FromSeconds(4);

    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private AttachableHolderSystem _attachables = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<InventoryComponent, InteractUsingEvent>(OnWearerInteractUsing);
        SubscribeLocalEvent<InventoryComponent, InteractHandEvent>(OnWearerInteractHand);
        SubscribeLocalEvent<InventoryComponent, CMUBackpackRocketDetachDoAfterEvent>(OnWearerDetachDoAfter);
        SubscribeLocalEvent<CMUBackpackRocketAttachmentComponent, InteractHandEvent>(OnBackpackInteractHand);
        SubscribeLocalEvent<CMUBackpackRocketAttachmentComponent, UseInHandEvent>(OnBackpackUseInHand);
        SubscribeLocalEvent<CMUBackpackRocketAttachmentComponent, CMUBackpackRocketDetachDoAfterEvent>(OnBackpackDetachDoAfter);
    }

    private void OnWearerInteractUsing(Entity<InventoryComponent> wearer, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            args.User != wearer.Owner ||
            !HasComp<CMUBackpackRocketLauncherComponent>(args.Used) ||
            !_inventory.TryGetSlotEntity(wearer.Owner, BackSlot, out var backpackUid) ||
            backpackUid is not { } backpack ||
            !TryComp(backpack, out AttachableHolderComponent? holder) ||
            _attachables.TryGetAttachable((backpack, holder), RocketSlot, out _))
        {
            return;
        }

        _attachables.StartAttach((backpack, holder), args.Used, args.User, RocketSlot);
        args.Handled = true;
    }

    private void OnWearerInteractHand(Entity<InventoryComponent> wearer, ref InteractHandEvent args)
    {
        if (args.Handled ||
            !_inventory.TryGetSlotEntity(wearer.Owner, BackSlot, out var backpackUid) ||
            backpackUid is not { } backpack ||
            !HasComp<CMUBackpackRocketAttachmentComponent>(backpack) ||
            !TryComp(backpack, out AttachableHolderComponent? holder) ||
            !_attachables.TryGetAttachable((backpack, holder), RocketSlot, out var launcher) ||
            !CanStealFromBehind(args.User, wearer.Owner))
        {
            return;
        }

        args.Handled = StartWearerDetach(wearer.Owner, launcher.Owner, args.User);
    }

    private void OnBackpackInteractHand(Entity<CMUBackpackRocketAttachmentComponent> backpack, ref InteractHandEvent args)
    {
        if (args.Handled ||
            !TryGetAttachedLauncher(backpack.Owner, out var launcher))
        {
            return;
        }

        if (TryGetWornOwner(backpack.Owner, out var wearer))
        {
            if (!CanStealFromBehind(args.User, wearer))
                return;

            args.Handled = StartWearerDetach(wearer, launcher, args.User);
        }
        else
        {
            args.Handled = StartBackpackDetach(backpack.Owner, launcher, args.User);
        }
    }

    private void OnBackpackUseInHand(Entity<CMUBackpackRocketAttachmentComponent> backpack, ref UseInHandEvent args)
    {
        if (args.Handled ||
            TryGetWornOwner(backpack.Owner, out _) ||
            !TryGetAttachedLauncher(backpack.Owner, out var launcher))
        {
            return;
        }

        args.Handled = StartBackpackDetach(backpack.Owner, launcher, args.User);
    }

    private bool StartBackpackDetach(EntityUid backpack, EntityUid launcher, EntityUid user)
    {
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            DetachDelay,
            new CMUBackpackRocketDetachDoAfterEvent(),
            backpack,
            backpack,
            launcher)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private bool StartWearerDetach(EntityUid wearer, EntityUid launcher, EntityUid user)
    {
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            DetachDelay,
            new CMUBackpackRocketDetachDoAfterEvent(),
            wearer,
            wearer,
            launcher)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnBackpackDetachDoAfter(
        Entity<CMUBackpackRocketAttachmentComponent> backpack,
        ref CMUBackpackRocketDetachDoAfterEvent args)
    {
        if (args.Handled ||
            args.Cancelled ||
            args.Used is not { } launcher ||
            TryGetWornOwner(backpack.Owner, out _) ||
            !TryGetAttachedLauncher(backpack.Owner, out var current) ||
            current != launcher ||
            !TryComp(backpack.Owner, out AttachableHolderComponent? holder))
        {
            return;
        }

        args.Handled = _attachables.Detach((backpack.Owner, holder), launcher, args.User, RocketSlot);
    }

    private void OnWearerDetachDoAfter(Entity<InventoryComponent> wearer, ref CMUBackpackRocketDetachDoAfterEvent args)
    {
        if (args.Handled ||
            args.Cancelled ||
            args.Used is not { } launcher ||
            !_inventory.TryGetSlotEntity(wearer.Owner, BackSlot, out var backpackUid) ||
            backpackUid is not { } backpack ||
            !HasComp<CMUBackpackRocketAttachmentComponent>(backpack) ||
            !TryGetAttachedLauncher(backpack, out var current) ||
            current != launcher ||
            !CanStealFromBehind(args.User, wearer.Owner) ||
            !TryComp(backpack, out AttachableHolderComponent? holder))
        {
            return;
        }

        args.Handled = _attachables.Detach((backpack, holder), launcher, args.User, RocketSlot);
    }

    private bool TryGetAttachedLauncher(EntityUid backpack, out EntityUid launcher)
    {
        launcher = default;
        if (!TryComp(backpack, out AttachableHolderComponent? holder) ||
            !_attachables.TryGetAttachable((backpack, holder), RocketSlot, out var attached))
        {
            return false;
        }

        launcher = attached.Owner;
        return true;
    }

    private bool TryGetWornOwner(EntityUid backpack, out EntityUid wearer)
    {
        wearer = default;
        if (!_containers.TryGetContainingContainer(backpack, out var container) ||
            !HasComp<InventoryComponent>(container.Owner) ||
            !_inventory.TryGetSlotEntity(container.Owner, BackSlot, out var worn) ||
            worn != backpack)
        {
            return false;
        }

        wearer = container.Owner;
        return true;
    }

    private bool CanStealFromBehind(EntityUid user, EntityUid wearer)
    {
        if (user == wearer || !HasComp<TransformComponent>(user) || !HasComp<TransformComponent>(wearer))
            return false;

        var direction = new Vector2(
            (float) Math.Cos(_transform.GetWorldRotation(wearer).Theta),
            (float) Math.Sin(_transform.GetWorldRotation(wearer).Theta));
        var offset = _transform.GetWorldPosition(user) - _transform.GetWorldPosition(wearer);
        return Vector2.Dot(offset, direction) < 0;
    }
}
