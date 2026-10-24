using Content.Shared.CMU14.Hygiene;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using System.Linq;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMULaundryHamperSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    [Dependency] private SharedInteractionSystem _interaction = default!;

    [Dependency] private SharedPopupSystem _popup = default!;

    [Dependency] private SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMULaundryHamperComponent, MapInitEvent>(OnHamperInit,
            after: new[] { typeof(SharedStorageSystem) });
        SubscribeLocalEvent<CMULaundryHamperComponent, EntInsertedIntoContainerMessage>(OnHamperInserted,
            after: new[] { typeof(SharedStorageSystem) });
        SubscribeLocalEvent<CMULaundryHamperComponent, EntRemovedFromContainerMessage>(OnHamperRemoved,
            after: new[] { typeof(SharedStorageSystem) });
        SubscribeLocalEvent<CMULaundryComponent, DragDropTargetEvent>(OnTransfer);
        SubscribeLocalEvent<CMULaundryComponent, CMULaundryTransferDoAfterEvent>(OnTransferFinished);
    }

    private void OnTransfer(Entity<CMULaundryComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !HasComp<CMULaundryHamperComponent>(args.Dragged) ||
            !TryComp<StorageComponent>(args.Dragged, out var source))
            return;
        args.Handled = true;
        if (source.Container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-hamper-empty"), ent, args.User);
            return;
        }
        if (ent.Comp.Broken)
        {
            _popup.PopupEntity(Loc.GetString("cmu-laundry-broken"), ent, args.User);
            return;
        }
        if (ent.Comp.FinishAt != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-hamper-washer-running"), ent, args.User);
            return;
        }
        var action = new DoAfterArgs(EntityManager, args.User, 3f,
            new CMULaundryTransferDoAfterEvent(), ent.Owner, ent.Owner, args.Dragged)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = false,
            BreakOnDropItem = false,
            BlockDuplicate = true,
            CancelDuplicate = false,
            ForceVisible = true,
        };
        if (_doAfter.TryStartDoAfter(action))
            _popup.PopupEntity(Loc.GetString("cmu-hamper-transfer-start"), ent, args.User);
    }

    private void OnTransferFinished(Entity<CMULaundryComponent> ent, ref CMULaundryTransferDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;
        if (args.Used is not { } hamper || !HasComp<CMULaundryHamperComponent>(hamper) ||
            !TryComp<StorageComponent>(hamper, out var source) ||
            !_interaction.InRangeUnobstructed(args.User, hamper, popup: true) ||
            !_interaction.InRangeUnobstructed(args.User, ent.Owner, popup: true))
            return;
        if (ent.Comp.Broken)
        {
            _popup.PopupEntity(Loc.GetString("cmu-laundry-broken"), ent, args.User);
            return;
        }
        if (ent.Comp.FinishAt != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-hamper-washer-running"), ent, args.User);
            return;
        }
        if (source.Container.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-hamper-empty"), ent, args.User);
            return;
        }
        var moved = 0;
        foreach (var item in source.Container.ContainedEntities.ToArray())
        {
            if (_storage.Insert(ent.Owner, item, out _, user: args.User, playSound: false))
                moved++;
        }
        var remaining = source.Container.ContainedEntities.Count;
        var message = moved == 0 ? "cmu-hamper-no-fit" :
            remaining == 0 ? "cmu-hamper-transferred-all" : "cmu-hamper-transferred-part";
        _popup.PopupEntity(Loc.GetString(message, ("count", moved), ("remaining", remaining)), ent, args.User);
    }

    private void OnHamperInit(Entity<CMULaundryHamperComponent> ent, ref MapInitEvent args)
        => RefreshFill(ent.Owner);

    private void OnHamperInserted(Entity<CMULaundryHamperComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == StorageComponent.ContainerId)
            RefreshFill(ent.Owner);
    }

    private void OnHamperRemoved(Entity<CMULaundryHamperComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == StorageComponent.ContainerId)
            RefreshFill(ent.Owner);
    }

    private void RefreshFill(EntityUid uid)
    {
        if (!TryComp<StorageComponent>(uid, out var storage))
            return;
        var stage = 0;
        if (storage.Container.ContainedEntities.Count > 0)
        {
            stage = 1;
            if (_appearance.TryGetData<int>(uid, StorageVisuals.StorageUsed, out var used) &&
                _appearance.TryGetData<int>(uid, StorageVisuals.Capacity, out var capacity) &&
                capacity > 0 && used >= capacity * 0.75f)
                stage = 2;
        }
        _appearance.SetData(uid, CMULaundryHamperVisuals.Fill, stage);
    }
}
