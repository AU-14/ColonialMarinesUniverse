using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Nutrition;
using Content.Shared.CMU14.Vendors;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Kitchen;

/// <summary>
///     Opening a map's kitchen shutters opens its DEFAC foodline, which shuts off the NutriCo machines on that map.
/// </summary>
public sealed partial class CMUFoodlineSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUKitchenShutterComponent, DoorStateChangedEvent>(OnShutterStateChanged);
        SubscribeLocalEvent<CMUKitchenShutterComponent, ComponentShutdown>(OnShutterShutdown);
        SubscribeLocalEvent<CMUKitchenShutterComponent, MapInitEvent>(OnShutterMapInit);
        SubscribeLocalEvent<CMUFoodlineVendorComponent, MapInitEvent>(OnVendorMapInit);
        SubscribeLocalEvent<CMUFoodlineVendorComponent, ActivatableUIOpenAttemptEvent>(OnVendorUIOpenAttempt);
    }

    private void OnShutterStateChanged(Entity<CMUKitchenShutterComponent> ent, ref DoorStateChangedEvent args)
    {
        UpdateMap(Transform(ent).MapID);
    }

    private void OnShutterShutdown(Entity<CMUKitchenShutterComponent> ent, ref ComponentShutdown args)
    {
        UpdateMap(Transform(ent).MapID);
    }

    private void OnShutterMapInit(Entity<CMUKitchenShutterComponent> ent, ref MapInitEvent args)
    {
        UpdateMap(Transform(ent).MapID);
    }

    private void OnVendorMapInit(Entity<CMUFoodlineVendorComponent> ent, ref MapInitEvent args)
    {
        SetOpen(ent, IsFoodlineOpen(Transform(ent).MapID));
    }

    /// <summary>Machines without a menu of their own that can say so just won't open.</summary>
    private void OnVendorUIOpenAttempt(Entity<CMUFoodlineVendorComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled ||
            !ent.Comp.FoodlineOpen ||
            HasComp<CMAutomatedVendorComponent>(ent) ||
            HasComp<CMUAutoDrinkComponent>(ent))
        {
            return;
        }

        args.Cancel();
        if (!args.Silent)
            _popup.PopupClient(Loc.GetString(ent.Comp.Message), ent, args.User);
    }

    /// <summary>
    ///     Whether this machine is shut off for the foodline. Tells the user so if it is.
    /// </summary>
    public bool IsClosedForFoodline(EntityUid vendor, EntityUid? user)
    {
        if (!TryComp<CMUFoodlineVendorComponent>(vendor, out var foodline) || !foodline.FoodlineOpen)
            return false;

        if (user != null)
            _popup.PopupClient(Loc.GetString(foodline.Message), vendor, user.Value);

        return true;
    }

    public bool IsFoodlineOpen(MapId map)
    {
        var shutters = EntityQueryEnumerator<CMUKitchenShutterComponent, DoorComponent, TransformComponent>();
        while (shutters.MoveNext(out var uid, out _, out var door, out var xform))
        {
            if (xform.MapID == map && door.State != DoorState.Closed && !TerminatingOrDeleted(uid))
                return true;
        }

        return false;
    }

    private void UpdateMap(MapId map)
    {
        if (_net.IsClient)
            return;

        var open = IsFoodlineOpen(map);

        // The MRE gate reads these from the map entity, so keep the replicated copy current.
        var mapEnt = _map.GetMapOrInvalid(map);
        if (TryComp<CMUShipMREStateComponent>(mapEnt, out var mre))
        {
            var hasShutters = HasShutters(map);

            if (mre.HasFoodline != hasShutters || mre.FoodlineOpen != open)
            {
                mre.HasFoodline = hasShutters;
                mre.FoodlineOpen = open;
                Dirty(mapEnt, mre);
            }
        }

        var vendors = EntityQueryEnumerator<CMUFoodlineVendorComponent, TransformComponent>();
        while (vendors.MoveNext(out var uid, out var vendor, out var xform))
        {
            if (xform.MapID == map)
                SetOpen((uid, vendor), open);
        }
    }

    private bool HasShutters(MapId map)
    {
        var shutters = EntityQueryEnumerator<CMUKitchenShutterComponent, TransformComponent>();
        while (shutters.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID == map && !TerminatingOrDeleted(uid))
                return true;
        }

        return false;
    }

    private void SetOpen(Entity<CMUFoodlineVendorComponent> ent, bool open)
    {
        if (_net.IsClient || ent.Comp.FoodlineOpen == open)
            return;

        ent.Comp.FoodlineOpen = open;
        Dirty(ent);
    }
}
