using Content.Shared.CMU14.Hygiene;
using Content.Shared.CMU14.Item.Stain;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using System;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMUWashclothSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    [Dependency] private SharedDoAfterSystem _doAfter = default!;

    [Dependency] private InventorySystem _inventory = default!;

    [Dependency] private MetaDataSystem _metadata = default!;

    [Dependency] private SharedPopupSystem _popup = default!;

    [Dependency] private CMUResidueSystem _residue = default!;

    [Dependency] private CMUItemStainSystem _stains = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUWashclothComponent, UseInHandEvent>(OnUse);
        SubscribeLocalEvent<CMUWashclothComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CMUWashclothComponent, CMUWashclothDoAfterEvent>(OnFinished);
        SubscribeLocalEvent<CMUWashclothComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(Entity<CMUWashclothComponent> ent, ref ExaminedEvent args)
        => args.PushMarkup(Loc.GetString("cmu-cloth-uses", ("uses", ent.Comp.UsesRemaining)));
    private void OnUse(Entity<CMUWashclothComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        StartWipe(ent, args.User, args.User);
    }

    private void OnAfterInteract(Entity<CMUWashclothComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<HumanoidProfileComponent>(target))
            return;
        args.Handled = true;
        StartWipe(ent, args.User, target);
    }

    private void StartWipe(Entity<CMUWashclothComponent> ent, EntityUid user, EntityUid target)
    {
        if (ent.Comp.UsesRemaining <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-cloth-spent"), ent, user);
            return;
        }
        if (!NeedsWipe(target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-cloth-already-clean"), ent, user);
            return;
        }
        var action = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(5),
            new CMUWashclothDoAfterEvent(), ent.Owner, target, ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BlockDuplicate = true,
        };
        if (_doAfter.TryStartDoAfter(action))
            _popup.PopupEntity(Loc.GetString("cmu-cloth-start"), user, user);
    }

    private void OnFinished(Entity<CMUWashclothComponent> ent, ref CMUWashclothDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.UsesRemaining <= 0)
            return;
        args.Handled = true;
        if (args.Target is not { } target || !NeedsWipe(target))
            return;
        _residue.Clean(target);
        _stains.TryClean(target);
        var slots = _inventory.GetSlotEnumerator(target, SlotFlags.WITHOUT_POCKET);
        while (slots.NextItem(out var item))
        {
            if (HasComp<ClothingComponent>(item))
                _stains.TryClean(item);
        }
        ent.Comp.UsesRemaining--;
        if (ent.Comp.UsesRemaining == 0)
        {
            _appearance.SetData(ent.Owner, CMUWashclothVisuals.Used, true);
            _metadata.SetEntityName(ent, Loc.GetString("cmu-cloth-used-name"));
            _metadata.SetEntityDescription(ent, Loc.GetString("cmu-cloth-used-desc"));
        }
        _popup.PopupEntity(Loc.GetString("cmu-cloth-finish"), args.User, args.User);
    }

    private bool NeedsWipe(EntityUid target)
    {
        if (TryComp<CMUResidueComponent>(target, out var residue) && residue.Color != null ||
            TryComp<CMUItemStainComponent>(target, out var bodyStain) && bodyStain.Color != null)
            return true;
        var slots = _inventory.GetSlotEnumerator(target, SlotFlags.WITHOUT_POCKET);
        while (slots.NextItem(out var item))
        {
            if (HasComp<ClothingComponent>(item) &&
                TryComp<CMUItemStainComponent>(item, out var stain) && stain.Color != null)
                return true;
        }
        return false;
    }
}
