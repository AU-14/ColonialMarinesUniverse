using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared.Actions;
using Content.Shared.Camera;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaMaskSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private YautjaPowerSystem _power = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaMaskComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<YautjaMaskComponent, YautjaToggleVisorActionEvent>(OnToggleVisor);
        SubscribeLocalEvent<YautjaMaskComponent, YautjaToggleMaskZoomActionEvent>(OnToggleZoom);
        SubscribeLocalEvent<YautjaMaskComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<YautjaMaskComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<YautjaMaskComponent, ComponentRemove>(OnRemove);

        SubscribeLocalEvent<YautjaMaskZoomComponent, GetEyeOffsetEvent>(OnZoomGetEyeOffset);
        SubscribeLocalEvent<YautjaMaskZoomComponent, ComponentRemove>(OnZoomRemove);
    }

    private void OnGetItemActions(Entity<YautjaMaskComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands || args.SlotFlags == null || (args.SlotFlags.Value & ent.Comp.Slots) == 0)
            return;

        args.AddAction(ref ent.Comp.ToggleVisorAction, ent.Comp.ToggleVisorActionId);
        args.AddAction(ref ent.Comp.ToggleZoomAction, ent.Comp.ToggleZoomActionId);
    }

    private void OnToggleVisor(Entity<YautjaMaskComponent> ent, ref YautjaToggleVisorActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        if (TryGetVisorToggleFailure(ent, args.Performer, out var failure))
        {
            _popup.PopupEntity(failure, args.Performer, args.Performer);
            return;
        }

        if (ent.Comp.VisorEnabled)
            DisableVisor(ent, args.Performer);
        else
            EnableVisor(ent, args.Performer);
    }

    private void OnToggleZoom(Entity<YautjaMaskComponent> ent, ref YautjaToggleMaskZoomActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        if (!_inventory.InSlotWithFlags((ent, null, null), ent.Comp.Slots) || ent.Comp.User != args.Performer)
            return;

        args.Handled = true;
        var enabling = !ent.Comp.Zoomed;
        if (_net.IsClient)
            return;

        SetZoom(ent, args.Performer, enabling);
    }

    private void OnEquipped(Entity<YautjaMaskComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        if (_net.IsClient)
            return;

        ent.Comp.User = args.Equipee;
        EnsureComp<YautjaHudViewerComponent>(args.Equipee);

        if (ent.Comp.PreserveVisorOnUnequip && ent.Comp.VisorEnabled)
            EnableVisor(ent, args.Equipee, false);
    }

    private void OnUnequipped(Entity<YautjaMaskComponent> ent, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        if (_net.IsClient)
            return;

        if (ent.Comp.PreserveVisorOnUnequip)
            DeleteVisorGlasses(ent, args.Equipee);
        else
            DisableVisor(ent, args.Equipee, false);

        SetZoom(ent, args.Equipee, false, false);
        RemoveMaskHudViewer(args.Equipee, ent.Owner);
        ent.Comp.User = null;
    }

    private void OnRemove(Entity<YautjaMaskComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsClient)
            return;

        DisableVisor(ent, ent.Comp.User);
        if (ent.Comp.User is { } user)
        {
            SetZoom(ent, user, false, false);
            RemoveMaskHudViewer(user, ent.Owner);
        }
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaMaskComponent>();
        while (query.MoveNext(out var uid, out var mask))
        {
            if (!mask.VisorEnabled ||
                mask.Drain <= FixedPoint2.Zero ||
                mask.User is not { } user ||
                time < mask.NextDrain)
                continue;

            mask.NextDrain = time + mask.DrainEvery;
            if (TryDrainWornYautjaPowerSource(user, mask.Drain))
                continue;

            _popup.PopupEntity(Loc.GetString("cmu-yautja-visor-low-power"), user, user, PopupType.MediumCaution);
            DisableVisor((uid, mask), user);
        }
    }

    private void EnableVisor(Entity<YautjaMaskComponent> mask, EntityUid user, bool feedback = true)
    {
        if (_net.IsClient)
            return;

        if (!CreateVisorGlasses(mask, user))
            return;

        mask.Comp.VisorEnabled = true;
        mask.Comp.User = user;
        mask.Comp.NextDrain = _timing.CurTime + mask.Comp.DrainEvery;
        Dirty(mask);

        _actions.SetToggled(mask.Comp.ToggleVisorAction, true);

        if (!feedback)
            return;

        _audio.PlayPvs(mask.Comp.ToggleVisorSound, mask.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-visor-enabled"), user, user);
    }

    private void DisableVisor(Entity<YautjaMaskComponent> mask, EntityUid? user, bool feedback = true)
    {
        if (_net.IsClient)
            return;

        mask.Comp.VisorEnabled = false;
        Dirty(mask);
        _actions.SetToggled(mask.Comp.ToggleVisorAction, false);

        DeleteVisorGlasses(mask, user);

        if (user == null)
            return;

        if (!feedback)
            return;

        _audio.PlayPvs(mask.Comp.ToggleVisorSound, mask.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-visor-disabled"), user.Value, user.Value);
    }

    private void SetZoom(Entity<YautjaMaskComponent> mask, EntityUid user, bool zoomed, bool feedback = true)
    {
        if (_net.IsClient)
            return;

        if (mask.Comp.Zoomed == zoomed && zoomed)
            return;

        mask.Comp.Zoomed = zoomed;
        Dirty(mask);
        _actions.SetToggled(mask.Comp.ToggleZoomAction, zoomed);

        var eye = EnsureComp<ContentEyeComponent>(user);
        if (zoomed)
        {
            var zoom = EnsureComp<YautjaMaskZoomComponent>(user);
            zoom.Mask = mask.Owner;
            zoom.Offset = GetMaskZoomOffset(mask, user);
            Dirty(user, zoom);
            _contentEye.SetZoom(user, Vector2.One * mask.Comp.ZoomLevel, true, eye);
        }
        else
        {
            if (TryComp(user, out YautjaMaskZoomComponent? zoom) && zoom.Mask == mask.Owner)
                RemComp<YautjaMaskZoomComponent>(user);

            _contentEye.ResetZoom(user, eye);
        }

        if (TryComp(user, out EyeComponent? eyeComponent))
            _contentEye.UpdateEyeOffset((user, eyeComponent));

        if (!feedback)
            return;

        _audio.PlayPvs(zoomed ? mask.Comp.ZoomOnSound : mask.Comp.ZoomOffSound, mask.Owner);
        _popup.PopupClient(Loc.GetString(zoomed ? "cmu-yautja-mask-zoom-enabled" : "cmu-yautja-mask-zoom-disabled"), user, user);
    }

    private Vector2 GetMaskZoomOffset(Entity<YautjaMaskComponent> mask, EntityUid user)
    {
        var direction = Transform(user).LocalRotation.GetCardinalDir();
        return direction.ToVec() * ((mask.Comp.ZoomOffset * mask.Comp.ZoomLevel - 1) / 2);
    }

    private void OnZoomGetEyeOffset(Entity<YautjaMaskZoomComponent> ent, ref GetEyeOffsetEvent args)
    {
        args.Offset += ent.Comp.Offset;
    }

    private void OnZoomRemove(Entity<YautjaMaskZoomComponent> ent, ref ComponentRemove args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (TryComp(ent.Comp.Mask, out YautjaMaskComponent? mask) &&
            mask.User == ent.Owner &&
            mask.Zoomed)
        {
            mask.Zoomed = false;
            Dirty(ent.Comp.Mask, mask);
            _actions.SetToggled(mask.ToggleZoomAction, false);

            if (TryComp(ent.Owner, out ContentEyeComponent? contentEye))
                _contentEye.ResetZoom(ent.Owner, contentEye);
        }

        if (TryComp(ent, out EyeComponent? eye))
            _contentEye.UpdateEyeOffset((ent.Owner, eye));
    }

    private bool TryGetVisorToggleFailure(Entity<YautjaMaskComponent> mask, EntityUid user, out string message)
    {
        if (mask.Comp.RequiresYautjaWearer && !HasComp<YautjaComponent>(user))
        {
            message = Loc.GetString("cmu-yautja-visor-denied");
            return true;
        }

        if (!CanUseVisorTech(user))
        {
            message = Loc.GetString("cmu-yautja-visor-denied");
            return true;
        }

        if (!_inventory.InSlotWithFlags((mask, null, null), mask.Comp.Slots) || mask.Comp.User != user)
        {
            message = Loc.GetString("cmu-yautja-visor-wear-mask", ("mask", Name(mask)));
            return true;
        }

        if (!HasWornYautjaPowerSource(user))
        {
            message = Loc.GetString("cmu-yautja-visor-no-bracer");
            return true;
        }

        if (HasBlockingEyeWear(user))
        {
            message = Loc.GetString("cmu-yautja-visor-eyes-blocked");
            return true;
        }

        message = string.Empty;
        return false;
    }

    private bool CanUseVisorTech(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user) ||
               HasComp<YautjaThrallComponent>(user);
    }

    private bool HasWornYautjaPowerSource(EntityUid user)
    {
        var slots = _inventory.GetSlotEnumerator(user, SlotFlags.GLOVES);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } contained)
                continue;

            if (HasComp<YautjaBracerComponent>(contained) ||
                HasComp<YautjaThrallBracerComponent>(contained))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryDrainWornYautjaPowerSource(EntityUid user, FixedPoint2 amount)
    {
        if (amount == FixedPoint2.Zero)
            return true;

        var slots = _inventory.GetSlotEnumerator(user, SlotFlags.GLOVES);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } contained)
                continue;

            if (TryComp(contained, out YautjaBracerComponent? bracer))
                return _power.TryDrainPower((contained, bracer), user, amount, popup: false);

            if (!TryComp(contained, out YautjaThrallBracerComponent? thrallBracer))
                continue;

            if (thrallBracer.Charge < amount)
                return false;

            var oldCharge = thrallBracer.Charge;
            thrallBracer.Charge = FixedPoint2.Max(FixedPoint2.Zero, thrallBracer.Charge - amount);
            if (oldCharge != thrallBracer.Charge)
                Dirty(contained, thrallBracer);

            return true;
        }

        return false;
    }

    private bool CreateVisorGlasses(Entity<YautjaMaskComponent> mask, EntityUid user)
    {
        DeleteVisorGlasses(mask, user);

        if (_inventory.TryGetSlotEntity(user, "eyes", out _))
            return false;

        var glasses = Spawn(mask.Comp.VisorGlassesPrototype, Transform(user).Coordinates);
        if (!_inventory.TryEquip(user, glasses, "eyes", silent: true, force: true))
        {
            QueueDel(glasses);
            return false;
        }

        var visor = EnsureComp<YautjaMaskVisorGlassesComponent>(glasses);
        visor.Mask = mask.Owner;
        visor.User = user;
        visor.ThermalVisionEnabled = true;
        Dirty(glasses, visor);

        mask.Comp.VisorGlasses = glasses;
        Dirty(mask);
        return true;
    }

    private void DeleteVisorGlasses(Entity<YautjaMaskComponent> mask, EntityUid? user)
    {
        var dirty = false;

        if (user != null &&
            _inventory.TryGetSlotEntity(user.Value, "eyes", out var equipped) &&
            TryComp(equipped.Value, out YautjaMaskVisorGlassesComponent? visor))
        {
            visor.ThermalVisionEnabled = false;
            Dirty(equipped.Value, visor);
            _inventory.TryUnequip(user.Value, "eyes", out _, silent: true, force: true);
            QueueDel(equipped.Value);
            if (mask.Comp.VisorGlasses == equipped.Value)
                mask.Comp.VisorGlasses = null;

            dirty = true;
        }

        if (mask.Comp.VisorGlasses is { } stored)
        {
            if (!Deleted(stored))
                QueueDel(stored);

            mask.Comp.VisorGlasses = null;
            dirty = true;
        }

        if (dirty)
            Dirty(mask);
    }

    private bool HasBlockingEyeWear(EntityUid user)
    {
        var slots = _inventory.GetSlotEnumerator(user, SlotFlags.EYES);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } contained &&
                !HasComp<YautjaMaskVisorGlassesComponent>(contained))
            {
                return true;
            }
        }

        return false;
    }

    private void RemoveMaskHudViewer(EntityUid user, EntityUid ignored)
    {
        if (HasOtherEquippedMaskHud(user, ignored))
            return;

        RemCompDeferred<YautjaHudViewerComponent>(user);
    }

    private bool HasOtherEquippedMaskHud(EntityUid user, EntityUid ignored)
    {
        var slots = _inventory.GetSlotEnumerator(user, SlotFlags.MASK | SlotFlags.HEAD | SlotFlags.EYES);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } contained || contained == ignored)
                continue;

            if (TryComp(contained, out YautjaMaskComponent? mask) &&
                _inventory.InSlotWithFlags((contained, null, null), mask.Slots))
            {
                return true;
            }
        }

        return false;
    }
}
