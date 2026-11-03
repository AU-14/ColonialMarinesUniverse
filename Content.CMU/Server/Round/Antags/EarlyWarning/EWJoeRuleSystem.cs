using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Threats;
using Content.Server.GameTicking.Rules;
using Content.Server.Antag.Components;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;
using Content.Shared.CCVar;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Slow;
using Content.Shared.Actions;
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
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationSystem _station = default!;

    public const string AntagId = "EWJoe";

    /// <summary>The Distress Signal rule and its ghost-role specifier.</summary>
    public const string DistressAntagId = "EWJoeDistress";
    public const string DistressHostId = "EWJoeDistressHost";

    private static readonly ProtoId<JobPrototype> HostJob = "AU14JobColonyWorkingJoe";

    /// <summary>The admin switch (ewjoemechanics): off, no Joe is made and the ones in the round are suspended.</summary>
    public bool Enabled { get; private set; } = true;

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
    /// Suspends or resumes every Joe in the round: their granted actions grey out or come back, and the ability
    /// systems end what is running (EWJoeMechanicsChangedEvent).
    /// </summary>
    private void OnEnabledChanged(bool enabled)
    {
        Enabled = enabled;
        var ev = new EWJoeMechanicsChangedEvent(enabled);
        RaiseLocalEvent(ref ev);

        // a Distress Signal ghost role nobody has taken yet goes away
        if (!enabled)
        {
            var spawners = EntityQueryEnumerator<GhostRoleAntagSpawnerComponent>();
            while (spawners.MoveNext(out var spawner, out var antag))
            {
                if (antag.Definition == DistressHostId)
                    QueueDel(spawner);
            }
        }

        var query = EntityQueryEnumerator<EWJoeComponent>();
        while (query.MoveNext(out _, out var joe))
        {
            foreach (var action in joe.GrantedActions)
            {
                if (Exists(action))
                    _actions.SetEnabled(action, enabled);
            }
        }
    }

    protected override void Added(EntityUid uid, EWJoeRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        if (!Enabled)
        {
            _adminLogger.Add(LogType.AntagSelection, LogImpact.Low,
                $"Early Warning Joe rule ended before selection: the antag is disabled (ewjoemechanics)");
            GameTicker.EndGameRule(uid, gameRule);
            // antag selection still walks ended rules; without its selection component this one picks nobody
            RemComp<AntagSelectionComponent>(uid);
            return;
        }

        if (!IsGrandMachineSelected())
            return;

        _adminLogger.Add(LogType.AntagSelection, LogImpact.Low,
            $"Early Warning Joe rule ended before selection: the Grand Machine threat is active");
        GameTicker.EndGameRule(uid, gameRule);
        RemComp<AntagSelectionComponent>(uid);
    }

    /// <summary>Where a Distress Signal rule's ghost-role colony Joe appears: on the planet, never on a ship.</summary>
    private void OnSelectLocation(Entity<EWJoeRuleComponent> ent, ref AntagSelectLocationEvent args)
    {
        if (ent.Comp.HostOnPlanet && FindHostSpot(GameTicker.DefaultMap) is { } spot)
            args.Coordinates.Add(spot);
    }

    /// <summary>
    /// A colony Working Joe spawn point on <paramref name="map"/>, or failing that any job or late-join spawn
    /// point there, or failing that a random tile of the map's station; null when there is none of those.
    /// </summary>
    public MapCoordinates? FindHostSpot(MapId map)
    {
        var joes = new List<MapCoordinates>();
        var others = new List<MapCoordinates>();
        var query = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var spawn, out var xform))
        {
            if (xform.MapID != map || spawn.SpawnType == SpawnPointType.Observer)
                continue;

            (spawn.Job == HostJob ? joes : others).Add(_transform.GetMapCoordinates(uid, xform));
        }

        if (joes.Count > 0)
            return _random.Pick(joes);

        if (others.Count > 0)
            return _random.Pick(others);

        // some planets have no player spawn points at all: any floor tile of the planet's station will do
        if (_station.GetStationInMap(map) is { } station
            && TryComp(station, out StationDataComponent? data)
            && TryFindRandomTileOnStation((station, data), out _, out _, out var coords))
        {
            return _transform.ToMapCoordinates(coords);
        }

        return null;
    }

    private void OnAntagSelected(Entity<EWJoeRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Def.ID != AntagId && args.Def.ID != DistressHostId)
            return;

        var joe = args.EntityUid;

        // an admin forcing the antag while it is switched off gets nothing
        if (!Enabled)
        {
            StandDown(joe);
            return;
        }

        // the specifier already added the components and the mind role; undo them if the
        // Grand Machine was selected before this round's antag roll resolved
        if (IsGrandMachineSelected())
        {
            StandDown(joe);
            return;
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

    private bool IsGrandMachineSelected()
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

    /// <summary>Records a granted action; one granted while the antag is switched off arrives greyed out.</summary>
    private void Grant(Entity<EWJoeComponent> ent, EntityUid action)
    {
        ent.Comp.GrantedActions.Add(action);
        if (!Enabled)
            _actions.SetEnabled(action, false);
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
