using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.NightVision;
using Content.Shared.Actions;
using Content.Shared.Camera;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Yautja;

public sealed class YautjaMaskSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContentEyeSystem _contentEye = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedNightVisionSystem _nightVision = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly YautjaPowerSystem _power = default!;

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

        if (!_inventory.InSlotWithFlags((ent, null, null), ent.Comp.Slots))
            return;

        args.Handled = true;
        if (_net.IsClient)
        {
            var message = ent.Comp.VisorEnabled ? "cmu-yautja-visor-disabled" : "cmu-yautja-visor-enabled";
            _popup.PopupPredicted(Loc.GetString(message), args.Performer, args.Performer);
            _audio.PlayPredicted(ent.Comp.ToggleVisorSound, ent.Owner, args.Performer);
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

        if (!_inventory.InSlotWithFlags((ent, null, null), ent.Comp.Slots))
            return;

        args.Handled = true;
        var enabling = !ent.Comp.Zoomed;
        if (_net.IsClient)
        {
            _audio.PlayPredicted(enabling ? ent.Comp.ZoomOnSound : ent.Comp.ZoomOffSound, ent.Owner, args.Performer);
            return;
        }

        SetZoom(ent, args.Performer, enabling);
    }

    private void OnEquipped(Entity<YautjaMaskComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        if (_net.IsClient)
            return;

        ent.Comp.User = args.Equipee;

        if (HasComp<YautjaComponent>(args.Equipee))
            EnableVisor(ent, args.Equipee, false);
    }

    private void OnUnequipped(Entity<YautjaMaskComponent> ent, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        if (_net.IsClient)
            return;

        DisableVisor(ent, args.Equipee, false);
        SetZoom(ent, args.Equipee, false, false);
        ent.Comp.User = null;
    }

    private void OnRemove(Entity<YautjaMaskComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsClient)
            return;

        DisableVisor(ent, ent.Comp.User);
        if (ent.Comp.User is { } user)
            SetZoom(ent, user, false, false);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaMaskComponent>();
        while (query.MoveNext(out var uid, out var mask))
        {
            if (!mask.VisorEnabled || mask.User is not { } user || time < mask.NextDrain)
                continue;

            mask.NextDrain = time + mask.DrainEvery;
            if (_power.TryRemovePower(user, mask.Drain))
                continue;

            DisableVisor((uid, mask), user);
        }
    }

    private void EnableVisor(Entity<YautjaMaskComponent> mask, EntityUid user, bool feedback = true)
    {
        if (_net.IsClient)
            return;

        mask.Comp.VisorEnabled = true;
        mask.Comp.User = user;
        mask.Comp.NextDrain = _timing.CurTime + mask.Comp.DrainEvery;
        Dirty(mask);
        EnsureComp<YautjaHudViewerComponent>(user);

        if (TryComp(mask, out NightVisionItemComponent? nightVision))
            _nightVision.EnableNightVisionItem((mask.Owner, nightVision), user);

        _actions.SetToggled(mask.Comp.ToggleVisorAction, true);

        if (!feedback)
            return;

        _audio.PlayPredicted(mask.Comp.ToggleVisorSound, mask.Owner, user);
        _popup.PopupClient(Loc.GetString("cmu-yautja-visor-enabled"), user, user);
    }

    private void DisableVisor(Entity<YautjaMaskComponent> mask, EntityUid? user, bool feedback = true)
    {
        if (_net.IsClient)
            return;

        mask.Comp.VisorEnabled = false;
        Dirty(mask);
        _actions.SetToggled(mask.Comp.ToggleVisorAction, false);

        if (TryComp(mask, out NightVisionItemComponent? nightVision))
            _nightVision.DisableNightVisionItem((mask.Owner, nightVision), user);

        if (user == null)
            return;

        if (!HasOtherActiveVisor(user.Value, mask.Owner))
            RemCompDeferred<YautjaHudViewerComponent>(user.Value);

        if (!feedback)
            return;

        _popup.PopupClient(Loc.GetString("cmu-yautja-visor-disabled"), user.Value, user.Value);
        _audio.PlayPredicted(mask.Comp.ToggleVisorSound, mask.Owner, user.Value);
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

        _audio.PlayPredicted(zoomed ? mask.Comp.ZoomOnSound : mask.Comp.ZoomOffSound, mask.Owner, user);
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

        if (TryComp(ent, out EyeComponent? eye))
            _contentEye.UpdateEyeOffset((ent.Owner, eye));
    }

    private bool HasOtherActiveVisor(EntityUid user, EntityUid ignored)
    {
        var slots = _inventory.GetSlotEnumerator(user, SlotFlags.MASK | SlotFlags.HEAD | SlotFlags.EYES);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } contained || contained == ignored)
                continue;

            if (TryComp(contained, out YautjaMaskComponent? mask) && mask.VisorEnabled)
                return true;
        }

        return false;
    }
}
