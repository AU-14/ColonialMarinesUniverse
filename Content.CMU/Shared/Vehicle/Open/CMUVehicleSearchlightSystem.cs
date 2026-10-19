using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared.ActionBlocker;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Works a vehicle's searchlight (<see cref="CMUVehicleSearchlightComponent"/>): clicked with an
/// empty hand it is taken in that hand and lit, pointing where its operator's cursor is until the hand
/// lets go of it. Fitted with a screwdriver in the other hand; its lamp's own handling takes it off.
/// </summary>
public sealed partial class CMUVehicleSearchlightSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> SearchlightTag = "CMUJeepSearchlight";

    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleSearchlightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUVehicleSearchlightComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUVehicleSearchlightComponent, CMUVehicleSearchlightFitDoAfterEvent>(OnFit);
        SubscribeLocalEvent<CMUVehicleSearchlightOperatorComponent, VirtualItemDeletedEvent>(OnLetGo);
        SubscribeNetworkEvent<CMUVehicleSearchlightAimEvent>(OnAimRequest);
    }

    private void OnMapInit(Entity<CMUVehicleSearchlightComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || Exists(ent.Comp.Beam))
            return;

        var beam = SpawnAttachedTo(ent.Comp.BeamPrototype, new EntityCoordinates(ent, default));
        var comp = EnsureComp<CMUVehicleHeadlightBeamComponent>(beam);
        comp.Vehicle = ent;
        comp.Mode = CMUHeadlightMode.High;
        if (Searchlights(ent).FirstOrDefault() is { } lamp)
            comp.Lamp = lamp.Id;
        Dirty(beam, comp);

        var follower = EnsureComp<CMUZVisualFollowerComponent>(beam);
        follower.Target = ent;
        Dirty(beam, follower);

        _lights.SetEnabled(beam, false);
        ent.Comp.Beam = beam;
        Dirty(ent);
    }

    private void OnShutdown(Entity<CMUVehicleSearchlightComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient)
            return;

        Release(ent);
        QueueDel(ent.Comp.Beam);
        ent.Comp.Beam = null;
    }

    private IEnumerable<CMUVehicleLampData> Searchlights(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out CMUVehicleLampsComponent? lamps))
            yield break;

        foreach (var lamp in lamps.Lamps)
        {
            if (lamp.Role == CMUVehicleLampRole.Searchlight)
                yield return lamp;
        }
    }

    private bool TryGetSearchlight(EntityUid vehicle, string part, [NotNullWhen(true)] out CMUVehicleLampData? lamp, [NotNullWhen(true)] out ItemSlot? slot)
    {
        slot = null;
        lamp = null;
        foreach (var searchlight in Searchlights(vehicle))
        {
            if (searchlight.Id != part)
                continue;

            lamp = searchlight;
            return _itemSlots.TryGetSlot(vehicle, lamp.SlotId, out slot);
        }

        return false;
    }

    /// <summary>
    /// Taken in an empty hand, the searchlight lights up for whoever holds it; clicked again, let go.
    /// Server only: the hand it fills and the light are the server's to change. The vehicle's part
    /// handling asks here first, before it takes lamps off and puts them back.
    /// </summary>
    public void PartInteractHand(Entity<CMUVehiclePartComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled ||
            ent.Comp.Vehicle is not { } vehicle ||
            !TryComp(vehicle, out CMUVehicleSearchlightComponent? searchlight) ||
            !TryGetSearchlight(vehicle, ent.Comp.Part, out var lamp, out var slot) ||
            slot.Item is not { } fitted)
        {
            return;
        }

        args.Handled = true;
        if (_net.IsClient)
            return;

        TryTakeHold((vehicle, searchlight), lamp, fitted, args.User);
    }

    public bool TryTakeHold(Entity<CMUVehicleSearchlightComponent> ent, CMUVehicleLampData lamp, EntityUid fitted, EntityUid user)
    {
        if (ent.Comp.Operator == user)
        {
            Release(ent);
            return false;
        }

        if (ent.Comp.Operator is { } other && Exists(other))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-searchlight-in-use", ("user", other)), ent, user);
            return false;
        }

        if (TryComp(ent, out CMUVehicleLampsComponent? lamps) && lamps.Broken.Contains(lamp.Id))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-searchlight-broken"), ent, user);
            return false;
        }

        if (_hands.GetActiveHand(user) is not { } hand ||
            _hands.TryGetHeldItem(user, hand, out _) ||
            !_virtualItem.TrySpawnVirtualItemInHand(fitted, user, out _, empty: hand))
        {
            _popup.PopupEntity(Loc.GetString("cmu-vehicle-searchlight-hand-full"), ent, user);
            return false;
        }

        ent.Comp.Operator = user;
        ent.Comp.Held = fitted;
        ent.Comp.Lamp = lamp.Id;
        Dirty(ent);

        var operating = EnsureComp<CMUVehicleSearchlightOperatorComponent>(user);
        operating.Vehicle = ent;
        Dirty(user, operating);

        if (TryComp(ent.Comp.Beam, out CMUVehicleHeadlightBeamComponent? beam))
        {
            beam.Lamp = lamp.Id;
            beam.Driver = lamp.Side == CMUVehicleLampSide.Left;
            Dirty(ent.Comp.Beam.Value, beam);
            _lights.SetEnabled(ent.Comp.Beam.Value, true);
        }

        _audio.PlayPvs(ent.Comp.SwitchSound, ent);
        return true;
    }

    /// <summary>
    /// Its operator lets go of the searchlight and it goes dark, still pointing where they left it.
    /// </summary>
    public void Release(Entity<CMUVehicleSearchlightComponent> ent)
    {
        if (ent.Comp.Operator is not { } user)
            return;

        // Cleared first: deleting the hand's hold raises the let go again.
        var held = ent.Comp.Held;
        ent.Comp.Operator = null;
        ent.Comp.Held = null;
        Dirty(ent);

        if (TryComp(user, out CMUVehicleSearchlightOperatorComponent? operating) && operating.Vehicle == ent.Owner)
            RemComp<CMUVehicleSearchlightOperatorComponent>(user);

        if (held != null && !TerminatingOrDeleted(user))
            _virtualItem.DeleteInHandsMatching(user, held.Value);

        if (ent.Comp.Beam is { } beam && !TerminatingOrDeleted(beam))
        {
            _lights.SetEnabled(beam, false);
            _audio.PlayPvs(ent.Comp.SwitchSound, ent);
        }
    }

    // Dropping the hand that holds it lets the searchlight go.
    private void OnLetGo(Entity<CMUVehicleSearchlightOperatorComponent> ent, ref VirtualItemDeletedEvent args)
    {
        if (TryComp(ent.Comp.Vehicle, out CMUVehicleSearchlightComponent? searchlight) &&
            searchlight.Operator == ent.Owner &&
            searchlight.Held == args.BlockingEntity)
        {
            Release((ent.Comp.Vehicle, searchlight));
        }
    }

    private void OnAimRequest(CMUVehicleSearchlightAimEvent ev, EntitySessionEventArgs args)
    {
        var vehicle = GetEntity(ev.Vehicle);
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryComp(vehicle, out CMUVehicleSearchlightComponent? searchlight) ||
            searchlight.Operator != user)
        {
            return;
        }

        Aim((vehicle, searchlight), ev.Angle);
    }

    /// <summary>
    /// Swings the searchlight towards a world angle, as far round as its arc goes; it keeps its bearing
    /// on the vehicle as the vehicle turns.
    /// </summary>
    public void Aim(Entity<CMUVehicleSearchlightComponent> ent, Angle worldAngle)
    {
        var aim = (worldAngle - _transform.GetWorldRotation(ent)).Reduced();
        if (ent.Comp.MaxYawDegrees > 0f)
        {
            var max = MathHelper.DegreesToRadians(ent.Comp.MaxYawDegrees);
            aim = new Angle(Math.Clamp(aim.Theta, -max, max));
        }

        ent.Comp.TargetAim = aim;
        if (ent.Comp.RotationSpeed <= 0f && ent.Comp.Beam is { } beam && !TerminatingOrDeleted(beam))
            _transform.SetLocalRotation(beam, aim);
    }

    /// <summary>
    /// Turns the beam towards where it is aimed at the searchlight's own speed.
    /// </summary>
    private void Swing(CMUVehicleSearchlightComponent searchlight, float frameTime)
    {
        if (searchlight.RotationSpeed <= 0f || searchlight.Beam is not { } beam || TerminatingOrDeleted(beam))
            return;

        var current = Transform(beam).LocalRotation;
        var delta = Angle.ShortestDistance(current, searchlight.TargetAim).Theta;
        if (Math.Abs(delta) < 0.0001)
            return;

        var step = MathHelper.DegreesToRadians(searchlight.RotationSpeed) * frameTime;
        _transform.SetLocalRotation(beam, current + new Angle(Math.Clamp(delta, -step, step)));
    }

    /// <summary>
    /// The searchlight's lens in the world, where the operator's aim is measured from.
    /// </summary>
    public Vector2 LensPosition(EntityUid vehicle, string? lamp)
    {
        var (position, rotation) = _transform.GetWorldPositionRotation(vehicle);
        if (lamp == null || !TryComp(vehicle, out CMUVehicleLampsComponent? lamps) ||
            lamps.Lamps.Find(l => l.Id == lamp) is not { } data)
        {
            return position;
        }

        return position + rotation.RotateVec(data.Offset);
    }

    /// <summary>
    /// Fitting a searchlight on an empty mount takes it in one hand and a screwdriver in the other,
    /// clicking with either. Asked first by the vehicle's part handling, like taking hold of it.
    /// </summary>
    public void PartInteractUsing(Entity<CMUVehiclePartComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            ent.Comp.Vehicle is not { } vehicle ||
            !TryComp(vehicle, out CMUVehicleSearchlightComponent? searchlight) ||
            !TryComp(vehicle, out CMUOpenVehicleComponent? jeep) ||
            !TryGetSearchlight(vehicle, ent.Comp.Part, out var lamp, out var slot) ||
            slot.HasItem)
        {
            return;
        }

        var user = args.User;
        EntityUid? light = null;
        EntityUid? screwdriver = null;
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (light == null && _tags.HasTag(held, SearchlightTag))
                light = held;
            else if (screwdriver == null && _tool.HasQuality(held, jeep.PartRemoveQuality))
                screwdriver = held;
        }

        if (args.Used != light && args.Used != screwdriver)
            return;

        args.Handled = true;
        if (light == null)
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-searchlight-needs-light"), vehicle, user);
            return;
        }

        if (screwdriver == null)
        {
            _popup.PopupClient(Loc.GetString("cmu-vehicle-searchlight-needs-screwdriver"), vehicle, user);
            return;
        }

        _tool.UseTool(screwdriver.Value, user, vehicle, (float) searchlight.FitDelay.TotalSeconds,
            jeep.PartRemoveQuality, new CMUVehicleSearchlightFitDoAfterEvent(lamp.SlotId, GetNetEntity(light.Value)));
    }

    private void OnFit(Entity<CMUVehicleSearchlightComponent> ent, ref CMUVehicleSearchlightFitDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var light = GetEntity(args.Searchlight);
        if (!_hands.IsHolding(args.User, light) ||
            !_itemSlots.TryGetSlot(ent.Owner, args.Slot, out var slot) ||
            slot.HasItem)
        {
            return;
        }

        _itemSlots.TryInsert(ent.Owner, args.Slot, light, args.User);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        // Each searchlight swings towards its aim. An operator lets go once they can't work it: too far
        // off, out of it, or the searchlight taken off or smashed under them.
        var query = EntityQueryEnumerator<CMUVehicleSearchlightComponent>();
        while (query.MoveNext(out var uid, out var searchlight))
        {
            Swing(searchlight, frameTime);
            if (searchlight.Operator is not { } user)
                continue;

            var still = !TerminatingOrDeleted(user) &&
                        searchlight.Lamp is { } lampId &&
                        TryGetSearchlight(uid, lampId, out _, out var slot) &&
                        slot.Item == searchlight.Held &&
                        !(TryComp(uid, out CMUVehicleLampsComponent? lamps) && lamps.Broken.Contains(lampId)) &&
                        _actionBlocker.CanInteract(user, null) &&
                        Vector2.Distance(_transform.GetWorldPosition(user), _transform.GetWorldPosition(uid)) <= searchlight.Reach;
            if (!still)
                Release((uid, searchlight));
        }
    }
}
