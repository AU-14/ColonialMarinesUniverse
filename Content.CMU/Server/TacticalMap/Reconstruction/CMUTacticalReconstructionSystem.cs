using Stopwatch = System.Diagnostics.Stopwatch;
using System.Linq;
using System.Numerics;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Verbs;
using Content.Shared.Wall;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Entrenching;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.TacticalMap.Reconstruction;

/// <summary>Shared, incrementally built full-map structural atlas. No actors or inventories are exported.</summary>
public sealed partial class CMUTacticalReconstructionSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;

    private static readonly CMUReconstructionUiKey Key = CMUReconstructionUiKey.Key;
    private readonly Dictionary<(EntityUid Console, EntityUid Actor), Survey> _surveys = new();
    private readonly Dictionary<EntityUid, Atlas> _atlases = new();
    private readonly Dictionary<(EntityUid Network, string Faction), List<CMUReconOrder>> _orders = new();
    private readonly Dictionary<string, CMUReconMaterial> _kinds = new();
    private TimeSpan _nextSend;
    private int _generation;
    private int _atlasId;
    private int _orderId;
    private int _workCursor;

    private sealed class Atlas
    {
        public int Id;
        public required EntityUid Network;
        public required EntityUid?[] Maps;
        public required Vector2i Origin;
        public required int Width;
        public required int Height;
        public required int MinDepth;
        public required byte[] Cells;
        public required uint[] Appearance;
        public required byte[] Directions;
        public required int[] Revisions;
        public required CMUReconChunk?[] Chunks;
        public TimeSpan RetainUntil;
        public bool BoundsDirty;
        public required CMUReconLabel[] Labels;
        public readonly Queue<int> Pending = new();
        public readonly HashSet<int> Dirty = new();
        public readonly Dictionary<(string Prototype, byte Variant, bool Entity), ushort> SurfaceIds = new();
        public readonly List<CMUReconSurface> Surfaces = new();
        public int Sweep;
        public int Loaded;
        public int Version;
        public TimeSpan NextSweep;
        public int WorkId = -1;
        public int WorkRow;
        public bool WorkChanged;
        public readonly byte[] WorkCells = new byte[CMUReconGeometry.ChunkSize * CMUReconGeometry.ChunkSize];
        public readonly uint[] WorkAppearance = new uint[CMUReconGeometry.ChunkSize * CMUReconGeometry.ChunkSize];
        public readonly byte[] WorkDirections = new byte[CMUReconGeometry.ChunkSize * CMUReconGeometry.ChunkSize];
        public int Across => Width / CMUReconGeometry.ChunkSize;
        public int PerLevel => Across * (Height / CMUReconGeometry.ChunkSize);
    }

    private sealed class Survey
    {
        public required Atlas Atlas;
        public required string Faction;
        public required int Generation;
        public required int[] Sent;
        public EntityUid Actor;
        public EntityUid Root;
        public EntityUid DrawingScope;
        public CMUReconMapChoice MapChoice;
        public MapTargets Targets;
        public int RequestId;
        public TimeSpan NextSwitch;
        public int SurfaceCount;
        public int Loaded;
        public int Cursor;
        public int ScannedVersion = -1;
        public TimeSpan NextRequest;
        public TimeSpan NextOrder;
        public int[] SentOrders = [];
        public bool? SentCanOrder;
        public bool ReuseGeometry;
        public int[] ChunkOrder = [];
        public CMUReconContactsMessage? LastContacts;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUTacticalReconstructionComponent, GetVerbsEvent<AlternativeVerb>>(OnVerb);
        RegisterInterface<CMUTacticalReconstructionComponent>(Key);
        RegisterInterface<TacticalMapUserComponent>(TacticalMapUserUi.Key);
        RegisterInterface<TacticalMapComputerComponent>(TacticalMapComputerUi.Key);
        Subs.BuiEvents<CMUTacticalReconstructionComponent>(Key, subs =>
        {
            subs.Event<CMUReconClassicMessage>(OnClassic);
            subs.Event<BoundUIClosedEvent>((Entity<CMUTacticalReconstructionComponent> e, ref BoundUIClosedEvent m) => CloseSurvey(e.Owner, m.Actor));
        });
        Subs.BuiEvents<TacticalMapComputerComponent>(TacticalMapComputerUi.Key, subs =>
            subs.Event<BoundUIClosedEvent>((Entity<TacticalMapComputerComponent> e, ref BoundUIClosedEvent m) => CloseSurvey(e.Owner, m.Actor)));
        SubscribeLocalEvent<TileChangedEvent>(OnTilesChanged);
        SubscribeLocalEvent<AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<DoorComponent, DoorStateChangedEvent>(OnDoorChanged);
    }

    private bool CanUse(EntityUid console, EntityUid actor) => !TerminatingOrDeleted(console) &&
        !TerminatingOrDeleted(actor) && (HasComp<TacticalMapUserComponent>(console)
            ? console == actor
            : _access.IsAllowed(actor, console) && _interaction.InRangeUnobstructed(actor, console));

    private bool CanOrder(EntityUid console, EntityUid actor) => CanUse(console, actor) &&
        (TryComp<TacticalMapUserComponent>(console, out var user) ? user.CanDraw && (user.Marines || user.Xenos || user.Govfor || user.Opfor || user.Clf || user.WeYu) :
            TryComp<TacticalMapComputerComponent>(console, out var computer) &&
            SharedTacticalMapSystem.NormalizeMapFaction(computer.Faction) != null &&
            _skills.HasSkill(actor, computer.Skill, computer.SkillLevel));

    private bool IsCurrentSurvey(EntityUid console, Survey survey)
    {
        var atlas = survey.Atlas;
        if (!TrySource(console, out var boundMap, out var faction) || survey.Faction != faction)
            return false;
        if (EntityManager.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().ReconstructionCanvasScope(console, survey.Root) != survey.DrawingScope)
            return false;
        var targets = GetMapTargets(survey.Actor, boundMap, faction);
        if ((survey.MapChoice == CMUReconMapChoice.Ship ? targets.Ship : targets.Planet) != survey.Root)
            return false;
        if (!TryComp<CMUZLevelMapComponent>(survey.Root, out var zMap))
            return atlas.Network == survey.Root && atlas.Maps.Length == 1 &&
                   atlas.Maps[0] == survey.Root && HasComp<MapGridComponent>(survey.Root);
        if (zMap.NetworkUid != atlas.Network ||
            !TryComp<CMUZLevelsNetworkComponent>(atlas.Network, out var network) || network.ZLevels.Count == 0 ||
            network.ZLevels.Keys.Min() != atlas.MinDepth ||
            (long) network.ZLevels.Keys.Max() - atlas.MinDepth + 1 != atlas.Maps.Length)
            return false;
        IReadOnlyDictionary<int, EntityUid?> currentMaps = network.ZLevels;
        for (var i = 0; i < atlas.Maps.Length; i++)
        {
            currentMaps.TryGetValue(atlas.MinDepth + i, out var map);
            if (map is { } uid && (TerminatingOrDeleted(uid) || !HasComp<MapGridComponent>(uid)))
                map = null;
            if (map != atlas.Maps[i])
                return false;
        }
        return true;
    }

    private void OnVerb(Entity<CMUTacticalReconstructionComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanUse(ent, args.User))
            return;
        var actor = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-recon-open"),
            Act = () => { if (CanUse(ent, actor)) _ui.TryOpenUi(ent.Owner, UiKey(ent), actor); },
        });
    }

    private void OnView(EntityUid ent, ref CMUReconViewMessage args)
    {
        if (args.Offset != Vector2i.Zero || args.MapChoice is not (CMUReconMapChoice.Automatic or CMUReconMapChoice.Planet or CMUReconMapChoice.Ship) ||
            args.RequestId < 0 || !CanUse(ent, args.Actor) || !_ui.IsUiOpen(ent, UiKey(ent), args.Actor))
            return;
        var key = (ent, args.Actor);
        if (_surveys.TryGetValue(key, out var old) &&
            (_timing.CurTime < old.NextSwitch || old.RequestId == args.RequestId && _timing.CurTime < old.NextRequest))
            return;
        if (TryComp<TacticalMapComputerComponent>(ent, out var computer))
            EntityManager.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().ResolveReconstructionFaction((ent, computer), args.Actor);
        if (!TryCreateSurvey(ent, args, out var survey))
        {
            _surveys.Remove(key);
            _ui.ServerSendUiMessage(ent, UiKey(ent), new CMUReconFeedbackMessage("cmu-recon-no-map") { RequestId = args.RequestId }, args.Actor);
            return;
        }
        survey.NextRequest = _timing.CurTime + TimeSpan.FromSeconds(2);
        survey.NextSwitch = _timing.CurTime + TimeSpan.FromSeconds(0.25);
        _surveys[key] = survey;
        // Metadata first. Geometry follows in bounded chunk batches.
        _ui.ServerSendUiMessage(ent, UiKey(ent), Snapshot(survey, args.Actor, CanOrder(ent, args.Actor), false), args.Actor);
        SendContacts(ent, args.Actor, survey);
    }

    /// <summary>Detached diagnostics copy. Opening the UI never sends this full allocation.</summary>
    public CMUReconSnapshotMessage? BuildSnapshot(EntityUid console, EntityUid actor) =>
        _surveys.TryGetValue((console, actor), out var survey) ? Snapshot(survey, actor, CanOrder(console, actor), true) : null;

    private CMUReconSnapshotMessage Snapshot(Survey survey, EntityUid actor, bool canOrder, bool includeCells)
    {
        var a = survey.Atlas;
        var pos = _transform.GetWorldPosition(actor);
        var operatorLevel = Array.IndexOf(a.Maps, Transform(actor).MapUid);
        return new CMUReconSnapshotMessage(survey.Generation, a.Origin, a.MinDepth, a.Maps.Length,
            includeCells ? a.Cells.ToArray() : [], Orders(survey).ToArray(), canOrder, a.Width, a.Height)
        {
            Appearance = includeCells ? a.Appearance.ToArray() : [],
            Directions = includeCells ? a.Directions.ToArray() : [],
            Surfaces = includeCells ? a.Surfaces.ToArray() : [],
            Labels = a.Labels,
            OperatorTile = new Vector2i((int) MathF.Floor(pos.X), (int) MathF.Floor(pos.Y)),
            OperatorPosition = operatorLevel >= 0 ? pos : null,
            OperatorDepth = a.MinDepth + operatorLevel,
            MapChoice = survey.MapChoice, AboardShip = survey.Targets.AboardShip,
            HasPlanet = survey.Targets.Planet != null, HasShip = survey.Targets.Ship != null,
            RequestId = survey.RequestId,
            AtlasId = a.Id, ReuseGeometry = survey.ReuseGeometry,
            Revisions = includeCells ? a.Revisions.ToArray() : [],
            LoadedChunks = includeCells ? a.Loaded : survey.Loaded,
            TotalChunks = a.Revisions.Length,
        };
    }

    private bool TryCreateSurvey(EntityUid console, CMUReconViewMessage request, out Survey survey)
    {
        survey = default!;
        if (!TrySource(console, out var boundMap, out var faction))
            return false;
        var targets = GetMapTargets(request.Actor, boundMap, faction);
        var choice = CMUReconMapSelection.Choose(request.MapChoice, targets.AboardShip, request.PreferPlanetOnShip,
            targets.Planet != null, targets.Ship != null);
        var selected = choice == CMUReconMapChoice.Ship ? targets.Ship : targets.Planet;
        if (selected is not { } root || !TryMapLayout(root, out var networkUid, out var min, out var maps)) return false;
        Survey Create(Atlas source)
        {
            var result = CreateSurvey(source, faction);
            result.Actor = request.Actor; result.Root = root; result.MapChoice = choice;
            result.DrawingScope = EntityManager.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().ReconstructionCanvasScope(console, root);
            result.Targets = targets; result.RequestId = request.RequestId;
            var focus = (_transform.GetWorldPosition(request.Actor) - (Vector2) source.Origin) / CMUReconGeometry.ChunkSize;
            var level = Math.Max(0, Array.IndexOf(source.Maps, Transform(request.Actor).MapUid));
            result.ChunkOrder = Enumerable.Range(0, source.Revisions.Length)
                .OrderBy(i => Math.Abs(i / source.PerLevel - level))
                .ThenBy(i => Math.Abs(i % source.PerLevel % source.Across - focus.X) +
                             Math.Abs(i % source.PerLevel / source.Across - focus.Y)).ToArray();
            if (request.AtlasId == source.Id && request.Revisions.Length == source.Revisions.Length &&
                request.SurfaceCount >= 0 && request.SurfaceCount <= source.Surfaces.Count)
            {
                result.ReuseGeometry = true;
                result.SurfaceCount = request.SurfaceCount;
                for (var i = 0; i < result.Sent.Length; i++)
                {
                    var revision = request.Revisions[i];
                    if (revision <= 0 || revision > source.Revisions[i]) continue;
                    result.Sent[i] = revision;
                    result.Loaded++;
                }
            }
            return result;
        }
        if (_atlases.TryGetValue(networkUid, out var retained) && !retained.BoundsDirty &&
            retained.MinDepth == min && retained.Maps.SequenceEqual(maps))
        {
            survey = Create(retained);
            return true;
        }
        var low = new Vector2(float.PositiveInfinity);
        var high = new Vector2(float.NegativeInfinity);
        foreach (var uid in maps)
        {
            if (uid is not { } map || !TryComp<MapGridComponent>(map, out var grid)) continue;
            if (grid.ChunkCount == 0)
                continue;
            // Map-as-grid entities have no collision fixtures, so LocalAABB is not maintained.
            // Read tile extents once when opening; expensive structure extraction stays budgeted.
            var tiles = _maps.GetAllTilesEnumerator(map, grid);
            while (tiles.MoveNext(out var tile))
            {
                if (tile is not { } value) continue;
                var position = (Vector2) value.GridIndices;
                low = Vector2.Min(low, position);
                high = Vector2.Max(high, position + Vector2.One);
            }
        }
        if (!float.IsFinite(low.X) || !float.IsFinite(high.X))
            return false;
        const int chunk = CMUReconGeometry.ChunkSize;
        var origin = new Vector2i((int) MathF.Floor(low.X / chunk) * chunk, (int) MathF.Floor(low.Y / chunk) * chunk);
        var width = (int) MathF.Ceiling(high.X / chunk) * chunk - origin.X;
        var height = (int) MathF.Ceiling(high.Y / chunk) * chunk - origin.Y;
        if (!CMUReconGeometry.ValidDimensions(width, height, maps.Length))
            return false;

        if (!_atlases.TryGetValue(networkUid, out var atlas) || atlas.Origin != origin || atlas.Width != width ||
            atlas.Height != height || atlas.MinDepth != min || !atlas.Maps.SequenceEqual(maps))
        {
            var count = width * height * maps.Length;
            atlas = new Atlas
            {
                Id = ++_atlasId,
                Network = networkUid, Maps = maps, Origin = origin, Width = width, Height = height, MinDepth = min,
                Cells = new byte[count], Appearance = new uint[count], Directions = new byte[count],
                Revisions = new int[count / (chunk * chunk)], Labels = ReadLabels(maps, min),
                Chunks = new CMUReconChunk?[count / (chunk * chunk)],
            };
            var actorLevel = Array.IndexOf(maps, Transform(request.Actor).MapUid);
            var focus = (_transform.GetWorldPosition(request.Actor) - (Vector2) origin) / chunk;
            var nearby = Enumerable.Range(0, atlas.PerLevel).OrderBy(i =>
                Math.Abs(i % atlas.Across - focus.X) + Math.Abs(i / atlas.Across - focus.Y)).ToArray();
            foreach (var level in Enumerable.Range(0, maps.Length).OrderBy(i => Math.Abs(i - Math.Max(0, actorLevel))))
                foreach (var i in nearby)
                    atlas.Pending.Enqueue(level * atlas.PerLevel + i);
            _atlases[networkUid] = atlas;
        }
        atlas.BoundsDirty = false;
        survey = Create(atlas);
        return true;
    }

    private Survey CreateSurvey(Atlas atlas, string faction)
    {
        atlas.RetainUntil = _timing.CurTime + TimeSpan.FromMinutes(5);
        return new Survey
        {
            Atlas = atlas,
            Faction = faction,
            Generation = ++_generation, Sent = new int[atlas.Revisions.Length],
        };
    }

    private CMUReconLabel[] ReadLabels(EntityUid?[] maps, int min)
    {
        var labels = new List<CMUReconLabel>();
        for (var level = 0; level < maps.Length; level++)
        {
            if (!TryComp<AreaGridComponent>(maps[level], out var areas))
                continue;
            IReadOnlyDictionary<Vector2i, string> names = areas.Labels;
            foreach (var (tile, name) in names.Take(128))
                if (!string.IsNullOrWhiteSpace(name))
                    labels.Add(new CMUReconLabel(tile, min + level, name));
        }
        return labels.ToArray();
    }

    private List<CMUReconOrder> Orders(Survey survey)
    {
        var key = (survey.DrawingScope, survey.Faction);
        if (!_orders.TryGetValue(key, out var orders))
            _orders[key] = orders = new List<CMUReconOrder>();

        ImportCanvas(survey, orders);
        return orders;
    }

    private void OnOrder(EntityUid ent, ref CMUReconOrderMessage args)
    {
        if (!_ui.IsUiOpen(ent, UiKey(ent), args.Actor) || !CanUse(ent, args.Actor))
            return;
        if (!CanOrder(ent, args.Actor) || !_surveys.TryGetValue((ent, args.Actor), out var survey) ||
            !IsCurrentSurvey(ent, survey) || survey.Generation != args.Generation || _timing.CurTime < survey.NextOrder)
        {
            Feedback(ent, args.Actor, "cmu-recon-order-invalid");
            return;
        }
        survey.NextOrder = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        var atlas = survey.Atlas;
        var x = (long) args.Tile.X - atlas.Origin.X;
        var y = (long) args.Tile.Y - atlas.Origin.Y;
        var level = (long) args.Depth - atlas.MinDepth;
        if (args.Kind is not CMUReconOrderKind.Rally and not CMUReconOrderKind.Move || level < 0 || level >= atlas.Maps.Length ||
            x < 0 || y < 0 || x >= atlas.Width || y >= atlas.Height ||
            !CMUReconGeometry.CanOrder(ReadCell(atlas.Maps[(int) level], args.Tile)))
        {
            Feedback(ent, args.Actor, "cmu-recon-order-invalid");
            return;
        }
        var orders = Orders(survey);
        if (orders.Count >= 32) orders.RemoveAt(0);
        orders.Add(new CMUReconOrder(++_orderId, args.Tile, args.Depth, args.Kind));
        PublishCanvas(survey);
        Feedback(ent, args.Actor, "cmu-recon-order-sent");
    }

    private void OnClear(EntityUid ent, ref CMUReconClearOrdersMessage args)
    {
        if (_ui.IsUiOpen(ent, UiKey(ent), args.Actor) && CanOrder(ent, args.Actor) &&
            _surveys.TryGetValue((ent, args.Actor), out var survey) && IsCurrentSurvey(ent, survey) &&
            _timing.CurTime >= survey.NextOrder)
        {
            survey.NextOrder = _timing.CurTime + TimeSpan.FromSeconds(0.5);
            Orders(survey).Clear();
            PublishCanvas(survey);
        }
    }

    private void Feedback(EntityUid console, EntityUid actor, string key) =>
        _ui.ServerSendUiMessage(console, UiKey(console), new CMUReconFeedbackMessage(key), actor);

    private void OnTilesChanged(ref TileChangedEvent args)
    {
        foreach (var change in args.Changes) MarkDirty(args.Entity.Owner, change.GridIndices);
    }

    private void OnAnchorChanged(ref AnchorStateChangedEvent args)
    {
        if (args.Transform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var mapGrid))
            MarkDirty(grid, _maps.WorldToTile(grid, mapGrid, _transform.GetWorldPosition(args.Entity)));
    }

    private void OnDoorChanged(Entity<DoorComponent> ent, ref DoorStateChangedEvent args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is { } grid && TryComp<MapGridComponent>(grid, out var mapGrid))
            MarkDirty(grid, _maps.WorldToTile(grid, mapGrid, _transform.GetWorldPosition(ent)));
    }

    private void MarkDirty(EntityUid grid, Vector2i tile)
    {
        foreach (var atlas in _atlases.Values)
        {
            var level = Array.IndexOf(atlas.Maps, (EntityUid?) grid);
            var local = tile - atlas.Origin;
            if (level >= 0 && (local.X < 0 || local.Y < 0 || local.X >= atlas.Width || local.Y >= atlas.Height)) atlas.BoundsDirty = true;
            if (level >= 0 && local.X >= 0 && local.Y >= 0 && local.X < atlas.Width && local.Y < atlas.Height)
                atlas.Dirty.Add(level * atlas.PerLevel + local.Y / CMUReconGeometry.ChunkSize * atlas.Across + local.X / CMUReconGeometry.ChunkSize);
        }
    }

    private void Rebuild(Atlas atlas, int id, Stopwatch timer)
    {
        const int size = CMUReconGeometry.ChunkSize;
        var level = id / atlas.PerLevel;
        var x = id % atlas.PerLevel % atlas.Across * size;
        var y = id % atlas.PerLevel / atlas.Across * size;
        if (atlas.WorkId != id)
        {
            atlas.WorkId = id;
            atlas.WorkRow = 0;
            atlas.WorkChanged = atlas.Revisions[id] == 0;
        }
        var absent = atlas.Maps[level] is not { } map || !TryComp<MapGridComponent>(map, out var grid) ||
            !_maps.HasChunk(map, grid, _maps.GridTileToChunkIndices(grid, atlas.Origin + new Vector2i(x, y)));
        if (absent && atlas.Revisions[id] == 0)
        {
            // Untouched arrays are already zero. Sparse upper floors need no per-cell survey.
            atlas.Loaded++;
            atlas.Revisions[id] = 1;
            atlas.Version++;
            atlas.Chunks[id] = new CMUReconChunk(level, x / size, y / size, [], Empty: true, Revision: 1);
            atlas.WorkId = -1;
            return;
        }
        for (; atlas.WorkRow < size; atlas.WorkRow++)
        {
            if (timer.Elapsed.TotalMilliseconds >= 2) return;
            for (var dx = 0; dx < size; dx++)
            {
                var index = CMUReconGeometry.Index(x + dx, y + atlas.WorkRow, level, atlas.Width, atlas.Height);
                var cell = absent ? default : ReadDetails(atlas.Maps[level], atlas.Origin + new Vector2i(x + dx, y + atlas.WorkRow), atlas);
                atlas.WorkChanged |= atlas.Cells[index] != cell.Material || atlas.Appearance[index] != cell.Appearance || atlas.Directions[index] != cell.Direction;
                var local = atlas.WorkRow * size + dx;
                atlas.WorkCells[local] = cell.Material;
                atlas.WorkAppearance[local] = cell.Appearance;
                atlas.WorkDirections[local] = cell.Direction;
            }
        }
        // Commit a complete chunk; readers never see a half-built revision.
        for (var row = 0; row < size; row++)
        {
            var index = CMUReconGeometry.Index(x, y + row, level, atlas.Width, atlas.Height);
            Array.Copy(atlas.WorkCells, row * size, atlas.Cells, index, size);
            Array.Copy(atlas.WorkAppearance, row * size, atlas.Appearance, index, size);
            Array.Copy(atlas.WorkDirections, row * size, atlas.Directions, index, size);
        }
        if (atlas.Revisions[id] == 0) atlas.Loaded++;
        if (atlas.WorkChanged) { atlas.Revisions[id]++; atlas.Version++; atlas.Chunks[id] = null; }
        atlas.WorkId = -1;
    }

    private static CMUReconChunk CopyChunk(Atlas atlas, int id)
    {
        if (atlas.Chunks[id] is { } cached) return cached;
        const int size = CMUReconGeometry.ChunkSize;
        var level = id / atlas.PerLevel;
        var x = id % atlas.PerLevel % atlas.Across;
        var y = id % atlas.PerLevel / atlas.Across;
        var cells = new byte[size * size];
        var appearance = new uint[size * size];
        var directions = new byte[size * size];
        for (var row = 0; row < size; row++)
        {
            var start = CMUReconGeometry.Index(x * size, y * size + row, level, atlas.Width, atlas.Height);
            Array.Copy(atlas.Cells, start, cells, row * size, size);
            Array.Copy(atlas.Appearance, start, appearance, row * size, size);
            Array.Copy(atlas.Directions, start, directions, row * size, size);
        }
        var empty = !cells.Any(c => c != 0) && !appearance.Any(a => a != 0);
        var result = empty ? new CMUReconChunk(level, x, y, [], Empty: true, Revision: atlas.Revisions[id]) :
            CMUReconChunkEncoding.Pack(new CMUReconChunk(level, x, y, cells, appearance, directions, Revision: atlas.Revisions[id]));
        atlas.Chunks[id] = result;
        return result;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        // One global work budget, shared fairly between open maps, rather than repeated per viewer.
        var active = _surveys.Values.Select(s => s.Atlas).Distinct().ToArray();
        foreach (var atlas in active) atlas.RetainUntil = _timing.CurTime + TimeSpan.FromMinutes(5);
        var timer = Stopwatch.StartNew();
        var workLimit = active.Any(a => a.Pending.Count > 0 || a.Dirty.Count > 0) ? 128 : 8;
        for (var work = 0; active.Length > 0 && work < workLimit && timer.Elapsed.TotalMilliseconds < 2; work++)
        {
            _workCursor %= active.Length;
            var atlas = active[_workCursor++];
            if (atlas.WorkId >= 0)
                Rebuild(atlas, atlas.WorkId, timer);
            else if (atlas.Dirty.Count > 0)
            {
                var id = atlas.Dirty.First();
                atlas.Dirty.Remove(id);
                Rebuild(atlas, id, timer);
            }
            else if (atlas.Pending.TryDequeue(out var id))
                Rebuild(atlas, id, timer);
            else if (_timing.CurTime >= atlas.NextSweep)
            {
                atlas.NextSweep = _timing.CurTime + TimeSpan.FromSeconds(0.1);
                atlas.Sweep %= atlas.Revisions.Length;
                Rebuild(atlas, atlas.Sweep++, timer);
            }
        }
        if (_timing.CurTime < _nextSend) return;
        _nextSend = _timing.CurTime + TimeSpan.FromSeconds(0.1);
        foreach (var (key, survey) in _surveys.ToArray())
        {
            if (!CanUse(key.Console, key.Actor) || !_ui.IsUiOpen(key.Console, UiKey(key.Console), key.Actor) || !IsCurrentSurvey(key.Console, survey))
            {
                _surveys.Remove(key);
                if (!TerminatingOrDeleted(key.Console)) _ui.CloseUi(key.Console, UiKey(key.Console), key.Actor);
                continue;
            }
            var atlas = survey.Atlas;
            var chunks = new List<CMUReconChunk>();
            var bytes = 0;
            // Empty chunks cost only coordinates; keep the same byte budget while filling sparse floors faster.
            for (var scanned = 0; survey.ScannedVersion != atlas.Version && scanned < atlas.Revisions.Length && chunks.Count < 128; scanned++)
            {
                survey.Cursor %= atlas.Revisions.Length;
                var id = survey.ChunkOrder[survey.Cursor++];
                if (survey.Sent[id] == atlas.Revisions[id]) continue;
                var chunk = CopyChunk(atlas, id);
                var cost = CMUReconChunkEncoding.EstimatedBytes(chunk);
                if (bytes + cost > 44 * 1024) { survey.Cursor--; break; }
                chunks.Add(chunk);
                bytes += cost;
                if (survey.Sent[id] == 0) survey.Loaded++;
                survey.Sent[id] = atlas.Revisions[id];
            }
            if (chunks.Count == 0) survey.ScannedVersion = atlas.Version;
            var palette = atlas.Surfaces.Skip(survey.SurfaceCount).ToArray();
            survey.SurfaceCount = atlas.Surfaces.Count;
            var orders = Orders(survey);
            var ordersChanged = orders.Count != survey.SentOrders.Length ||
                orders.Where((order, index) => order.Id != survey.SentOrders[index]).Any();
            if (ordersChanged) survey.SentOrders = orders.Select(order => order.Id).ToArray();
            var canOrder = CanOrder(key.Console, key.Actor);
            if (chunks.Count == 0 && palette.Length == 0 && !ordersChanged && survey.SentCanOrder == canOrder) continue;
            survey.SentCanOrder = canOrder;
            _ui.ServerSendUiMessage(key.Console, UiKey(key.Console), new CMUReconPatchMessage(survey.Generation, chunks.ToArray(),
                ordersChanged ? orders.ToArray() : [], canOrder)
            {
                OrdersChanged = ordersChanged,
                Surfaces = palette, LoadedChunks = survey.Loaded, TotalChunks = atlas.Revisions.Length,
            }, key.Actor);
        }
        SendContacts();
        foreach (var key in _atlases.Keys.Where(k => TerminatingOrDeleted(k) || _atlases[k].RetainUntil <= _timing.CurTime).ToArray())
            _atlases.Remove(key);
        foreach (var key in _orders.Keys.Where(k => TerminatingOrDeleted(k.Network)).ToArray())
        {
            _orders.Remove(key);
            _canvasBaselines.Remove(key);
        }
    }
}
