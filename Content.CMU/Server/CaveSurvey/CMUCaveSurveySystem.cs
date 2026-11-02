using System.Numerics;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Water;
using Content.Shared.Actions;
using Content.Shared.CMU14.CaveSurvey;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Wall;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.CaveSurvey;

public sealed partial class CMUCaveSurveySystem : EntitySystem
{
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private ZLevelBuildingSystem _building = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const int MaxWorlds = 4;
    private const int ObserversPerTick = 8;
    private static readonly EntProtoId SurveyAction = "CMUActionCaveSurvey";
    private static readonly CMUCaveSurveyUiKey Key = CMUCaveSurveyUiKey.Key;
    private readonly Queue<EntityUid> _observers = new();
    private readonly List<CMUCaveSurveySample> _samples = new(16);
    private readonly CMUReconMaterial?[] _footprint = new CMUReconMaterial?[16];
    private TimeSpan _nextScan;
    private long _revision;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCaveSurveyMapComponent, UseInHandEvent>(OnRead);
        SubscribeLocalEvent<CMUCaveSurveyMapComponent, GetVerbsEvent<AlternativeVerb>>(OnMapVerbs);
        Subs.BuiEvents<CMUCaveSurveyAccessComponent>(Key, subs =>
        {
            subs.Event<CMUCaveSurveyViewMessage>(OnView);
            subs.Event<CMUCaveSurveySurfaceMessage>(OnSurface);
            subs.Event<BoundUIClosedEvent>((Entity<CMUCaveSurveyAccessComponent> e, ref BoundUIClosedEvent m) => _viewGenerations.Remove(e.Owner));
        });
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _observers.Clear();
            _outgoing.Clear();
            _viewGenerations.Clear();
            _revision = 0;
            _nextScan = TimeSpan.Zero;
            var memories = EntityQueryEnumerator<CMUCaveSurveyMemoryComponent>();
            while (memories.MoveNext(out var uid, out var memory))
            {
                memory.Worlds.Clear();
                memory.SelectedWorld = default;
                memory.LastMap = default;
                memory.NextObservation = memory.NextRequest = TimeSpan.Zero;
                memory.NextCopy = memory.NextRead = TimeSpan.Zero;
                memory.LastViewRequest = -1;
                memory.WarnedFull = false;
                _ui.CloseUi(uid, Key);
                _actions.RemoveAction(memory.Action);
                memory.Action = null;
                RemComp<CMUCaveSurveyAccessComponent>(uid);
            }
        });
    }

    public override void Update(float frameTime)
    {
        SendPending();
        if (_observers.Count == 0 && _timing.CurTime >= _nextScan)
        {
            _nextScan = _timing.CurTime + TimeSpan.FromSeconds(0.25);
            var actors = EntityQueryEnumerator<ActorComponent>();
            while (actors.MoveNext(out var uid, out _)) _observers.Enqueue(uid);
        }
        for (var i = 0; i < ObserversPerTick && _observers.TryDequeue(out var actor); i++)
            Observe(actor);
    }

    /// <summary>Small, wall-clipped observations of existing terrain. This never invokes generation.</summary>
    public void Observe(EntityUid actor)
    {
        if (!Exists(actor) || !_mob.IsAlive(actor) || HasComp<Content.Shared.Ghost.Components.GhostComponent>(actor)) return;
        var xform = Transform(actor);
        if (xform.MapUid is not { } map || !TryComp<CMUZLevelMapComponent>(map, out var level) || level.Depth >= 0 ||
            !TryComp<MapGridComponent>(xform.GridUid ?? map, out var grid)) return;
        var gridUid = xform.GridUid ?? map;
        var position = _transform.GetWorldPosition(actor);
        var tile = new Vector2i((int) MathF.Floor(position.X), (int) MathF.Floor(position.Y));
        var memory = EnsureComp<CMUCaveSurveyMemoryComponent>(actor);
        if (memory.LastMap == map && memory.LastTile == tile && memory.NextObservation > _timing.CurTime) return;
        memory.LastMap = map;
        memory.LastTile = tile;
        memory.NextObservation = _timing.CurTime + TimeSpan.FromSeconds(2);

        var origin = tile - Vector2i.One;
        var stone = CompOrNull<ZGeneratedStoneComponent>(map);
        var chunkSize = stone == null ? 8 : Math.Max(2, _building.GetLevelSettings(map).ChunkSize);
        for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
        {
            var at = origin + new Vector2i(x, y);
            var local = _maps.WorldToTile(gridUid, grid, (Vector2) at + new Vector2(0.5f));
            var generated = stone == null || stone.LocalizedToAuthoredLevel || stone.GeneratedChunks.Contains(
                new Vector2i(CMUCaveSurveyData.FloorDiv(local.X, chunkSize), CMUCaveSurveyData.FloorDiv(local.Y, chunkSize)));
            _footprint[y * 4 + x] = generated ? ReadMaterial(gridUid, grid, local) : null;
        }
        _samples.Clear();
        for (var y = 0; y < 4; y++)
        for (var x = 0; x < 4; x++)
        {
            if (_footprint[y * 4 + x] is not { } material || !Visible(x, y)) continue;
            _samples.Add(new(origin + new Vector2i(x, y), level.Depth, material));
        }
        if (_samples.Count == 0) return;
        if (!memory.Worlds.TryGetValue(level.NetworkUid, out var data))
        {
            if (memory.Worlds.Count >= MaxWorlds) return;
            memory.Worlds.Add(level.NetworkUid, data = new());
        }
        data.Record(_samples, ++_revision);
        if (!memory.Worlds.ContainsKey(memory.SelectedWorld)) memory.SelectedWorld = level.NetworkUid;
        GrantAccess(actor);
        WarnIfFull(actor, memory, data);
    }

    private CMUReconMaterial ReadMaterial(EntityUid uid, MapGridComponent grid, Vector2i tile)
    {
        var material = _maps.GetTileRef(uid, grid, tile).Tile.IsEmpty ? CMUReconMaterial.Empty : CMUReconMaterial.Ground;
        var anchored = _maps.GetAnchoredEntities(uid, grid, tile);
        while (anchored.MoveNext(out var entity))
        {
            if (entity is not { } candidate || TerminatingOrDeleted(candidate) || EntityManager.IsQueuedForDeletion(candidate)) continue;
            if (TryComp<DoorComponent>(candidate, out var door))
            {
                if (door.State != DoorState.Open) return CMUReconMaterial.Door;
                material = CMUReconMaterial.OpenDoor;
            }
            else if (HasComp<WallComponent>(candidate)) return CMUReconMaterial.Rock;
            else if (TryComp<PhysicsComponent>(candidate, out var physics) && physics.CanCollide &&
                     (physics.CollisionLayer & (int) CollisionGroup.Impassable) != 0) return CMUReconMaterial.Wall;
            else if (HasComp<CMUZLevelLadderComponent>(candidate) || HasComp<CMUZLevelHighGroundComponent>(candidate))
                material = CMUReconMaterial.Stairs;
            else if (HasComp<RMCWaterComponent>(candidate) && material != CMUReconMaterial.Stairs)
                material = CMUReconMaterial.Water;
        }
        return material;
    }

    private bool Blocked(int x, int y) => _footprint[y * 4 + x] is null or CMUReconMaterial.Rock or CMUReconMaterial.Wall or CMUReconMaterial.Door;

    private bool Visible(int targetX, int targetY)
    {
        var x = 1;
        var y = 1;
        var dx = Math.Abs(targetX - x);
        var dy = Math.Abs(targetY - y);
        var sx = Math.Sign(targetX - x);
        var sy = Math.Sign(targetY - y);
        var error = dx - dy;
        while (x != targetX || y != targetY)
        {
            var previousX = x;
            var previousY = y;
            var twice = 2 * error;
            if (twice > -dy) { error -= dy; x += sx; }
            if (twice < dx) { error += dx; y += sy; }
            // Do not reveal through the diagonal seam between two solid walls.
            if (x != previousX && y != previousY && Blocked(x, previousY) && Blocked(previousX, y)) return false;
            if ((x != targetX || y != targetY) && Blocked(x, y)) return false;
        }
        return true;
    }

    private void GrantAccess(EntityUid actor)
    {
        if (HasComp<CMUCaveSurveyAccessComponent>(actor)) return;
        EnsureComp<CMUCaveSurveyAccessComponent>(actor);
        _ui.SetUi(actor, Key, new InterfaceData("CMUCaveSurveyBui"));
        if (!HasComp<TacticalMapUserComponent>(actor))
            EnsureComp<CMUCaveSurveyMemoryComponent>(actor).Action = _actions.AddAction(actor, SurveyAction);
    }

    public void Open(EntityUid actor)
    {
        if (!TryComp<CMUCaveSurveyMemoryComponent>(actor, out var memory) || memory.Worlds.Count == 0) return;
        GrantAccess(actor);
        _ui.TryOpenUi(actor, Key, actor);
    }

    private bool CanUseMap(EntityUid actor, EntityUid map) => Exists(actor) && Exists(map) && _mob.IsAlive(actor) &&
        HasComp<CMUCaveSurveyMapComponent>(map) && _interaction.InRangeAndAccessible(actor, map);

    private void OnRead(Entity<CMUCaveSurveyMapComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        ReadMap(args.User, ent.Owner);
    }

    private void OnMapVerbs(Entity<CMUCaveSurveyMapComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract) return;
        var actor = args.User;
        args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString("cmu-survey-write"), Act = () => WriteMap(actor, ent.Owner) });
        args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString("cmu-survey-read"), Act = () => ReadMap(actor, ent.Owner) });
    }

    public bool WriteMap(EntityUid actor, EntityUid map)
    {
        if (!CanUseMap(actor, map)) return false;
        if (!TryComp<CMUCaveSurveyMemoryComponent>(actor, out var memory) || memory.Worlds.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-survey-no-recording"), map, actor);
            return false;
        }
        if (memory.NextCopy > _timing.CurTime) return false;
        memory.NextCopy = _timing.CurTime + TimeSpan.FromSeconds(1);
        var written = EnsureComp<CMUCaveSurveyMemoryComponent>(map);
        foreach (var (world, data) in memory.Worlds)
        {
            if (written.Worlds.TryGetValue(world, out var existing)) existing.Merge(data);
            else if (written.Worlds.Count < MaxWorlds) written.Worlds.Add(world, data.Snapshot());
        }
        written.SelectedWorld = memory.SelectedWorld;
        _popup.PopupEntity(Loc.GetString("cmu-survey-written"), map, actor);
        return true;
    }

    public bool ReadMap(EntityUid actor, EntityUid map)
    {
        if (!CanUseMap(actor, map)) return false;
        if (!TryComp<CMUCaveSurveyMemoryComponent>(map, out var written) || written.Worlds.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-survey-blank"), map, actor);
            return false;
        }
        var memory = EnsureComp<CMUCaveSurveyMemoryComponent>(actor);
        if (memory.NextRead > _timing.CurTime) return false;
        memory.NextRead = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        foreach (var (world, data) in written.Worlds)
        {
            if (TerminatingOrDeleted(world)) continue;
            if (memory.Worlds.TryGetValue(world, out var existing)) existing.Merge(data);
            else if (memory.Worlds.Count < MaxWorlds) memory.Worlds.Add(world, existing = data.Snapshot());
            if (existing != null) WarnIfFull(actor, memory, existing);
        }
        if (memory.Worlds.ContainsKey(written.SelectedWorld)) memory.SelectedWorld = written.SelectedWorld;
        Open(actor);
        return true;
    }

    private void WarnIfFull(EntityUid actor, CMUCaveSurveyMemoryComponent memory, CMUCaveSurveyData data)
    {
        if (!data.Full || memory.WarnedFull) return;
        memory.WarnedFull = true;
        _popup.PopupEntity(Loc.GetString("cmu-survey-full"), actor, actor);
    }
}
