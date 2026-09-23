using System.Numerics;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Robust.Client;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.TacticalMap.Reconstruction;

public readonly record struct CMUReconCamera(Vector2 Center, float Yaw, float Pitch, float Distance,
    int Depth, bool Overhead, bool LowWalls, bool Isolated, bool Labels, bool Fit);

/// <summary>One recent survey, including partial loads; CPU data only. GPU resources die with the window.</summary>
public sealed class CMUReconstructionCacheSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IBaseClient _client = default!;
    private Entry? _entry;
    private int _requestId;

    public int NextRequestId() => ++_requestId;

    private sealed record Entry(EntityUid Console, EntityUid Actor, string? Faction, EntityUid? Map,
        EntityUid? Location, EntityUid? ActorLocation, TimeSpan Expires, CMUReconSnapshotMessage Scene, CMUReconCamera Camera);

    public override void Initialize()
    {
        base.Initialize();
        _client.RunLevelChanged += OnRunLevelChanged;
    }

    public override void Shutdown()
    {
        _client.RunLevelChanged -= OnRunLevelChanged;
        _entry = null;
        base.Shutdown();
    }

    private void OnRunLevelChanged(object? sender, RunLevelChangedEventArgs args) => _entry = null;

    public void Remember(EntityUid console, EntityUid actor, CMUReconSnapshotMessage scene, CMUReconCamera camera)
    {
        if (_client.RunLevel != ClientRunLevel.InGame || scene.TotalChunks == 0 || scene.LoadedChunks == 0 ||
            !Identity(console, out var faction, out var map))
            return;
        _entry = new Entry(console, actor, faction, map, Transform(console).MapUid, Transform(actor).MapUid,
            _timing.RealTime + TimeSpan.FromMinutes(5), scene, camera);
    }

    public bool TryTake(EntityUid console, EntityUid actor, out CMUReconSnapshotMessage scene, out CMUReconCamera camera,
        bool preferPlanetOnShip = false)
    {
        scene = default!;
        camera = default;
        var entry = _entry;
        _entry = null;
        if (entry == null || entry.Expires <= _timing.RealTime || entry.Console != console || entry.Actor != actor ||
            !Identity(console, out var faction, out var map) || entry.Faction != faction ||
            entry.Map != map || entry.Location != Transform(console).MapUid || entry.ActorLocation != Transform(actor).MapUid ||
            CMUReconMapSelection.Choose(CMUReconMapChoice.Automatic, entry.Scene.AboardShip, preferPlanetOnShip,
                entry.Scene.HasPlanet, entry.Scene.HasShip) != entry.Scene.MapChoice)
            return false;
        scene = entry.Scene;
        camera = entry.Camera;
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
        if (_entry is { } entry && (_timing.RealTime >= entry.Expires || Deleted(entry.Console) || Deleted(entry.Actor)))
            _entry = null;
    }
}
