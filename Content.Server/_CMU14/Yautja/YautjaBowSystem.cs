using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Projectiles;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaBowSystem : EntitySystem
{
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaTrapSystem _trap = default!;

    private const string ProjectileSlotId = "projectiles";
    private static readonly TimeSpan YautjaTrapXenoInterferenceDuration = TimeSpan.FromSeconds(100);
    private static readonly SoundSpecifier SnareTriggerSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/tablehit1.ogg");
    private readonly Dictionary<EntityUid, (EntityUid User, EntityUid Arrow)> _pendingNockPopups = new();
    private readonly Dictionary<EntityUid, (EntityUid User, EntityUid Arrow)> _pendingUnloadPopups = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBowComponent, DroppedEvent>(OnBowDropped);
        SubscribeLocalEvent<YautjaBowComponent, InteractUsingEvent>(OnBowInteractUsing, before: new[] { typeof(ItemSlotsSystem) });
        SubscribeLocalEvent<YautjaBowComponent, ItemSlotInsertAttemptEvent>(OnBowInsertAttempt);
        SubscribeLocalEvent<YautjaBowComponent, ItemSlotEjectAttemptEvent>(OnBowEjectAttempt);
        SubscribeLocalEvent<YautjaBowComponent, EntInsertedIntoContainerMessage>(OnBowInserted);
        SubscribeLocalEvent<YautjaBowComponent, EntRemovedFromContainerMessage>(OnBowRemoved);
        SubscribeLocalEvent<YautjaBowComponent, MapInitEvent>(OnBowMapInit);
        SubscribeLocalEvent<YautjaArrowComponent, MapInitEvent>(OnArrowMapInit);
        SubscribeLocalEvent<YautjaArrowComponent, UseInHandEvent>(OnArrowUseInHand);
        SubscribeLocalEvent<YautjaArrowComponent, YautjaArrowWarheadSelectedEvent>(OnArrowWarheadSelected);
        SubscribeLocalEvent<YautjaSnareArrowProjectileComponent, ProjectileHitEvent>(OnSnareProjectileHit);
    }

    private void OnBowMapInit(Entity<YautjaBowComponent> ent, ref MapInitEvent args)
    {
        SetBowLoadedVisual(ent, null);
    }

    private void OnBowDropped(Entity<YautjaBowComponent> ent, ref DroppedEvent args)
    {
        if (!_itemSlots.TryGetSlot(ent, ProjectileSlotId, out var slot) ||
            !slot.HasItem)
        {
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-bow-projectile-falls-out", ("bow", Name(ent))),
            ent,
            args.User);
        _pendingUnloadPopups.Remove(ent);
        _itemSlots.TryEjectToGround(ent, slot, null, dropAt: ent);
    }

    private void OnBowInteractUsing(Entity<YautjaBowComponent> ent, ref InteractUsingEvent args)
    {
        if (!HasComp<YautjaArrowComponent>(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bow-not-arrow"), ent, args.User);
            args.Handled = true;
            return;
        }

        if (_itemSlots.TryGetSlot(ent, ProjectileSlotId, out var slot) &&
            slot.HasItem)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-bow-already-loaded", ("bow", Name(ent))),
                ent,
                args.User);
            args.Handled = true;
        }
    }

    private void OnBowInsertAttempt(Entity<YautjaBowComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.User is not { } user)
        {
            args.Cancelled = true;
            return;
        }

        if (!_hands.IsHolding(user, ent))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-bow-must-hold", ("bow", Name(ent)), ("arrow", Name(args.Item))),
                ent,
                user);
            args.Cancelled = true;
            return;
        }

        if (!HasComp<YautjaComponent>(user))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-bow-not-strong-enough", ("bow", Name(ent))),
                ent,
                user);
            args.Cancelled = true;
            return;
        }

        if (args.Slot.ID == ProjectileSlotId)
            _pendingNockPopups[ent] = (user, args.Item);
    }

    private void OnBowEjectAttempt(Entity<YautjaBowComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.User is not { } user ||
            args.Slot.ID != ProjectileSlotId ||
            args.Cancelled)
        {
            return;
        }

        _pendingUnloadPopups[ent] = (user, args.Item);
    }

    private void OnBowInserted(Entity<YautjaBowComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ProjectileSlotId)
            return;

        SetBowLoadedVisual(ent, args.Entity);

        if (!_pendingNockPopups.Remove(ent, out var pending) ||
            pending.Arrow != args.Entity ||
            Deleted(pending.User))
        {
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-bow-nock", ("arrow", Name(args.Entity)), ("bow", Name(ent))),
            ent,
            pending.User);
    }

    private void OnBowRemoved(Entity<YautjaBowComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ProjectileSlotId)
            return;

        SetBowLoadedVisual(ent, null);

        if (!_pendingUnloadPopups.Remove(ent, out var pending) ||
            pending.Arrow != args.Entity ||
            Deleted(pending.User))
        {
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-bow-unload", ("arrow", Name(args.Entity)), ("bow", Name(ent))),
            ent,
            pending.User);
    }

    private void OnArrowMapInit(Entity<YautjaArrowComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.SelectedWarhead == YautjaArrowWarhead.Standard && ent.Comp.PrimaryWarhead != YautjaArrowWarhead.Standard)
            ent.Comp.SelectedWarhead = ent.Comp.PrimaryWarhead;

        ApplyArrowState(ent, ent.Comp.SelectedWarhead, ent.Comp.Activated, rename: false);
    }

    private void OnArrowUseInHand(Entity<YautjaArrowComponent> ent, ref UseInHandEvent args)
    {
        if (ent.Comp.SelectedWarhead == YautjaArrowWarhead.Snare ||
            ent.Comp.PrimaryWarhead == YautjaArrowWarhead.Snare)
        {
            return;
        }

        if (!CanUseYautjaTech(args.User))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-arrow-denied", ("arrow", Name(ent))),
                ent,
                args.User);
            args.Handled = true;
            return;
        }

        if (ent.Comp.Dynamic && !ent.Comp.Activated)
        {
            var options = new List<DialogOption>
            {
                new(Loc.GetString("cmu-yautja-arrow-dynamic-explosive"), new YautjaArrowWarheadSelectedEvent(GetNetEntity(args.User), YautjaArrowWarhead.Explosive)),
                new(Loc.GetString("cmu-yautja-arrow-dynamic-emp"), new YautjaArrowWarheadSelectedEvent(GetNetEntity(args.User), YautjaArrowWarhead.Emp)),
            };

            _dialog.OpenOptions(
                ent,
                args.User,
                Loc.GetString("cmu-yautja-arrow-dynamic-title"),
                options,
                Loc.GetString("cmu-yautja-arrow-dynamic-message"));
            args.Handled = true;
            return;
        }

        if (ent.Comp.Dynamic && ent.Comp.Activated)
        {
            ApplyArrowState(ent, ent.Comp.PrimaryWarhead, false);
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-arrow-source-deactivated", ("arrow", Name(ent))),
                ent,
                args.User);
            args.Handled = true;
            return;
        }

        if (ent.Comp.SecondaryWarhead is not { } secondary)
        {
            args.Handled = true;
            return;
        }

        if (ent.Comp.Activated)
        {
            ApplyArrowState(ent, ent.Comp.PrimaryWarhead, false);
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-arrow-source-deactivated", ("arrow", Name(ent))),
                ent,
                args.User);
        }
        else
        {
            ApplyArrowState(ent, secondary, true);
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-arrow-source-activated", ("arrow", Name(ent))),
                ent,
                args.User);
        }

        args.Handled = true;
    }

    private void OnArrowWarheadSelected(Entity<YautjaArrowComponent> ent, ref YautjaArrowWarheadSelectedEvent args)
    {
        if (!TryGetEntity(args.User, out var user) || !CanUseYautjaTech(user.Value))
            return;

        if (!ent.Comp.Dynamic)
            return;

        if (args.Warhead is not (YautjaArrowWarhead.Explosive or YautjaArrowWarhead.Emp))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-arrow-dynamic-invalid"), ent, user.Value);
            return;
        }

        ApplyArrowState(ent, args.Warhead, true);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-arrow-dynamic-changed", ("choice", DynamicWarheadName(args.Warhead))),
            ent,
            user.Value);
    }

    private bool CanUseYautjaTech(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user);
    }

    private void OnSnareProjectileHit(Entity<YautjaSnareArrowProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (Deleted(args.Target))
            return;

        var snare = Spawn(ent.Comp.SnareArrowPrototype, Transform(args.Target).Coordinates);
        var trap = EnsureComp<YautjaTrapComponent>(snare);
        trap.Armed = true;
        trap.TrapOwner = args.Shooter;
        trap.DisarmPopup = "cmu-yautja-snare-arrow-disarmed";
        trap.TriggerPopup = "cmu-yautja-snare-arrow-triggered";
        trap.TriggerSound = SnareTriggerSound;
        trap.BlocksXenoHeal = true;
        trap.TrappedMobInteractResists = true;
        trap.BroadcastOnTrigger = false;
        trap.CanTriggerYautja = true;
        trap.CanConfigureRange = false;
        trap.ShowRecoverPopup = false;
        trap.LogTrappedMobFreed = true;
        trap.ForceXenoHelpEmote = true;
        trap.ForceHumanPainEmote = true;
        trap.XenoInterferenceDuration = YautjaTrapXenoInterferenceDuration;
        Dirty(snare, trap);

        if (!_trap.TryTriggerTrap((snare, trap), args.Target))
            return;

        _appearance.SetData(snare, ToggleableVisuals.Enabled, true);
    }

    private void ApplyArrowState(Entity<YautjaArrowComponent> ent, YautjaArrowWarhead warhead, bool activated, bool rename = true)
    {
        ent.Comp.SelectedWarhead = warhead;
        ent.Comp.Activated = activated;
        Dirty(ent);

        if (TryComp(ent, out CartridgeAmmoComponent? cartridge))
        {
            cartridge.Prototype = ProjectileFor(ent.Comp, warhead);
            cartridge.Spent = false;
            Dirty(ent, cartridge);
        }

        _appearance.SetData(ent, YautjaArrowVisuals.State, VisualStateFor(ent.Comp, warhead, activated));
        if (rename)
            _meta.SetEntityName(ent, NameFor(ent.Comp, warhead, activated));
    }

    private string NameFor(YautjaArrowComponent arrow, YautjaArrowWarhead warhead, bool activated)
    {
        if (arrow.Dynamic)
        {
            if (!activated)
                return Loc.GetString("cmu-yautja-arrow-name-inert-dynamic");

            return warhead switch
            {
                YautjaArrowWarhead.Explosive => Loc.GetString("cmu-yautja-arrow-name-explosive-dynamic"),
                YautjaArrowWarhead.Emp => Loc.GetString("cmu-yautja-arrow-name-emp-dynamic"),
                _ => Loc.GetString("cmu-yautja-arrow-name-inert-dynamic"),
            };
        }

        return warhead switch
        {
            YautjaArrowWarhead.Explosive when activated => Loc.GetString("cmu-yautja-arrow-name-activated-explosive"),
            YautjaArrowWarhead.Emp when activated => Loc.GetString("cmu-yautja-arrow-name-activated-emp"),
            YautjaArrowWarhead.Snare => Loc.GetString("cmu-yautja-arrow-name-snare"),
            _ => Loc.GetString("cmu-yautja-arrow-name-inert"),
        };
    }

    private string DynamicWarheadName(YautjaArrowWarhead warhead)
    {
        return warhead switch
        {
            YautjaArrowWarhead.Emp => Loc.GetString("cmu-yautja-arrow-warhead-emp"),
            _ => Loc.GetString("cmu-yautja-arrow-warhead-explosive"),
        };
    }

    private static EntProtoId ProjectileFor(YautjaArrowComponent arrow, YautjaArrowWarhead warhead)
    {
        return warhead switch
        {
            YautjaArrowWarhead.Explosive => arrow.ExplosiveProjectile,
            YautjaArrowWarhead.Emp => arrow.EmpProjectile,
            YautjaArrowWarhead.Snare => arrow.SnareProjectile,
            _ => arrow.StandardProjectile,
        };
    }

    private static YautjaArrowVisualState VisualStateFor(YautjaArrowComponent arrow, YautjaArrowWarhead warhead, bool activated)
    {
        if (arrow.Dynamic && !activated)
            return YautjaArrowVisualState.Dynamic;

        return warhead switch
        {
            YautjaArrowWarhead.Explosive when activated => YautjaArrowVisualState.Explosive,
            YautjaArrowWarhead.Emp when activated => YautjaArrowVisualState.Emp,
            YautjaArrowWarhead.Snare => YautjaArrowVisualState.Snare,
            _ => YautjaArrowVisualState.Inert,
        };
    }

    private void SetBowLoadedVisual(EntityUid bow, EntityUid? arrow)
    {
        if (arrow is not { } arrowUid ||
            !TryComp(arrowUid, out YautjaArrowComponent? arrowComp))
        {
            _appearance.SetData(bow, YautjaBowVisuals.LoadedIcon, "none");
            return;
        }

        _appearance.SetData(bow, YautjaBowVisuals.LoadedIcon, BowLoadedIconFor(arrowComp));
    }

    private static string BowLoadedIconFor(YautjaArrowComponent arrow)
    {
        if (arrow.Dynamic && !arrow.Activated)
            return "loaded";

        return arrow.SelectedWarhead switch
        {
            YautjaArrowWarhead.Explosive when arrow.Activated => "expl",
            YautjaArrowWarhead.Emp when arrow.Activated => "emp",
            YautjaArrowWarhead.Snare => "trap",
            _ => "loaded",
        };
    }
}
