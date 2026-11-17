using Content.Server.Camera;
using Content.Shared._RMC14.Camera;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.LawEnforcement;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.LawEnforcement;

/// <summary>
/// Fits <see cref="CMUDashCameraComponent"/>s to vehicles, where they join their camera network under the vehicle's
/// name. Taking one off needs a law enforcement ID and a screwdriver; anyone else has to short it out with a
/// multitool first.
/// </summary>
public sealed partial class CMUDashCameraSystem : EntitySystem
{
    private static readonly ProtoId<ToolQualityPrototype> Screwing = "Screwing";
    private static readonly ProtoId<ToolQualityPrototype> Pulsing = "Pulsing";

    [Dependency] private SharedRMCCameraSystem _camera = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private CameraNetworkSystem _networks = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUDashCameraComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CMUDashCameraComponent, CMUDashCameraMountDoAfterEvent>(OnMountDoAfter);
        SubscribeLocalEvent<CMUDashCameraComponent, ExaminedEvent>(OnCameraExamined);

        SubscribeLocalEvent<CMUDashCameraMountComponent, InteractUsingEvent>(OnVehicleInteractUsing,
            after: [typeof(Content.Shared._RMC14.Vehicle.HardpointSystem), typeof(Content.Shared._RMC14.Vehicle.VehicleLockSystem)]);
        SubscribeLocalEvent<CMUDashCameraMountComponent, CMUDashCameraRemoveDoAfterEvent>(OnRemoveDoAfter);
        SubscribeLocalEvent<CMUDashCameraMountComponent, CMUDashCameraShortDoAfterEvent>(OnShortDoAfter);
        SubscribeLocalEvent<CMUDashCameraMountComponent, ExaminedEvent>(OnVehicleExamined);
    }

    private void OnAfterInteract(Entity<CMUDashCameraComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } vehicle || !HasComp<VehicleComponent>(vehicle))
            return;

        args.Handled = true;
        if (HasComp<CMUDashCameraMountComponent>(vehicle))
        {
            _popup.PopupEntity(Loc.GetString("cmu-dash-camera-already", ("vehicle", vehicle)), vehicle, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.MountDelay, new CMUDashCameraMountDoAfterEvent(), ent, vehicle, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-dash-camera-fitting", ("vehicle", vehicle)), vehicle, args.User);
    }

    private void OnMountDoAfter(Entity<CMUDashCameraComponent> ent, ref CMUDashCameraMountDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } vehicle)
            return;

        args.Handled = true;
        if (HasComp<CMUDashCameraMountComponent>(vehicle) || TerminatingOrDeleted(vehicle))
            return;

        var slot = _container.EnsureContainer<ContainerSlot>(vehicle, CMUDashCameraMountComponent.ContainerId);
        if (!_container.Insert(ent.Owner, slot))
            return;

        var mount = EnsureComp<CMUDashCameraMountComponent>(vehicle);
        mount.Camera = ent;
        ent.Comp.Vehicle = vehicle;
        ent.Comp.Shorted = false;

        _camera.SetCameraName(ent, Loc.GetString("cmu-dash-camera-name", ("vehicle", Name(vehicle))), null);
        _networks.AddNetwork(ent, ent.Comp.Network);
        _networks.RefreshMemberDirectory(ent);
        _popup.PopupEntity(Loc.GetString("cmu-dash-camera-fitted", ("vehicle", vehicle)), vehicle, args.User);
    }

    private void OnVehicleInteractUsing(Entity<CMUDashCameraMountComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp(ent.Comp.Camera, out CMUDashCameraComponent? camera))
            return;

        var user = args.User;
        var used = args.Used;
        if (_tool.HasQuality(used, Screwing))
        {
            args.Handled = true;
            if (!camera.Shorted && !HasAuthorizedId(user, camera))
            {
                _popup.PopupEntity(Loc.GetString("cmu-dash-camera-no-id"), ent, user);
                return;
            }

            if (_tool.UseTool(used, user, ent, (float) camera.RemoveDelay.TotalSeconds, Screwing, new CMUDashCameraRemoveDoAfterEvent()))
                _popup.PopupEntity(Loc.GetString("cmu-dash-camera-unscrewing", ("vehicle", ent.Owner)), ent, user);

            return;
        }

        if (!_tool.HasQuality(used, Pulsing))
            return;

        args.Handled = true;
        if (camera.Shorted)
        {
            _popup.PopupEntity(Loc.GetString("cmu-dash-camera-already-shorted"), ent, user);
            return;
        }

        if (_tool.UseTool(used, user, ent, (float) camera.ShortDelay.TotalSeconds, Pulsing, new CMUDashCameraShortDoAfterEvent()))
            _popup.PopupEntity(Loc.GetString("cmu-dash-camera-shorting"), ent, user);
    }

    private bool HasAuthorizedId(EntityUid user, CMUDashCameraComponent camera)
    {
        if (!_idCard.TryFindIdCard(user, out var card))
            return false;

        return Prototype(card)?.ID is { } id && camera.AuthorizedIds.Contains(id);
    }

    private void OnShortDoAfter(Entity<CMUDashCameraMountComponent> ent, ref CMUDashCameraShortDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var camera = ent.Comp.Camera;
        if (!TryComp(camera, out CMUDashCameraComponent? dashCamera) || dashCamera.Shorted)
            return;

        dashCamera.Shorted = true;
        _networks.RemoveNetwork(camera, dashCamera.Network);
        _popup.PopupEntity(Loc.GetString("cmu-dash-camera-shorted"), ent, args.User, PopupType.MediumCaution);
    }

    private void OnRemoveDoAfter(Entity<CMUDashCameraMountComponent> ent, ref CMUDashCameraRemoveDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        var user = args.User;
        var camera = ent.Comp.Camera;
        RemComp<CMUDashCameraMountComponent>(ent);
        if (!TryComp(camera, out CMUDashCameraComponent? dashCamera) || TerminatingOrDeleted(camera))
            return;

        dashCamera.Vehicle = null;
        if (!dashCamera.Shorted)
            _networks.RemoveNetwork(camera, dashCamera.Network);

        _camera.SetCameraName(camera, Name(camera), null);

        if (_container.TryGetContainer(ent, CMUDashCameraMountComponent.ContainerId, out var container))
            _container.Remove(camera, container);

        _hands.PickupOrDrop(user, camera);
        _popup.PopupEntity(Loc.GetString("cmu-dash-camera-removed", ("vehicle", ent.Owner)), ent, user);
    }

    private void OnCameraExamined(Entity<CMUDashCameraComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Shorted ? "cmu-dash-camera-examine-shorted" : "cmu-dash-camera-examine"));
    }

    private void OnVehicleExamined(Entity<CMUDashCameraMountComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var shorted = TryComp(ent.Comp.Camera, out CMUDashCameraComponent? camera) && camera.Shorted;
        args.PushMarkup(Loc.GetString(shorted
            ? "cmu-dash-camera-examine-vehicle-shorted"
            : "cmu-dash-camera-examine-vehicle"));
    }
}
