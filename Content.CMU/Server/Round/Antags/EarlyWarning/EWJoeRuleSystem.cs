using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Spawning;
using Content.Server.CMU14.Threats;
using Content.Server.GameTicking.Rules;
using Content.Server.Antag.Components;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;
using Content.Shared.CCVar;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Pulling;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Slow;
using Content.Shared.Actions;
using Content.Shared.CMU14.Fireman;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Threats.GrandMachine;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Movement.Systems;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// The Early Warning Joe game rule entity.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeRuleComponent : Component
{
    /// <summary>
    /// Distress Signal: the rule spawns its own colony Working Joe (an antag ghost role) on the planet, at a colony
    /// Joe spawn point when the map has one, because the planet's jobs were cleared.
    /// </summary>
    [DataField]
    public bool HostOnPlanet;

    /// <summary>
    /// FLOW-07: an admin made this rule through "Make Early Warning Joe" while the ewjoemechanics switch was off.
    /// </summary>
    [DataField]
    public bool AdminForced;
}

/// <summary>
/// Sets up the selected Working Joe as the Early Warning Joe, applies level grants from the level
/// table, and withdraws the antag when the Grand Machine threat itself is selected (D10).
/// </summary>
public sealed partial class EWJoeRuleSystem : GameRuleSystem<EWJoeRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private CMUSafeSpawnSystem _safeSpawn = default!;
    [Dependency] private FiremanCarrySystem _fireman = default!;

    public const string AntagId = "EWJoe";

    /// <summary>The Distress Signal rule and its ghost-role specifier.</summary>
    public const string DistressAntagId = "EWJoeDistress";
    public const string DistressHostId = "EWJoeDistressHost";

    private static readonly ProtoId<JobPrototype> HostJob = "AU14JobColonyWorkingJoe";

    /// <summary>The department faction whose jobs' spawn points can host a Distress Signal Joe (FLOW-01 tier 2).</summary>
    private const string ColonistFaction = "colonist";

    /// <summary>FLOW-01: tier 3 tries this many random planet tiles before giving up.</summary>
    private const int RandomTileAttempts = 50;

    /// <summary>
    /// The admin switch (ewjoemechanics): off, the rules roll no Joe. Joes already in the round are untouched.
    /// </summary>
    public bool Enabled { get; private set; } = true;

    /// <summary>Open <see cref="AdminOverride"/> scopes.</summary>
    private int _adminOverride;

    /// <summary>
    /// FLOW-07: the switch, or an admin's "Make Early Warning Joe" in progress. The override bypasses only the
    /// switch; the Grand Machine stand-down (D10) still applies.
    /// </summary>
    private bool Active => Enabled || _adminOverride > 0;

    /// <summary>
    /// FLOW-07: while the returned scope is open, a rule added or an antag selected ignores the ewjoemechanics
    /// switch. The admin verb's whole chain (AddGameRule, then TryInitializeAntag) runs synchronously inside it.
    /// </summary>
    public IDisposable AdminOverride()
    {
        _adminOverride++;
        return new AdminOverrideScope(this);
    }

    private sealed class AdminOverrideScope : IDisposable
    {
        private readonly EWJoeRuleSystem _system;
        private bool _disposed;

        public AdminOverrideScope(EWJoeRuleSystem system)
        {
            _system = system;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _system._adminOverride--;
        }
    }

    /// <summary>The Grand Machine threat prototype; the antag stands down when it is the round's threat.</summary>
    public const string GrandMachineThreatId = "GrandMachineThreat";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<EWJoeRuleComponent, AntagSelectLocationEvent>(OnSelectLocation);
        SubscribeLocalEvent<ThreatSelectedEvent>(OnThreatSelected);
        SubscribeLocalEvent<EWJoeComponent, ComponentShutdown>(OnShutdown);

        Subs.CVar(_cfg, CCVars.CMUEWJoeEnabled, OnEnabledChanged, true);
    }

    /// <summary>
    /// Off, a Distress Signal ghost role nobody has taken yet is withdrawn: it is a roll that has not made a Joe.
    /// </summary>
    private void OnEnabledChanged(bool enabled)
    {
        Enabled = enabled;
        if (enabled)
            return;

        var spawners = EntityQueryEnumerator<GhostRoleAntagSpawnerComponent>();
        while (spawners.MoveNext(out var spawner, out var antag))
        {
            if (antag.Definition == DistressHostId)
                QueueDel(spawner);
        }
    }

    protected override void Added(EntityUid uid, EWJoeRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        if (!Active)
        {
            _adminLogger.Add(LogType.AntagSelection, LogImpact.Low,
                $"Early Warning Joe rule ended before selection: the antag is disabled (ewjoemechanics)");
            GameTicker.EndGameRule(uid, gameRule);
            // antag selection still walks ended rules; without its selection component this one picks nobody
            RemComp<AntagSelectionComponent>(uid);
            return;
        }

        // FLOW-07: an admin's "Make Early Warning Joe" while the switch is off
        if (!Enabled)
        {
            component.AdminForced = true;
            _adminLogger.Add(LogType.AntagSelection, LogImpact.Medium,
                $"Early Warning Joe rule {ToPrettyString(uid):rule} added by an admin while the antag is disabled (ewjoemechanics)");
        }

        if (!IsGrandMachineSelected())
            return;

        if (_adminOverride > 0)
        {
            _adminLogger.Add(LogType.AntagSelection, LogImpact.Medium,
                $"Admin-made Early Warning Joe refused: the Grand Machine threat is active");
        }

        _adminLogger.Add(LogType.AntagSelection, LogImpact.Low,
            $"Early Warning Joe rule ended before selection: the Grand Machine threat is active");
        GameTicker.EndGameRule(uid, gameRule);
        RemComp<AntagSelectionComponent>(uid);
    }

    /// <summary>
    /// Where a Distress Signal rule's ghost-role colony Joe appears: on the planet, never on a ship. Every safe
    /// spot is offered, so the engine's random pick has only valid options (FLOW-01); a spot another Joe
    /// spawner already stands on is left out while any other is free, so two spawners spread out.
    /// </summary>
    private void OnSelectLocation(Entity<EWJoeRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (!ent.Comp.HostOnPlanet)
            return;

        var spots = FindHostSpots(GameTicker.DefaultMap);
        var free = spots.Where(spot => !HostSpawnerAt(spot)).ToList();
        args.Coordinates.AddRange(free.Count > 0 ? free : spots);
    }

    /// <summary>Whether a Distress Signal Joe spawner already stands within a tile of <paramref name="spot"/>.</summary>
    private bool HostSpawnerAt(MapCoordinates spot)
    {
        var spawners = EntityQueryEnumerator<GhostRoleAntagSpawnerComponent, TransformComponent>();
        while (spawners.MoveNext(out var uid, out var antag, out var xform))
        {
            if (antag.Definition != DistressHostId || xform.MapID != spot.MapId)
                continue;

            if ((_transform.GetWorldPosition(xform) - spot.Position).Length() < 1f)
                return true;
        }

        return false;
    }

    /// <summary>
    /// One safe host spot on <paramref name="map"/>, picked at random from <see cref="FindHostSpots"/>; null
    /// when there is none.
    /// </summary>
    public MapCoordinates? FindHostSpot(MapId map)
    {
        var spots = FindHostSpots(map);
        return spots.Count > 0 ? (MapCoordinates?) _random.Pick(spots) : null;
    }

    /// <summary>
    /// FLOW-01: every safe colony Working Joe spawn point on <paramref name="map"/>; failing that, every safe
    /// colonist job or late-join spawn point on the map's station grids; failing that, one safe random tile of
    /// the map's station (<see cref="RandomTileAttempts"/> tries). Every candidate passes
    /// the <see cref="CMUSafeSpawnSystem"/> check. Empty when nothing is safe.
    /// </summary>
    public List<MapCoordinates> FindHostSpots(MapId map)
    {
        var joes = new List<MapCoordinates>();
        var colonists = new List<MapCoordinates>();

        StationDataComponent? data = null;
        var stationUid = _station.GetStationInMap(map);
        if (stationUid is { } station)
            TryComp(station, out data);

        var colonistJobs = ColonistJobs();
        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawn, out var xform))
        {
            if (xform.MapID != map || spawn.SpawnType == SpawnPointType.Observer)
                continue;

            List<MapCoordinates> tier;
            if (spawn.Job == HostJob)
                tier = joes;
            else if (data != null
                     && xform.GridUid is { } grid
                     && _station.GetOwningStation(grid) == stationUid
                     && (spawn.SpawnType == SpawnPointType.LateJoin
                         || spawn.Job is { } job && colonistJobs.Contains(job)))
                tier = colonists;
            else
                continue;

            if (_safeSpawn.IsSafeSpawnTile(xform.Coordinates))
                tier.Add(_transform.GetMapCoordinates(uid, xform));
        }

        if (joes.Count > 0)
            return joes;

        if (colonists.Count > 0)
            return colonists;

        // some planets have no usable player spawn points at all: any safe floor tile of the planet's station
        if (data != null && _safeSpawn.TryFindSafeTile(data.Grids, RandomTileAttempts, out var coords))
            return new List<MapCoordinates> { _transform.ToMapCoordinates(coords) };

        return new List<MapCoordinates>();
    }

    /// <summary>Every job of a department whose faction is the colonists'.</summary>
    private HashSet<ProtoId<JobPrototype>> ColonistJobs()
    {
        var jobs = new HashSet<ProtoId<JobPrototype>>();
        foreach (var department in _proto.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (department.Faction == ColonistFaction)
                jobs.UnionWith(department.Roles);
        }

        return jobs;
    }

    private void OnAntagSelected(Entity<EWJoeRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Def.ID != AntagId && args.Def.ID != DistressHostId)
            return;

        var joe = args.EntityUid;

        // a rule that selects while the antag is switched off makes nobody, unless an admin is making this one
        if (!Active)
        {
            StandDown(joe);
            return;
        }

        // the specifier already added the components and the mind role; undo them if the
        // Grand Machine was selected before this round's antag roll resolved
        if (IsGrandMachineSelected())
        {
            if (_adminOverride > 0)
            {
                _adminLogger.Add(LogType.AntagSelection, LogImpact.Medium,
                    $"Admin-made Early Warning Joe {ToPrettyString(joe):joe} stood down: the Grand Machine threat is active");
            }

            StandDown(joe);
            return;
        }

        // FLOW-01 defence in depth: a Distress Signal host whose spawner stood somewhere unsafe moves to a safe
        // spot. A player's own body (a rolled or admin-made Joe) is never moved.
        if (ent.Comp.HostOnPlanet && !_safeSpawn.IsSafeSpawnTile(Transform(joe).Coordinates, joe))
        {
            if (FindHostSpot(GameTicker.DefaultMap) is { } safe)
            {
                _transform.SetMapCoordinates(joe, safe);
                _adminLogger.Add(LogType.AntagSelection, LogImpact.Low,
                    $"Early Warning Joe {ToPrettyString(joe):joe} took over on an unsafe tile and was moved to {safe.ToString()}");
            }
            else
            {
                _adminLogger.Add(LogType.AntagSelection, LogImpact.Medium,
                    $"Early Warning Joe {ToPrettyString(joe):joe} took over on an unsafe tile and no safe spot was found");
            }
        }

        var comp = EnsureComp<EWJoeComponent>(joe);
        EnsureComp<GrandMachineTouchedComponent>(joe);
        // the armor system only runs ModifyDamage for entities that wear armor; the Joe's innate
        // armor comes through CMGetArmorEvent, so it needs the user marker
        if (!HasComp<CMArmorUserComponent>(joe))
        {
            comp.AddedArmorUser = true;
            EnsureComp<CMArmorUserComponent>(joe);
        }

        RemoveCamera((joe, comp));
        ApplyLevel((joe, comp), 0);

        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"Early Warning Joe selected: {ToPrettyString(joe):joe}");
    }

    /// <summary>
    /// Colony Fall picks its threat after roundstart, so the antag may already exist when the Grand
    /// Machine wins the vote. It stands down rather than fight its own master (D10).
    /// </summary>
    private void OnThreatSelected(ref ThreatSelectedEvent args)
    {
        if (!IsGrandMachineSelected())
            return;

        var joes = new List<EntityUid>();
        var joeQuery = EntityQueryEnumerator<EWJoeComponent>();
        while (joeQuery.MoveNext(out var uid, out _))
        {
            joes.Add(uid);
        }

        foreach (var uid in joes)
        {
            StandDown(uid);
        }

        var rules = new List<(EntityUid, GameRuleComponent)>();
        var ruleQuery = EntityQueryEnumerator<EWJoeRuleComponent, GameRuleComponent>();
        while (ruleQuery.MoveNext(out var ruleUid, out _, out var rule))
        {
            rules.Add((ruleUid, rule));
        }

        foreach (var (ruleUid, rule) in rules)
        {
            GameTicker.EndGameRule(ruleUid, rule);
        }
    }

    /// <summary>Strips the role, the markers and (through component shutdown) every grant from one Joe.</summary>
    private void StandDown(EntityUid uid)
    {
        _messages.Notify(uid, Loc.GetString("ew-joe-signal-fades"));
        RemComp<EWJoeComponent>(uid);
    }

    /// <summary>
    /// However the antag is removed (stand-down, or an admin taking the component off), the mind role and
    /// the Grand Machine's mark go with it (CORE-03), after the removal event and the grant cleanup.
    /// </summary>
    private void OnShutdown(Entity<EWJoeComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var ev = new EWJoeRemovedEvent(ent);
        RaiseLocalEvent(ent, ref ev, broadcast: true);

        ClearGrants(ent);

        if (_mind.TryGetMind(ent, out var mindId, out _) && _role.MindHasRole<EWJoeRoleComponent>(mindId))
            _role.MindRemoveRole<EWJoeRoleComponent>(mindId);

        RemComp<GrandMachineTouchedComponent>(ent);
    }

    /// <summary>Whether this round's threat is the Grand Machine, under which the antag stands down (D10).</summary>
    public bool IsGrandMachineSelected()
    {
        return _auRound.SelectedThreat?.ID == GrandMachineThreatId;
    }

    /// <summary>
    /// Advances the Joe one level if the level table has one. Returns false at the last level.
    /// </summary>
    public bool TryAdvanceLevel(Entity<EWJoeComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!_proto.TryIndex(ent.Comp.LevelTable, out var table))
            return false;

        if (ent.Comp.Level + 1 >= table.Levels.Count)
            return false;

        ApplyLevel((ent, ent.Comp), ent.Comp.Level + 1);
        return true;
    }

    /// <summary>
    /// Raises the Joe to <paramref name="level"/>, granting every level between the last applied one
    /// and the target. A level at or below the applied one is ignored; <see cref="LowerLevel"/> goes down.
    /// </summary>
    public void ApplyLevel(Entity<EWJoeComponent?> ent, int level)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (!_proto.TryIndex(ent.Comp.LevelTable, out var table) || table.Levels.Count == 0)
        {
            Log.Error($"Early Warning Joe level table {ent.Comp.LevelTable} is missing or empty");
            return;
        }

        level = Math.Clamp(level, 0, table.Levels.Count - 1);
        if (level <= ent.Comp.AppliedLevel)
            return;

        if (ent.Comp.AppliedLevel < 0 && TryComp(ent, out RMCInnateSlowdownComponent? innate))
            ent.Comp.OriginalSlowdown = innate.Slowdown;

        var oldLevel = ent.Comp.Level;
        for (var i = ent.Comp.AppliedLevel + 1; i <= level; i++)
        {
            GrantLevel((ent, ent.Comp), table.Levels[i], i);
        }

        ent.Comp.AppliedLevel = level;
        ent.Comp.Level = level;
        Dirty(ent, ent.Comp);
        UpdateSlowdown((ent, ent.Comp));
        UpdateGripGrants((ent, ent.Comp));
        RecordProgress((ent, ent.Comp));

        var ev = new EWJoeLevelChangedEvent(ent, oldLevel, level);
        RaiseLocalEvent(ent, ref ev, broadcast: true);
    }

    /// <summary>
    /// Grants a lower level does not take back: the colony's wanted record is what the colony already
    /// knows, not a power. Stand-down (<see cref="ClearGrants"/>) still removes them.
    /// </summary>
    private static readonly HashSet<string> KeptOnLowering = new() { "ColonyBounty" };

    /// <summary>
    /// Playtest 4 (the respawn): drops the Joe to <paramref name="level"/>. Every action and component
    /// granted by a level above it goes, an action one of those levels had taken away comes back when a
    /// kept level grants it (Test of Faith below Judgement's level), and the level-changed event is
    /// raised with the lower level. A later <see cref="ApplyLevel"/> grants the lost levels again.
    /// Returns false when the Joe is already at or below the target.
    /// </summary>
    public bool LowerLevel(Entity<EWJoeComponent?> ent, int level)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!_proto.TryIndex(ent.Comp.LevelTable, out var table) || table.Levels.Count == 0)
            return false;

        level = Math.Max(0, level);
        if (level >= ent.Comp.AppliedLevel)
            return false;

        var oldLevel = ent.Comp.Level;
        var removedActions = new HashSet<string>();
        var restore = new HashSet<string>();
        for (var i = level + 1; i <= ent.Comp.AppliedLevel && i < table.Levels.Count; i++)
        {
            foreach (var actionId in table.Levels[i].Actions)
            {
                removedActions.Add(actionId.Id);
            }

            foreach (var removeId in table.Levels[i].RemoveActions)
            {
                restore.Add(removeId.Id);
            }
        }

        foreach (var action in ent.Comp.GrantedActions.ToArray())
        {
            if (Exists(action) && MetaData(action).EntityPrototype?.ID is { } id && !removedActions.Contains(id))
                continue;

            RemoveGrantedAction((ent, ent.Comp), action);
        }

        foreach (var (name, grantedAt) in ent.Comp.GrantedComponents.ToArray())
        {
            if (grantedAt <= level || KeptOnLowering.Contains(name))
                continue;

            RemComp(ent, _componentFactory.GetRegistration(name).Type);
            ent.Comp.GrantedComponents.Remove(name);
        }

        // an action a lost level replaced comes back if a kept level grants it
        foreach (var restoreId in restore)
        {
            var keptGrants = false;
            for (var i = 0; i <= level; i++)
            {
                if (table.Levels[i].Actions.Any(a => a.Id == restoreId))
                    keptGrants = true;
            }

            if (!keptGrants || ent.Comp.GrantedActions.Any(a => Exists(a) && MetaData(a).EntityPrototype?.ID == restoreId))
                continue;

            if (_actions.AddAction(ent, restoreId) is { } action)
                Grant((ent, ent.Comp), action);
        }

        ent.Comp.AppliedLevel = level;
        ent.Comp.Level = level;
        Dirty(ent, ent.Comp);
        UpdateSlowdown((ent, ent.Comp));
        UpdateGripGrants((ent, ent.Comp));
        RecordProgress((ent, ent.Comp));

        var ev = new EWJoeLevelChangedEvent(ent, oldLevel, level);
        RaiseLocalEvent(ent, ref ev, broadcast: true);
        return true;
    }

    /// <summary>Takes a granted action off the Joe: detach it, delete it, forget it.</summary>
    private void RemoveGrantedAction(Entity<EWJoeComponent> ent, EntityUid action)
    {
        if (Exists(action))
        {
            // RemoveAction only detaches the action from the performer; the entity stays in the action
            // container until it is deleted
            _actions.RemoveAction(action);
            QueueDel(action);
        }

        ent.Comp.GrantedActions.Remove(action);
    }

    /// <summary>
    /// Playtest 4 follow-up (CORE-04): the round-end summary reads the Joe's record from its mind role, so a
    /// gibbed Joe keeps its line and a respawned one is credited with the highest level it reached.
    /// </summary>
    public void RecordProgress(Entity<EWJoeComponent> ent)
    {
        if (!_mind.TryGetMind(ent, out var mindId, out var mind)
            || !_role.MindHasRole<EWJoeRoleComponent>((mindId, mind), out var role))
        {
            return;
        }

        var record = role.Value.Comp2;
        record.Recorded = true;
        record.HighestLevel = Math.Max(record.HighestLevel, ent.Comp.Level);
        record.ThrallsMade = ent.Comp.ThrallsMade;
        record.ConsolesAssimilated = ent.Comp.ConsolesAssimilated;
    }

    private void Grant(Entity<EWJoeComponent> ent, EntityUid action)
    {
        ent.Comp.GrantedActions.Add(action);
    }

    private void GrantLevel(Entity<EWJoeComponent> ent, EWJoeLevelDef def, int level)
    {
        foreach (var actionId in def.Actions)
        {
            if (_actions.AddAction(ent, actionId) is { } action)
                Grant(ent, action);
        }

        // an upgrade takes the action it replaces (playtest 3: Judgement replaces Test of Faith)
        foreach (var removeId in def.RemoveActions)
        {
            foreach (var action in ent.Comp.GrantedActions.ToArray())
            {
                if (Exists(action) && MetaData(action).EntityPrototype?.ID == removeId.Id)
                    RemoveGrantedAction(ent, action);
            }
        }

        foreach (var (name, entry) in def.Components)
        {
            if (ent.Comp.GrantedComponents.ContainsKey(name))
                continue;

            // a component the mob already carries belongs to the mob, not to the level
            if (HasComp(ent, _componentFactory.GetRegistration(name).Type))
                continue;

            EntityManager.AddComponents(ent, new ComponentRegistry { [name] = entry }, removeExisting: false);
            ent.Comp.GrantedComponents[name] = level;
        }
    }

    /// <summary>
    /// Removes every action and component the level table granted and restores the mob's own slowdown.
    /// </summary>
    public void ClearGrants(Entity<EWJoeComponent> ent)
    {
        foreach (var action in ent.Comp.GrantedActions.ToArray())
        {
            RemoveGrantedAction(ent, action);
        }

        foreach (var name in ent.Comp.GrantedComponents.Keys)
        {
            RemComp(ent, _componentFactory.GetRegistration(name).Type);
        }

        ent.Comp.GrantedComponents.Clear();

        if (ent.Comp.OriginalSlowdown is { } original && TryComp(ent, out RMCInnateSlowdownComponent? slowdown))
        {
            slowdown.Slowdown = original;
            Dirty(ent, slowdown);
            _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        }

        ent.Comp.OriginalSlowdown = null;
        ent.Comp.AppliedLevel = -1;

        ClearGripGrants(ent);

        if (ent.Comp.AddedArmorUser)
        {
            // the armor system adds the same marker on equip and never removes it; keep it while armor is worn
            var wearingArmor = false;
            foreach (var item in _inventory.GetHandOrInventoryEntities(ent.Owner))
            {
                if (HasComp<CMArmorComponent>(item))
                {
                    wearingArmor = true;
                    break;
                }
            }

            if (!wearingArmor)
                RemComp<CMArmorUserComponent>(ent);
            ent.Comp.AddedArmorUser = false;
        }
    }

    private void UpdateSlowdown(Entity<EWJoeComponent> ent)
    {
        if (!TryComp(ent, out RMCInnateSlowdownComponent? slowdown))
            return;

        var baseSlowdown = ent.Comp.OriginalSlowdown ?? ent.Comp.BaseSlowdown;
        slowdown.Slowdown = Math.Max(0f, baseSlowdown - ent.Comp.SlowdownShedPerLevel * ent.Comp.Level);
        Dirty(ent, slowdown);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    /// <summary>
    /// N5 (FLOW-02) Machine Grip grants for the Joe's current level: no upgrade delay from level 0, the escape
    /// multiplier by level, and from <see cref="EWJoeComponent.XenoGripLevel"/> the small-caste xeno grab with pull
    /// immunity. Values the mob had of its own are recorded the first time and put back by <see cref="ClearGripGrants"/>,
    /// the way <see cref="EWJoeComponent.OriginalSlowdown"/> is.
    /// </summary>
    private void UpdateGripGrants(Entity<EWJoeComponent> ent)
    {
        if (TryComp(ent, out CanFiremanCarryComponent? carry))
        {
            ent.Comp.OriginalAggressiveGrabDelay ??= carry.AggressiveGrabDelay;
            // review fix: a manual second Ctrl-click must not beat the telegraph (the decided one-click grab keeps
            // the victim's 0.4 s window). The upgrade delay is scaled by the CQC skill, so store the telegraph
            // divided by that multiplier: the manual upgrade unlocks exactly when the telegraph would land.
            var skillMultiplier = _skills.GetSkillDelayMultiplier(ent.Owner, carry.Skill);
            var delay = skillMultiplier > 0 ? ent.Comp.GripTelegraph / skillMultiplier : ent.Comp.GripTelegraph;
            _fireman.SetAggressiveGrabDelay((ent.Owner, carry), delay);
        }

        var multipliers = ent.Comp.GripEscapeMultipliers;
        var multiplier = multipliers.Count == 0 ? 1f : multipliers[Math.Clamp(ent.Comp.Level, 0, multipliers.Count - 1)];
        if (TryComp(ent, out CMUGrabEscapeMultiplierComponent? escape))
        {
            if (!ent.Comp.AddedGripEscapeMultiplier)
                ent.Comp.OriginalGripEscapeMultiplier ??= escape.Multiplier;
        }
        else
        {
            escape = AddComp<CMUGrabEscapeMultiplierComponent>(ent);
            ent.Comp.AddedGripEscapeMultiplier = true;
        }

        escape.Multiplier = multiplier;
        Dirty(ent, escape);

        if (ent.Comp.Level >= ent.Comp.XenoGripLevel)
        {
            if (!HasComp<CMUXenoGrabberComponent>(ent))
            {
                AddComp<CMUXenoGrabberComponent>(ent);
                ent.Comp.AddedXenoGrabber = true;
            }

            // a synthetic body already carries one from its add-list; only a Joe without any gets the copy
            var immunityName = _componentFactory.GetComponentName<ParalyzeImmuneFromComponent>();
            if (!HasComp<ParalyzeImmuneFromComponent>(ent) &&
                _proto.TryIndex(ent.Comp.XenoGripPullImmunity, out var immunity) &&
                immunity.Components.TryGetValue(immunityName, out var entry))
            {
                EntityManager.AddComponents(ent, new ComponentRegistry { [immunityName] = entry }, removeExisting: false);
                ent.Comp.AddedParalyzeImmuneFrom = true;
            }
        }
        else
        {
            RemoveXenoGrip(ent);
        }
    }

    /// <summary>Undoes <see cref="UpdateGripGrants"/>: the mob's own delay and multiplier come back, the grants go.</summary>
    private void ClearGripGrants(Entity<EWJoeComponent> ent)
    {
        if (ent.Comp.OriginalAggressiveGrabDelay is { } delay)
            _fireman.SetAggressiveGrabDelay(ent.Owner, delay);

        ent.Comp.OriginalAggressiveGrabDelay = null;

        if (ent.Comp.AddedGripEscapeMultiplier)
        {
            RemComp<CMUGrabEscapeMultiplierComponent>(ent);
        }
        else if (ent.Comp.OriginalGripEscapeMultiplier is { } original &&
                 TryComp(ent, out CMUGrabEscapeMultiplierComponent? escape))
        {
            escape.Multiplier = original;
            Dirty(ent, escape);
        }

        ent.Comp.AddedGripEscapeMultiplier = false;
        ent.Comp.OriginalGripEscapeMultiplier = null;

        RemoveXenoGrip(ent);
    }

    private void RemoveXenoGrip(Entity<EWJoeComponent> ent)
    {
        if (ent.Comp.AddedXenoGrabber)
            RemComp<CMUXenoGrabberComponent>(ent);

        if (ent.Comp.AddedParalyzeImmuneFrom)
            RemComp<ParalyzeImmuneFromComponent>(ent);

        ent.Comp.AddedXenoGrabber = false;
        ent.Comp.AddedParalyzeImmuneFrom = false;
    }

    /// <summary>The colony Joe wears an overwatch camera; the antag must not be watched through it (D13).</summary>
    private void RemoveCamera(Entity<EWJoeComponent> ent)
    {
        if (!_inventory.TryGetSlotEntity(ent, ent.Comp.CameraSlot, out var camera))
            return;

        // D13 targets the overwatch camera only; a Joe wearing something else there keeps it
        if (MetaData(camera.Value).EntityPrototype?.ID == ent.Comp.CameraPrototype.Id)
            QueueDel(camera.Value);
    }
}
