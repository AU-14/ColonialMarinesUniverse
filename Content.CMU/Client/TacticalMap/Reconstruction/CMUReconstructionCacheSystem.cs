using System.Numerics;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

public readonly record struct CMUReconCamera(Vector2 Center, float Yaw, float Pitch, float Distance,
    int Depth, bool Overhead, bool LowWalls, bool Isolated, bool Labels, bool Fit,
    bool Contacts = true, bool Names = false);

/// <summary>One recent survey, including partial loads and uploaded textures, retained for this game session.</summary>
public sealed partial class CMUReconstructionCacheSystem : EntitySystem
{
    [Dependency] private IBaseClient _client = default!;
    private Entry? _entry;
    private int _requestId;
    // Camera preferences contain no contacts or orders and outlive the disposable terrain cache.
    private readonly Dictionary<int, CMUReconCamera> _views = new();

    public CMUReconCamera? GetView(int atlasId) => _views.TryGetValue(atlasId, out var camera) ? camera : null;

    public void SaveView(int atlasId, CMUReconCamera camera)
    {
        if (atlasId != 0) _views[atlasId] = camera;
    }

    public int NextRequestId() => ++_requestId;

    private sealed record Entry(EntityUid Console, EntityUid Actor, string? Faction, EntityUid? Map,
        EntityUid? Location, EntityUid? ActorLocation, CMUReconSnapshotMessage Scene, CMUReconCamera Camera,
        CMUReconRenderData Render);

    public override void Initialize()
    {
        base.Initialize();
        _client.RunLevelChanged += OnRunLevelChanged;
        SubscribeNetworkEvent<CMUReconPreloadState>(OnPreloadState);
    }

    public override void Shutdown()
    {
        _client.RunLevelChanged -= OnRunLevelChanged;
        CancelPreload(false);
        _views.Clear();
        Clear();
        base.Shutdown();
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args)
    {
        CancelPreload(false);
        _preloadContext = null;
        _views.Clear();
        Clear();
    }

    private void Clear()
    {
        _entry?.Render.Dispose();
        _entry = null;
    }

    public void Remember(EntityUid console, EntityUid actor, CMUReconSnapshotMessage scene, CMUReconCamera camera,
        CMUReconRenderData render)
    {
        if (_client.RunLevel != ClientRunLevel.InGame || scene.TotalChunks == 0 || scene.LoadedChunks == 0 ||
            !Identity(console, out var faction, out var map))
        {
            render.Dispose();
            return;
        }
        Clear();
        _entry = new Entry(console, actor, faction, map, Transform(console).MapUid, Transform(actor).MapUid,
            scene, camera, render);
    }

    public bool TryTake(EntityUid console, EntityUid actor, out CMUReconSnapshotMessage scene, out CMUReconCamera camera,
        out CMUReconRenderData render, bool preferPlanetOnShip = false)
    {
        scene = default!;
        camera = default;
        render = default!;
        if (console == actor && _preloadContext == PreloadContext()) PromotePreload();
        CancelPreload();
        var entry = _entry;
        if (entry == null || entry.Render.Disposed || entry.Console != console || entry.Actor != actor ||
            !Identity(console, out var faction, out var map) || entry.Faction != faction ||
            entry.Map != map || entry.Location != Transform(console).MapUid || entry.ActorLocation != Transform(actor).MapUid ||
            CMUReconMapSelection.Choose(CMUReconMapChoice.Automatic, entry.Scene.AboardShip, preferPlanetOnShip,
                entry.Scene.HasPlanet, entry.Scene.HasShip) != entry.Scene.MapChoice)
        {
            Clear();
            return false;
        }
        _entry = null;
        scene = entry.Scene;
        camera = entry.Camera;
        render = entry.Render;
        return true;
    }

    private bool Identity(EntityUid source, out string? faction, out EntityUid? map)
    {
        if (TryComp<TacticalMapUserComponent>(source, out var user))
        {
            faction = $"{user.Marines}:{user.Xenos}:{user.Govfor}:{user.Opfor}:{user.Clf}:{user.WeYu}:{user.Abomination}";
            map = user.Map;
            return true;
        }
        if (TryComp<TacticalMapComputerComponent>(source, out var computer))
        {
            faction = computer.Faction;
            map = computer.Map;
            return true;
        }
        faction = null;
        map = null;
        return false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_entry is { } entry && (Deleted(entry.Console) || Deleted(entry.Actor) ||
            _players.LocalEntity != entry.Actor || !Identity(entry.Console, out var faction, out var boundMap) ||
            faction != entry.Faction || boundMap != entry.Map || Transform(entry.Actor).MapUid != entry.ActorLocation ||
            Transform(entry.Console).MapUid != entry.Location ||
            entry.Map is { } map && Deleted(map) || entry.Location is { } location && Deleted(location)))
            Clear();
    }
}
