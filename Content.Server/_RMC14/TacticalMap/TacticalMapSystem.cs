using System.Linq;
using System.Numerics;
using Content.Server._RMC14.Announce;
using Content.Server._RMC14.Marines;
using Content.Server._RMC14.Rules;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Overwatch;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Sensor;
using Content.Shared._RMC14.SupplyDrop;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.HiveLeader;
using Content.Shared.Actions;
using Content.Shared.Atmos.Rotting;
using Content.Shared.AU14.Objectives;
using Content.Shared.Cuffs.Components;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.Traits.Assorted;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._RMC14.TacticalMap;

public sealed class TacticalMapSystem : SharedTacticalMapSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly CMDistressSignalRuleSystem _distressSignal = default!;
    [Dependency] private readonly XenoEvolutionSystem _evolution = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SquadSystem _squad = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly XenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private readonly RMCUnrevivableSystem _unrevivableSystem = default!;

    private EntityQuery<ActiveTacticalMapTrackedComponent> _activeTacticalMapTrackedQuery;
    private EntityQuery<MarineMapTrackedComponent> _marineMapTrackedQuery;
    private EntityQuery<MapGridComponent> _mapGridQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;
    private EntityQuery<RottingComponent> _rottingQuery;
    private EntityQuery<SquadTeamComponent> _squadTeamQuery;
    private EntityQuery<TacticalMapIconComponent> _tacticalMapIconQuery;
    private EntityQuery<TacticalMapComponent> _tacticalMapQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private EntityQuery<XenoMapTrackedComponent> _xenoMapTrackedQuery;
    private EntityQuery<XenoStructureMapTrackedComponent> _xenoStructureMapTrackedQuery;
    private EntityQuery<OpforMapTrackedComponent> _opforMapTrackedQuery;
    private EntityQuery<GovforMapTrackedComponent> _govforMapTrackedQuery;
    private EntityQuery<ClfMapTrackedComponent> _clfMapTrackedQuery;

    private readonly HashSet<Entity<TacticalMapTrackedComponent>> _toInit = new();
    private readonly HashSet<Entity<ActiveTacticalMapTrackedComponent>> _toUpdate = new();
    private readonly List<TacticalMapLine> _emptyLines = new();
    private readonly Dictionary<Vector2i, string> _emptyLabels = new();
    private TimeSpan _announceCooldown;
    private TimeSpan _mapUpdateEvery;
    private TimeSpan _forceMapUpdateEvery;
    private TimeSpan _nextForceMapUpdate = TimeSpan.FromSeconds(30);

    public override void Initialize()
    {
        base.Initialize();

        _activeTacticalMapTrackedQuery = GetEntityQuery<ActiveTacticalMapTrackedComponent>();
        _marineMapTrackedQuery = GetEntityQuery<MarineMapTrackedComponent>();
        _mapGridQuery = GetEntityQuery<MapGridComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _rottingQuery = GetEntityQuery<RottingComponent>();
        _squadTeamQuery = GetEntityQuery<SquadTeamComponent>();
        _tacticalMapIconQuery = GetEntityQuery<TacticalMapIconComponent>();
        _tacticalMapQuery = GetEntityQuery<TacticalMapComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
        _xenoMapTrackedQuery = GetEntityQuery<XenoMapTrackedComponent>();
        _xenoStructureMapTrackedQuery = GetEntityQuery<XenoStructureMapTrackedComponent>();
        _opforMapTrackedQuery = GetEntityQuery<OpforMapTrackedComponent>();
        _govforMapTrackedQuery = GetEntityQuery<GovforMapTrackedComponent>();
        _clfMapTrackedQuery = GetEntityQuery<ClfMapTrackedComponent>();

        SubscribeLocalEvent<XenoOvipositorChangedEvent>(OnOvipositorChanged);

        SubscribeLocalEvent<TacticalMapComponent, MapInitEvent>(OnTacticalMapMapInit);

        SubscribeLocalEvent<TacticalMapUserComponent, MapInitEvent>(OnUserMapInit);
        SubscribeLocalEvent<TacticalMapUserComponent, RoleAddedEvent>(OnUserFactionChanged);
        SubscribeLocalEvent<TacticalMapUserComponent, MindAddedMessage>(OnUserFactionChanged);

        SubscribeLocalEvent<TacticalMapComputerComponent, MapInitEvent>(OnComputerMapInit);
        SubscribeLocalEvent<TacticalMapComputerComponent, BeforeActivatableUIOpenEvent>(OnComputerBeforeUIOpen);

        SubscribeLocalEvent<TacticalMapTrackedComponent, MapInitEvent>(OnTrackedMapInit);
        SubscribeLocalEvent<TacticalMapTrackedComponent, MobStateChangedEvent>(OnTrackedMobStateChanged);
        SubscribeLocalEvent<TacticalMapTrackedComponent, RoleAddedEvent>(OnTrackedChanged);
        SubscribeLocalEvent<TacticalMapTrackedComponent, MindAddedMessage>(OnTrackedChanged);
        SubscribeLocalEvent<TacticalMapTrackedComponent, SquadMemberUpdatedEvent>(OnTrackedChanged);
        SubscribeLocalEvent<TacticalMapTrackedComponent, EntParentChangedMessage>(OnTrackedChanged);

        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, ComponentRemove>(OnActiveRemove);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, EntityTerminatingEvent>(OnActiveRemove);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, MoveEvent>(OnActiveTrackedMove);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, RoleAddedEvent>(OnActiveTrackedRoleAdded);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, MindAddedMessage>(OnActiveTrackedMindAdded);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, SquadMemberUpdatedEvent>(OnActiveSquadMemberUpdated);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, MobStateChangedEvent>(OnActiveMobStateChanged);
        SubscribeLocalEvent<ActiveTacticalMapTrackedComponent, HiveLeaderStatusChangedEvent>(OnHiveLeaderStatusChanged);

        SubscribeLocalEvent<MapBlipIconOverrideComponent, MapInitEvent>(OnMapBlipOverrideMapInit);


        SubscribeLocalEvent<RottingComponent, MapInitEvent>(OnRottingMapInit);
        SubscribeLocalEvent<RottingComponent, ComponentRemove>(OnRottingRemove);

        SubscribeLocalEvent<UnrevivableComponent, MapInitEvent>(OnUnrevivableMapInit);
        SubscribeLocalEvent<UnrevivableComponent, ComponentRemove>(OnUnrevivablRemove);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);

        SubscribeLocalEvent<TacticalMapLiveUpdateOnOviComponent, MapInitEvent>(OnLiveUpdateOnOviMapInit);
        SubscribeLocalEvent<TacticalMapLiveUpdateOnOviComponent, MobStateChangedEvent>(OnLiveUpdateOnOviStateChanged);

        Subs.BuiEvents<TacticalMapUserComponent>(TacticalMapUserUi.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnUserBUIOpened);
                subs.Event<BoundUIClosedEvent>(OnUserBUIClosed);
                subs.Event<TacticalMapUpdateCanvasMsg>(OnUserUpdateCanvasMsg);
                subs.Event<TacticalMapQueenEyeMoveMsg>(OnUserQueenEyeMoveMsg);
            });

        Subs.BuiEvents<TacticalMapComputerComponent>(TacticalMapComputerUi.Key,
            subs =>
            {
                subs.Event<BoundUIOpenedEvent>(OnComputerBUIOpened);
                subs.Event<TacticalMapUpdateCanvasMsg>(OnComputerUpdateCanvasMsg);
                subs.Event<TacticalMapCreateLabelMsg>(OnComputerCreateLabelMsg);
                subs.Event<TacticalMapEditLabelMsg>(OnComputerEditLabelMsg);
                subs.Event<TacticalMapDeleteLabelMsg>(OnComputerDeleteLabelMsg);
                subs.Event<TacticalMapMoveLabelMsg>(OnComputerMoveLabelMsg);
            });

        Subs.CVar(_config,
            RMCCVars.RMCTacticalMapAnnounceCooldownSeconds,
            v => _announceCooldown = TimeSpan.FromSeconds(v),
            true);

        Subs.CVar(_config,
            RMCCVars.RMCTacticalMapUpdateEverySeconds,
            v => _mapUpdateEvery = TimeSpan.FromSeconds(v),
            true);

        Subs.CVar(_config,
            RMCCVars.RMCTacticalMapForceUpdateEverySeconds,
            v => _forceMapUpdateEvery = TimeSpan.FromSeconds(v),
            true);
    }

    private void OnOvipositorChanged(ref XenoOvipositorChangedEvent ev)
    {
        var users = EntityQueryEnumerator<TacticalMapLiveUpdateOnOviComponent, TacticalMapUserComponent>();
        while (users.MoveNext(out var uid, out var onOvi, out var user))
        {
            if (!onOvi.Enabled)
                continue;

            user.LiveUpdate = ev.Attached;
            Dirty(uid, user);
        }
    }

    private void OnTacticalMapMapInit(Entity<TacticalMapComponent> ent, ref MapInitEvent args)
    {
        var tracked = EntityQueryEnumerator<ActiveTacticalMapTrackedComponent, TacticalMapTrackedComponent>();
        while (tracked.MoveNext(out var uid, out var active, out var comp))
        {
            UpdateActiveTracking((uid, comp));
            UpdateTracked((uid, active));
        }

        var users = EntityQueryEnumerator<TacticalMapUserComponent>();
        while (users.MoveNext(out var userId, out var userComp))
        {
            userComp.Map = ent;
            Dirty(userId, userComp);
        }

        var computers = EntityQueryEnumerator<TacticalMapComputerComponent>();
        while (computers.MoveNext(out var computerId, out var computerComp))
        {
            computerComp.Map = ent;
            Dirty(computerId, computerComp);
        }
    }

    private void OnUserMapInit(Entity<TacticalMapUserComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.Action, ent.Comp.ActionId);

        if (TryGetTacticalMap(out var map))
            ent.Comp.Map = map;

        SyncUserFactionFlags(ent);
        SyncTrackedFaction(ent.Owner);
        Dirty(ent);
    }

    private void OnUserFactionChanged<T>(Entity<TacticalMapUserComponent> ent, ref T args)
    {
        if (_timing.ApplyingState || TerminatingOrDeleted(ent))
            return;

        SyncUserFactionFlags(ent);
        SyncTrackedFaction(ent.Owner);
    }

    // Swap MarineMapTracked for the correct faction-specific tracked component based on
    // MarineComponent.Faction. Otherwise opfor/govfor/clf humans land in map.MarineBlips
    // because base.yml ships with MarineMapTracked for every humanoid.
    private void SyncTrackedFaction(EntityUid uid)
    {
        if (!TryComp<MarineComponent>(uid, out var marine))
            return;

        var faction = (marine.Faction ?? string.Empty).ToUpperInvariant();
        var wantMarines = false;
        var wantOpfor = false;
        var wantGovfor = false;
        var wantClf = false;
        if (faction.Contains("CLF"))
            wantClf = true;
        else if (faction.Contains("OPFOR") || faction.Contains("OPF"))
            wantOpfor = true;
        else if (faction.Contains("GOVFOR") || faction.Contains("GOV"))
            wantGovfor = true;
        else
            wantMarines = true;

        if (wantMarines)
            EnsureComp<MarineMapTrackedComponent>(uid);
        else
            RemComp<MarineMapTrackedComponent>(uid);

        if (wantOpfor)
            EnsureComp<OpforMapTrackedComponent>(uid);
        else
            RemComp<OpforMapTrackedComponent>(uid);

        if (wantGovfor)
            EnsureComp<GovforMapTrackedComponent>(uid);
        else
            RemComp<GovforMapTrackedComponent>(uid);

        if (wantClf)
            EnsureComp<ClfMapTrackedComponent>(uid);
        else
            RemComp<ClfMapTrackedComponent>(uid);

        // BreakTracking on old map so the stale blip is cleared, then force re-add.
        if (TryComp<ActiveTacticalMapTrackedComponent>(uid, out var active))
        {
            if (_tacticalMapQuery.TryComp(active.Map, out var oldMap))
            {
                oldMap.MarineBlips.Remove(uid.Id);
                oldMap.OpforBlips.Remove(uid.Id);
                oldMap.GovforBlips.Remove(uid.Id);
                oldMap.ClfBlips.Remove(uid.Id);
                oldMap.MapDirty = true;
            }
            UpdateTracked((uid, active));
        }
    }

    // Sync TacticalMapUser faction flags to the player's actual faction.
    // This is what keeps opfor/govfor/clf segregation working: humans all share the
    // base marine prototype (marines: true), so we translate MarineComponent.Faction
    // into the correct per-faction flag at role-assignment time. Ghosts are forced
    // to see every faction live.
    private void SyncUserFactionFlags(Entity<TacticalMapUserComponent> ent)
    {
        if (HasComp<GhostComponent>(ent))
        {
            var changed = !ent.Comp.Marines || !ent.Comp.Xenos || !ent.Comp.Opfor
                || !ent.Comp.Govfor || !ent.Comp.Clf || !ent.Comp.LiveUpdate;
            ent.Comp.Marines = true;
            ent.Comp.Xenos = true;
            ent.Comp.Opfor = true;
            ent.Comp.Govfor = true;
            ent.Comp.Clf = true;
            ent.Comp.LiveUpdate = true;
            if (changed)
                Dirty(ent);
            return;
        }

        if (HasComp<XenoComponent>(ent))
            return;

        if (!TryComp<MarineComponent>(ent, out var marine))
            return;

        var faction = (marine.Faction ?? string.Empty).ToUpperInvariant();
        var marines = false;
        var opfor = false;
        var govfor = false;
        var clf = false;
        if (faction.Contains("CLF"))
            clf = true;
        else if (faction.Contains("OPFOR") || faction.Contains("OPF"))
            opfor = true;
        else if (faction.Contains("GOVFOR") || faction.Contains("GOV"))
            govfor = true;
        else
            marines = true;

        if (ent.Comp.Marines == marines && ent.Comp.Opfor == opfor
            && ent.Comp.Govfor == govfor && ent.Comp.Clf == clf)
            return;

        ent.Comp.Marines = marines;
        ent.Comp.Opfor = opfor;
        ent.Comp.Govfor = govfor;
        ent.Comp.Clf = clf;
        Dirty(ent);
    }

    private void OnComputerMapInit(Entity<TacticalMapComputerComponent> ent, ref MapInitEvent args)
    {
        if (TryGetTacticalMap(out var map))
            ent.Comp.Map = map;

        Dirty(ent);
    }

    private void OnComputerBeforeUIOpen(Entity<TacticalMapComputerComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateMapData((ent, ent));
    }

    private void OnTrackedMapInit(Entity<TacticalMapTrackedComponent> ent, ref MapInitEvent args)
    {
        _toInit.Add(ent);
        if (TryComp(ent, out ActiveTacticalMapTrackedComponent? active))
            _toUpdate.Add((ent, active));
    }

    private void OnTrackedMobStateChanged(Entity<TacticalMapTrackedComponent> ent, ref MobStateChangedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        UpdateActiveTracking(ent, args.NewMobState);
    }

    private void OnTrackedChanged<T>(Entity<TacticalMapTrackedComponent> ent, ref T args)
    {
        if (_timing.ApplyingState || TerminatingOrDeleted(ent))
            return;

        UpdateActiveTracking(ent);
    }

    private void OnActiveRemove<T>(Entity<ActiveTacticalMapTrackedComponent> ent, ref T args)
    {
        BreakTracking(ent);
    }

    private void OnActiveTrackedMove(Entity<ActiveTacticalMapTrackedComponent> ent, ref MoveEvent args)
    {
        _toUpdate.Add(ent);
    }

    private void OnActiveTrackedRoleAdded(Entity<ActiveTacticalMapTrackedComponent> ent, ref RoleAddedEvent args)
    {
        UpdateIcon(ent);
        UpdateTracked(ent);
    }

    private void OnActiveTrackedMindAdded(Entity<ActiveTacticalMapTrackedComponent> ent, ref MindAddedMessage args)
    {
        UpdateIcon(ent);
        UpdateTracked(ent);
    }

    private void OnActiveSquadMemberUpdated(Entity<ActiveTacticalMapTrackedComponent> ent, ref SquadMemberUpdatedEvent args)
    {
        if (_squadTeamQuery.TryComp(args.Squad, out var squad))
        {
            if (squad.MinimapBackground != null)
            {
                ent.Comp.Background = squad.MinimapBackground;
                ent.Comp.Color = Color.White;
            }
            else
                ent.Comp.Color = squad.Color;
        }
        else if (ent.Comp.Background != null)
            UpdateIcon(ent);
    }

    private void OnActiveMobStateChanged(Entity<ActiveTacticalMapTrackedComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateIcon(ent);
        UpdateTracked(ent);
    }

    private void OnHiveLeaderStatusChanged(Entity<ActiveTacticalMapTrackedComponent> ent, ref HiveLeaderStatusChangedEvent args)
    {
        UpdateIcon(ent);
        UpdateHiveLeader(ent, args.BecameLeader);
        UpdateTracked(ent);
    }

    private void OnMapBlipOverrideMapInit(Entity<MapBlipIconOverrideComponent> ent, ref MapInitEvent args)
    {
        if (_activeTacticalMapTrackedQuery.TryComp(ent, out var active))
        {
            UpdateIcon((ent.Owner, active));
            UpdateTracked((ent.Owner, active));
        }
    }

    private void OnRottingMapInit(Entity<RottingComponent> ent, ref MapInitEvent args)
    {
        if (_activeTacticalMapTrackedQuery.TryComp(ent, out var active))
            UpdateTracked((ent, active));
    }

    private void OnRottingRemove(Entity<RottingComponent> ent, ref ComponentRemove args)
    {
        if (_activeTacticalMapTrackedQuery.TryComp(ent, out var active))
            UpdateTracked((ent, active));
    }

    private void OnUnrevivableMapInit(Entity<UnrevivableComponent> ent, ref MapInitEvent args)
    {
        if (_activeTacticalMapTrackedQuery.TryComp(ent, out var active))
            UpdateTracked((ent, active));
    }

    private void OnUnrevivablRemove(Entity<UnrevivableComponent> ent, ref ComponentRemove args)
    {
        if (_activeTacticalMapTrackedQuery.TryComp(ent, out var active))
            UpdateTracked((ent, active));
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        _nextForceMapUpdate = TimeSpan.FromSeconds(30);
    }

    private void OnLiveUpdateOnOviMapInit(Entity<TacticalMapLiveUpdateOnOviComponent> ent, ref MapInitEvent args)
    {
        if (!ent.Comp.Enabled ||
            !TryComp(ent, out TacticalMapUserComponent? user))
        {
            return;
        }

        user.LiveUpdate = _evolution.HasOvipositor();
        Dirty(ent, user);
    }

    private void OnLiveUpdateOnOviStateChanged(Entity<TacticalMapLiveUpdateOnOviComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            RemCompDeferred<TacticalMapLiveUpdateOnOviComponent>(ent);
    }

    private void OnUserBUIOpened(Entity<TacticalMapUserComponent> ent, ref BoundUIOpenedEvent args)
    {
        EnsureComp<ActiveTacticalMapUserComponent>(ent);
        UpdateTacticalMapState(ent);
    }

    private void OnComputerBUIOpened(Entity<TacticalMapComputerComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateTacticalMapComputerState(ent);
    }

    private void UpdateTacticalMapState(Entity<TacticalMapUserComponent> ent)
    {
        var mapName = _distressSignal.SelectedPlanetMapName ?? string.Empty;

        // Get squad objectives if player is in a squad
        Dictionary<SquadObjectiveType, string>? squadObjectives = null;
        if (TryComp<SquadMemberComponent>(ent, out var squadMember) &&
            _squad.TryGetMemberSquad((ent, squadMember), out var squad))
        {
            squadObjectives = _squad.GetSquadObjectives((squad.Owner, squad.Comp));
        }

        var state = new TacticalMapBuiState(mapName, squadObjectives);
        _ui.SetUiState(ent.Owner, TacticalMapUserUi.Key, state);
    }

    private void UpdateTacticalMapComputerState(Entity<TacticalMapComputerComponent> computer)
    {
        var mapName = _distressSignal.SelectedPlanetMapName ?? string.Empty;
        var state = new TacticalMapBuiState(mapName);
        _ui.SetUiState(computer.Owner, TacticalMapComputerUi.Key, state);
    }

    private void OnUserBUIClosed(Entity<TacticalMapUserComponent> ent, ref BoundUIClosedEvent args)
    {
        RemCompDeferred<ActiveTacticalMapUserComponent>(ent);
    }

    private void OnUserUpdateCanvasMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapUpdateCanvasMsg args)
    {
        var user = args.Actor;
        if (!ent.Comp.CanDraw)
            return;

        var lines = args.Lines;
        if (lines.Count > LineLimit)
            lines = lines[..LineLimit];

        var labels = args.Labels;

        var time = _timing.CurTime;
        if (time < ent.Comp.NextAnnounceAt)
            return;

        var nextAnnounce = time + _announceCooldown;
        ent.Comp.LastAnnounceAt = time;
        ent.Comp.NextAnnounceAt = nextAnnounce;
        Dirty(ent);

        if (ent.Comp.Marines)
            UpdateCanvas(lines, labels, true, false, false, false, false, user, ent.Comp.Sound);

        if (ent.Comp.Xenos)
            UpdateCanvas(lines, labels, false, true, false, false, false, user, ent.Comp.Sound);

        if (ent.Comp.Opfor)
            UpdateCanvas(lines, labels, false, false, true, false, false, user, ent.Comp.Sound);

        if (ent.Comp.Govfor)
            UpdateCanvas(lines, labels, false, false, false, true, false, user, ent.Comp.Sound);

        if (ent.Comp.Clf)
            UpdateCanvas(lines, labels, false, false, false, false, true, user, ent.Comp.Sound);
    }

    private void OnComputerUpdateCanvasMsg(Entity<TacticalMapComputerComponent> ent, ref TacticalMapUpdateCanvasMsg args)
    {
        var user = args.Actor;
        if (!_skills.HasSkill(user, ent.Comp.Skill, ent.Comp.SkillLevel))
            return;

        var lines = args.Lines;
        if (lines.Count > LineLimit)
            lines = lines[..LineLimit];

        var labels = args.Labels;

        var time = _timing.CurTime;
        if (time < ent.Comp.NextAnnounceAt)
            return;

        var nextAnnounce = time + _announceCooldown;
        ent.Comp.LastAnnounceAt = time;
        ent.Comp.NextAnnounceAt = nextAnnounce;
        Dirty(ent);

        // Mirror the old behavior: update other computers' announce timestamps as well
        var computers = EntityQueryEnumerator<TacticalMapComputerComponent>();
        while (computers.MoveNext(out var uid, out var computer))
        {
            computer.LastAnnounceAt = time;
            computer.NextAnnounceAt = nextAnnounce;
            Dirty(uid, computer);
        }

        var (wantsMarines, wantsXenos, wantsOpfor, wantsGovfor, wantsClf) = ResolveComputerWriteFaction(ent, user);
        if (!wantsMarines && !wantsXenos && !wantsOpfor && !wantsGovfor && !wantsClf)
            return;

        UpdateCanvas(lines, labels, wantsMarines, wantsXenos, wantsOpfor, wantsGovfor, wantsClf, user);
    }

    // Resolves which faction's canvas a drawing/label from a TacticalMapComputer should be written to.
    // If the computer has no faction yet, the first user with an identifiable faction locks theirs
    // in permanently; users with no faction do not assign anything and their action is dropped.
    private (bool marines, bool xenos, bool opfor, bool govfor, bool clf) ResolveComputerWriteFaction(
        Entity<TacticalMapComputerComponent> computer, EntityUid user)
    {
        var faction = computer.Comp.Faction?.ToUpperInvariant();
        if (!string.IsNullOrEmpty(faction))
        {
            return (faction == "MARINES" || faction == "UNMC",
                    faction == "XENONIDS" || faction == "XENONID",
                    faction == "OPFOR",
                    faction == "GOVFOR",
                    faction == "CLF");
        }

        string? assign = null;
        var result = (marines: false, xenos: false, opfor: false, govfor: false, clf: false);

        if (HasComp<XenoComponent>(user))
        {
            assign = "XENONIDS";
            result = (false, true, false, false, false);
        }
        else if (TryComp<MarineComponent>(user, out var marine))
        {
            var userFaction = (marine.Faction ?? string.Empty).ToUpperInvariant();
            if (userFaction.Contains("CLF"))
            {
                assign = "CLF";
                result = (false, false, false, false, true);
            }
            else if (userFaction.Contains("OPFOR") || userFaction.Contains("OPF"))
            {
                assign = "OPFOR";
                result = (false, false, true, false, false);
            }
            else if (userFaction.Contains("GOVFOR") || userFaction.Contains("GOV"))
            {
                assign = "GOVFOR";
                result = (false, false, false, true, false);
            }
            else
            {
                assign = "MARINES";
                result = (true, false, false, false, false);
            }
        }

        if (assign != null)
        {
            computer.Comp.Faction = assign;
            Dirty(computer);
        }

        return result;
    }

    private void OnUserCreateLabelMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapCreateLabelMsg args)
    {
        var user = args.Actor;
        if (!ent.Comp.CanDraw)
            return;

        var time = _timing.CurTime;
        if (time < ent.Comp.NextAnnounceAt)
            return;

        if (ent.Comp.Marines)
            UpdateIndividualLabel(args.Position, args.Text, true, false, false, false, false, user, LabelOperation.Create);

        if (ent.Comp.Xenos)
            UpdateIndividualLabel(args.Position, args.Text, false, true, false, false, false, user, LabelOperation.Create);

        if (ent.Comp.Opfor)
            UpdateIndividualLabel(args.Position, args.Text, false, false, true, false, false, user, LabelOperation.Create);

        if (ent.Comp.Govfor)
            UpdateIndividualLabel(args.Position, args.Text, false, false, false, true, false, user, LabelOperation.Create);

        if (ent.Comp.Clf)
            UpdateIndividualLabel(args.Position, args.Text, false, false, false, false, true, user, LabelOperation.Create);
    }

    private void OnUserEditLabelMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapEditLabelMsg args)
    {
        var user = args.Actor;
        if (!ent.Comp.CanDraw)
            return;

        var time = _timing.CurTime;
        if (time < ent.Comp.NextAnnounceAt)
            return;

        if (ent.Comp.Marines)
            UpdateIndividualLabel(args.Position, args.NewText, true, false, false, false, false, user, LabelOperation.Edit);

        if (ent.Comp.Xenos)
            UpdateIndividualLabel(args.Position, args.NewText, false, true, false, false, false, user, LabelOperation.Edit);

        if (ent.Comp.Opfor)
            UpdateIndividualLabel(args.Position, args.NewText, false, false, true, false, false, user, LabelOperation.Edit);

        if (ent.Comp.Govfor)
            UpdateIndividualLabel(args.Position, args.NewText, false, false, false, true, false, user, LabelOperation.Edit);

        if (ent.Comp.Clf)
            UpdateIndividualLabel(args.Position, args.NewText, false, false, false, false, true, user, LabelOperation.Edit);
    }

    private void OnUserDeleteLabelMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapDeleteLabelMsg args)
    {
        var user = args.Actor;
        if (!ent.Comp.CanDraw)
            return;

        var time = _timing.CurTime;
        if (time < ent.Comp.NextAnnounceAt)
            return;

        if (ent.Comp.Marines)
            UpdateIndividualLabel(args.Position, string.Empty, true, false, false, false, false, user, LabelOperation.Delete);

        if (ent.Comp.Xenos)
            UpdateIndividualLabel(args.Position, string.Empty, false, true, false, false, false, user, LabelOperation.Delete);

        if (ent.Comp.Opfor)
            UpdateIndividualLabel(args.Position, string.Empty, false, false, true, false, false, user, LabelOperation.Delete);

        if (ent.Comp.Govfor)
            UpdateIndividualLabel(args.Position, string.Empty, false, false, false, true, false, user, LabelOperation.Delete);

        if (ent.Comp.Clf)
            UpdateIndividualLabel(args.Position, string.Empty, false, false, false, false, true, user, LabelOperation.Delete);
    }

    private void OnUserMoveLabelMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapMoveLabelMsg args)
    {
        var user = args.Actor;
        if (!ent.Comp.CanDraw)
            return;

        var time = _timing.CurTime;
        if (time < ent.Comp.NextAnnounceAt)
            return;

        if (ent.Comp.Marines)
            UpdateMoveLabel(args.OldPosition, args.NewPosition, true, false, false, false, false, user);

        if (ent.Comp.Xenos)
            UpdateMoveLabel(args.OldPosition, args.NewPosition, false, true, false, false, false, user);

        if (ent.Comp.Opfor)
            UpdateMoveLabel(args.OldPosition, args.NewPosition, false, false, true, false, false, user);

        if (ent.Comp.Govfor)
            UpdateMoveLabel(args.OldPosition, args.NewPosition, false, false, false, true, false, user);

        if (ent.Comp.Clf)
            UpdateMoveLabel(args.OldPosition, args.NewPosition, false, false, false, false, true, user);
    }

    private void OnComputerCreateLabelMsg(Entity<TacticalMapComputerComponent> ent, ref TacticalMapCreateLabelMsg args)
    {
        var user = args.Actor;
        if (!_skills.HasSkill(user, ent.Comp.Skill, ent.Comp.SkillLevel))
            return;

        var time = _timing.CurTime;
        if (time < ent.Comp.NextAnnounceAt)
            return;

        var (wantsMarines, wantsXenos, wantsOpfor, wantsGovfor, wantsClf) = ResolveComputerWriteFaction(ent, user);
        if (!wantsMarines && !wantsXenos && !wantsOpfor && !wantsGovfor && !wantsClf)
            return;

        UpdateIndividualLabel(args.Position, args.Text, wantsMarines, wantsXenos, wantsOpfor, wantsGovfor, wantsClf, user, LabelOperation.Create);
    }

    private void OnComputerEditLabelMsg(Entity<TacticalMapComputerComponent> ent, ref TacticalMapEditLabelMsg args)
    {
        var user = args.Actor;
        if (!_skills.HasSkill(user, ent.Comp.Skill, ent.Comp.SkillLevel))
            return;

        var time = _timing.CurTime;
        if (time < ent.Comp.NextAnnounceAt)
            return;

        var (wantsMarines, wantsXenos, wantsOpfor, wantsGovfor, wantsClf) = ResolveComputerWriteFaction(ent, user);
        if (!wantsMarines && !wantsXenos && !wantsOpfor && !wantsGovfor && !wantsClf)
            return;

        UpdateIndividualLabel(args.Position, args.NewText, wantsMarines, wantsXenos, wantsOpfor, wantsGovfor, wantsClf, user, LabelOperation.Edit);
    }

    private void OnComputerDeleteLabelMsg(Entity<TacticalMapComputerComponent> ent, ref TacticalMapDeleteLabelMsg args)
    {
        var user = args.Actor;
        if (!_skills.HasSkill(user, ent.Comp.Skill, ent.Comp.SkillLevel))
            return;

        var time = _timing.CurTime;
        if (time < ent.Comp.NextAnnounceAt)
            return;

        var (wantsMarines, wantsXenos, wantsOpfor, wantsGovfor, wantsClf) = ResolveComputerWriteFaction(ent, user);
        if (!wantsMarines && !wantsXenos && !wantsOpfor && !wantsGovfor && !wantsClf)
            return;

        UpdateIndividualLabel(args.Position, string.Empty, wantsMarines, wantsXenos, wantsOpfor, wantsGovfor, wantsClf, user, LabelOperation.Delete);
    }

    private void OnComputerMoveLabelMsg(Entity<TacticalMapComputerComponent> ent, ref TacticalMapMoveLabelMsg args)
    {
        var user = args.Actor;
        if (!_skills.HasSkill(user, ent.Comp.Skill, ent.Comp.SkillLevel))
            return;

        var time = _timing.CurTime;
        if (time < ent.Comp.NextAnnounceAt)
            return;

        var (wantsMarines, wantsXenos, wantsOpfor, wantsGovfor, wantsClf) = ResolveComputerWriteFaction(ent, user);
        if (!wantsMarines && !wantsXenos && !wantsOpfor && !wantsGovfor && !wantsClf)
            return;

        UpdateMoveLabel(args.OldPosition, args.NewPosition, wantsMarines, wantsXenos, wantsOpfor, wantsGovfor, wantsClf, user);
    }

    private enum LabelOperation
    {
        Create,
        Edit,
        Delete
    }

    private void OnUserQueenEyeMoveMsg(Entity<TacticalMapUserComponent> ent, ref TacticalMapQueenEyeMoveMsg args)
    {
        var user = args.Actor;
        HandleQueenEyeMove(user, args.Position);
    }

    private void HandleQueenEyeMove(EntityUid user, Vector2i position)
    {
        if (!TryComp<QueenEyeActionComponent>(user, out var queenEyeComp) ||
            queenEyeComp.Eye == null)
            return;

        var eye = queenEyeComp.Eye.Value;

        if (!TryGetTacticalMap(out var map) ||
            !TryComp<MapGridComponent>(map.Owner, out var grid))
            return;

        var queenTransform = Transform(user);
        var eyeTransform = Transform(eye);
        var mapTransform = Transform(map.Owner);

        if (queenTransform.MapID != mapTransform.MapID)
            return;

        var tileCoords = new Vector2(position.X, position.Y);
        var worldPos = _transform.ToMapCoordinates(new EntityCoordinates(map.Owner, tileCoords * grid.TileSize));

        _transform.SetWorldPosition(eye, worldPos.Position);
    }

    public new void OpenComputerMap(Entity<TacticalMapComputerComponent?> computer, EntityUid user)
    {
        if (!Resolve(computer, ref computer.Comp, false))
            return;

        _ui.TryOpenUi(computer.Owner, TacticalMapComputerUi.Key, user);
        UpdateMapData((computer, computer.Comp));
        UpdateTacticalMapComputerState((computer.Owner, computer.Comp));
    }

    private void UpdateIndividualLabel(Vector2i position, string text, bool marine, bool xeno, bool opfor, bool govfor, bool clf, EntityUid user, LabelOperation operation)
    {
        var maps = EntityQueryEnumerator<TacticalMapComponent>();
        while (maps.MoveNext(out var mapId, out var map))
        {
            map.MapDirty = true;

            if (marine)
            {
                switch (operation)
                {
                    case LabelOperation.Create:
                    case LabelOperation.Edit:
                        if (string.IsNullOrWhiteSpace(text))
                            map.MarineLabels.Remove(position);
                        else
                            map.MarineLabels[position] = text;
                        break;
                    case LabelOperation.Delete:
                        map.MarineLabels.Remove(position);
                        break;
                }

                _adminLog.Add(LogType.RMCTacticalMapUpdated,
                    $"{ToPrettyString(user)} {operation.ToString().ToLower()}d a marine tactical map label at {position} for {ToPrettyString(mapId)}");
            }

            if (xeno)
            {
                switch (operation)
                {
                    case LabelOperation.Create:
                    case LabelOperation.Edit:
                        if (string.IsNullOrWhiteSpace(text))
                            map.XenoLabels.Remove(position);
                        else
                            map.XenoLabels[position] = text;
                        break;
                    case LabelOperation.Delete:
                        map.XenoLabels.Remove(position);
                        break;
                }

                _adminLog.Add(LogType.RMCTacticalMapUpdated,
                    $"{ToPrettyString(user)} {operation.ToString().ToLower()}d a xenonid tactical map label at {position} for {ToPrettyString(mapId)}");
            }

            if (opfor)
            {
                switch (operation)
                {
                    case LabelOperation.Create:
                    case LabelOperation.Edit:
                        if (string.IsNullOrWhiteSpace(text))
                            map.OpforLabels.Remove(position);
                        else
                            map.OpforLabels[position] = text;
                        break;
                    case LabelOperation.Delete:
                        map.OpforLabels.Remove(position);
                        break;
                }

                _adminLog.Add(LogType.RMCTacticalMapUpdated,
                    $"{ToPrettyString(user)} {operation.ToString().ToLower()}d an opfor tactical map label at {position} for {ToPrettyString(mapId)}");
            }

            if (govfor)
            {
                switch (operation)
                {
                    case LabelOperation.Create:
                    case LabelOperation.Edit:
                        if (string.IsNullOrWhiteSpace(text))
                            map.GovforLabels.Remove(position);
                        else
                            map.GovforLabels[position] = text;
                        break;
                    case LabelOperation.Delete:
                        map.GovforLabels.Remove(position);
                        break;
                }

                _adminLog.Add(LogType.RMCTacticalMapUpdated,
                    $"{ToPrettyString(user)} {operation.ToString().ToLower()}d a govfor tactical map label at {position} for {ToPrettyString(mapId)}");
            }

            if (clf)
            {
                switch (operation)
                {
                    case LabelOperation.Create:
                    case LabelOperation.Edit:
                        if (string.IsNullOrWhiteSpace(text))
                            map.ClfLabels.Remove(position);
                        else
                            map.ClfLabels[position] = text;
                        break;
                    case LabelOperation.Delete:
                        map.ClfLabels.Remove(position);
                        break;
                }

                _adminLog.Add(LogType.RMCTacticalMapUpdated,
                    $"{ToPrettyString(user)} {operation.ToString().ToLower()}d a clf tactical map label at {position} for {ToPrettyString(mapId)}");
            }
        }
    }

    private void UpdateMoveLabel(Vector2i oldPosition, Vector2i newPosition, bool marine, bool xeno, bool opfor, bool govfor, bool clf, EntityUid user)
    {
        var maps = EntityQueryEnumerator<TacticalMapComponent>();
        while (maps.MoveNext(out var mapId, out var map))
        {
            map.MapDirty = true;

            if (marine && map.MarineLabels.TryGetValue(oldPosition, out var marineText))
            {
                map.MarineLabels.Remove(oldPosition);
                map.MarineLabels[newPosition] = marineText;

                _adminLog.Add(LogType.RMCTacticalMapUpdated,
                    $"{ToPrettyString(user)} moved a marine tactical map label from {oldPosition} to {newPosition} for {ToPrettyString(mapId)}");
            }

            if (xeno && map.XenoLabels.TryGetValue(oldPosition, out var xenoText))
            {
                map.XenoLabels.Remove(oldPosition);
                map.XenoLabels[newPosition] = xenoText;

                _adminLog.Add(LogType.RMCTacticalMapUpdated,
                    $"{ToPrettyString(user)} moved a xenonid tactical map label from {oldPosition} to {newPosition} for {ToPrettyString(mapId)}");
            }

            if (opfor && map.OpforLabels.TryGetValue(oldPosition, out var opforText))
            {
                map.OpforLabels.Remove(oldPosition);
                map.OpforLabels[newPosition] = opforText;

                _adminLog.Add(LogType.RMCTacticalMapUpdated,
                    $"{ToPrettyString(user)} moved an opfor tactical map label from {oldPosition} to {newPosition} for {ToPrettyString(mapId)}");
            }

            if (govfor && map.GovforLabels.TryGetValue(oldPosition, out var govforText))
            {
                map.GovforLabels.Remove(oldPosition);
                map.GovforLabels[newPosition] = govforText;

                _adminLog.Add(LogType.RMCTacticalMapUpdated,
                    $"{ToPrettyString(user)} moved a govfor tactical map label from {oldPosition} to {newPosition} for {ToPrettyString(mapId)}");
            }

            if (clf && map.ClfLabels.TryGetValue(oldPosition, out var clfText))
            {
                map.ClfLabels.Remove(oldPosition);
                map.ClfLabels[newPosition] = clfText;

                _adminLog.Add(LogType.RMCTacticalMapUpdated,
                    $"{ToPrettyString(user)} moved a clf tactical map label from {oldPosition} to {newPosition} for {ToPrettyString(mapId)}");
            }
        }
    }

    private bool TryGetGridCoordinates(Vector2i tacticalPosition, out EntityCoordinates coordinates)
    {
        coordinates = default;

        var maps = EntityQueryEnumerator<TacticalMapComponent>();
        while (maps.MoveNext(out var mapId, out var map))
        {
            if (!_transformQuery.TryComp(mapId, out var mapTransform) ||
                !_mapGridQuery.TryComp(mapId, out var mapGrid))
            {
                continue;
            }

            coordinates = new EntityCoordinates(mapId, new Vector2(tacticalPosition.X, tacticalPosition.Y));
            return true;
        }

        return false;
    }

    private bool TeamHasActiveSensors(string faction)
    {
        if (string.IsNullOrWhiteSpace(faction))
            return false;

        var comps = EntityQueryEnumerator<SensorTowerComponent>();
        while (comps.MoveNext(out _, out var comp))
        {
            if (comp.State != SensorTowerState.On)
                continue;

            if (string.Equals(comp.Faction, faction, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private void UpdateActiveTracking(Entity<TacticalMapTrackedComponent> tracked, MobState mobState)
    {
        if (!tracked.Comp.TrackDead && mobState == MobState.Dead)
        {
            RemCompDeferred<ActiveTacticalMapTrackedComponent>(tracked);
            return;
        }

        if (LifeStage(tracked) < EntityLifeStage.MapInitialized)
            return;

        var active = EnsureComp<ActiveTacticalMapTrackedComponent>(tracked);
        var activeEnt = new Entity<ActiveTacticalMapTrackedComponent>(tracked, active);
        UpdateIcon(activeEnt);
        UpdateRotting(activeEnt);
        UpdateColor(activeEnt);
    }

    private void UpdateActiveTracking(Entity<TacticalMapTrackedComponent> tracked)
    {
        var state = _mobStateQuery.CompOrNull(tracked)?.CurrentState ?? MobState.Alive;
        UpdateActiveTracking(tracked, state);
    }

    private void BreakTracking(Entity<ActiveTacticalMapTrackedComponent> tracked)
    {
        if (!_tacticalMapQuery.TryComp(tracked.Comp.Map, out var tacticalMap))
            return;

        tacticalMap.MarineBlips.Remove(tracked.Owner.Id);
        tacticalMap.XenoBlips.Remove(tracked.Owner.Id);
        tacticalMap.XenoStructureBlips.Remove(tracked.Owner.Id);
        tacticalMap.OpforBlips.Remove(tracked.Owner.Id);
        tacticalMap.GovforBlips.Remove(tracked.Owner.Id);
        tacticalMap.ClfBlips.Remove(tracked.Owner.Id);
        tacticalMap.MapDirty = true;
        tracked.Comp.Map = null;
    }

    private void UpdateIcon(Entity<ActiveTacticalMapTrackedComponent> tracked)
    {
        SpriteSpecifier.Rsi? mapBlipOverride = null;
        if (TryComp<MapBlipIconOverrideComponent>(tracked, out var mapBlipOverrideComp) && mapBlipOverrideComp.Icon != null)
            mapBlipOverride = mapBlipOverrideComp.Icon;

        if (_tacticalMapIconQuery.TryComp(tracked, out var iconComp))
        {
            tracked.Comp.Icon = mapBlipOverride ?? iconComp.Icon;
            tracked.Comp.Background = iconComp.Background;
            UpdateSquadBackground(tracked);
            return;
        }

        tracked.Comp.Icon = mapBlipOverride;
        UpdateSquadBackground(tracked);
    }

    private void UpdateSquadBackground(Entity<ActiveTacticalMapTrackedComponent> tracked)
    {
        //Don't get job background if we have a squad, and if we do and it doesn't have it's own background
        //Still don't apply it
        if (!_squad.TryGetMemberSquad(tracked.Owner, out var squad))
            return;

        tracked.Comp.Background = squad.Comp.MinimapBackground;
        if (TryComp(tracked, out TacticalMapIconComponent? icon))
        {
            icon.Background = tracked.Comp.Background;
            Dirty(tracked, icon);
        }
    }

    private void UpdateRotting(Entity<ActiveTacticalMapTrackedComponent> tracked)
    {
        tracked.Comp.Undefibbable = _rottingQuery.HasComp(tracked);
    }

    private void UpdateColor(Entity<ActiveTacticalMapTrackedComponent> tracked)
    {
        if (_squad.TryGetMemberSquad(tracked.Owner, out var squad))
        {
            if (squad.Comp.MinimapBackground == null)
                tracked.Comp.Color = squad.Comp.Color;
            else
            {
                tracked.Comp.Background = squad.Comp.MinimapBackground;
                tracked.Comp.Color = Color.White;
            }
        }
        else
        {
            tracked.Comp.Color = Color.White;
        }

        if (TryComp(tracked, out TacticalMapIconComponent? icon))
        {
            icon.Background = tracked.Comp.Background;
            Dirty(tracked, icon);
        }
    }

    private void UpdateHiveLeader(Entity<ActiveTacticalMapTrackedComponent> tracked, bool isLeader)
    {
        tracked.Comp.HiveLeader = isLeader;
    }

    private void UpdateTracked(Entity<ActiveTacticalMapTrackedComponent> ent)
    {
        // If the tracked entity is restrained/cuffed, hide from tacmap UNLESS their faction has active sensors.
        if (TryComp<CuffableComponent>(ent, out var cuffable) && cuffable.CuffedHandCount > 0)
        {
            // Determine the entity's faction so we can check sensors
            string? entFaction = null;
            if (_opforMapTrackedQuery.HasComp(ent))
                entFaction = "OPFOR";
            else if (_govforMapTrackedQuery.HasComp(ent))
                entFaction = "GOVFOR";
            else if (_clfMapTrackedQuery.HasComp(ent))
                entFaction = "CLF";
            else if (_marineMapTrackedQuery.HasComp(ent))
                entFaction = "MARINES";
            else if (TryComp(ent, out Content.Shared._RMC14.Marines.MarineComponent? marineCuffComp) &&
                     !string.IsNullOrWhiteSpace(marineCuffComp.Faction))
                entFaction = marineCuffComp.Faction.ToUpperInvariant();

            // If the faction has active sensors, don't hide — let the normal tracking flow handle it
            if (entFaction != null && TeamHasActiveSensors(entFaction))
            {
                // Fall through to normal blip placement below
            }
            else
            {
                if (!_tacticalMapQuery.TryComp(ent.Comp.Map, out var curMap))
                {
                    // Even if we don't know map, clear from all tactical maps to be safe
                    var maps = EntityQueryEnumerator<TacticalMapComponent>();
                    while (maps.MoveNext(out var mapId, out var map))
                    {
                        map.MarineBlips.Remove(ent.Owner.Id);
                        map.XenoBlips.Remove(ent.Owner.Id);
                        map.XenoStructureBlips.Remove(ent.Owner.Id);
                        map.OpforBlips.Remove(ent.Owner.Id);
                        map.GovforBlips.Remove(ent.Owner.Id);
                        map.ClfBlips.Remove(ent.Owner.Id);
                        map.MapDirty = true;
                    }
                }
                else
                {
                    curMap.MarineBlips.Remove(ent.Owner.Id);
                    curMap.XenoBlips.Remove(ent.Owner.Id);
                    curMap.XenoStructureBlips.Remove(ent.Owner.Id);
                    curMap.OpforBlips.Remove(ent.Owner.Id);
                    curMap.GovforBlips.Remove(ent.Owner.Id);
                    curMap.ClfBlips.Remove(ent.Owner.Id);
                    curMap.MapDirty = true;
                }

                return;
            }
        }

        if (!_transformQuery.TryComp(ent.Owner, out var xform) ||
            xform.GridUid is not { } gridId ||
            !_mapGridQuery.TryComp(gridId, out var gridComp) ||
            !_tacticalMapQuery.TryComp(gridId, out var tacticalMap) ||
            !_transform.TryGetGridTilePosition((ent.Owner, xform), out var indices, gridComp))
        {
            BreakTracking(ent);
            return;
        }

        if (ent.Comp.Icon == null)
            UpdateIcon(ent);

        if (ent.Comp.Icon is not { } icon)
        {
            BreakTracking(ent);
            return;
        }

        if (ent.Comp.Map != xform.GridUid)
        {
            BreakTracking(ent);
            ent.Comp.Map = xform.GridUid;
        }

        var status = TacticalMapBlipStatus.Alive;
        if (_mobState.IsDead(ent))
        {
            var stage = _unrevivableSystem.GetUnrevivableStage(ent.Owner, 5);
            if (_rottingQuery.HasComp(ent) || _unrevivableSystem.IsUnrevivable(ent))
                status = TacticalMapBlipStatus.Undefibabble;
            else if (stage <= 1)
                status = TacticalMapBlipStatus.Defibabble;
            else if (stage == 2)
                status = TacticalMapBlipStatus.Defibabble2;
            else if (stage == 3)
                status = TacticalMapBlipStatus.Defibabble3;
            else if (stage == 4)
                status = TacticalMapBlipStatus.Defibabble4;
        }

        var blip = new TacticalMapBlip(indices, icon, ent.Comp.Color, status, ent.Comp.Background, ent.Comp.HiveLeader);

        // Determine which faction map this entity should appear on.
        // Priority:
        // 1. If entity has an explicit tracked component (Opfor/Govfor/Clf/Xeno/XenoStructure/Marine)
        // 2. If entity has a MarineComponent with a Faction string, use that faction (OPFOR/GOVFOR/CLF/etc.)
        // 3. Fallback to Marine map if nothing else.

        bool placed = false;

        // Xeno categories keep existing behavior
        if (_xenoMapTrackedQuery.HasComp(ent))
        {
            tacticalMap.XenoBlips[ent.Owner.Id] = blip;
            tacticalMap.MapDirty = true;
            placed = true;
        }

        if (!placed && _xenoStructureMapTrackedQuery.HasComp(ent))
        {
            tacticalMap.XenoStructureBlips[ent.Owner.Id] = blip;
            tacticalMap.MapDirty = true;
            placed = true;
        }

        // Explicit opfor/govfor/clf components
        if (!placed && _opforMapTrackedQuery.HasComp(ent))
        {
            tacticalMap.OpforBlips[ent.Owner.Id] = blip;
            tacticalMap.MapDirty = true;
            placed = true;
        }

        if (!placed && _govforMapTrackedQuery.HasComp(ent))
        {
            tacticalMap.GovforBlips[ent.Owner.Id] = blip;
            tacticalMap.MapDirty = true;
            placed = true;
        }

        if (!placed && _clfMapTrackedQuery.HasComp(ent))
        {
            tacticalMap.ClfBlips[ent.Owner.Id] = blip;
            tacticalMap.MapDirty = true;
            placed = true;
        }

        // Fallback: infer from MarineComponent.Faction
        if (!placed && TryComp(ent, out Content.Shared._RMC14.Marines.MarineComponent? marineComp) && !string.IsNullOrWhiteSpace(marineComp.Faction))
        {
            var faction = marineComp.Faction.ToUpperInvariant();
            if (faction == "OPFOR")
            {
                tacticalMap.OpforBlips[ent.Owner.Id] = blip;
                placed = true;
            }
            else if (faction == "GOVFOR")
            {
                tacticalMap.GovforBlips[ent.Owner.Id] = blip;
                placed = true;
            }
            else if (faction == "CLF")
            {
                tacticalMap.ClfBlips[ent.Owner.Id] = blip;
                placed = true;
            }
            else
            {
                // treat any other as marine
                tacticalMap.MarineBlips[ent.Owner.Id] = blip;
                placed = true;
            }

            if (placed)
                tacticalMap.MapDirty = true;
        }

        // If still not placed, default to marine map if they are marine-tracked or as ultimate fallback.
        if (!placed)
        {
            if (_marineMapTrackedQuery.HasComp(ent))
            {
                tacticalMap.MarineBlips[ent.Owner.Id] = blip;
                tacticalMap.MapDirty = true;
            }
            else
            {
                // ultimate fallback to marine map
                tacticalMap.MarineBlips[ent.Owner.Id] = blip;
                tacticalMap.MapDirty = true;
            }
        }
    }

    public override void UpdateUserData(Entity<TacticalMapUserComponent> user, TacticalMapComponent map)
    {
        var lines = EnsureComp<TacticalMapLinesComponent>(user);
        var labels = EnsureComp<TacticalMapLabelsComponent>(user);
        var playerId = user.Owner.Id;

        // Collect infra ids (comms, sensors, tunnels) so we can exclude them from enemy sprite replacement
        var infraIds = new HashSet<int>();
        var comms = EntityQueryEnumerator<CommunicationsTowerComponent>();
        while (comms.MoveNext(out var cid, out _))
            infraIds.Add(cid.Id);

        var sensors = EntityQueryEnumerator<SensorTowerComponent>();
        while (sensors.MoveNext(out var sid, out _))
            infraIds.Add(sid.Id);

        var tunnels = EntityQueryEnumerator<XenoTunnelComponent>();
        while (tunnels.MoveNext(out var tid, out _))
            infraIds.Add(tid.Id);

        // Local helper to apply enemy sprite to any blip in the target dictionary that belongs to a different human faction
        void ApplyEnemySpritesToUser(string userFaction, Dictionary<int, TacticalMapBlip> blips, int playerId)
        {
            if (string.IsNullOrWhiteSpace(userFaction))
                return;

            var factionHasSensors = TeamHasActiveSensors(userFaction);
            var enemyRsi = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "enemy_blip");
            var keys = blips.Keys.ToList();
            foreach (var id in keys)
            {
                // Never remove or replace the player's own blip
                if (id == playerId)
                    continue;
                if (infraIds.Contains(id))
                    continue;

                // never change xeno entities/structures here
                if (map.XenoBlips.ContainsKey(id) || map.XenoStructureBlips.ContainsKey(id))
                    continue;

                bool isFriendly = (map.MarineBlips.ContainsKey(id) && userFaction == "MARINES")
                    || (map.OpforBlips.ContainsKey(id) && userFaction == "OPFOR")
                    || (map.GovforBlips.ContainsKey(id) && userFaction == "GOVFOR")
                    || (map.ClfBlips.ContainsKey(id) && userFaction == "CLF");

                if (isFriendly)
                    continue;

                if (!factionHasSensors)
                {
                    // Without sensors, do not show other human factions
                    blips.Remove(id);
                    continue;
                }

                // With sensors, show other humans as enemy_blip
                var orig = blips[id];
                blips[id] = new TacticalMapBlip(orig.Indices, enemyRsi, orig.Color, orig.Status, orig.Background, false);
            }
        }

        if (user.Comp.Xenos)
        {
            user.Comp.XenoBlips = user.Comp.LiveUpdate ? map.XenoBlips : map.LastUpdateXenoBlips.ToDictionary();
            user.Comp.XenoStructureBlips = user.Comp.LiveUpdate ? map.XenoStructureBlips : map.LastUpdateXenoStructureBlips.ToDictionary();

            if (!user.Comp.LiveUpdate)
            {
                if (map.XenoBlips.TryGetValue(playerId, out var playerXenoBlip))
                    user.Comp.XenoBlips[playerId] = playerXenoBlip;
                else if (map.XenoStructureBlips.TryGetValue(playerId, out var playerXenoStructureBlip)) // Shouldn't happen but just in case
                    user.Comp.XenoStructureBlips[playerId] = playerXenoStructureBlip;
            }

            var alwaysVisible = EntityQueryEnumerator<TacticalMapAlwaysVisibleComponent>();
            while (alwaysVisible.MoveNext(out var uid, out var comp))
            {
                if (!comp.VisibleToXenos)
                    continue;

                if (user.Comp.XenoBlips.ContainsKey(uid.Id) || user.Comp.XenoStructureBlips.ContainsKey(uid.Id))
                    continue;

                var blip = FindBlipInMap(uid.Id, map);
                if (blip == null)
                    continue;

                if (comp.VisibleAsXenoStructure)
                    user.Comp.XenoStructureBlips[uid.Id] = blip.Value;
                else
                    user.Comp.XenoBlips[uid.Id] = blip.Value;
            }

            lines.XenoLines = map.XenoLines;
            labels.XenoLabels = map.XenoLabels;
        }
        else
        {
            lines.XenoLines = _emptyLines;
            labels.XenoLabels = _emptyLabels;
        }

        // Marines: mark enemies with enemy sprite
        if (user.Comp.Marines)
        {
            user.Comp.MarineBlips = user.Comp.LiveUpdate ? map.MarineBlips : map.LastUpdateMarineBlips.ToDictionary();

            if (!user.Comp.LiveUpdate && map.MarineBlips.TryGetValue(playerId, out var playerMarineBlip))
                user.Comp.MarineBlips[playerId] = playerMarineBlip;

            var alwaysVisible = EntityQueryEnumerator<TacticalMapAlwaysVisibleComponent>();
            while (alwaysVisible.MoveNext(out var uid, out var comp))
            {
                if (!comp.VisibleToMarines)
                    continue;

                if (user.Comp.MarineBlips.ContainsKey(uid.Id))
                    continue;

                var blip = FindBlipInMap(uid.Id, map);
                if (blip != null)
                    user.Comp.MarineBlips[uid.Id] = blip.Value;
            }

            lines.MarineLines = map.MarineLines;
            labels.MarineLabels = map.MarineLabels;
            // Ensure non-friendly humans appear as enemy_blip for this user when their team has active sensors
            ApplyEnemySpritesToUser("MARINES", user.Comp.MarineBlips, playerId);
        }
        else
        {
            lines.MarineLines = _emptyLines;
            labels.MarineLabels = _emptyLabels;
        }
        Dirty(user);
        if (user.Comp.Opfor)
        {
            user.Comp.OpforBlips = user.Comp.LiveUpdate ? map.OpforBlips : map.LastUpdateOpforBlips.ToDictionary();

            if (!user.Comp.LiveUpdate && map.OpforBlips.TryGetValue(playerId, out var playerOpforBlip))
                user.Comp.OpforBlips[playerId] = playerOpforBlip;

            var alwaysVisible = EntityQueryEnumerator<TacticalMapAlwaysVisibleComponent>();
            while (alwaysVisible.MoveNext(out var uid, out var comp))
            {
                if (!comp.VisibleToOpfor)
                    continue;

                if (user.Comp.OpforBlips.ContainsKey(uid.Id))
                    continue;

                var blip = FindBlipInMap(uid.Id, map);
                if (blip != null)
                    user.Comp.OpforBlips[uid.Id] = blip.Value;
            }

            // Include communication towers, sensor towers and tunnels explicitly
            comms = EntityQueryEnumerator<CommunicationsTowerComponent>();
            while (comms.MoveNext(out var towerId, out var comm))
            {
                var blip = FindBlipInMap(towerId.Id, map);
                if (blip != null && !user.Comp.OpforBlips.ContainsKey(towerId.Id))
                {
                    var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "comms_tower");
                    var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                    user.Comp.OpforBlips[towerId.Id] = full;
                }
            }

            sensors = EntityQueryEnumerator<SensorTowerComponent>();
            while (sensors.MoveNext(out var sensorId, out var sensor))
            {
                var blip = FindBlipInMap(sensorId.Id, map);
                if (blip != null && !user.Comp.OpforBlips.ContainsKey(sensorId.Id))
                {
                    var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "sensor_tower");
                    var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                    user.Comp.OpforBlips[sensorId.Id] = full;
                }
            }

            // Only include tunnels for OPFOR users if OPFOR currently has active sensors
            if (TeamHasActiveSensors("OPFOR"))
            {
                tunnels = EntityQueryEnumerator<XenoTunnelComponent>();
                while (tunnels.MoveNext(out var tunId, out var tun))
                {
                    var blip = FindBlipInMap(tunId.Id, map);
                    if (blip != null && !user.Comp.OpforBlips.ContainsKey(tunId.Id))
                    {
                        var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "tunnel");
                        var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                        user.Comp.OpforBlips[tunId.Id] = full;
                    }
                }
            }

            lines.OpforLines = map.OpforLines;
            labels.OpforLabels = map.OpforLabels;
            ApplyEnemySpritesToUser("OPFOR", user.Comp.OpforBlips, playerId);
        }

        if (user.Comp.Govfor)
        {
            user.Comp.GovforBlips = user.Comp.LiveUpdate ? map.GovforBlips : map.LastUpdateGovforBlips.ToDictionary();

            if (!user.Comp.LiveUpdate && map.GovforBlips.TryGetValue(playerId, out var playerGovforBlip))
                user.Comp.GovforBlips[playerId] = playerGovforBlip;

            var alwaysVisible = EntityQueryEnumerator<TacticalMapAlwaysVisibleComponent>();
            while (alwaysVisible.MoveNext(out var uid, out var comp))
            {
                if (!comp.VisibleToGovfor)
                    continue;

                if (user.Comp.GovforBlips.ContainsKey(uid.Id))
                    continue;

                var blip = FindBlipInMap(uid.Id, map);
                if (blip != null)
                    user.Comp.GovforBlips[uid.Id] = blip.Value;
            }

            // Include comms/sensors/tunnels for govfor
            var comms2 = EntityQueryEnumerator<CommunicationsTowerComponent>();
            while (comms2.MoveNext(out var towerId, out var comm))
             {
                 var blip = FindBlipInMap(towerId.Id, map);
                 if (blip != null && !user.Comp.GovforBlips.ContainsKey(towerId.Id))
                 {
                     var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "comms_tower");
                     var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                     user.Comp.GovforBlips[towerId.Id] = full;
                 }
             }

             var sensors2 = EntityQueryEnumerator<SensorTowerComponent>();
             while (sensors2.MoveNext(out var sensorId, out var sensor))
             {
                 var blip = FindBlipInMap(sensorId.Id, map);
                 if (blip != null && !user.Comp.GovforBlips.ContainsKey(sensorId.Id))
                 {
                     var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "sensor_tower");
                     var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                     user.Comp.GovforBlips[sensorId.Id] = full;
                 }
             }

             // Only include tunnels for GOVFOR users if GOVFOR currently has active sensors
             if (TeamHasActiveSensors("GOVFOR"))
             {
                 var tunnels2 = EntityQueryEnumerator<XenoTunnelComponent>();
                 while (tunnels2.MoveNext(out var tunId, out var tun))
                 {
                     var blip = FindBlipInMap(tunId.Id, map);
                     if (blip != null && !user.Comp.GovforBlips.ContainsKey(tunId.Id))
                     {
                         var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "tunnel");
                         var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                         user.Comp.GovforBlips[tunId.Id] = full;
                     }
                 }
             }

            lines.GovforLines = map.GovforLines;
            labels.GovforLabels = map.GovforLabels;
            ApplyEnemySpritesToUser("GOVFOR", user.Comp.GovforBlips, playerId);
        }

        if (user.Comp.Clf)
        {
            user.Comp.ClfBlips = user.Comp.LiveUpdate ? map.ClfBlips : map.LastUpdateClfBlips.ToDictionary();

            if (!user.Comp.LiveUpdate && map.ClfBlips.TryGetValue(playerId, out var playerClfBlip))
                user.Comp.ClfBlips[playerId] = playerClfBlip;

            var alwaysVisible = EntityQueryEnumerator<TacticalMapAlwaysVisibleComponent>();
            while (alwaysVisible.MoveNext(out var uid, out var comp))
            {
                if (!comp.VisibleToClf)
                    continue;

                if (user.Comp.ClfBlips.ContainsKey(uid.Id))
                    continue;

                var blip = FindBlipInMap(uid.Id, map);
                if (blip != null)
                    user.Comp.ClfBlips[uid.Id] = blip.Value;
            }

            // Include comms/sensors/tunnels for clf
            var comms3 = EntityQueryEnumerator<CommunicationsTowerComponent>();
            while (comms3.MoveNext(out var towerId, out var comm))
            {
                var blip = FindBlipInMap(towerId.Id, map);
                if (blip != null && !user.Comp.ClfBlips.ContainsKey(towerId.Id))
                {
                    var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "comms_tower");
                    var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                    user.Comp.ClfBlips[towerId.Id] = full;
                }
            }

            var sensors3 = EntityQueryEnumerator<SensorTowerComponent>();
            while (sensors3.MoveNext(out var sensorId, out var sensor))
            {
                var blip = FindBlipInMap(sensorId.Id, map);
                if (blip != null && !user.Comp.ClfBlips.ContainsKey(sensorId.Id))
                {
                    var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "sensor_tower");
                    var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                    user.Comp.ClfBlips[sensorId.Id] = full;
                }
            }

            // Only include tunnels for CLF users if CLF currently has active sensors
            if (TeamHasActiveSensors("CLF"))
            {
                var tunnels3 = EntityQueryEnumerator<XenoTunnelComponent>();
                while (tunnels3.MoveNext(out var tunId, out var tun))
                {
                    var blip = FindBlipInMap(tunId.Id, map);
                    if (blip != null && !user.Comp.ClfBlips.ContainsKey(tunId.Id))
                    {
                        var image = blip.Value.Image ?? new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "tunnel");
                        var full = new TacticalMapBlip(blip.Value.Indices, image, blip.Value.Color, blip.Value.Status, blip.Value.Background, blip.Value.HiveLeader);
                        user.Comp.ClfBlips[tunId.Id] = full;
                    }
                }
            }

            lines.ClfLines = map.ClfLines;
            labels.ClfLabels = map.ClfLabels;
            ApplyEnemySpritesToUser("CLF", user.Comp.ClfBlips, playerId);
        }

        Dirty(user, lines);
        Dirty(user, labels);
    }

    private TacticalMapBlip? FindBlipInMap(int entityId, TacticalMapComponent map)
    {
        if (map.MarineBlips.TryGetValue(entityId, out var marineBlip))
            return marineBlip;
        if (map.XenoStructureBlips.TryGetValue(entityId, out var structureBlip))
            return structureBlip;
        if (map.XenoBlips.TryGetValue(entityId, out var xenoBlip))
            return xenoBlip;

        if (map.OpforBlips.TryGetValue(entityId, out var opforBlip))
            return opforBlip;
        if (map.GovforBlips.TryGetValue(entityId, out var govforBlip))
            return govforBlip;
        if (map.ClfBlips.TryGetValue(entityId, out var clfBlip))
            return clfBlip;
        return null;
    }

    private void UpdateCanvas(List<TacticalMapLine> lines, Dictionary<Vector2i, string> labels, bool marine, bool xeno, bool opfor, bool govfor, bool clf, EntityUid user, SoundSpecifier? sound = null)
    {
        var maps = EntityQueryEnumerator<TacticalMapComponent>();
        while (maps.MoveNext(out var mapId, out var map))
        {
            map.MapDirty = true;

            // Collect infra IDs (comms, sensors, tunnels) so they will not be converted to enemy_blip
            var infraIds = new HashSet<int>();
            var commsAll = EntityQueryEnumerator<CommunicationsTowerComponent>();
            while (commsAll.MoveNext(out var commId, out _))
                infraIds.Add(commId.Id);
            var sensorsAll = EntityQueryEnumerator<SensorTowerComponent>();
            while (sensorsAll.MoveNext(out var sensorId, out _))
                infraIds.Add(sensorId.Id);
            var tunnelsAll = EntityQueryEnumerator<XenoTunnelComponent>();
            while (tunnelsAll.MoveNext(out var tunId, out _))
                infraIds.Add(tunId.Id);

            // Helper to convert non-friendly human blips in a last-update dictionary to enemy_blip images.
            void ReduceHumanBlipsToEnemy(Dictionary<int, TacticalMapBlip> lastUpdate, string teamFaction)
            {
                if (lastUpdate == null)
                    return;

                var enemyRsi = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "enemy_blip");
                var keys = lastUpdate.Keys.ToList();
                foreach (var id in keys)
                {
                    if (infraIds.Contains(id))
                        continue; // keep infra as-is

                    // Never replace xeno entities/structures
                    if (map.XenoBlips.ContainsKey(id) || map.XenoStructureBlips.ContainsKey(id))
                        continue;

                    bool isFriendly = (teamFaction == "MARINES" && map.MarineBlips.ContainsKey(id))
                        || (teamFaction == "OPFOR" && map.OpforBlips.ContainsKey(id))
                        || (teamFaction == "GOVFOR" && map.GovforBlips.ContainsKey(id))
                        || (teamFaction == "CLF" && map.ClfBlips.ContainsKey(id));

                    if (isFriendly)
                        continue;

                    if (!lastUpdate.TryGetValue(id, out var orig))
                        continue;

                    lastUpdate[id] = new TacticalMapBlip(orig.Indices, enemyRsi, orig.Color, orig.Status, orig.Background, false);
                }
            }

            if (marine)
            {
                map.MarineLines = lines;
                map.MarineLabels = new Dictionary<Vector2i, string>(labels);
                map.LastUpdateMarineBlips = map.MarineBlips.ToDictionary();

                var includeEv = new TacticalMapIncludeXenosEvent();
                RaiseLocalEvent(ref includeEv);
                if (includeEv.Include)
                {
                    foreach (var blip in map.XenoBlips)
                    {
                        map.LastUpdateMarineBlips.TryAdd(blip.Key, blip.Value);
                    }
                }

                // If MARINES control any active sensors, show enemy human blips and xenon/tunnel blips in the marine last-update
                if (TeamHasActiveSensors("MARINES"))
                {
                    // Include enemy human factions as full blips
                    foreach (var kv in map.OpforBlips)
                        map.LastUpdateMarineBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.GovforBlips)
                        map.LastUpdateMarineBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.ClfBlips)
                        map.LastUpdateMarineBlips.TryAdd(kv.Key, kv.Value);

                    // Include xenon blips and structures (tunnels) so they appear on the update
                    foreach (var kv in map.XenoBlips)
                        map.LastUpdateMarineBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.XenoStructureBlips)
                        map.LastUpdateMarineBlips.TryAdd(kv.Key, kv.Value);
                }

                // Convert other human factions to enemy_blip images for update recipients
                if (TeamHasActiveSensors("MARINES"))
                    ReduceHumanBlipsToEnemy(map.LastUpdateMarineBlips, "MARINES");

                _marineAnnounce.AnnounceARESStaging(user, "The tactical map has been updated.", sound, null, "MARINES");
                _adminLog.Add(LogType.RMCTacticalMapUpdated, $"{ToPrettyString(user)} updated the marine tactical map for {ToPrettyString(mapId)}");
            }

            if (xeno)
            {
                map.XenoLines = lines;
                map.XenoLabels = new Dictionary<Vector2i, string>(labels);
                map.LastUpdateXenoBlips = map.XenoBlips.ToDictionary();
                map.LastUpdateXenoStructureBlips = map.XenoStructureBlips.ToDictionary();
                _xenoAnnounce.AnnounceSameHive(user, "The tactical map has been updated.", sound);
                _adminLog.Add(LogType.RMCTacticalMapUpdated, $"{ToPrettyString(user)} updated the xenonid tactical map for {ToPrettyString(mapId)}");
            }

            var ev = new TacticalMapUpdatedEvent(lines.ToList(), user);
            if (opfor)
            {
                map.OpforLines = lines;
                map.OpforLabels = new Dictionary<Vector2i, string>(labels);
                map.LastUpdateOpforBlips = map.OpforBlips.ToDictionary();
                if (TeamHasActiveSensors("OPFOR"))
                {
                    foreach (var kv in map.MarineBlips)
                        map.LastUpdateOpforBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.GovforBlips)
                        map.LastUpdateOpforBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.ClfBlips)
                        map.LastUpdateOpforBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.XenoBlips)
                        map.LastUpdateOpforBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.XenoStructureBlips)
                        map.LastUpdateOpforBlips.TryAdd(kv.Key, kv.Value);
                }
                // Convert others to enemy blips for opfor updates
                if (TeamHasActiveSensors("OPFOR"))
                    ReduceHumanBlipsToEnemy(map.LastUpdateOpforBlips, "OPFOR");
                _marineAnnounce.AnnounceARESStaging(user, "The tactical map has been updated.", sound, null, "OPFOR");
                _adminLog.Add(LogType.RMCTacticalMapUpdated, $"{ToPrettyString(user)} updated the opfor tactical map for {ToPrettyString(mapId)}");
            }

            if (govfor)
            {
                map.GovforLines = lines;
                map.GovforLabels = new Dictionary<Vector2i, string>(labels);
                map.LastUpdateGovforBlips = map.GovforBlips.ToDictionary();
                if (TeamHasActiveSensors("GOVFOR"))
                {
                    foreach (var kv in map.MarineBlips)
                        map.LastUpdateGovforBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.OpforBlips)
                        map.LastUpdateGovforBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.ClfBlips)
                        map.LastUpdateGovforBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.XenoBlips)
                        map.LastUpdateGovforBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.XenoStructureBlips)
                        map.LastUpdateGovforBlips.TryAdd(kv.Key, kv.Value);
                }
                if (TeamHasActiveSensors("GOVFOR"))
                    ReduceHumanBlipsToEnemy(map.LastUpdateGovforBlips, "GOVFOR");
                _marineAnnounce.AnnounceARESStaging(user, "The tactical map has been updated.", sound, null, "GOVFOR");
                _adminLog.Add(LogType.RMCTacticalMapUpdated, $"{ToPrettyString(user)} updated the govfor tactical map for {ToPrettyString(mapId)}");
            }

            if (clf)
            {
                map.ClfLines = lines;
                map.ClfLabels = new Dictionary<Vector2i, string>(labels);
                map.LastUpdateClfBlips = map.ClfBlips.ToDictionary();
                if (TeamHasActiveSensors("CLF"))
                {
                    foreach (var kv in map.MarineBlips)
                        map.LastUpdateClfBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.OpforBlips)
                        map.LastUpdateClfBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.GovforBlips)
                        map.LastUpdateClfBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.XenoBlips)
                        map.LastUpdateClfBlips.TryAdd(kv.Key, kv.Value);
                    foreach (var kv in map.XenoStructureBlips)
                        map.LastUpdateClfBlips.TryAdd(kv.Key, kv.Value);
                }
                if (TeamHasActiveSensors("CLF"))
                    ReduceHumanBlipsToEnemy(map.LastUpdateClfBlips, "CLF");
                _marineAnnounce.AnnounceARESStaging(user, "The tactical map has been updated.", sound, null, "CLF");
                _adminLog.Add(LogType.RMCTacticalMapUpdated, $"{ToPrettyString(user)} updated the clf tactical map for {ToPrettyString(mapId)}");
            }

            RaiseLocalEvent(ref ev);
            // Immediately update open tactical computers on this map so canvases reflect the enemy_blip changes
            var computers = EntityQueryEnumerator<TacticalMapComputerComponent>();
            while (computers.MoveNext(out var computerId, out var computer))
            {
                if (computer.Map != mapId)
                    continue;

                if (!_ui.IsUiOpen(computerId, TacticalMapComputerUi.Key))
                    continue;

                UpdateMapData((computerId, computer), map);
            }
        }
    }

    // Use the shared implementation (it knows about faction filtering)
    protected new void UpdateMapData(Entity<TacticalMapComputerComponent> computer, TacticalMapComponent map)
    {
        // First let shared logic populate computer.Blips
        base.UpdateMapData(computer, map);

        // If this computer's faction controls any active sensor towers, then on that computer
        // we should show other human factions only as reduced blips (background only), but
        // we must keep infrastructure (comms/sensors/tunnels) fully visible.
        // Normalize the computer faction: empty => MARINES
        var normalizedFaction = string.IsNullOrWhiteSpace(computer.Comp.Faction) ? "MARINES" : computer.Comp.Faction.ToUpperInvariant();

        // Only proceed if this faction actually has active sensors on the map.
        if (!TeamHasActiveSensors(normalizedFaction))
            return;

        // Collect infrastructure IDs to exclude them from reduction
        var infraIds = new HashSet<int>();
        var comms = EntityQueryEnumerator<CommunicationsTowerComponent>();
        while (comms.MoveNext(out var cid, out _))
            infraIds.Add(cid.Id);

        var sensors = EntityQueryEnumerator<SensorTowerComponent>();
        while (sensors.MoveNext(out var sid, out _))
            infraIds.Add(sid.Id);

        var tunnels = EntityQueryEnumerator<XenoTunnelComponent>();
        while (tunnels.MoveNext(out var tid, out _))
            infraIds.Add(tid.Id);

        // Helper to determine if an entity ID belongs to a human faction in the map
        string? GetFactionForId(int id)
        {
            if (map.MarineBlips.ContainsKey(id)) return "MARINES";
            if (map.OpforBlips.ContainsKey(id)) return "OPFOR";
            if (map.GovforBlips.ContainsKey(id)) return "GOVFOR";
            if (map.ClfBlips.ContainsKey(id)) return "CLF";
            return null;
        }

        var enemyRsi = new SpriteSpecifier.Rsi(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "enemy_blip");
        var keys = computer.Comp.Blips.Keys.ToList();
        foreach (var id in keys)
        {
            if (infraIds.Contains(id))
                continue; // keep infra icons intact

            var srcFaction = GetFactionForId(id);
            if (srcFaction == null)
                continue; // not a human faction or unknown - skip

            if (srcFaction == normalizedFaction)
                continue; // friendly - keep full icon

            // Replace with enemy_blip image (preserve color/background/status)
            var orig = computer.Comp.Blips[id];
            computer.Comp.Blips[id] = new TacticalMapBlip(orig.Indices, enemyRsi, orig.Color, orig.Status, orig.Background, false);
        }

        // Add other human factions as enemy_blip if they aren't already visible on this computer
        // (computers normally only get their chosen faction; sensors should reveal others as enemy_blip)
        foreach (var kv in map.OpforBlips)
        {
            var id = kv.Key;
            if (infraIds.Contains(id) || computer.Comp.Blips.ContainsKey(id))
                continue;
            computer.Comp.Blips[id] = new TacticalMapBlip(kv.Value.Indices, enemyRsi, kv.Value.Color, kv.Value.Status, kv.Value.Background, false);
        }

        foreach (var kv in map.GovforBlips)
        {
            var id = kv.Key;
            if (infraIds.Contains(id) || computer.Comp.Blips.ContainsKey(id))
                continue;
            computer.Comp.Blips[id] = new TacticalMapBlip(kv.Value.Indices, enemyRsi, kv.Value.Color, kv.Value.Status, kv.Value.Background, false);
        }

        foreach (var kv in map.ClfBlips)
        {
            var id = kv.Key;
            if (infraIds.Contains(id) || computer.Comp.Blips.ContainsKey(id))
                continue;
            computer.Comp.Blips[id] = new TacticalMapBlip(kv.Value.Indices, enemyRsi, kv.Value.Color, kv.Value.Status, kv.Value.Background, false);
        }

        // Ensure xenon blips/structures are visible on canvases when sensors are active (native icons)
        foreach (var kv in map.XenoBlips)
        {
            var id = kv.Key;
            if (infraIds.Contains(id) || computer.Comp.Blips.ContainsKey(id))
                continue;
            computer.Comp.Blips[id] = kv.Value;
        }

        foreach (var kv in map.XenoStructureBlips)
        {
            var id = kv.Key;
            if (infraIds.Contains(id) || computer.Comp.Blips.ContainsKey(id))
                continue;
            computer.Comp.Blips[id] = kv.Value;
        }

        // Ensure the computer's UI reflects these changes immediately
        Dirty(computer);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
        {
            _toInit.Clear();
            _toUpdate.Clear();
        }

        try
        {
            foreach (var init in _toInit)
            {
                if (!init.Comp.Running)
                    continue;

                var wasActive = HasComp<ActiveTacticalMapTrackedComponent>(init);
                UpdateActiveTracking(init);

                if (!wasActive && TryComp(init, out ActiveTacticalMapTrackedComponent? active))
                    UpdateTracked((init, active));
            }
        }
        finally
        {
            _toInit.Clear();
        }

        var time = _timing.CurTime;
        if (time > _nextForceMapUpdate)
        {
            _nextForceMapUpdate = time + _forceMapUpdateEvery;
            var tracked = EntityQueryEnumerator<ActiveTacticalMapTrackedComponent>();
            while (tracked.MoveNext(out var ent, out var comp))
            {
                _toUpdate.Add((ent, comp));
            }
        }

        try
        {
            foreach (var update in _toUpdate)
            {
                if (!update.Comp.Running)
                    continue;

                UpdateTracked(update);
            }
        }
        finally
        {
            _toUpdate.Clear();
        }

        var maps = EntityQueryEnumerator<TacticalMapComponent>();
        while (maps.MoveNext(out var map))
        {
            if (!map.MapDirty)
                continue;

            // Process updates per-faction using the NextUpdatePerFaction dictionary on the map component.
            var factions = new[] { "MARINES", "XENONIDS", "OPFOR", "GOVFOR", "CLF" };

            foreach (var faction in factions)
            {
                if (!map.NextUpdatePerFaction.TryGetValue(faction, out var next) || time < next)
                    continue;

                // Advance the timer immediately to avoid re-entrancy causing multiple updates this tick.
                map.NextUpdatePerFaction[faction] = time + _mapUpdateEvery;

                // Update open computers that are configured for this faction
                var computers = EntityQueryEnumerator<TacticalMapComputerComponent>();
                while (computers.MoveNext(out var computerId, out var computer))
                {
                    if (!_ui.IsUiOpen(computerId, TacticalMapComputerUi.Key))
                        continue;

                    var compFaction = computer.Faction?.ToUpperInvariant();
                    string normalized = string.IsNullOrWhiteSpace(compFaction) || compFaction == "" || compFaction == "MARINES" || compFaction == "UNMC"
                        ? "MARINES"
                        : compFaction == "XENONIDS" || compFaction == "XENONID" ? "XENONIDS" : compFaction;

                    if (normalized != faction)
                        continue;

                    UpdateMapData((computerId, computer), map);
                }

                // Update dropship weapons terminals similarly
                var dropshipWeapons = EntityQueryEnumerator<TacticalMapComputerComponent, DropshipTerminalWeaponsComponent>();
                while (dropshipWeapons.MoveNext(out var weaponsId, out var weaponsComputer, out _))
                {
                    if (!_ui.IsUiOpen(weaponsId, DropshipTerminalWeaponsUi.Key))
                        continue;

                    var compFaction = weaponsComputer.Faction?.ToUpperInvariant();
                    string normalized = string.IsNullOrWhiteSpace(compFaction) || compFaction == "" || compFaction == "MARINES" || compFaction == "UNMC"
                        ? "MARINES"
                        : compFaction == "XENONIDS" || compFaction == "XENONID" ? "XENONIDS" : compFaction;

                    if (normalized != faction)
                        continue;

                    UpdateMapData((weaponsId, weaponsComputer), map);
                }

                // Update live users (map tables / players) viewing this faction
                var users = EntityQueryEnumerator<ActiveTacticalMapUserComponent, TacticalMapUserComponent>();
                while (users.MoveNext(out var userId, out _, out var userComp))
                {
                    if (faction == "MARINES" && userComp.Marines)
                        UpdateUserData((userId, userComp), map);
                    else if (faction == "XENONIDS" && userComp.Xenos)
                        UpdateUserData((userId, userComp), map);
                    else if (faction == "OPFOR" && userComp.Opfor)
                        UpdateUserData((userId, userComp), map);
                    else if (faction == "GOVFOR" && userComp.Govfor)
                        UpdateUserData((userId, userComp), map);
                    else if (faction == "CLF" && userComp.Clf)
                        UpdateUserData((userId, userComp), map);
                }

                // Update tunnel UI users as well
                var tunnelUsers = EntityQueryEnumerator<TunnelUIUserComponent, TacticalMapUserComponent>();
                while (tunnelUsers.MoveNext(out var tunnelUserId, out _, out var tunnelUserComp))
                {
                    if (faction == "MARINES" && tunnelUserComp.Marines)
                        UpdateUserData((tunnelUserId, tunnelUserComp), map);
                    else if (faction == "XENONIDS" && tunnelUserComp.Xenos)
                        UpdateUserData((tunnelUserId, tunnelUserComp), map);
                    else if (faction == "OPFOR" && tunnelUserComp.Opfor)
                        UpdateUserData((tunnelUserId, tunnelUserComp), map);
                    else if (faction == "GOVFOR" && tunnelUserComp.Govfor)
                        UpdateUserData((tunnelUserId, tunnelUserComp), map);
                    else if (faction == "CLF" && tunnelUserComp.Clf)
                        UpdateUserData((tunnelUserId, tunnelUserComp), map);
                }
            }

            // We've processed per-faction updates for this map this tick; clear dirty flag.
            map.MapDirty = false;
        }
    }

     private void OnSensorTowerStateChanged(EntityUid towerUid, SensorTowerStateChangedEvent ev)
     {
        // When a sensor changes state, update the tactical map computers (canvas) on the map and mark it dirty.
        if (!TryComp<TransformComponent>(towerUid, out var xform) || xform.GridUid is not { } gridId)
        {
            var maps = EntityQueryEnumerator<TacticalMapComponent>();
            while (maps.MoveNext(out var map))
            {
                map.MapDirty = true;
            }
            return;
        }

        if (!_tacticalMapQuery.TryComp(gridId, out var tacticalMap))
        {
            var maps = EntityQueryEnumerator<TacticalMapComponent>();
            while (maps.MoveNext(out var map))
            {
                map.MapDirty = true;
            }
            return;
        }

        // Force immediate update for any open computers on this map only (canvas first).
        var computers = EntityQueryEnumerator<TacticalMapComputerComponent>();
        while (computers.MoveNext(out var computerId, out var computer))
        {
            if (!_ui.IsUiOpen(computerId, TacticalMapComputerUi.Key))
                continue;

            UpdateMapData((computerId, computer), tacticalMap);
        }

        // Do not push updates directly to users here. Mark map dirty and let the normal update loop / network replication
        // send the updated data to users as their computers/clients poll or the map broadcast occurs.
        tacticalMap.MapDirty = true;
     }
}
