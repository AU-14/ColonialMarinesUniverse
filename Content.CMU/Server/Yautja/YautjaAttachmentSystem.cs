using Content.Server.Administration.Logs;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Hands;
using Content.Shared._RMC14.Inventory;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Database;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaAttachmentSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly YautjaPowerSystem _power = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaGearContainerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<YautjaGearContainerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<YautjaGearContainerComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaBracerUnequippedEvent>(OnBracerUnequipped);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaToggleCasterActionEvent>(OnToggleCaster);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaToggleWristBladesActionEvent>(OnToggleWristBlades);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaToggleScimitarActionEvent>(OnToggleScimitar);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaToggleShieldActionEvent>(OnToggleShield);
        SubscribeLocalEvent<YautjaGearContainerComponent, YautjaToggleChainGauntletActionEvent>(OnToggleChainGauntlet);

        SubscribeLocalEvent<YautjaStoredGearComponent, RMCItemDropAttemptEvent>(OnStoredGearDropAttempt);
        SubscribeLocalEvent<YautjaStoredGearComponent, ThrowItemAttemptEvent>(OnStoredGearThrowAttempt);
        SubscribeLocalEvent<YautjaStoredGearComponent, FellDownThrowAttemptEvent>(OnStoredGearFellDownThrowAttempt);
        SubscribeLocalEvent<YautjaStoredGearComponent, ContainerGettingRemovedAttemptEvent>(OnStoredGearRemoveAttempt);
        SubscribeLocalEvent<YautjaStoredGearComponent, DroppedEvent>(OnStoredGearDropped);
        SubscribeLocalEvent<YautjaStoredGearComponent, RMCDroppedEvent>(OnStoredGearRMCDropped);
    }

    private void OnMapInit(Entity<YautjaGearContainerComponent> ent, ref MapInitEvent args)
    {
        EnsureContainer(ent);

        foreach (var kind in ent.Comp.GearPrototypes.Keys)
        {
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

        ent.Comp.Gear.Clear();
    }

    private void OnGetItemActions(Entity<YautjaGearContainerComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands || args.SlotFlags == null || (args.SlotFlags.Value & ent.Comp.Slots) == 0)
            return;

        args.AddAction(ref ent.Comp.ToggleCasterAction, ent.Comp.ToggleCasterActionId);
        args.AddAction(ref ent.Comp.ToggleWristBladesAction, ent.Comp.ToggleWristBladesActionId);
        args.AddAction(ref ent.Comp.ToggleScimitarAction, ent.Comp.ToggleScimitarActionId);
        args.AddAction(ref ent.Comp.ToggleShieldAction, ent.Comp.ToggleShieldActionId);
        args.AddAction(ref ent.Comp.ToggleChainGauntletAction, ent.Comp.ToggleChainGauntletActionId);
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
        ToggleGear(ent, args, YautjaGearKind.WristBlades);
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

    private void OnStoredGearDropAttempt(Entity<YautjaStoredGearComponent> ent, ref RMCItemDropAttemptEvent args)
    {
        if (!ent.Comp.Deployed)
            return;

        if (TryGetCurrentHolder(ent.Owner, out var user))
            TryRetractStoredGear(ent, user);

        args.Cancelled = true;
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

        TryRetractStoredGear(ent, args.Thrower);
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

        var container = EnsureContainer(bracer);
        if (EnsureGear(bracer, kind) is not { } gear)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-gear-missing"), user, user, PopupType.SmallCaution);
            return;
        }

        if (container.Contains(gear))
        {
            DeployGear(bracer, user, gear, kind);
            return;
        }

        RetractGear(bracer, user, gear, kind);
    }

    private void DeployGear(Entity<YautjaGearContainerComponent> bracer, EntityUid user, EntityUid gear, YautjaGearKind kind)
    {
        if (!_hands.TryPickupAnyHand(user, gear))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hands-full"), user, user, PopupType.SmallCaution);
            return;
        }

        SetGearState(bracer, gear, kind, true);
        PlayGearSound(GetDeploySound(bracer.Comp, kind), user);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-gear-deployed", ("item", gear)), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} deployed Yautja gear {ToPrettyString(gear):gear} from {ToPrettyString(bracer.Owner):bracer}");
    }

    private void RetractGear(Entity<YautjaGearContainerComponent> bracer, EntityUid user, EntityUid gear, YautjaGearKind kind)
    {
        if (!_hands.IsHolding(user, gear))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-gear-not-held"), user, user, PopupType.SmallCaution);
            return;
        }

        var container = EnsureContainer(bracer);
        if (!TryInsertStoredGear(gear, container))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-gear-retract-failed"), user, user, PopupType.SmallCaution);
            return;
        }

        SetGearState(bracer, gear, kind, false);
        PlayGearSound(GetRetractSound(bracer.Comp, kind), user);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-gear-retracted", ("item", gear)), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} retracted Yautja gear {ToPrettyString(gear):gear} into {ToPrettyString(bracer.Owner):bracer}");
    }

    private void RetractHeldGear(Entity<YautjaGearContainerComponent> bracer, EntityUid user)
    {
        var container = EnsureContainer(bracer);
        foreach (var (kind, gear) in bracer.Comp.Gear)
        {
            if (TerminatingOrDeleted(gear) || !_hands.IsHolding(user, gear))
                continue;

            if (TryInsertStoredGear(gear, container))
            {
                SetGearState(bracer, gear, kind, false);
                PlayGearSound(GetRetractSound(bracer.Comp, kind), user);
            }
        }
    }

    private bool TryRetractStoredGear(Entity<YautjaStoredGearComponent> gear, EntityUid user)
    {
        if (TerminatingOrDeleted(gear.Owner) ||
            gear.Comp.Bracer is not { } bracer ||
            TerminatingOrDeleted(bracer) ||
            !TryComp<YautjaGearContainerComponent>(bracer, out var bracerComp))
        {
            return false;
        }

        var bracerEnt = (bracer, bracerComp);
        var container = EnsureContainer(bracerEnt);
        if (container.Contains(gear.Owner))
        {
            SetGearState(bracerEnt, gear.Owner, gear.Comp.Kind, false);
            return true;
        }

        var inserted = TryInsertStoredGear(gear.Owner, container, gear.Comp);

        if (!inserted)
            return false;

        SetGearState(bracerEnt, gear.Owner, gear.Comp.Kind, false);
        PlayGearSound(GetRetractSound(bracerComp, gear.Comp.Kind), user);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} auto-retracted Yautja gear {ToPrettyString(gear.Owner):gear} into {ToPrettyString(bracer):bracer}");
        return true;
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

    private EntityUid? EnsureGear(Entity<YautjaGearContainerComponent> bracer, YautjaGearKind kind)
    {
        if (bracer.Comp.Gear.TryGetValue(kind, out var existing) && !TerminatingOrDeleted(existing))
            return existing;

        if (!bracer.Comp.GearPrototypes.TryGetValue(kind, out var prototype))
            return null;

        var container = EnsureContainer(bracer);
        var gear = Spawn(prototype, Transform(bracer.Owner).Coordinates);
        SetGearState(bracer, gear, kind, false);

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
        bracer.Comp.Gear[kind] = gear;
        Dirty(bracer);

        var comp = EnsureComp<YautjaStoredGearComponent>(gear);
        comp.Bracer = bracer.Owner;
        comp.Kind = kind;
        comp.Deployed = deployed;
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

    private bool CanUseYautjaGear(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               (TryComp(user, out YautjaThrallComponent? thrall) && thrall.Blooded && thrall.TechAuthorized);
    }
}
