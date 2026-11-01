using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Gravity;
using Content.Server.Players.JobWhitelist;
using Content.Server.Spawners.EntitySystems;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Atmos;
using Content.Shared.CMU14.Yautja;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost.Components;
using Content.Shared.Gravity;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Content.Shared.StepTrigger.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja;

// cmss13 /datum/job/antag/pred_surv/stranded: one whitelisted yautja stuck on the colony, spawning in an offmap
// hut with a gear rack and a one-way pad down. lives apart from YautjaPredatorRoundSystem, the slot doesn't
// care whether there's a hunt
public sealed partial class YautjaStrandedSystem : EntitySystem
{
    public static readonly ProtoId<JobPrototype> StrandedJob = "CMUYautjaStranded";
    public static readonly ProtoId<JobPrototype> HunterJob = "CMUYautjaHunter";

    private const string HutFloor = "CMUYautjaTileHunterFloorsFloor";
    private const string HutPadFloor = "CMUYautjaTileHunterFloorsHunterGlow";
    private static readonly EntProtoId HutWall = "CMUHunterShipWallTurfClosedWallHuntershipHunterBase";
    private static readonly EntProtoId HutVendor = "CMUYautjaStrandedLoadoutVendor";
    private static readonly EntProtoId HutLocker = "CMUHunterShipMeatLockerFoodBreadMeatXenoSlicex5FoodMeatTomatox5";
    private static readonly EntProtoId HutBed = "CMUHunterShipPlacedCMBedAbedSouthOffset6x7";
    private static readonly EntProtoId HutCloset = "CMUYautjaStrandedHutCloset";
    private static readonly EntProtoId HutTable = "CMUHunterShipPlacedTableReinforcedPrison1tilethickSouth";
    private static readonly EntProtoId HutStew = "CMUHunterShipPlacedFoodSoupStewStewSouthOffset0x6";
    private static readonly EntProtoId HutFork = "CMUHunterShipPlacedForkForkSouthOffset10x3";
    private static readonly EntProtoId HutSpoon = "CMUHunterShipPlacedSpoonSpoonSouthOffset4x5";
    private static readonly EntProtoId HutLantern = "CMUHunterShipPlacedRMCFlashlightLanternYautjaSouth";
    private static readonly EntProtoId HutLight = "CMUHunterShipRedGlowFloorLight";
    private static readonly EntProtoId HutTeleporter = "CMUYautjaStrandedTeleporter";

    // interior is x -3..3, y -2..2, walls ring it one tile out
    private static readonly Vector2i HutSpawnTile = new(0, 0);
    private static readonly Vector2i HutPadTile = new(0, -2);

    private static readonly SoundSpecifier TeleportSound =
        new SoundPathSpecifier("/Audio/CMU14/Yautja/Equipment/pred_bracer.wav");
    private static readonly TimeSpan StepDuplicateWindow = TimeSpan.FromMilliseconds(500);

    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private GravitySystem _gravity = default!;
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private YautjaRankManager _rankManager = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private StationSpawningSystem _stationSpawning = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaTeleportSystem _teleport = default!;

