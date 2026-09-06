using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.GameTicking;
using Content.Shared.SurveillanceCamera;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Camera;

// we would be using the upstream system for cameras IF IT WAS NOT ABOMINABLE DOGSHIT
public abstract partial class SharedRMCCameraSystem : EntitySystem
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<string, int> _cameraNames = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<RMCCameraComponent, MapInitEvent>(OnCameraMapInit, after: new [] { typeof(AreaSystem), typeof(SharedDropshipWeaponSystem) });

        Subs.BuiEvents<RMCCameraComputerComponent>(RMCCameraUiKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnComputerBuiOpened);
                subs.Event<BoundUIClosedEvent>(OnComputerBuiClosed);
                subs.Event<RMCCameraWatchBuiMsg>(OnComputerWatchBuiMsg);
                subs.Event<RMCCameraPreviousBuiMsg>(OnComputerPreviousBuiMsg);
                subs.Event<RMCCameraNextBuiMsg>(OnComputerNextBuiMsg);
                subs.Event<RMCCameraRefreshSubnetsBuiMsg>(OnComputerRefreshSubnetsBuiMsg);
                subs.Event<RMCCameraSessionNetworkBuiMsg>(OnComputerSessionNetworkBuiMsg);
                subs.Event<CameraSessionResyncMessage>(OnComputerSessionResyncBuiMsg);
                subs.Event<RMCCameraDisconnectBuiMsg>(OnComputerDisconnectBuiMsg);
                subs.Event<RMCCameraNetworkEditorCreateBuiMsg>(OnEditorCreateBuiMsg);
                subs.Event<RMCCameraNetworkEditorRenameBuiMsg>(OnEditorRenameBuiMsg);
                subs.Event<RMCCameraNetworkEditorDeleteBuiMsg>(OnEditorDeleteBuiMsg);
                subs.Event<RMCCameraNetworkEditorSetHiddenBuiMsg>(OnEditorSetHiddenBuiMsg);
                subs.Event<RMCCameraNetworkEditorSaveCameraBuiMsg>(OnEditorSaveCameraBuiMsg);
            });
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _cameraNames.Clear();
        OnCameraEditorRoundRestartCleanup();
    }

    protected virtual void OnCameraEditorRoundRestartCleanup()
    {
    }

    private void OnCameraMapInit(Entity<RMCCameraComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Rename)
        {
            if (!_area.TryGetArea(ent, out _, out var areaProto))
                return;

            var areaName = areaProto.Name;
            var count = _cameraNames.GetValueOrDefault(areaName) + 1;

            ent.Comp.Rename = false; // Do not run again.
            ent.Comp.NameOverride = $"{areaName} #{count}";
            Dirty(ent);

            _cameraNames[areaName] = count;
        }
        else
        {
            var name = Name(ent);
            if (ent.Comp.NameOverride != null)
                name = ent.Comp.NameOverride;

            var count = _cameraNames.GetValueOrDefault(name) + 1;
            _cameraNames[name] = count;
        }

    }

    private void OnComputerBuiOpened(Entity<RMCCameraComputerComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (_timing.ApplyingState || !CanUseComputer(ent, args.Actor))
            return;

        OnComputerUiOpened(ent, args.Actor);
    }

    protected virtual void OnComputerUiOpened(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
    }

    private void OnComputerBuiClosed(Entity<RMCCameraComputerComponent> ent, ref BoundUIClosedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        OnComputerUiClosed(ent, args.Actor);
    }

    protected virtual void OnComputerUiClosed(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
    }

    protected virtual bool CanUseComputer(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
        return true;
    }

    protected void RevokeComputerSession(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
        OnComputerUiClosed(computer, actor);
    }

    private bool TryUseComputer(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
        if (CanUseComputer(computer, actor))
            return true;

        RevokeComputerSession(computer, actor);
        return false;
    }

    private void OnComputerWatchBuiMsg(Entity<RMCCameraComputerComponent> ent, ref RMCCameraWatchBuiMsg args)
    {
        if (_timing.ApplyingState || !TryUseComputer(ent, args.Actor))
            return;

        if (!TryGetEntity(args.Camera, out var camera) || camera is not { } cameraUid)
        {
            RefreshRejectedSelection(ent);
            return;
        }

        if (!TrySelectCameraFor(ent, args.Actor, cameraUid))
            RefreshRejectedSelection(ent);
    }

    private void OnComputerPreviousBuiMsg(Entity<RMCCameraComputerComponent> ent, ref RMCCameraPreviousBuiMsg args)
    {
        if (!TryUseComputer(ent, args.Actor))
            return;

        SelectRelativeCamera(ent, args.Actor, -1);
    }

    private void OnComputerNextBuiMsg(Entity<RMCCameraComputerComponent> ent, ref RMCCameraNextBuiMsg args)
    {
        if (!TryUseComputer(ent, args.Actor))
            return;

        SelectRelativeCamera(ent, args.Actor, 1);
    }

    private void OnComputerRefreshSubnetsBuiMsg(Entity<RMCCameraComputerComponent> ent, ref RMCCameraRefreshSubnetsBuiMsg args)
    {
        if (_timing.ApplyingState || !TryUseComputer(ent, args.Actor))
            return;

        RefreshFor(ent, args.Actor);
    }

    private void OnComputerSessionNetworkBuiMsg(
        Entity<RMCCameraComputerComponent> computer,
        ref RMCCameraSessionNetworkBuiMsg args)
    {
        if (_timing.ApplyingState || !TryUseComputer(computer, args.Actor))
            return;

        OnSessionNetworkBuiMsg(computer, args);
    }

    protected virtual void OnSessionNetworkBuiMsg(
        Entity<RMCCameraComputerComponent> computer,
        RMCCameraSessionNetworkBuiMsg args)
    {
    }

    private void OnComputerSessionResyncBuiMsg(
        Entity<RMCCameraComputerComponent> computer,
        ref CameraSessionResyncMessage args)
    {
        if (_timing.ApplyingState || !TryUseComputer(computer, args.Actor))
            return;

        OnSessionResyncBuiMsg(computer, args);
    }

    protected virtual void OnSessionResyncBuiMsg(
        Entity<RMCCameraComputerComponent> computer,
        CameraSessionResyncMessage args)
    {
    }

    private void OnComputerDisconnectBuiMsg(Entity<RMCCameraComputerComponent> ent, ref RMCCameraDisconnectBuiMsg args)
    {
        if (_timing.ApplyingState || !TryUseComputer(ent, args.Actor))
            return;

        DisconnectFor(ent, args.Actor);
    }

    private void OnEditorCreateBuiMsg(
        Entity<RMCCameraComputerComponent> computer,
        ref RMCCameraNetworkEditorCreateBuiMsg args)
    {
        if (!_timing.ApplyingState && TryUseComputer(computer, args.Actor))
            OnEditorCreate(computer, args);
    }

    private void OnEditorRenameBuiMsg(
        Entity<RMCCameraComputerComponent> computer,
        ref RMCCameraNetworkEditorRenameBuiMsg args)
    {
        if (!_timing.ApplyingState && TryUseComputer(computer, args.Actor))
            OnEditorRename(computer, args);
    }

    private void OnEditorDeleteBuiMsg(
        Entity<RMCCameraComputerComponent> computer,
        ref RMCCameraNetworkEditorDeleteBuiMsg args)
    {
        if (!_timing.ApplyingState && TryUseComputer(computer, args.Actor))
            OnEditorDelete(computer, args);
    }

    private void OnEditorSetHiddenBuiMsg(
        Entity<RMCCameraComputerComponent> computer,
        ref RMCCameraNetworkEditorSetHiddenBuiMsg args)
    {
        if (!_timing.ApplyingState && TryUseComputer(computer, args.Actor))
            OnEditorSetHidden(computer, args);
    }

    private void OnEditorSaveCameraBuiMsg(
        Entity<RMCCameraComputerComponent> computer,
        ref RMCCameraNetworkEditorSaveCameraBuiMsg args)
    {
        if (!_timing.ApplyingState && TryUseComputer(computer, args.Actor))
            OnEditorSaveCamera(computer, args);
    }

    protected virtual void OnEditorCreate(
        Entity<RMCCameraComputerComponent> computer,
        RMCCameraNetworkEditorCreateBuiMsg args)
    {
    }

    protected virtual void OnEditorRename(
        Entity<RMCCameraComputerComponent> computer,
        RMCCameraNetworkEditorRenameBuiMsg args)
    {
    }

    protected virtual void OnEditorDelete(
        Entity<RMCCameraComputerComponent> computer,
        RMCCameraNetworkEditorDeleteBuiMsg args)
    {
    }

    protected virtual void OnEditorSetHidden(
        Entity<RMCCameraComputerComponent> computer,
        RMCCameraNetworkEditorSetHiddenBuiMsg args)
    {
    }

    protected virtual void OnEditorSaveCamera(
        Entity<RMCCameraComputerComponent> computer,
        RMCCameraNetworkEditorSaveCameraBuiMsg args)
    {
    }

    protected virtual void RefreshRejectedSelection(Entity<RMCCameraComputerComponent> computer)
    {
    }

    protected virtual void RefreshFor(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
    }

    protected virtual void DisconnectFor(Entity<RMCCameraComputerComponent> computer, EntityUid actor)
    {
    }

    protected virtual void SelectRelativeCamera(
        Entity<RMCCameraComputerComponent> computer,
        EntityUid actor,
        int offset)
    {
    }

    public virtual bool TrySelectCameraFor(
        Entity<RMCCameraComputerComponent> computer,
        EntityUid actor,
        EntityUid camera)
    {
        return false;
    }

    public void SetCameraName(EntityUid camera,  string name, RMCCameraComponent? cameraComponent)
    {
        if (!Resolve(camera, ref cameraComponent, false))
            return;

        cameraComponent.NameOverride = name;
        Dirty(camera, cameraComponent);
    }

    public void SetCameraRename(EntityUid camera, bool rename, RMCCameraComponent? cameraComponent)
    {
        if (!Resolve(camera, ref cameraComponent, false))
            return;

        cameraComponent.Rename = rename;
        Dirty(camera, cameraComponent);
    }

    protected string GetCameraName(EntityUid uid, RMCCameraComponent camera)
    {
        return camera.NameOverride ?? Name(uid);
    }
}
