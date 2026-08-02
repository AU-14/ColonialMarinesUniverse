using Content.Server.Administration.Logs;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Inventory;
using Content.Shared.Actions;
using Content.Shared.ActionBlocker;
using Content.Shared.CombatMode;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Database;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Content.Shared._RMC14.Xenonids.Construction.ResinWhisper;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaAttachmentSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedDoorSystem _doors = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaPowerSystem _power = default!;

    private static readonly TimeSpan BracerAttachmentForceAirlockDoAfter = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BracerAttachmentForceResinOpenDoAfter = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan BracerAttachmentForceResinCloseDoAfter = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaGearContainerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<YautjaGearContainerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<YautjaGearContainerComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<YautjaGearContainerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaBracerUnequippedEvent>(OnBracerUnequipped);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaToggleCasterActionEvent>(OnToggleCaster);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaToggleWristBladesActionEvent>(OnToggleWristBlades);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaToggleScimitarActionEvent>(OnToggleScimitar);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaToggleShieldActionEvent>(OnToggleShield);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaToggleChainGauntletActionEvent>(OnToggleChainGauntlet);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaRemoveBracerAttachmentsActionEvent>(OnRemoveBracerAttachments);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaBracerAttachmentSlotSelectedEvent>(OnBracerAttachmentSlotSelected);

        SubscribeLocalEvent<YautjaBadBloodGearChoiceComponent, GotEquippedEvent>(OnBadBloodBracerEquipped);

        Subs.BuiEvents<YautjaBadBloodGearChoiceComponent>(YautjaBadBloodWeaponChoiceUI.Key, subs =>
        {
            subs.Event<YautjaBadBloodWeaponChoiceMsg>(OnBadBloodWeaponChoice);
        });

        SubscribeLocalEvent<YautjaStoredGearComponent, RMCItemDropAttemptEvent>(OnStoredGearDropAttempt);
        SubscribeLocalEvent<YautjaStoredGearComponent, UseInHandEvent>(OnStoredGearUseInHand);
        SubscribeLocalEvent<YautjaStoredGearComponent, ThrowItemAttemptEvent>(OnStoredGearThrowAttempt);
        SubscribeLocalEvent<YautjaStoredGearComponent, FellDownThrowAttemptEvent>(OnStoredGearFellDownThrowAttempt);
        SubscribeLocalEvent<YautjaStoredGearComponent, ContainerGettingRemovedAttemptEvent>(OnStoredGearRemoveAttempt);
        SubscribeLocalEvent<YautjaStoredGearComponent, ComponentShutdown>(OnStoredGearShutdown);
        SubscribeLocalEvent<YautjaStoredGearComponent, DroppedEvent>(OnStoredGearDropped);
        SubscribeLocalEvent<YautjaStoredGearComponent, RMCDroppedEvent>(OnStoredGearRMCDropped);
        SubscribeLocalEvent<DoorComponent, YautjaBracerAttachmentForceDoorDoAfterEvent>(OnBracerAttachmentForceDoorDoAfter);
        SubscribeLocalEvent<AirlockComponent, InteractUsingEvent>(OnForceAirlockWithBracerAttachment);
        SubscribeLocalEvent<ResinDoorComponent, InteractUsingEvent>(OnForceResinDoorWithBracerAttachment);
    }

    private void OnMapInit(Entity<YautjaGearContainerComponent> ent, ref MapInitEvent args)
    {
        EnsureContainer(ent);

        HashSet<YautjaGearKind>? deferred = null;
        if (TryComp(ent.Owner, out YautjaBadBloodGearChoiceComponent? choice) && !choice.Chosen)
            deferred = new HashSet<YautjaGearKind>(choice.Choices);

        foreach (var kind in ent.Comp.GearPrototypes.Keys)
        {
            if (deferred != null && deferred.Contains(kind))
                continue;

            EnsureGear(ent, kind);
        }
    }

    private void OnShutdown(Entity<YautjaGearContainerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var gear in ent.Comp.Gear.Values)
        {
            if (!TerminatingOrDeleted(gear))
                QueueDel(gear);
        }

        foreach (var gear in ent.Comp.SecondaryGear.Values)
        {
            if (!TerminatingOrDeleted(gear))
                QueueDel(gear);
        }

        ent.Comp.Gear.Clear();
        ent.Comp.SecondaryGear.Clear();
        ent.Comp.InstalledGear.Clear();
    }

    private void OnGetItemActions(Entity<YautjaGearContainerComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands || args.SlotFlags == null || (args.SlotFlags.Value & ent.Comp.Slots) == 0)
            return;

        var youngblood = HasComp<YautjaYoungbloodComponent>(args.User);
        if (!youngblood && HasAction(ent.Comp, YautjaGearKind.Caster))
            args.AddAction(ref ent.Comp.ToggleCasterAction, ent.Comp.ToggleCasterActionId);

        if (HasAction(ent.Comp, YautjaGearKind.WristBlades))
            args.AddAction(ref ent.Comp.ToggleWristBladesAction, ent.Comp.ToggleWristBladesActionId);
        if (HasAction(ent.Comp, YautjaGearKind.Scimitar))
            args.AddAction(ref ent.Comp.ToggleScimitarAction, ent.Comp.ToggleScimitarActionId);

        if (!youngblood && HasAction(ent.Comp, YautjaGearKind.Shield))
            args.AddAction(ref ent.Comp.ToggleShieldAction, ent.Comp.ToggleShieldActionId);

        if (HasAction(ent.Comp, YautjaGearKind.ChainGauntlet))
            args.AddAction(ref ent.Comp.ToggleChainGauntletAction, ent.Comp.ToggleChainGauntletActionId);
    }

    private static bool HasAction(YautjaGearContainerComponent bracer, YautjaGearKind kind)
    {
        return bracer.ActionWhitelist == null || bracer.ActionWhitelist.Contains(kind);
    }

    private void OnInteractUsing(Entity<YautjaGearContainerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !TryComp(args.Used, out YautjaStoredGearComponent? stored))
        {
            return;
        }

        if (!CanUseYautjaGear(args.User) ||
            IsYoungbloodRestrictedGear(args.User, stored.Kind))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-denied"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (stored.Deployed)
            return;

        EnsureAttachedWeapon(args.Used, stored);

        if (ShouldPromptForBracerAttachmentSlot(ent.Comp, stored.Kind))
        {
            args.Handled = true;
            OpenBracerAttachmentSlotDialog(ent, args.User, args.Used, stored.Kind);
            return;
        }

        var secondarySlot = ShouldUseSecondaryAttachmentSlot(ent.Comp, stored.Kind, args.Used);
        if (!TryInstallStoredGear(ent, args.User, args.Used, stored, secondarySlot))
            return;

        args.Handled = true;
    }

    private void OnBracerAttachmentSlotSelected(Entity<YautjaGearContainerComponent> ent, ref YautjaBracerAttachmentSlotSelectedEvent args)
    {
        if (!TryGetEntity(args.User, out var user) ||
            !TryGetEntity(args.Gear, out var gear) ||
            !TryComp(gear.Value, out YautjaStoredGearComponent? stored) ||
            stored.Kind != args.Kind)
        {
            return;
        }

        if (!CanUseYautjaGear(user.Value) ||
            IsYoungbloodRestrictedGear(user.Value, stored.Kind))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-denied"), user.Value, user.Value, PopupType.SmallCaution);
            return;
        }

        if (stored.Deployed)
            return;

        EnsureAttachedWeapon(gear.Value, stored);

        if (!_hands.IsHolding(user.Value, gear.Value))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-attachment-not-held", ("item", gear.Value)), user.Value, user.Value, PopupType.SmallCaution);
            return;
        }

        TryInstallStoredGear(ent, user.Value, gear.Value, stored, args.SecondarySlot);
    }

    private bool TryInstallStoredGear(
        Entity<YautjaGearContainerComponent> ent,
        EntityUid user,
        EntityUid gear,
        YautjaStoredGearComponent stored,
        bool secondarySlot)
    {
        if (secondarySlot && !CanUseSecondaryAttachmentSlot(stored.Kind))
            return false;

        if (CanUseSecondaryAttachmentSlot(stored.Kind) &&
            AreBothBracerAttachmentSlotsOccupied(ent.Comp) &&
            !IsInstalledBracerAttachment(ent.Comp, gear))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-attachments-full", ("bracer", ent.Owner)), user, user, PopupType.SmallCaution);
            return false;
        }

        if (secondarySlot)
        {
            if (ent.Comp.SecondaryGear.TryGetValue(stored.Kind, out var oldSecondary) &&
                oldSecondary != gear &&
                !TerminatingOrDeleted(oldSecondary))
            {
                if (TryComp(oldSecondary, out YautjaStoredGearComponent? oldStored) && oldStored.Deployed)
                    return false;

                ent.Comp.InstalledGear.Remove(oldSecondary);
                QueueDel(oldSecondary);
            }

            ent.Comp.SecondaryGear[stored.Kind] = gear;
        }
        else
        {
            if (ent.Comp.Gear.TryGetValue(stored.Kind, out var oldGear) &&
                oldGear != gear &&
                !TerminatingOrDeleted(oldGear))
            {
                if (TryComp(oldGear, out YautjaStoredGearComponent? oldStored) && oldStored.Deployed)
                    return false;

                ent.Comp.InstalledGear.Remove(oldGear);
                QueueDel(oldGear);
            }

            ent.Comp.Gear[stored.Kind] = gear;
        }

        if (ent.Comp.SecondaryGear.TryGetValue(stored.Kind, out var secondaryGear) &&
            secondaryGear == gear &&
            ent.Comp.Gear.TryGetValue(stored.Kind, out var samePrimary) &&
            samePrimary == gear)
        {
            ent.Comp.SecondaryGear.Remove(stored.Kind);
        }

        var container = EnsureContainer(ent);
        if (!_containers.Insert(gear, container, force: true))
        {
            if (secondarySlot)
                ent.Comp.SecondaryGear.Remove(stored.Kind);
            else
                ent.Comp.Gear.Remove(stored.Kind);

            return false;
        }

        ent.Comp.InstalledGear.Add(gear);
        SetGearState(ent, gear, stored.Kind, false);
        if (MetaData(gear).EntityPrototype is { } prototype && !secondarySlot)
            ent.Comp.GearPrototypes[stored.Kind] = prototype.ID;

        Dirty(ent);
        PlayGearSound(ent.Comp.InstallAttachmentSound, user);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-attachment-installed", ("item", gear), ("bracer", ent.Owner)), user, user);
        return true;
    }

    private void OpenBracerAttachmentSlotDialog(Entity<YautjaGearContainerComponent> bracer, EntityUid user, EntityUid gear, YautjaGearKind kind)
    {
        var userNet = GetNetEntity(user);
        var gearNet = GetNetEntity(gear);
        var options = new List<DialogOption>
        {
            new(Loc.GetString("cmu-yautja-bracer-attachment-right"), new YautjaBracerAttachmentSlotSelectedEvent(userNet, gearNet, kind, true)),
            new(Loc.GetString("cmu-yautja-bracer-attachment-left"), new YautjaBracerAttachmentSlotSelectedEvent(userNet, gearNet, kind, false)),
        };

        _dialog.OpenOptions(
            bracer.Owner,
            user,
            Loc.GetString("cmu-yautja-bracer-attachment-slot-title", ("bracer", bracer.Owner)),
            options,
            Loc.GetString("cmu-yautja-bracer-attachment-slot-message", ("item", gear), ("bracer", bracer.Owner)));
    }

    private bool ShouldPromptForBracerAttachmentSlot(YautjaGearContainerComponent bracer, YautjaGearKind kind)
    {
        return CanUseSecondaryAttachmentSlot(kind) && !HasInstalledBracerAttachment(bracer);
    }

    private bool HasInstalledBracerAttachment(YautjaGearContainerComponent bracer)
    {
        return IsBracerAttachmentSlotOccupied(bracer, secondarySlot: false) ||
               IsBracerAttachmentSlotOccupied(bracer, secondarySlot: true);
    }

    private bool ShouldUseSecondaryAttachmentSlot(YautjaGearContainerComponent bracer, YautjaGearKind kind, EntityUid gear)
    {
        if (!CanUseSecondaryAttachmentSlot(kind))
            return false;

        var leftOccupied = IsBracerAttachmentSlotOccupied(bracer, secondarySlot: false);
        var rightOccupied = IsBracerAttachmentSlotOccupied(bracer, secondarySlot: true);

        if (leftOccupied && !rightOccupied)
            return true;

        if (rightOccupied && !leftOccupied)
            return false;

        return bracer.Gear.TryGetValue(kind, out var primaryGear) &&
               primaryGear != gear &&
               bracer.InstalledGear.Contains(primaryGear);
    }

    private bool AreBothBracerAttachmentSlotsOccupied(YautjaGearContainerComponent bracer)
    {
        return IsBracerAttachmentSlotOccupied(bracer, secondarySlot: false) &&
               IsBracerAttachmentSlotOccupied(bracer, secondarySlot: true);
    }

    private bool IsBracerAttachmentSlotOccupied(YautjaGearContainerComponent bracer, bool secondarySlot)
    {
        var slots = secondarySlot ? bracer.SecondaryGear : bracer.Gear;
        foreach (var (kind, gear) in slots)
        {
            if (!CanUseSecondaryAttachmentSlot(kind) ||
                !IsInstalledBracerAttachment(bracer, gear))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool IsInstalledBracerAttachment(YautjaGearContainerComponent bracer, EntityUid gear)
    {
        return bracer.InstalledGear.Contains(gear) &&
               !TerminatingOrDeleted(gear) &&
               TryComp(gear, out YautjaStoredGearComponent? stored) &&
               CanUseSecondaryAttachmentSlot(stored.Kind);
    }

    private static bool CanUseSecondaryAttachmentSlot(YautjaGearKind kind)
    {
        return kind is YautjaGearKind.WristBlades or YautjaGearKind.Scimitar or YautjaGearKind.Shield or YautjaGearKind.ChainGauntlet;
    }

    private void OnBracerUnequipped(Entity<YautjaGearContainerComponent> ent, ref YautjaBracerUnequippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        RetractHeldGear(ent, args.User);
    }

    private void OnToggleCaster(Entity<YautjaGearContainerComponent> ent, ref YautjaToggleCasterActionEvent args)
    {
        ToggleGear(ent, args, YautjaGearKind.Caster);
    }

    private void OnToggleWristBlades(Entity<YautjaGearContainerComponent> ent, ref YautjaToggleWristBladesActionEvent args)
    {
        ToggleBracerAttachments(ent, args);
    }

    private void OnToggleScimitar(Entity<YautjaGearContainerComponent> ent, ref YautjaToggleScimitarActionEvent args)
    {
        ToggleGear(ent, args, YautjaGearKind.Scimitar);
    }

    private void OnToggleShield(Entity<YautjaGearContainerComponent> ent, ref YautjaToggleShieldActionEvent args)
    {
        ToggleGear(ent, args, YautjaGearKind.Shield);
    }

    private void OnToggleChainGauntlet(Entity<YautjaGearContainerComponent> ent, ref YautjaToggleChainGauntletActionEvent args)
    {
        ToggleGear(ent, args, YautjaGearKind.ChainGauntlet);
    }

    private void OnRemoveBracerAttachments(Entity<YautjaGearContainerComponent> ent, ref YautjaRemoveBracerAttachmentsActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        var user = args.Performer;

        if (!CanUseYautjaGear(user) ||
            !_power.TryGetWornBracer(user, out var wornBracer) ||
            wornBracer.Owner != ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
            return;
        }

        TryRemoveBracerAttachments(ent, user);
    }

    private void OnStoredGearDropAttempt(Entity<YautjaStoredGearComponent> ent, ref RMCItemDropAttemptEvent args)
    {
        if (!ent.Comp.Deployed)
            return;

        if (TryGetCurrentHolder(ent.Owner, out var user))
            TryRetractStoredGear(ent, user);

        args.Cancelled = true;
    }

    private void OnStoredGearUseInHand(Entity<YautjaStoredGearComponent> ent, ref UseInHandEvent args)
    {
        var holder = ent.Comp.AttachmentHolder ?? ent.Owner;
        var holderStored = ent.Comp.AttachmentHolder is { } attachmentHolder && TryComp(attachmentHolder, out YautjaStoredGearComponent? attachmentStored)
            ? attachmentStored
            : ent.Comp;

        if (args.Handled ||
            !holderStored.Deployed ||
            holderStored.Bracer is not { } bracer ||
            TerminatingOrDeleted(bracer) ||
            !TryComp<YautjaGearContainerComponent>(bracer, out var bracerComp) ||
            !bracerComp.InstalledGear.Contains(holder) ||
            !CanUseYautjaGear(args.User) ||
            !_power.TryGetWornBracer(args.User, out var wornBracer) ||
            wornBracer.Owner != bracer)
        {
            return;
        }

        RetractDeployedInstalledAttachments((bracer, bracerComp), args.User);
        args.Handled = true;
    }

    private void OnForceAirlockWithBracerAttachment(Entity<AirlockComponent> target, ref InteractUsingEvent args)
    {
        OnForceDoorWithBracerAttachment(target.Owner, ref args);
    }

    private void OnForceResinDoorWithBracerAttachment(Entity<ResinDoorComponent> target, ref InteractUsingEvent args)
    {
        OnForceDoorWithBracerAttachment(target.Owner, ref args);
    }

    private void OnForceDoorWithBracerAttachment(EntityUid target, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            target != args.Target ||
            !TryComp(target, out DoorComponent? door) ||
            !CanUseBracerAttachmentOnDoor(args.User, args.Used))
        {
            return;
        }

        var close = false;
        TimeSpan delay;

        if (HasComp<ResinDoorComponent>(target))
        {
            if (_combatMode.IsInCombatMode(args.User))
                return;

            switch (door.State)
            {
                case DoorState.Closed:
                    delay = BracerAttachmentForceResinOpenDoAfter;
                    break;
                case DoorState.Open:
                    close = true;
                    delay = BracerAttachmentForceResinCloseDoAfter;
                    break;
                default:
                    return;
            }
        }
        else
        {
            if (!HasComp<AirlockComponent>(target) ||
                door.State != DoorState.Closed ||
                _doors.IsBolted(target))
            {
                return;
            }

            delay = BracerAttachmentForceAirlockDoAfter;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            args.User,
            delay,
            new YautjaBracerAttachmentForceDoorDoAfterEvent(close),
            target,
            target,
            args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 2f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
    }

    private void OnBracerAttachmentForceDoorDoAfter(Entity<DoorComponent> door, ref YautjaBracerAttachmentForceDoorDoAfterEvent args)
    {
        if (args.Cancelled ||
            args.Target != door.Owner ||
            args.Used is not { } used ||
            !CanUseBracerAttachmentOnDoor(args.User, used))
        {
            return;
        }

        if (args.Close)
        {
            if (door.Comp.State == DoorState.Open)
            {
                _doors.StartClosing(door.Owner, door.Comp, args.User);
                args.Handled = true;
            }

            return;
        }

        if (door.Comp.State != DoorState.Closed)
            return;

        _doors.StartOpening(door.Owner, door.Comp, args.User);
        args.Handled = true;
    }

    private bool CanUseBracerAttachmentOnDoor(EntityUid user, EntityUid used)
    {
        var holder = used;
        YautjaStoredGearComponent? stored = null;
        if (TryComp(used, out YautjaStoredGearComponent? usedStored))
        {
            holder = usedStored.AttachmentHolder ?? used;
            stored = usedStored.AttachmentHolder is { } attachmentHolder && TryComp(attachmentHolder, out YautjaStoredGearComponent? attachmentStored)
                ? attachmentStored
                : usedStored;
        }

        if (!CanUseYautjaGear(user) ||
            _hands.GetActiveItem(user) != used ||
            HasComp<YautjaChainGauntletComponent>(used) ||
            stored == null ||
            !stored.Deployed ||
            stored.Bracer is not { } bracer ||
            TerminatingOrDeleted(bracer) ||
            !TryComp(bracer, out YautjaGearContainerComponent? bracerComp) ||
            !bracerComp.InstalledGear.Contains(holder) ||
            !_power.TryGetWornBracer(user, out var wornBracer) ||
            wornBracer.Owner != bracer ||
            !_actionBlocker.CanConsciouslyPerformAction(user) ||
            !_actionBlocker.CanUseHeldEntity(user, used))
        {
            return false;
        }

        return true;
    }

    private void OnStoredGearThrowAttempt(Entity<YautjaStoredGearComponent> ent, ref ThrowItemAttemptEvent args)
    {
        if (!ent.Comp.Deployed)
            return;

        TryRetractStoredGear(ent, args.User);
        args.Cancelled = true;
    }

    private void OnStoredGearFellDownThrowAttempt(Entity<YautjaStoredGearComponent> ent, ref FellDownThrowAttemptEvent args)
    {
        if (!ent.Comp.Deployed)
            return;

        args.Cancelled = true;
    }

    private void OnStoredGearRemoveAttempt(EntityUid uid, YautjaStoredGearComponent comp, ContainerGettingRemovedAttemptEvent args)
    {
        if (!comp.Deployed || comp.Retracting || !HasComp<HandsComponent>(args.Container.Owner))
            return;

        var ent = (uid, comp);
        if (!TryRetractStoredGear(ent, args.Container.Owner))
            return;

        args.Cancel();
    }

    private void OnStoredGearShutdown(Entity<YautjaStoredGearComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent.Owner) ||
            ent.Comp.AttachedWeapon is not { } attached ||
            TerminatingOrDeleted(attached))
        {
            return;
        }

        QueueDel(attached);
        ent.Comp.AttachedWeapon = null;
    }

    private void OnStoredGearDropped(Entity<YautjaStoredGearComponent> ent, ref DroppedEvent args)
    {
        if (!ent.Comp.Deployed)
            return;

        TryRetractStoredGear(ent, args.User);
    }

    private void OnStoredGearRMCDropped(Entity<YautjaStoredGearComponent> ent, ref RMCDroppedEvent args)
    {
        if (!ent.Comp.Deployed)
            return;

        TryRetractStoredGear(ent, args.User);
    }

    private void ToggleGear(Entity<YautjaGearContainerComponent> bracer, InstantActionEvent args, YautjaGearKind kind)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        var user = args.Performer;

        if (!CanUseYautjaGear(user) ||
            !_power.TryGetWornBracer(user, out var wornBracer) ||
            wornBracer.Owner != bracer.Owner)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
            return;
        }

        if (IsYoungbloodRestrictedGear(user, kind))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-youngblood-gear-denied"), user, user, PopupType.SmallCaution);
            return;
        }

        TryToggleGear(bracer, user, kind);
    }

    private void ToggleBracerAttachments(Entity<YautjaGearContainerComponent> bracer, InstantActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        var user = args.Performer;

        if (!CanUseYautjaGear(user) ||
            !_power.TryGetWornBracer(user, out var wornBracer) ||
            wornBracer.Owner != bracer.Owner)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
            return;
        }

        TryToggleBracerAttachments(bracer, user);
    }

    public bool TryToggleBracerAttachments(Entity<YautjaGearContainerComponent> bracer, EntityUid user)
    {
        if (AnyInstalledGearDeployed(bracer.Comp))
        {
            RetractDeployedInstalledAttachments(bracer, user);
            _actions.SetToggled(bracer.Comp.ToggleWristBladesAction, AnyInstalledGearDeployed(bracer.Comp));
            return true;
        }

        if (!_power.HasPowerPopup(user, bracer.Comp.BracerAttachmentDeployPowerCost, popupOnServer: true) ||
            !_power.TryRemovePower(user, bracer.Comp.BracerAttachmentDeployPowerCost, popup: false))
        {
            return false;
        }

        var deployed = false;
        foreach (var gear in EnumerateInstalledBracerAttachmentsBySourceSlot(bracer.Comp))
        {
            if (TerminatingOrDeleted(gear) ||
                !TryComp(gear, out YautjaStoredGearComponent? stored))
            {
                continue;
            }

            DeployGear(bracer, user, gear, stored.Kind);
            deployed |= stored.Deployed;
        }

        if (!deployed)
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-attachments-none"), user, user, PopupType.SmallCaution);

        _actions.SetToggled(bracer.Comp.ToggleWristBladesAction, AnyInstalledGearDeployed(bracer.Comp));
        return true;
    }

    public bool TryToggleCaster(Entity<YautjaGearContainerComponent> bracer, EntityUid user)
    {
        return TryToggleGear(bracer, user, YautjaGearKind.Caster);
    }

    public bool TryRemoveBracerAttachments(Entity<YautjaGearContainerComponent> bracer, EntityUid user)
    {
        if (AnyInstalledGearDeployed(bracer.Comp))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-attachments-retract-first"), user, user, PopupType.SmallCaution);
            return false;
        }

        var installed = new EntityUid[bracer.Comp.InstalledGear.Count];
        bracer.Comp.InstalledGear.CopyTo(installed);

        var removed = false;
        foreach (var gear in installed)
        {
            if (TerminatingOrDeleted(gear) ||
                !TryComp(gear, out YautjaStoredGearComponent? stored))
            {
                continue;
            }

            if (!CanUseSecondaryAttachmentSlot(stored.Kind))
            {
                continue;
            }

            RemoveInstalledAttachment(bracer, user, gear, stored);
            removed = true;
        }

        if (!removed)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-attachments-none"), user, user, PopupType.SmallCaution);
            return false;
        }

        Dirty(bracer);
        return true;
    }

    private bool TryToggleGear(Entity<YautjaGearContainerComponent> bracer, EntityUid user, YautjaGearKind kind)
    {
        var container = EnsureContainer(bracer);
        if (EnsureGear(bracer, kind) is not { } gear)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-gear-missing"), user, user, PopupType.SmallCaution);
            return false;
        }

        var secondary = bracer.Comp.SecondaryGear.GetValueOrDefault(kind);
        if (IsStoredAndRetracted(container, gear) &&
            (secondary == default || TerminatingOrDeleted(secondary) || IsStoredAndRetracted(container, secondary)))
        {
            if (!_power.HasPowerPopup(user, bracer.Comp.BracerAttachmentDeployPowerCost, popupOnServer: true) ||
                !_power.TryRemovePower(user, bracer.Comp.BracerAttachmentDeployPowerCost, popup: false))
            {
                return false;
            }

            DeployGear(bracer, user, gear, kind);
            if (secondary != default && !TerminatingOrDeleted(secondary))
                DeployGear(bracer, user, secondary, kind);

            return true;
        }

        var toggled = false;
        if (ShouldRetract(container, gear))
        {
            RetractGear(bracer, user, gear, kind);
            toggled = true;
        }

        if (secondary != default && !TerminatingOrDeleted(secondary) && ShouldRetract(container, secondary))
        {
            RetractGear(bracer, user, secondary, kind);
            toggled = true;
        }

        return toggled;
    }

    private bool IsStoredAndRetracted(Container bracerContainer, EntityUid gear)
    {
        return bracerContainer.Contains(gear) &&
               (!TryComp(gear, out YautjaStoredGearComponent? stored) || !stored.Deployed);
    }

    private bool ShouldRetract(Container bracerContainer, EntityUid gear)
    {
        return (TryComp(gear, out YautjaStoredGearComponent? stored) && stored.Deployed) ||
               !bracerContainer.Contains(gear);
    }

    private void DeployGear(Entity<YautjaGearContainerComponent> bracer, EntityUid user, EntityUid gear, YautjaGearKind kind)
    {
        if (!TryComp(gear, out YautjaStoredGearComponent? stored))
            return;

        var deployed = EnsureAttachedWeapon(gear, stored) ?? gear;
        if (!TryPickupStoredGear(bracer, user, gear, deployed, kind))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hands-full"), user, user, PopupType.SmallCaution);
            return;
        }

        SetGearState(bracer, gear, kind, true);
        PlayGearSound(GetDeploySound(bracer.Comp, kind), user);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-gear-deployed", ("item", deployed)), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} deployed Yautja gear {ToPrettyString(deployed):gear} from {ToPrettyString(bracer.Owner):bracer}");
    }

    private bool TryPickupStoredGear(Entity<YautjaGearContainerComponent> bracer, EntityUid user, EntityUid gear, EntityUid deployed, YautjaGearKind kind)
    {
        if (!bracer.Comp.InstalledGear.Contains(gear) ||
            !CanUseSecondaryAttachmentSlot(kind))
            return _hands.TryPickupAnyHand(user, deployed, checkActionBlocker: false);

        var location = IsSecondaryAttachment(bracer.Comp, kind, gear)
            ? HandLocation.Right
            : HandLocation.Left;

        if (!TryGetHandByLocation(user, location, out var handId))
            return kind == YautjaGearKind.Shield && _hands.TryPickupAnyHand(user, deployed, checkActionBlocker: false);

        if (_hands.TryPickup(user, deployed, handId, checkActionBlocker: false))
            return true;

        // Shields are a single attachment. The selected bracer side is only a
        // preferred hand; a free opposite hand must still be accepted.
        return kind == YautjaGearKind.Shield &&
               _hands.TryPickupAnyHand(user, deployed, checkActionBlocker: false);
    }

    private bool TryGetHandByLocation(EntityUid user, HandLocation location, out string handId)
    {
        handId = string.Empty;
        if (!TryComp(user, out HandsComponent? hands))
            return false;

        foreach (var (id, hand) in _hands.EnumerateHandsInSortedOrder((user, hands)))
        {
            if (hand.Location != location)
                continue;

            handId = id;
            return true;
        }

        return false;
    }

    private void RetractGear(Entity<YautjaGearContainerComponent> bracer, EntityUid user, EntityUid gear, YautjaGearKind kind)
    {
        if (!TryComp(gear, out YautjaStoredGearComponent? stored))
            return;

        var deployed = stored.AttachedWeapon ?? gear;
        if (!_hands.IsHolding(user, deployed))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-gear-not-held"), user, user, PopupType.SmallCaution);
            return;
        }

        if (!TryInsertDeployedGear(gear, deployed, stored))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-gear-retract-failed"), user, user, PopupType.SmallCaution);
            return;
        }

        SetGearState(bracer, gear, kind, false);
        PlayGearSound(GetRetractSound(bracer.Comp, kind), user);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-gear-retracted", ("item", deployed)), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} retracted Yautja gear {ToPrettyString(deployed):gear} into {ToPrettyString(bracer.Owner):bracer}");
    }

    private void RetractHeldGear(Entity<YautjaGearContainerComponent> bracer, EntityUid user)
    {
        var container = EnsureContainer(bracer);
        foreach (var (kind, gear) in bracer.Comp.Gear)
        {
            if (TerminatingOrDeleted(gear) ||
                !TryComp(gear, out YautjaStoredGearComponent? stored) ||
                stored.AttachedWeapon is not { } deployed ||
                !_hands.IsHolding(user, deployed))
            {
                if (TerminatingOrDeleted(gear) || !_hands.IsHolding(user, gear))
                    continue;

                if (TryInsertStoredGear(gear, container))
                {
                    SetGearState(bracer, gear, kind, false);
                    PlayGearSound(GetRetractSound(bracer.Comp, kind), user);
                }

                continue;
            }

            if (TryInsertDeployedGear(gear, deployed, stored))
            {
                SetGearState(bracer, gear, kind, false);
                PlayGearSound(GetRetractSound(bracer.Comp, kind), user);
            }
        }

        foreach (var (kind, gear) in bracer.Comp.SecondaryGear)
        {
            if (TerminatingOrDeleted(gear) ||
                !TryComp(gear, out YautjaStoredGearComponent? stored) ||
                stored.AttachedWeapon is not { } deployed ||
                !_hands.IsHolding(user, deployed))
            {
                if (TerminatingOrDeleted(gear) || !_hands.IsHolding(user, gear))
                    continue;

                if (TryInsertStoredGear(gear, container))
                {
                    SetGearState(bracer, gear, kind, false);
                    PlayGearSound(GetRetractSound(bracer.Comp, kind), user);
                }

                continue;
            }

            if (TryInsertDeployedGear(gear, deployed, stored))
            {
                SetGearState(bracer, gear, kind, false);
                PlayGearSound(GetRetractSound(bracer.Comp, kind), user);
            }
        }
    }

    private bool AnyInstalledGearDeployed(YautjaGearContainerComponent bracer)
    {
        foreach (var gear in bracer.InstalledGear)
        {
            if (TryComp(gear, out YautjaStoredGearComponent? stored) && stored.Deployed)
                return true;
        }

        return false;
    }

    private IEnumerable<EntityUid> EnumerateInstalledBracerAttachmentsBySourceSlot(YautjaGearContainerComponent bracer)
    {
        foreach (var (kind, gear) in bracer.Gear)
        {
            if (CanUseSecondaryAttachmentSlot(kind) &&
                IsInstalledBracerAttachment(bracer, gear))
            {
                yield return gear;
            }
        }

        foreach (var (kind, gear) in bracer.SecondaryGear)
        {
            if (CanUseSecondaryAttachmentSlot(kind) &&
                IsInstalledBracerAttachment(bracer, gear))
            {
                yield return gear;
            }
        }
    }

    private void RetractDeployedInstalledAttachments(Entity<YautjaGearContainerComponent> bracer, EntityUid user)
    {
        var installed = new EntityUid[bracer.Comp.InstalledGear.Count];
        bracer.Comp.InstalledGear.CopyTo(installed);

        foreach (var gear in installed)
        {
            if (TerminatingOrDeleted(gear) ||
                !TryComp(gear, out YautjaStoredGearComponent? stored) ||
                !stored.Deployed)
            {
                continue;
            }

            var deployed = stored.AttachedWeapon ?? gear;
            if (!_hands.IsHolding(user, deployed))
                continue;

            RetractGear(bracer, user, gear, stored.Kind);
        }
    }

    private void RemoveInstalledAttachment(
        Entity<YautjaGearContainerComponent> bracer,
        EntityUid user,
        EntityUid gear,
        YautjaStoredGearComponent stored)
    {
        bracer.Comp.InstalledGear.Remove(gear);

        if (bracer.Comp.Gear.TryGetValue(stored.Kind, out var primary) && primary == gear)
        {
            bracer.Comp.Gear.Remove(stored.Kind);
            bracer.Comp.GearPrototypes.Remove(stored.Kind);
        }

        if (bracer.Comp.SecondaryGear.TryGetValue(stored.Kind, out var secondary) && secondary == gear)
            bracer.Comp.SecondaryGear.Remove(stored.Kind);

        stored.Bracer = null;
        stored.Deployed = false;
        stored.Retracting = false;

        if (!_hands.TryPickupAnyHand(user, gear, checkActionBlocker: false))
            _transform.SetCoordinates(gear, Transform(user).Coordinates);

        PlayGearSound(bracer.Comp.RemoveAttachmentSound, user);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-attachment-removed", ("item", gear), ("bracer", bracer.Owner)), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} removed Yautja bracer attachment {ToPrettyString(gear):gear} from {ToPrettyString(bracer.Owner):bracer}");
    }

    private bool TryRetractStoredGear(Entity<YautjaStoredGearComponent> gear, EntityUid user)
    {
        var holder = gear.Comp.AttachmentHolder ?? gear.Owner;
        var stored = gear.Comp.AttachmentHolder is { } attachmentHolder && TryComp(attachmentHolder, out YautjaStoredGearComponent? attachmentStored)
            ? attachmentStored
            : gear.Comp;

        if (TerminatingOrDeleted(holder) ||
            stored.Bracer is not { } bracer ||
            TerminatingOrDeleted(bracer) ||
            !TryComp<YautjaGearContainerComponent>(bracer, out var bracerComp))
        {
            return false;
        }

        var bracerEnt = (bracer, bracerComp);
        if (stored.AttachedWeapon is { } deployed && !TerminatingOrDeleted(deployed))
        {
            if (stored.AttachedContainer?.Contains(deployed) == true)
            {
                SetGearState(bracerEnt, holder, stored.Kind, false);
                return true;
            }

            if (!TryInsertDeployedGear(holder, deployed, stored))
                return false;

            SetGearState(bracerEnt, holder, stored.Kind, false);
            PlayGearSound(GetRetractSound(bracerComp, stored.Kind), user);
            PopupAutoRetract(stored.Kind, user);
            _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} auto-retracted Yautja gear {ToPrettyString(deployed):gear} into {ToPrettyString(bracer):bracer}");
            return true;
        }

        var container = EnsureContainer(bracerEnt);
        if (container.Contains(holder))
        {
            SetGearState(bracerEnt, holder, stored.Kind, false);
            return true;
        }

        var inserted = TryInsertStoredGear(holder, container, stored);

        if (!inserted)
            return false;

        SetGearState(bracerEnt, holder, stored.Kind, false);
        PlayGearSound(GetRetractSound(bracerComp, stored.Kind), user);
        PopupAutoRetract(stored.Kind, user);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} auto-retracted Yautja gear {ToPrettyString(holder):gear} into {ToPrettyString(bracer):bracer}");
        return true;
    }

    private void PopupAutoRetract(YautjaGearKind kind, EntityUid user)
    {
        if (kind != YautjaGearKind.Caster)
            return;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-caster-deactivated"), user, user);
    }

    private bool TryGetCurrentHolder(EntityUid gear, out EntityUid user)
    {
        user = default;
        if (!_containers.TryGetContainingContainer((gear, null, null), out var container) ||
            !HasComp<HandsComponent>(container.Owner))
        {
            return false;
        }

        user = container.Owner;
        return true;
    }

    private bool TryInsertStoredGear(EntityUid gear, Container container, YautjaStoredGearComponent? stored = null)
    {
        if (!Resolve(gear, ref stored, false))
            return _containers.Insert(gear, container, force: true);

        stored.Retracting = true;
        try
        {
            return _containers.Insert(gear, container, force: true);
        }
        finally
        {
            stored.Retracting = false;
        }
    }

    private EntityUid? EnsureAttachedWeapon(EntityUid holder, YautjaStoredGearComponent stored)
    {
        if (stored.DeployedPrototype == null)
            return null;

        var container = EnsureAttachedContainer(holder, stored);
        if (stored.AttachedWeapon is { } existing && !TerminatingOrDeleted(existing))
        {
            if (!container.Contains(existing) &&
                !_containers.IsEntityInContainer(existing))
            {
                _containers.Insert(existing, container, force: true);
            }

            return existing;
        }

        var weapon = Spawn(stored.DeployedPrototype.Value, Transform(holder).Coordinates);
        var weaponStored = EnsureComp<YautjaStoredGearComponent>(weapon);
        weaponStored.AttachmentHolder = holder;
        weaponStored.Kind = stored.Kind;
        weaponStored.Bracer = stored.Bracer;
        weaponStored.Deployed = stored.Deployed;
        stored.AttachedWeapon = weapon;

        if (!_containers.Insert(weapon, container, force: true))
        {
            QueueDel(weapon);
            stored.AttachedWeapon = null;
            return null;
        }

        return weapon;
    }

    private ContainerSlot EnsureAttachedContainer(EntityUid holder, YautjaStoredGearComponent stored)
    {
        stored.AttachedContainer ??= _containers.EnsureContainer<ContainerSlot>(holder, stored.AttachedContainerId);
        return stored.AttachedContainer;
    }

    private bool TryInsertDeployedGear(EntityUid holder, EntityUid deployed, YautjaStoredGearComponent stored)
    {
        if (holder == deployed)
        {
            return stored.Bracer is { } bracer &&
                   TryComp(bracer, out YautjaGearContainerComponent? bracerComp) &&
                   TryInsertStoredGear(holder, EnsureContainer((bracer, bracerComp)), stored);
        }

        var container = EnsureAttachedContainer(holder, stored);
        var deployedStored = CompOrNull<YautjaStoredGearComponent>(deployed);
        stored.Retracting = true;
        if (deployedStored != null)
            deployedStored.Retracting = true;

        try
        {
            return _containers.Insert(deployed, container, force: true);
        }
        finally
        {
            if (deployedStored != null)
                deployedStored.Retracting = false;

            stored.Retracting = false;
        }
    }

    private EntityUid? EnsureGear(Entity<YautjaGearContainerComponent> bracer, YautjaGearKind kind)
    {
        if (bracer.Comp.Gear.TryGetValue(kind, out var existing) && !TerminatingOrDeleted(existing))
            return existing;

        if (!bracer.Comp.GearPrototypes.TryGetValue(kind, out var prototype))
            return null;

        var container = EnsureContainer(bracer);
        var gear = Spawn(prototype, Transform(bracer.Owner).Coordinates);
        SetGearState(bracer, gear, kind, false);
        if (TryComp(gear, out YautjaStoredGearComponent? stored))
            EnsureAttachedWeapon(gear, stored);

        if (!_containers.Insert(gear, container, force: true))
        {
            QueueDel(gear);
            bracer.Comp.Gear.Remove(kind);
            return null;
        }

        return gear;
    }

    private void SetGearState(Entity<YautjaGearContainerComponent> bracer, EntityUid gear, YautjaGearKind kind, bool deployed)
    {
        if (!bracer.Comp.SecondaryGear.TryGetValue(kind, out var secondary) || secondary != gear)
            bracer.Comp.Gear[kind] = gear;

        Dirty(bracer);

        var comp = EnsureComp<YautjaStoredGearComponent>(gear);
        comp.Bracer = bracer.Owner;
        comp.Kind = kind;
        comp.Deployed = deployed;

        if (comp.AttachedWeapon is { } attached && !TerminatingOrDeleted(attached))
        {
            var attachedStored = EnsureComp<YautjaStoredGearComponent>(attached);
            attachedStored.AttachmentHolder = gear;
            attachedStored.Bracer = bracer.Owner;
            attachedStored.Kind = kind;
            attachedStored.Deployed = deployed;
        }

        _actions.SetToggled(GetAction(bracer.Comp, kind), IsKindDeployed(bracer.Comp, kind));
    }

    private static bool IsSecondaryAttachment(YautjaGearContainerComponent bracer, YautjaGearKind kind, EntityUid gear)
    {
        return bracer.SecondaryGear.TryGetValue(kind, out var secondary) && secondary == gear;
    }

    private bool IsKindDeployed(YautjaGearContainerComponent bracer, YautjaGearKind kind)
    {
        if (bracer.Gear.TryGetValue(kind, out var gear) &&
            TryComp(gear, out YautjaStoredGearComponent? stored) &&
            stored.Deployed)
        {
            return true;
        }

        return bracer.SecondaryGear.TryGetValue(kind, out var secondary) &&
               TryComp(secondary, out YautjaStoredGearComponent? secondaryStored) &&
               secondaryStored.Deployed;
    }

    private static EntityUid? GetAction(YautjaGearContainerComponent bracer, YautjaGearKind kind)
    {
        return kind switch
        {
            YautjaGearKind.Caster => bracer.ToggleCasterAction,
            YautjaGearKind.WristBlades => bracer.ToggleWristBladesAction,
            YautjaGearKind.Scimitar => bracer.ToggleScimitarAction,
            YautjaGearKind.Shield => bracer.ToggleShieldAction,
            YautjaGearKind.ChainGauntlet => bracer.ToggleChainGauntletAction,
            _ => null,
        };
    }

    private bool IsYoungbloodRestrictedGear(EntityUid user, YautjaGearKind kind)
    {
        return HasComp<YautjaYoungbloodComponent>(user) &&
               kind is YautjaGearKind.Caster or YautjaGearKind.Shield;
    }

    private Container EnsureContainer(Entity<YautjaGearContainerComponent> bracer)
    {
        bracer.Comp.Container ??= _containers.EnsureContainer<Container>(bracer.Owner, bracer.Comp.ContainerId);
        return bracer.Comp.Container;
    }

    private void PlayGearSound(SoundSpecifier sound, EntityUid user)
    {
        _audio.PlayPvs(sound, user);
    }

    private static SoundSpecifier GetDeploySound(YautjaGearContainerComponent bracer, YautjaGearKind kind)
    {
        return kind switch
        {
            YautjaGearKind.Caster => bracer.CasterDeploySound,
            YautjaGearKind.WristBlades => bracer.WristBladesDeploySound,
            _ => bracer.DeploySound,
        };
    }

    private static SoundSpecifier GetRetractSound(YautjaGearContainerComponent bracer, YautjaGearKind kind)
    {
        return kind switch
        {
            YautjaGearKind.Caster => bracer.CasterRetractSound,
            YautjaGearKind.WristBlades => bracer.WristBladesRetractSound,
            _ => bracer.RetractSound,
        };
    }

    private void OnBadBloodBracerEquipped(Entity<YautjaBadBloodGearChoiceComponent> ent, ref GotEquippedEvent args)
    {
        if (ent.Comp.Chosen)
            return;

        if (!TryComp(ent.Owner, out YautjaBracerComponent? bracer))
            return;

        if ((args.SlotFlags & bracer.Slots) == 0)
            return;

        _ui.TryOpenUi(ent.Owner, YautjaBadBloodWeaponChoiceUI.Key, args.Equipee);
        UpdateBadBloodChoiceUi(ent);
    }

    private void OnBadBloodWeaponChoice(Entity<YautjaBadBloodGearChoiceComponent> ent, ref YautjaBadBloodWeaponChoiceMsg args)
    {
        if (ent.Comp.Chosen)
            return;

        if (!ent.Comp.Choices.Contains(args.Choice))
            return;

        if (ent.Comp.PendingChoice == args.Choice)
        {
            ent.Comp.Chosen = true;
            ent.Comp.PendingChoice = null;
            Dirty(ent);

            if (TryComp(ent.Owner, out YautjaGearContainerComponent? gear))
            {
                EnsureGear((ent.Owner, gear), args.Choice);
                GrantChosenGearAction((ent.Owner, gear), args.Choice);
            }

            _ui.CloseUi(ent.Owner, YautjaBadBloodWeaponChoiceUI.Key);

            return;
        }

        ent.Comp.PendingChoice = args.Choice;
        Dirty(ent);
        UpdateBadBloodChoiceUi(ent);
    }

    private void GrantChosenGearAction(Entity<YautjaGearContainerComponent> gear, YautjaGearKind kind)
    {
        if (!TryComp(gear.Owner, out YautjaBracerComponent? bracer) || bracer.User is not { } user)
            return;

        switch (kind)
        {
            case YautjaGearKind.WristBlades:
                _actions.AddAction(user, ref gear.Comp.ToggleWristBladesAction, gear.Comp.ToggleWristBladesActionId, gear.Owner);
                break;
            case YautjaGearKind.Scimitar:
                _actions.AddAction(user, ref gear.Comp.ToggleScimitarAction, gear.Comp.ToggleScimitarActionId, gear.Owner);
                break;
            case YautjaGearKind.ChainGauntlet:
                _actions.AddAction(user, ref gear.Comp.ToggleChainGauntletAction, gear.Comp.ToggleChainGauntletActionId, gear.Owner);
                break;
        }
    }

    private void UpdateBadBloodChoiceUi(Entity<YautjaBadBloodGearChoiceComponent> ent)
    {
        var state = new YautjaBadBloodWeaponChoiceBuiState(ent.Comp.Choices, ent.Comp.PendingChoice);
        _ui.SetUiState(ent.Owner, YautjaBadBloodWeaponChoiceUI.Key, state);
    }

    private bool CanUseYautjaGear(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               (TryComp(user, out YautjaThrallComponent? thrall) && thrall.Blooded && thrall.TechAuthorized);
    }
}
