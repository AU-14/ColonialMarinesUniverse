using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.GameTicking;
using Content.Shared.NameModifier.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.Camera;

// we would be using the upstream system for cameras IF IT WAS NOT ABOMINABLE DOGSHIT
public abstract partial class SharedRMCCameraSystem : EntitySystem
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private NameModifierSystem _nameModifier = default!;

    private readonly HashSet<EntProtoId> _refresh = new();

    private readonly Dictionary<string, int> _cameraNames = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<RMCCameraComponent, MapInitEvent>(OnCameraMapInit, after: new [] { typeof(AreaSystem), typeof(SharedDropshipWeaponSystem) });
        SubscribeLocalEvent<RMCCameraComponent, ComponentRemove>(OnCameraRemove);
        SubscribeLocalEvent<RMCCameraComponent, EntityTerminatingEvent>(OnCameraTerminating);

        SubscribeLocalEvent<RMCCameraComputerComponent, MapInitEvent>(OnComputerMapInit, after: new [] { typeof(AreaSystem), typeof(SharedDropshipWeaponSystem) });

        SubscribeLocalEvent<RMCCameraWatcherComponent, ComponentRemove>(OnWatcherRemove);
        SubscribeLocalEvent<RMCCameraWatcherComponent, EntityTerminatingEvent>(OnWatcherTerminating);

        Subs.BuiEvents<RMCCameraComputerComponent>(RMCCameraUiKey.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnComputerBuiOpened);
                subs.Event<BoundUIClosedEvent>(OnComputerBuiClosed);
                subs.Event<RMCCameraWatchBuiMsg>(OnComputerWatchBuiMsg);
                subs.Event<RMCCameraPreviousBuiMsg>(OnComputerPreviousBuiMsg);
                subs.Event<RMCCameraNextBuiMsg>(OnComputerNextBuiMsg);
            });
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _cameraNames.Clear();
    }

    private void OnCameraMapInit(Entity<RMCCameraComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Id is { } id)
            _refresh.Add(id);

        if (ent.Comp.Rename)
        {
            if (!_area.TryGetArea(ent, out _, out var areaProto))
                return;

            var areaName = areaProto.Name;
            var count = _cameraNames.GetValueOrDefault(areaName);
            _metaData.SetEntityName(ent, $"{areaName} #{++count}");
            _cameraNames[areaName] = count;
        }
        else
        {
            var name = Name(ent);
            if (ent.Comp.NameOverride != null)
                name = ent.Comp.NameOverride;

            var count = _cameraNames.GetValueOrDefault(name);
            _cameraNames[name] = count;
        }
    }

    private void OnCameraRemove(Entity<RMCCameraComponent> ent, ref ComponentRemove args)
    {
        OnCameraRemoved(ent);
    }

    private void OnCameraTerminating(Entity<RMCCameraComponent> ent, ref EntityTerminatingEvent args)
    {
        OnCameraRemoved(ent);
    }

    private void OnComputerMapInit(Entity<RMCCameraComputerComponent> ent, ref MapInitEvent args)
    {
        RebuildComputerCameras(ent.Owner, ent.Comp);
    }

    private void OnWatcherRemove(Entity<RMCCameraWatcherComponent> ent, ref ComponentRemove args)
    {
        OnWatcherRemoved(ent);
    }

    private void OnWatcherTerminating(Entity<RMCCameraWatcherComponent> ent, ref EntityTerminatingEvent args)
    {
        OnWatcherRemoved(ent);
    }

    private void OnComputerBuiOpened(Entity<RMCCameraComputerComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        var actor = args.Actor;
        ent.Comp.Watchers.Add(actor);
        Dirty(ent);

        var watcher = EnsureComp<RMCCameraWatcherComponent>(actor);
        watcher.Computer = null;
        Dirty(actor, watcher);

        Refresh(ent, null);
    }

    private void OnComputerBuiClosed(Entity<RMCCameraComputerComponent> ent, ref BoundUIClosedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        var actor = args.Actor;
        ent.Comp.Watchers.Remove(actor);
        Dirty(ent);

        RemCompDeferred<RMCCameraWatcherComponent>(actor);
    }

    private void OnComputerWatchBuiMsg(Entity<RMCCameraComputerComponent> ent, ref RMCCameraWatchBuiMsg args)
    {
        if (_timing.ApplyingState)
            return;

        if (!TryGetEntity(args.Camera, out var camera))
            return;

        if (!ent.Comp.CameraIds.Contains(args.Camera))
            return;

        var old = ent.Comp.CurrentCamera;
        ent.Comp.CurrentCamera = camera;
        Refresh(ent, old);
    }

    private void OnComputerPreviousBuiMsg(Entity<RMCCameraComputerComponent> ent, ref RMCCameraPreviousBuiMsg args)
    {
        var old = ent.Comp.CurrentCamera;
        var index = 0;
        if (old != null &&
            TryGetNetEntity(old, out var netCamera))
        {
            index = ent.Comp.CameraIds.IndexOf(netCamera.Value) - 1;
            if (index < 0 || index >= ent.Comp.CameraIds.Count)
                index = ent.Comp.CameraIds.Count - 1;
        }

        if (index >= 0 &&
            index < ent.Comp.CameraIds.Count &&
            TryGetEntity(ent.Comp.CameraIds[index], out var camera))
        {
            ent.Comp.CurrentCamera = camera;
        }

        Refresh(ent, old);
    }

    private void OnComputerNextBuiMsg(Entity<RMCCameraComputerComponent> ent, ref RMCCameraNextBuiMsg args)
    {
        var old = ent.Comp.CurrentCamera;
        var index = 0;
        if (old != null &&
            TryGetNetEntity(old, out var netCamera))
        {
            index = ent.Comp.CameraIds.IndexOf(netCamera.Value) + 1;
            if (index < 0 || index >= ent.Comp.CameraIds.Count)
                index = 0;
        }

        if (index >= 0 &&
            index < ent.Comp.CameraIds.Count &&
            TryGetEntity(ent.Comp.CameraIds[index], out var camera))
        {
            ent.Comp.CurrentCamera = camera;
        }

        Refresh(ent, old);
    }

    protected virtual void Refresh(Entity<RMCCameraComputerComponent> ent, EntityUid? old)
    {
        Dirty(ent);
    }

    protected virtual void OnWatcherRemoved(Entity<RMCCameraWatcherComponent> watcher)
    {
        if (TryComp(watcher.Comp.Computer, out RMCCameraComputerComponent? computer))
        {
            computer.Watchers.Remove(watcher);
            Dirty(watcher.Comp.Computer.Value, computer);
        }
    }

    public bool GetComputerCameraName(Entity<RMCCameraComputerComponent> computer, EntityUid camera, [NotNullWhen(true)] out string? name)
    {
        var index = computer.Comp.CameraIds.IndexOf(GetNetEntity(camera));
        if (index < 0 || index >= computer.Comp.CameraNames.Count)
        {
            name = default;
            return false;
        }

        if (index >= computer.Comp.CameraNames.Count)
        {
            name = default;
            return false;
        }

        name = computer.Comp.CameraNames[index];
        return true;
    }

    private void OnCameraRemoved(Entity<RMCCameraComponent> camera)
    {
        var netCamera = GetNetEntity(camera);
        var computers = EntityQueryEnumerator<RMCCameraComputerComponent>();
        while (computers.MoveNext(out var uid, out var comp))
        {
            foreach (var protoId in comp.ProtoIds)
            {
                if (protoId != camera.Comp.Id || TerminatingOrDeleted(uid))
                    continue;

                var index = comp.CameraIds.IndexOf(netCamera);
                if (index >= 0)
                {
                    comp.CameraIds.RemoveAt(index);
                    if (index < comp.CameraNames.Count)
                        comp.CameraNames.RemoveAt(index);
                }

                var old = comp.CurrentCamera;
                if (old == camera)
                    comp.CurrentCamera = null;

                if (old == camera)
                    Refresh((uid, comp), old);
                else
                    Dirty(uid, comp);
            }
        }
    }

    public void AddProtoId(RMCCameraComputerComponent computer, EntProtoId protoId)
    {
        computer.ProtoIds.Add(protoId);
    }

    public void RemoveProtoId(RMCCameraComputerComponent computer, EntProtoId protoId)
    {
        computer.ProtoIds.Remove(protoId);

        var cameraQuery = EntityQueryEnumerator<RMCCameraComponent>();
        while (cameraQuery.MoveNext(out var uid, out var camera))
        {
            if (camera.Id != protoId)
                continue;

            computer.CameraIds.Remove(GetNetEntity(uid));

            var name = Name(uid);
            if (camera.NameOverride != null)
                name = camera.NameOverride;

            computer.CameraNames.Remove(name);
        }
    }

    public void RefreshCameras(EntProtoId protoId)
    {
        _refresh.Add(protoId);
    }

    public void RebuildComputerCameras(EntityUid computerUid, RMCCameraComputerComponent? computer = null)
    {
        if (!Resolve(computerUid, ref computer, false))
            return;

        computer.CameraIds.Clear();
        computer.CameraNames.Clear();

        var query = EntityQueryEnumerator<RMCCameraComponent>();
        while (query.MoveNext(out var uid, out var camera))
        {
            foreach (var protoId in computer.ProtoIds)
            {
                if (camera.Id != protoId)
                    continue;

                computer.CameraIds.Add(GetNetEntity(uid));
                computer.CameraNames.Add(GetCameraName(uid, camera));
                break;
            }
        }

        if (computer.CurrentCamera is { } current &&
            !computer.CameraIds.Contains(GetNetEntity(current)))
        {
            computer.CurrentCamera = null;
        }

        Dirty(computerUid, computer);
    }

    public void SetCameraId(EntityUid camera,  EntProtoId protoId, RMCCameraComponent? cameraComponent)
    {
        if (!Resolve(camera, ref cameraComponent, false))
            return;

        cameraComponent.Id = protoId;
        Dirty(camera, cameraComponent);
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

    public override void Update(float frameTime)
    {
        if (_refresh.Count == 0)
            return;

        if (_net.IsClient)
        {
            _refresh.Clear();
            return;
        }

        var monitors = new HashSet<Entity<RMCCameraComputerComponent>>();
        foreach (var refresh in _refresh)
        {
            monitors.Clear();
            var monitorQuery = EntityQueryEnumerator<RMCCameraComputerComponent>();
            while (monitorQuery.MoveNext(out var uid, out var computer))
            {
                foreach (var protoId in computer.ProtoIds)
                {
                    if (protoId == refresh)
                        monitors.Add((uid, computer));
                }
            }

            if (monitors.Count == 0)
                continue;

            foreach (var monitor in monitors)
                RebuildComputerCameras(monitor.Owner, monitor.Comp);
        }

        _refresh.Clear();
    }

    private string GetCameraName(EntityUid uid, RMCCameraComponent camera)
    {
        return camera.NameOverride ?? _nameModifier.GetBaseName(uid);
    }
}