    private int _slotRound = -1;
    private EntityUid? _hutGrid;
    private readonly HashSet<NetUserId> _strandedPlayers = new();
    private readonly HashSet<NetUserId> _hunterPlayers = new();
    private readonly List<EntityUid> _strandedMobs = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextStepAt = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RulePlayerSpawningEvent>(OnRulePlayerSpawning);
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: [typeof(SpawnPointSystem)]);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
        SubscribeLocalEvent<IsRoleAllowedEvent>(OnIsRoleAllowed);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<YautjaStrandedTeleporterComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<YautjaStrandedTeleporterComponent, StepTriggeredOnEvent>(OnStepTriggered);
        SubscribeLocalEvent<YautjaStrandedTeleporterComponent, YautjaStrandedTeleportChosenEvent>(OnTeleportChosen);
    }

    public bool WasStranded(NetUserId player) => _strandedPlayers.Contains(player);

    public IReadOnlyCollection<EntityUid> StrandedMobs => _strandedMobs;

    private void OnRulePlayerSpawning(RulePlayerSpawningEvent ev)
    {
        // runs before round start job assignment, so a readied whitelisted player can roll into the slot.
        // cmss13 always opens it on hunt rounds, here it's the same flat chance whether there's a hunt or not
        if (_random.Prob(Math.Clamp(_config.GetCVar(YautjaStrandedCVars.Chance), 0f, 1f)))
            OpenStrandedSlot();
    }

    public bool OpenStrandedSlot()
    {
        if (_slotRound == _gameTicker.RoundId)
            return false;

        var slots = Math.Clamp(_config.GetCVar(YautjaStrandedCVars.Slots), 0, 10);
        if (slots <= 0 || GetColonyStation() is not { } station)
            return false;

        _stationJobs.SetRoundStartJobSlot(station, StrandedJob, slots);
        _stationJobs.TrySetJobSlot(station, StrandedJob.Id, slots, true);
        _slotRound = _gameTicker.RoundId;
        Log.Info($"Opened {slots} stranded Yautja slot(s) on {ToPrettyString(station)}");
        return true;
    }

    private EntityUid? GetColonyStation()
    {
        // the stranded hunter belongs to the planet, not the hunter ship, so pick the station on the default map
        EntityUid? fallback = null;
        var query = EntityQueryEnumerator<StationJobsComponent, StationDataComponent>();
        while (query.MoveNext(out var station, out _, out var data))
        {
            fallback ??= station;
            foreach (var grid in data.Grids)
            {
                if (Transform(grid).MapID == _gameTicker.DefaultMap)
                    return station;
            }
        }

        return fallback;
    }

    private void OnPlayerSpawning(PlayerSpawningEvent ev)
    {
        if (ev.SpawnResult != null || ev.Job != StrandedJob)
            return;

        if (EnsureHut() is not { } hut)
            return;

        var entitlement = ev.PlayerSession is { } session
            ? _rankManager.ResolveProfileCapabilitiesCached(session.UserId)
            : YautjaProfileCapabilities.Default;
        var active = entitlement.ForStatus(
            ev.HumanoidCharacterProfile?.YautjaProfile.Status ?? YautjaProfileStatus.Normal);

        var coordinates = new EntityCoordinates(hut, HutSpawnTile + new Vector2(0.5f, 0.5f));
        ev.SpawnResult = _stationSpawning.SpawnPlayerMob(
            coordinates,
            ev.Job,
            ev.HumanoidCharacterProfile,
            ev.Station,
            authoritativeYautjaRank: active.Rank,
            authoritativeYautjaCapabilities: entitlement);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == StrandedJob.Id)
        {
            _strandedPlayers.Add(ev.Player.UserId);
            _strandedMobs.Add(ev.Mob);
            _popup.PopupEntity(Loc.GetString("cmu-yautja-stranded-greeting"), ev.Mob, ev.Mob, PopupType.Large);
        }
        else if (ev.JobId == HunterJob.Id)
        {
            _hunterPlayers.Add(ev.Player.UserId);
        }
    }

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent ev)
    {
        // cmss13 has_been_predator counts yautja_stranded, one go at being a yautja per round
        if (_hunterPlayers.Contains(ev.Player.UserId))
            ev.Jobs.Add(StrandedJob);

        if (_strandedPlayers.Contains(ev.Player.UserId))
            ev.Jobs.Add(HunterJob);
    }

    private void OnIsRoleAllowed(ref IsRoleAllowedEvent ev)
    {
        if (ev.Cancelled || ev.Jobs == null)
            return;

        var userId = ev.Player.UserId;
        foreach (var job in ev.Jobs)
        {
            if (job == StrandedJob)
            {
                // the whitelist cvar can be off on a server and only the hunter id is special-cased for it,
                // so check the hunter whitelist directly rather than trusting the stranded job's own flag
                if (_hunterPlayers.Contains(userId) || !_jobWhitelist.IsAllowed(ev.Player, HunterJob))
                {
                    ev.Cancelled = true;
                    return;
                }
            }
            else if (job == HunterJob && _strandedPlayers.Contains(userId))
            {
                ev.Cancelled = true;
                return;
            }
        }
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        var any = false;
        foreach (var mob in _strandedMobs)
        {
            if (Deleted(mob))
                continue;

            if (!any)
            {
                ev.AddLine(Loc.GetString("cmu-yautja-stranded-round-end-header"));
                any = true;
            }

            var status = Loc.GetString(_mobState.IsDead(mob)
                ? "cmu-yautja-stranded-round-end-dead"
                : "cmu-yautja-stranded-round-end-alive");
            ev.AddLine(Loc.GetString("cmu-yautja-stranded-round-end-entry", ("name", Name(mob)), ("status", status)));
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _slotRound = -1;
        _hutGrid = null;
        _strandedPlayers.Clear();
        _hunterPlayers.Clear();
        _strandedMobs.Clear();
        _nextStepAt.Clear();
    }

    // cmss13 lazy-loads predsurv_base the first time it's needed, this builds the same tiny hut in code so
    // there's no extra map file to keep in sync
    public EntityUid? EnsureHut()
    {
        if (_hutGrid is { } existing && !TerminatingOrDeleted(existing))
            return existing;

        var mapUid = _map.CreateMap(out var mapId, runMapInit: true);
        _metaData.SetEntityName(mapUid, Loc.GetString("cmu-yautja-stranded-hut-name"));

        var air = new GasMixture(2500) { Temperature = Atmospherics.T20C };
        air.SetMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard);
        air.SetMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard);
        _atmos.SetMapAtmosphere(mapUid, false, air);

        var grid = _map.CreateGridEntity(mapId);
        var floor = new Tile(_tiles[HutFloor].TileId);
        var padFloor = new Tile(_tiles[HutPadFloor].TileId);
        for (var x = -4; x <= 4; x++)
        {
            for (var y = -3; y <= 3; y++)
            {
                var tile = new Vector2i(x, y);
                _map.SetTile(grid.Owner, grid.Comp, tile, tile == HutPadTile ? padFloor : floor);
            }
        }

        var gravity = EnsureComp<GravityComponent>(grid);
        _gravity.EnableGravity(grid, gravity);
        gravity.Inherent = true;
        Dirty(grid, gravity);

        for (var x = -4; x <= 4; x++)
        {
            for (var y = -3; y <= 3; y++)
            {
                if (Math.Abs(x) == 4 || Math.Abs(y) == 3)
                    SpawnInHut(grid, HutWall, new Vector2i(x, y));
            }
        }

        // same furniture as cmss13 predsurv_base, plus our meat locker. two racks side by side auto-join into
        // the left/right pair, and the vend limits live on the user so the second one doesn't double the kit
        SpawnInHut(grid, HutVendor, new Vector2i(-2, 2));
        SpawnInHut(grid, HutVendor, new Vector2i(-1, 2));
        SpawnInHut(grid, HutCloset, new Vector2i(1, 2));
        SpawnInHut(grid, HutLocker, new Vector2i(2, 2));
        SpawnInHut(grid, HutBed, new Vector2i(3, 2));

        var stewTable = new Vector2i(3, 0);
        SpawnInHut(grid, HutTable, stewTable);
        SpawnInHut(grid, HutStew, stewTable);
        SpawnInHut(grid, HutStew, stewTable);

        var toolTable = new Vector2i(3, -1);
        SpawnInHut(grid, HutTable, toolTable);
        SpawnInHut(grid, HutFork, toolTable);
        SpawnInHut(grid, HutSpoon, toolTable);
        SpawnInHut(grid, HutLantern, toolTable);

        SpawnInHut(grid, HutLight, HutSpawnTile);
        SpawnInHut(grid, HutLight, HutPadTile);
        SpawnInHut(grid, HutTeleporter, HutPadTile);

        _hutGrid = grid.Owner;
        return grid.Owner;
    }

    private void SpawnInHut(Entity<MapGridComponent> grid, EntProtoId prototype, Vector2i tile)
    {
        SpawnAtPosition(prototype, new EntityCoordinates(grid, tile + new Vector2(0.5f, 0.5f)));
    }

    private void OnStepAttempt(Entity<YautjaStrandedTeleporterComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = !HasComp<GhostComponent>(args.Tripper);
    }

    private void OnStepTriggered(Entity<YautjaStrandedTeleporterComponent> ent, ref StepTriggeredOnEvent args)
    {
        var user = args.Tripper;
        if (!CanUsePad(user))
            return;

        var now = _timing.CurTime;
        if (_nextStepAt.TryGetValue(user, out var next) && now < next)
            return;

        _nextStepAt[user] = now + StepDuplicateWindow;

        var options = new List<DialogOption>();
        foreach (var (uid, name) in GetGroundDestinations())
        {
            options.Add(new DialogOption(name,
                new YautjaStrandedTeleportChosenEvent(GetNetEntity(user), GetNetEntity(uid))));
        }

        if (options.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hunt-teleporter-no-destination"), user, user, PopupType.SmallCaution);
            return;
        }

        _dialog.OpenOptions(
            ent.Owner,
            user,
            Loc.GetString("cmu-yautja-stranded-teleporter-title"),
            options,
            Loc.GetString("cmu-yautja-stranded-teleporter-message"));
    }

    private void OnTeleportChosen(Entity<YautjaStrandedTeleporterComponent> ent, ref YautjaStrandedTeleportChosenEvent args)
    {
        if (!TryGetEntity(args.User, out var user) ||
            !TryGetEntity(args.Destination, out var destination) ||
            !CanUsePad(user.Value))
        {
            return;
        }

        // the dialog stays open after they step off, so make sure they're still on the pad
        if (!_transform.InRange(Transform(user.Value).Coordinates, Transform(ent).Coordinates, 1f))
            return;

        if (!TryComp(destination, out YautjaRelayDestinationComponent? relay) ||
            relay.Kind != YautjaRelayDestinationKind.Ground)
        {
            return;
        }

        var coordinates = _transform.GetMapCoordinates(destination.Value);
        if (coordinates.MapId == MapId.Nullspace)
            return;

        _teleport.TeleportTrain(user.Value, coordinates);
        _audio.PlayPvs(TeleportSound, user.Value);
    }

    private bool CanUsePad(EntityUid user)
    {
        // cmss13 checks TRAIT_YAUTJA_TECH
        return !HasComp<GhostComponent>(user) &&
               (HasComp<YautjaComponent>(user) || HasComp<YautjaTechAuthorizedComponent>(user));
    }

    private List<(EntityUid Uid, string Name)> GetGroundDestinations()
    {
        var destinations = new List<(EntityUid Uid, string Name)>();
        var query = EntityQueryEnumerator<YautjaRelayDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var relay, out var xform))
        {
            if (relay.Kind != YautjaRelayDestinationKind.Ground || xform.MapID == MapId.Nullspace)
                continue;

            var name = relay.DisplayName.Trim();
            if (name.Length == 0)
                name = Name(uid);

            destinations.Add((uid, name));
        }

        destinations.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        return destinations;
    }
}
