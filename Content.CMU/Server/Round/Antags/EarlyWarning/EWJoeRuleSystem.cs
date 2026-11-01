using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Threats;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids.Zoom;
using Content.Shared.Actions;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Threats.GrandMachine;
using Content.Shared.Database;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning;

/// <summary>
/// Marker for the Early Warning Joe game rule entity.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeRuleComponent : Component;

/// <summary>
/// Sets up the selected Working Joe as the Early Warning Joe, applies level grants from the level
/// table, and withdraws the antag when the Grand Machine threat itself is selected (D10).
/// </summary>
public sealed partial class EWJoeRuleSystem : GameRuleSystem<EWJoeRuleComponent>
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContentEyeSystem _contentEye = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private AuRoundSystem _auRound = default!;

    public const string AntagId = "EWJoe";

    /// <summary>The Grand Machine threat prototype; the antag stands down when it is the round's threat.</summary>
    public const string GrandMachineThreatId = "GrandMachineThreat";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
        SubscribeLocalEvent<ThreatSelectedEvent>(OnThreatSelected);
        SubscribeLocalEvent<EWJoeComponent, ComponentShutdown>(OnShutdown);
    }

    protected override void Added(EntityUid uid, EWJoeRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        if (!IsGrandMachineSelected())
            return;

        _adminLogger.Add(LogType.AntagSelection, LogImpact.Low,
            $"Early Warning Joe rule ended before selection: the Grand Machine threat is active");
        GameTicker.EndGameRule(uid, gameRule);
    }

    private void OnAntagSelected(Entity<EWJoeRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Def.ID != AntagId)
            return;

        var joe = args.EntityUid;

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
        EnsureComp<CMArmorUserComponent>(joe);

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
        _popup.PopupEntity(Loc.GetString("ew-joe-signal-fades"), uid, uid, PopupType.LargeCaution);

        if (_mind.TryGetMind(uid, out var mindId, out _))
            _role.MindRemoveRole<EWJoeRoleComponent>(mindId);

        RemComp<GrandMachineTouchedComponent>(uid);
        RemComp<EWJoeComponent>(uid);
    }

    private void OnShutdown(Entity<EWJoeComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var ev = new EWJoeRemovedEvent();
        RaiseLocalEvent(ent, ref ev);

        ClearGrants(ent);
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
    /// and the target. Levels never go down: a level at or below the applied one is ignored.
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

        var ev = new EWJoeLevelChangedEvent(oldLevel, level);
        RaiseLocalEvent(ent, ref ev);
    }

    private void GrantLevel(Entity<EWJoeComponent> ent, EWJoeLevelDef def, int level)
    {
        foreach (var actionId in def.Actions)
        {
            if (_actions.AddAction(ent, actionId) is { } action)
                ent.Comp.GrantedActions.Add(action);
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
        var hadZoom = ent.Comp.GrantedComponents.ContainsKey("XenoZoom") && HasComp<XenoZoomComponent>(ent);

        foreach (var action in ent.Comp.GrantedActions)
        {
            if (!Exists(action))
                continue;

            // RemoveAction only detaches the action from the performer; the entity stays in the
            // action container until it is deleted
            _actions.RemoveAction(action);
            QueueDel(action);
        }

        ent.Comp.GrantedActions.Clear();

        foreach (var name in ent.Comp.GrantedComponents.Keys)
        {
            RemComp(ent, _componentFactory.GetRegistration(name).Type);
        }

        ent.Comp.GrantedComponents.Clear();

        // the zoom system leaves max zoom and the PVS scale behind when its component is removed;
        // put the eye back exactly as a mob without XenoZoom has it
        if (hadZoom)
        {
            _contentEye.SetMaxZoom(ent, SharedContentEyeSystem.DefaultZoom);
            _contentEye.ResetZoom(ent);
            _contentEye.UpdatePvsScale(ent);
            if (TryComp(ent, out EyeComponent? eye))
                _contentEye.UpdateEyeOffset((ent.Owner, eye));
        }

        if (ent.Comp.OriginalSlowdown is { } original && TryComp(ent, out RMCInnateSlowdownComponent? slowdown))
        {
            slowdown.Slowdown = original;
            Dirty(ent, slowdown);
            _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        }

        ent.Comp.OriginalSlowdown = null;
        ent.Comp.AppliedLevel = -1;
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
        if (_inventory.TryGetSlotEntity(ent, ent.Comp.CameraSlot, out var camera))
            QueueDel(camera.Value);
    }
}
