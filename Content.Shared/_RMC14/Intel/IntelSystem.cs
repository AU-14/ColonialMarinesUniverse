using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.ARES;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Shared._RMC14.Intel.IntelSpawnerType;

namespace Content.Shared._RMC14.Intel;

public sealed class IntelSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly AreaSystem _area = default!;
    [Dependency] private readonly ARESSystem _ares = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedCMChatSystem _rmcChat = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    // Runtime overrides set by server systems when an active platoon declares a TechTree.
    // Key is normalized team string (lowercase), value is prototype id string.
    private readonly Dictionary<string, string> _teamTechTreeOverrides = new();

    private static readonly ImmutableArray<char> UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToImmutableArray();

    private static readonly EntProtoId<IntelTechTreeComponent> TechTreeProto = "RMCIntelTechTree";

    // Team-specific tech tree prototype ids (convenience constants).
    // Use these when you want to reference a specific team's tech-tree proto in code.
    private static readonly EntProtoId<IntelTechTreeComponent> TechTreeProtoGovfor = "RMCIntelTechTree_govfor";
    private static readonly EntProtoId<IntelTechTreeComponent> TechTreeProtoOpfor = "RMCIntelTechTree_opfor";
    private static readonly EntProtoId<IntelTechTreeComponent> TechTreeProtoClf = "RMCIntelTechTree_clf";
    private static readonly EntProtoId<IntelTechTreeComponent> TechTreeProtoScientist = "RMCIntelTechTree_clf";

    private static readonly EntProtoId PaperScrapProto = "RMCIntelPaperScrap";
    private static readonly EntProtoId ProgressReportProto = "RMCIntelProgressReport";
    private static readonly EntProtoId FolderProto = "RMCIntelFolder";
    private static readonly EntProtoId TechnicalManualProto = "RMCIntelTechnicalManual";
    // private static readonly EntProtoId DiskProto = "RMCIntelDisk";
    private static readonly EntProtoId ExperimentalDevicesProto = "RMCIntelRetrieveHealthAnalyzer";
    // private static readonly EntProtoId ResearchPaperProto = "RMCIntelResearchPaper";
    // private static readonly EntProtoId VialBoxProto = "RMCIntelVialBox";

    private readonly Dictionary<IntelSpawnerType, float> _paperScrapChances = new()
    {
        [Close] = 20, [Medium] = 5, [Far] = 2, [Science] = 10,
    };

    private readonly Dictionary<IntelSpawnerType, float> _progressReportChances = new()
    {
        [Close] = 10, [Medium] = 55, [Far] = 3, [Science] = 10,
    };

    private readonly Dictionary<IntelSpawnerType, float> _folderChances = new()
    {
        [Close] = 20, [Medium] = 5, [Far] = 2, [Science] = 10,
    };

    private readonly Dictionary<IntelSpawnerType, float> _technicalManualChances = new()
    {
        [Close] = 20, [Medium] = 40, [Far] = 20, [Science] = 20,
    };

    private readonly Dictionary<IntelSpawnerType, float> _diskChances = new()
    {
        [Close] = 20, [Medium] = 40, [Far] = 20, [Science] = 20,
    };

    private readonly Dictionary<IntelSpawnerType, float> _experimentalDeviceChances = new()
    {
        [Close] = 10, [Medium] = 20, [Far] = 40, [Science] = 30,
    };

    private readonly Dictionary<IntelSpawnerType, float> _researchPaperChances = new()
    {
        [Close] = 25, [Medium] = 20, [Far] = 5, [Science] = 50,
    };

    private readonly Dictionary<IntelSpawnerType, float> _vialBoxChances = new()
    {
        [Close] = 15, [Medium] = 30, [Far] = 5, [Science] = 50,
    };

    private int _paperScraps;
    private int _progressReports;
    private int _folders;
    private int _technicalManuals;
    private int _disks;
    private int _experimentalDevices;
    private int _researchPapers;
    private int _vialBoxes;
    private TimeSpan _maxProcessTime;
    private TimeSpan _announceEvery;
    private int _powerObjectiveWattsRequired;
    private int _intelHumanoidsCorpsesMax;

    private readonly Dictionary<IntelSpawnerType, List<Entity<IntelSpawnerComponent>>> _spawners = new();
    private readonly Queue<Entity<IntelRetrieveItemObjectiveComponent>> _activePositionIntels = new();
    private readonly Queue<Entity<IntelRescueSurvivorObjectiveComponent>> _activeSurvivorIntels = new();
    private readonly Queue<Entity<IntelRecoverCorpseObjectiveComponent>> _activeCorpseIntels = new();
    private readonly HashSet<Entity<IntelContainerComponent>> _nearby = new();

    private EntityQuery<IntelReadObjectiveComponent> _readObjectiveQuery;

    public override void Initialize()
    {
        _readObjectiveQuery = GetEntityQuery<IntelReadObjectiveComponent>();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<DropshipLandedOnPlanetEvent>(OnDropshipLandedOnPlanet);

        SubscribeLocalEvent<IntelNumberComponent, RefreshNameModifiersEvent>(OnNumberRefreshNameModifiers);

        SubscribeLocalEvent<IntelUnlocksComponent, ComponentRemove>(OnUnlocksRemove);
        SubscribeLocalEvent<IntelUnlocksComponent, EntityTerminatingEvent>(OnUnlocksRemove);

        SubscribeLocalEvent<IntelRequiresComponent, ComponentRemove>(OnRequiresRemove);
        SubscribeLocalEvent<IntelRequiresComponent, EntityTerminatingEvent>(OnRequiresRemove);

        SubscribeLocalEvent<IntelReadObjectiveComponent, UseInHandEvent>(OnReadUseInHand);
        SubscribeLocalEvent<IntelReadObjectiveComponent, IntelReadDoAfterEvent>(OnReadDoAfter);

        SubscribeLocalEvent<IntelRetrieveItemObjectiveComponent, MapInitEvent>(OnRetrieveMapInit, after: [typeof(AreaSystem)]);
        SubscribeLocalEvent<IntelRetrieveItemObjectiveComponent, ContainerGettingInsertedAttemptEvent>(OnHandPickUp);
        SubscribeLocalEvent<IntelRetrieveItemObjectiveComponent, PullAttemptEvent>(OnIntelPullAttempt);
        SubscribeLocalEvent<ActiveIntelCorpseComponent, PullAttemptEvent>(OnIntelCorpsePullAttempt);

        SubscribeLocalEvent<ViewIntelObjectivesComponent, MapInitEvent>(OnViewIntelObjectivesMapInit, after: [typeof(AreaSystem)]);
        SubscribeLocalEvent<ViewIntelObjectivesComponent, ViewIntelObjectivesActionEvent>(OnViewIntelObjectivesAction);

        SubscribeLocalEvent<IntelHasUnlockedComponent, RefreshNameModifiersEvent>(OnHasUnlockedRefreshName);

        SubscribeLocalEvent<IntelSerialComponent, MapInitEvent>(OnIntelSerialMapInit, after: [typeof(AreaSystem)]);
        SubscribeLocalEvent<IntelSerialComponent, RefreshNameModifiersEvent>(OnIntelSerialRefreshNameModifiers);
        SubscribeLocalEvent<IntelSerialComponent, ExaminedEvent>(OnIntelSerialExamined);

        SubscribeLocalEvent<IntelRecoverCorpseObjectiveOnDeathComponent, MobStateChangedEvent>(OnRescueCorpseObjectiveOnDeathChanged);

        SubscribeLocalEvent<IntelRecoverCorpseObjectiveComponent, MapInitEvent>(OnRescueCorpseObjectiveMapInit, after: [typeof(AreaSystem)]);

        SubscribeLocalEvent<IntelKnowledgeComponent, ComponentRemove>(OnKnowledgeRemove);
        SubscribeLocalEvent<IntelKnowledgeComponent, EntityTerminatingEvent>(OnKnowledgeRemove);

        SubscribeLocalEvent<IntelReadComponent, ComponentRemove>(OnReadRemove);
        SubscribeLocalEvent<IntelReadComponent, EntityTerminatingEvent>(OnReadRemove);

        SubscribeLocalEvent<IntelConsoleComponent, InteractHandEvent>(OnConsoleInteractHand);
        SubscribeLocalEvent<IntelConsoleComponent, IntelSubmitDoAfterEvent>(OnConsoleSubmitDoAfter);

        SubscribeLocalEvent<IntelCluesComponent, MapInitEvent>(OnIntelCluesMapInit, after: [typeof(AreaSystem)]);

        Subs.CVar(_config, RMCCVars.RMCIntelPaperScraps, v => _paperScraps = v, true);
        Subs.CVar(_config, RMCCVars.RMCIntelProgressReports, v => _progressReports = v, true);
        Subs.CVar(_config, RMCCVars.RMCIntelFolders, v => _folders = v, true);
        Subs.CVar(_config, RMCCVars.RMCIntelTechnicalManuals, v => _technicalManuals = v, true);
        Subs.CVar(_config, RMCCVars.RMCIntelDisks, v => _disks = v, true);
        Subs.CVar(_config, RMCCVars.RMCIntelExperimentalDevices, v => _experimentalDevices = v, true);
        Subs.CVar(_config, RMCCVars.RMCIntelResearchPapers, v => _researchPapers = v, true);
        Subs.CVar(_config, RMCCVars.RMCIntelVialBoxes, v => _vialBoxes = v, true);
        Subs.CVar(_config, RMCCVars.RMCIntelMaxProcessTimeMilliseconds, v => _maxProcessTime = TimeSpan.FromMilliseconds(v), true);
        Subs.CVar(_config, RMCCVars.RMCIntelAnnounceEveryMinutes, v => _announceEvery = TimeSpan.FromMinutes(v), true);
        Subs.CVar(_config, RMCCVars.RMCIntelPowerObjectiveWattsRequired, v => _powerObjectiveWattsRequired = v, true);
        Subs.CVar(_config, RMCCVars.RMCIntelHumanoidCorpsesMax, v => _intelHumanoidsCorpsesMax = v, true);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _spawners.Clear();
        _activePositionIntels.Clear();
        _activeSurvivorIntels.Clear();
    }

    private void OnDropshipLandedOnPlanet(ref DropshipLandedOnPlanetEvent ev)
    {
        var tree = EnsureTechTree();
        tree.Comp.DoAnnouncements = true;
        Dirty(tree);
    }

    private void OnNumberRefreshNameModifiers(Entity<IntelNumberComponent> ent, ref RefreshNameModifiersEvent args)
    {
        args.AddModifier("rmc-intel-suffix", extraArgs: ("number", ent.Comp.Number));
    }

    private void OnUnlocksRemove<T>(Entity<IntelUnlocksComponent> ent, ref T args)
    {
        foreach (var unlocks in ent.Comp.Unlocks)
        {
            if (TryComp(unlocks, out IntelRequiresComponent? requires))
            {
                requires.Requires.Remove(ent);
            }
        }
    }

    private void OnRequiresRemove<T>(Entity<IntelRequiresComponent> ent, ref T args)
    {
        foreach (var requires in ent.Comp.Requires)
        {
            if (TryComp(requires, out IntelUnlocksComponent? unlocks))
            {
                unlocks.Unlocks.Remove(ent);
            }
        }
    }

    private void OnReadUseInHand(Entity<IntelReadObjectiveComponent> ent, ref UseInHandEvent args)
    {
        var user = args.User;

        if (HasComp<IntelRescueSurvivorObjectiveComponent>(user))
        {
            _popup.PopupClient(Loc.GetString("rmc-intel-survivor-read", ("thing", Name(ent))), ent, user);
            return;
        }

        var delay = ent.Comp.Delay * _skills.GetSkillDelayMultiplier(user, ent.Comp.Skill);
        var ev = new IntelReadDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, ent) { BreakOnDropItem = true, NeedHand = true };
        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupClient($"You start reading the {Name(ent)}", ent, user);
    }

    private void OnHandPickUp(EntityUid ent,
        IntelRetrieveItemObjectiveComponent component,
        ContainerGettingInsertedAttemptEvent args)
    {
        var user = args.Container.Owner;
        if (HasComp<IntelRescueSurvivorObjectiveComponent>(user))
        {
            args.Cancel();
            _popup.PopupClient(Loc.GetString("rmc-intel-survivor-pickup", ("thing", Name(ent))), ent, user);
        }
    }

    private void OnIntelPullAttempt(Entity<IntelRetrieveItemObjectiveComponent> ent, ref PullAttemptEvent args)
    {
        var user = args.PullerUid;
        if (HasComp<IntelRescueSurvivorObjectiveComponent>(user))
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("rmc-intel-survivor-pickup", ("thing", Name(ent))), ent, user);
        }
    }

    private void OnIntelCorpsePullAttempt(Entity<ActiveIntelCorpseComponent> ent, ref PullAttemptEvent args)
    {
        var user = args.PullerUid;
        if (HasComp<IntelRescueSurvivorObjectiveComponent>(user))
        {
            args.Cancelled = true;

            var msg = HasComp<XenoComponent>(ent)
                ? Loc.GetString("rmc-intel-survivor-xeno-pull", ("thing", Name(ent)))
                : Loc.GetString("rmc-intel-survivor-corpse-pull", ("thing", Name(ent)));

            _popup.PopupClient(msg, ent, user);
        }
    }

    private void OnReadDoAfter(Entity<IntelReadObjectiveComponent> ent, ref IntelReadDoAfterEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        args.Handled = true;
        if (args.Cancelled)
        {
            _popup.PopupClient("You get distracted and lose your train of thought, you'll have to start over reading this.", ent, user);
            return;
        }

        if (ent.Comp.State == IntelObjectiveState.Inactive)
        {
            _popup.PopupClient("You don't notice anything useful. You probably need to find its instructions on a paper scrap", ent, user);
            return;
        }

        // TODO RMC14 clues

        _popup.PopupClient($"You finish reading the {Name(ent)}", ent, user);
        if (ent.Comp.State == IntelObjectiveState.Complete)
            return;

        ent.Comp.State = IntelObjectiveState.Complete;
        Dirty(ent);

        if (_net.IsClient)
            return;

        // Resolve the actor's team and ensure we credit that team's tech tree explicitly.
        var team = ResolveTeam(user);
        var tree = EnsureTechTree(team);
        tree.Comp.Tree.Documents.Current++;
        AddPoints(tree, ent.Comp.Value, team);

        var knowledge = EnsureComp<IntelKnowledgeComponent>(user);
        knowledge.Read.Add(ent);
        Dirty(user, knowledge);

        var read = EnsureComp<IntelReadComponent>(ent);
        read.Readers.Add(user);
        Dirty(ent, read);

        if (TryComp(ent, out IntelRetrieveItemObjectiveComponent? retrieve) &&
            retrieve.State == IntelObjectiveState.Inactive)
        {
            retrieve.State = IntelObjectiveState.Active;
            Dirty(ent, retrieve);
        }

        UpdateTree(tree);
    }

    private void OnRetrieveMapInit(Entity<IntelRetrieveItemObjectiveComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.State != IntelObjectiveState.Active)
            return;

        EnsureComp<ActiveIntelPositionComponent>(ent);
    }

    private void OnViewIntelObjectivesMapInit(Entity<ViewIntelObjectivesComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.Action, ent.Comp.ActionId);
    }

    private void OnViewIntelObjectivesAction(Entity<ViewIntelObjectivesComponent> ent, ref ViewIntelObjectivesActionEvent args)
    {
        if (_net.IsServer)
        {
            // Determine actor performing the action from the BaseActionEvent in args
            var performer = args.Performer;

            // If the performer is a ghost, populate team trees for all teams (with AU-backed points)
            var teamTrees = new Dictionary<string, IntelTechTree>();
            if (HasComp<GhostComponent>(performer))
            {
                var techQuery = EntityQueryEnumerator<IntelTechTreeComponent>();
                while (techQuery.MoveNext(out var uid, out var comp))
                {
                    // Ghosts see each team's intel tree as-is (internal points only).
                    var copy = comp.Tree;
                    copy.Points = GetAuWinPoints(comp.Team, copy.Points);
                    teamTrees[comp.Team] = copy;
                }

                ent.Comp.TeamTrees = teamTrees;
            }
            else
            {
                // Non-ghosts: resolve the team from their MarineComponent faction.
                // Use the faction string directly (normalized to lower-case) if present; otherwise fall back to Team.None.
                string team = Team.None;
                if (TryComp(performer, out Content.Shared._RMC14.Marines.MarineComponent? marine) && !string.IsNullOrEmpty(marine.Faction))
                {
                    team = marine.Faction.ToLowerInvariant();
                }

                var treeEntity = EnsureTechTree(team);
                var tree = treeEntity.Comp.Tree;
                // Show the AU win points instead of internal tech points for UI display
                var displayTree = tree;
                displayTree.Points = GetAuWinPoints(team, displayTree.Points);
                ent.Comp.Tree = displayTree;
                ent.Comp.TeamTrees.Clear();
            }

            Dirty(ent);
        }

        _ui.OpenUi(ent.Owner, ViewIntelObjectivesUI.Key, ent);
    }

    private void OnHasUnlockedRefreshName(Entity<IntelHasUnlockedComponent> ent, ref RefreshNameModifiersEvent args)
    {
        args.AddModifier("rmc-intel-unlocked", extraArgs: ("unlocked", string.Join(", ", ent.Comp.Unlocked)));
    }

    private void OnIntelSerialMapInit(Entity<IntelSerialComponent> ent, ref MapInitEvent args)
    {
        int Number()
        {
            return _random.Next(0, 10);
        }

        char Char()
        {
            return _random.Pick(UppercaseLetters);
        }

        ent.Comp.Serial = $"{Number()}{Char()}{Number()}{Number()}{Number()}{Number()}{Char()}";
        _nameModifier.RefreshNameModifiers(ent.Owner);
    }

    private void OnIntelSerialRefreshNameModifiers(Entity<IntelSerialComponent> ent, ref RefreshNameModifiersEvent args)
    {
        args.AddModifier("rmc-intel-serial-name", extraArgs: ("serial", ent.Comp.Serial));
    }

    private void OnIntelSerialExamined(Entity<IntelSerialComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(IntelSerialComponent)))
        {
            args.PushMarkup(Loc.GetString("rmc-intel-serial-examine", ("serial", ent.Comp.Serial)));
        }
    }

    private void OnRescueCorpseObjectiveOnDeathChanged(Entity<IntelRecoverCorpseObjectiveOnDeathComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.OldMobState == MobState.Dead || args.NewMobState != MobState.Dead)
            return;

        if (HasComp<IntelRecoverCorpseObjectiveComponent>(args.Target))
            return;

        var comp = EnsureComp<IntelRecoverCorpseObjectiveComponent>(args.Target);
        comp.Value = ent.Comp.Value;
        Dirty(args.Target, comp);
    }

    private void OnRescueCorpseObjectiveMapInit(Entity<IntelRecoverCorpseObjectiveComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<ActiveIntelCorpseComponent>(ent);
    }

    private void OnKnowledgeRemove<T>(Entity<IntelKnowledgeComponent> ent, ref T args)
    {
        foreach (var read in ent.Comp.Read)
        {
            if (TerminatingOrDeleted(read))
                continue;

            if (TryComp(read, out IntelReadComponent? readComp))
            {
                readComp.Readers.Remove(ent);
                Dirty(read, readComp);
            }
        }
    }

    private void OnReadRemove<T>(Entity<IntelReadComponent> ent, ref T args)
    {
        foreach (var reader in ent.Comp.Readers)
        {
            if (TerminatingOrDeleted(reader))
                continue;

            if (TryComp(reader, out IntelKnowledgeComponent? knowledge))
            {
                knowledge.Read.Remove(ent);
                Dirty(reader, knowledge);
            }
        }
    }

    private void OnConsoleInteractHand(Entity<IntelConsoleComponent> ent, ref InteractHandEvent args)
    {
        var msg = "You start typing in intel into the computer...";
        if (!TryComp(args.User, out IntelKnowledgeComponent? knowledge) ||
            !knowledge.Read.TryFirstOrNull(out var read))
        {
            msg += " and you have nothing new to add...";
            _popup.PopupClient(msg, ent, args.User, PopupType.Medium);
            return;
        }

        _popup.PopupClient(msg, ent, args.User, PopupType.Medium);

        var delay = ent.Comp.Delay * _skills.GetSkillDelayMultiplier(args.User, ent.Comp.Skill);
        var ev = new IntelSubmitDoAfterEvent { Intel = GetNetEntity(read.Value) };
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, ev, ent, ent, ent) { BreakOnMove = true };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnConsoleSubmitDoAfter(Entity<IntelConsoleComponent> ent, ref IntelSubmitDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        if (args.Cancelled)
        {
            _popup.PopupEntity(
                "You get distracted and lose your train of thought, you'll have to start the typing over...",
                ent,
                args.User,
                PopupType.MediumCaution
            );

            args.Repeat = false;
            return;
        }

        void StopPopup(ref IntelSubmitDoAfterEvent args)
        {
            if (args.Amount == 0)
                _popup.PopupEntity("...and you have nothing new to add...", ent, args.User, PopupType.Medium);
            else
                _popup.PopupEntity($"...and done! You uploaded {args.Amount} entries!", ent, args.User, PopupType.Medium);
        }

        if (!TryComp(args.User, out IntelKnowledgeComponent? knowledge))
        {
            StopPopup(ref args);
            return;
        }

        if (!TryGetEntity(args.Intel, out var intel) ||
            !TryComp(intel, out IntelUnlocksComponent? unlocks) ||
            !unlocks.Unlocks.TryFirstOrNull(out var unlock))
        {
            if (!knowledge.Read.TryFirstOrNull(out intel))
            {
                StopPopup(ref args);
                return;
            }

            args.Intel = GetNetEntity(intel.Value);
            if (!TryComp(intel, out unlocks) ||
                !unlocks.Unlocks.TryFirstOrNull(out unlock))
            {
                knowledge.Read.Remove(intel.Value);
                args.Repeat = true;
                return;
            }
        }

        if (TryComp(unlock, out IntelCluesComponent? cluesComp))
        {
            var msg = Loc.GetString(cluesComp.Clue, ("intel", unlock), ("area", cluesComp.InitialArea));
            _rmcChat.ChatMessageToOne(msg, args.User);
            _popup.PopupEntity(msg, ent, args.User, PopupType.Medium);

            if (TryComp(unlock, out IntelRetrieveItemObjectiveComponent? retrieve) &&
                retrieve.State != IntelObjectiveState.Complete &&
                cluesComp.Category is { } category)
            {
                var tree = EnsureTechTree(ent.Comp.Team);
                var clues = tree.Comp.Tree.Clues.GetOrNew(category);
                clues[GetNetEntity(unlock.Value)] = msg;
            }
        }

        unlocks.Unlocks.Remove(unlock.Value);
        ActivateIntel(intel.Value, unlock.Value);
        args.Amount++;
        _audio.PlayPvs(ent.Comp.TypingSound, ent);

        if (unlocks.Unlocks.Count == 0)
            knowledge.Read.Remove(intel.Value);

        if (unlocks.Unlocks.Count == 0)
        {
            var teamKey = ent.Comp.Team;


            var teamTree = EnsureTechTree(teamKey);

            if (TryComp(unlock, out IntelReadObjectiveComponent? readComp))
            {
                teamTree.Comp.Tree.Documents.Current++;
                AddPoints(teamTree, readComp.Value, teamKey);
            }
            else if (TryComp(unlock, out IntelRetrieveItemObjectiveComponent? retrieveComp))
            {
                teamTree.Comp.Tree.RetrieveItems.Current++;
                AddPoints(teamTree, retrieveComp.Value, teamKey);
            }

            // Feedback to the user for debugging/verification: which team got credited.
            Logger.Debug($"[IntelSystem] AddPoints: credited  to team='{teamKey}");
        }
    }

    private void OnIntelCluesMapInit(Entity<IntelCluesComponent> ent, ref MapInitEvent args)
    {
        if (!_area.TryGetArea(ent, out var area, out _))
            return;

        ent.Comp.InitialArea = Name(area.Value);
    }

    private List<EntityUid> SpawnIntel(EntProtoId proto, int count, Dictionary<IntelSpawnerType, float> chances)
    {
        var items = new List<EntityUid>();
        for (var i = 0; i < count; i++)
        {
            var type = _random.Pick(chances);
            if (!_spawners.TryGetValue(type, out var spawners) ||
                spawners.Count <= 0)
            {
                continue;
            }

            var spawner = _random.Pick(spawners);
            var coords = _transform.GetMoverCoordinates(spawner);
            var intel = Spawn(proto, coords);
            items.Add(intel);

            EnsureComp<ActiveIntelPositionComponent>(intel);

            var number = EnsureComp<IntelNumberComponent>(intel);
            number.Number = _random.Next(100, 1000);
            Dirty(intel, number);
            _nameModifier.RefreshNameModifiers(intel);

            _nearby.Clear();
            _entityLookup.GetEntitiesInRange(coords, 0.5f, _nearby, LookupFlags.Uncontained);

            foreach (var nearby in _nearby)
            {
                if (HasComp<StorageComponent>(nearby) &&
                    _storage.Insert(nearby, intel, out _))
                {
                    break;
                }
                else if (_entityStorage.Insert(intel, nearby))
                {
                    break;
                }
            }
        }

        return items;
    }



    public Entity<IntelTechTreeComponent> EnsureTechTree(string team)
    {
        if (TryGetTechTree(team, out var tree))
            return tree.Value;

        var protoId = TechTreeProto;



        if (!string.IsNullOrEmpty(team))
        {
            var teamKey = team.ToLowerInvariant();
            // Prefer explicit team constants where available
            EntProtoId<IntelTechTreeComponent> candidateProto = teamKey switch
            {
                var t when t == Team.GovFor => TechTreeProtoGovfor,
                var t when t == Team.OpFor => TechTreeProtoOpfor,
                var t when t == Team.CLF => TechTreeProtoClf,
                var t when t == "scientist" => TechTreeProtoScientist,
                _ => (EntProtoId<IntelTechTreeComponent>)($"{(string)TechTreeProto}_{teamKey}")
            };

            var candidateIdStr = (string) candidateProto;
            if (_prototypes.HasIndex(candidateIdStr))
            {
                protoId = candidateProto;
            }
        }

        // Check for a runtime override set by server systems (e.g. AuRoundSystem, PlatoonSpawnRuleSystem)
        if (_teamTechTreeOverrides.TryGetValue(team.ToLowerInvariant(), out var overrideProtoId) &&
            _prototypes.TryIndex<EntityPrototype>(overrideProtoId, out _))
        {
            protoId = overrideProtoId;
        }

        // Log which prototype id we're about to spawn for visibility during testing.
        Logger.Info($"[IntelSystem] Spawning tech tree for team='{team}' using prototype='{(string)protoId}'");

        var treeId = Spawn(protoId);
        var treeComp = EnsureComp<IntelTechTreeComponent>(treeId);
        treeComp.Team = team;
        tree = (treeId, treeComp);

        foreach (var tier in treeComp.Tree.Options)
        {
            for (var i = 0; i < tier.Count; i++)
            {
                var option = tier[i];
                if (option.CurrentCost == 0)
                    tier[i] = option with { CurrentCost = option.Cost };
            }
        }

        return tree.Value;
    }

    // Convenience overload: ensure the default (None) tech tree.
    public Entity<IntelTechTreeComponent> EnsureTechTree()
    {
        return EnsureTechTree(Team.None);
    }

    public bool TryGetTechTree([NotNullWhen(true)] out Entity<IntelTechTreeComponent>? tree)
    {
        return TryGetTechTree(Team.None, out tree);
    }

    public bool TryGetTechTree(string team, [NotNullWhen(true)] out Entity<IntelTechTreeComponent>? tree)
    {
        var techTreeQuery = EntityQueryEnumerator<IntelTechTreeComponent>();
        while (techTreeQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.Team == team || (team == Team.None && comp.Team == Team.None))
            {
                tree = (uid, comp);
                return true;
            }
        }

        tree = default;
        return false;
    }

    public void RunSpawners()
    {
        try
        {
            var spawnerQuery = EntityQueryEnumerator<IntelSpawnerComponent>();
            while (spawnerQuery.MoveNext(out var uid, out var comp))
            {
                if (EntityManager.IsQueuedForDeletion(uid))
                    continue;

                _spawners.GetOrNew(comp.IntelType).Add((uid, comp));
            }

            if (_spawners.Count == 0)
                return;

            foreach (var spawners in _spawners.Values)
            {
                foreach (var spawner in spawners)
                {
                    QueueDel(spawner);
                }
            }

            var tree = EnsureTechTree();
            var lows = SpawnIntel(PaperScrapProto, _paperScraps, _paperScrapChances);
            var mediums = SpawnIntel(ProgressReportProto, _progressReports, _progressReportChances);
            mediums.AddRange(SpawnIntel(FolderProto, _folders, _folderChances));
            var highs = SpawnIntel(TechnicalManualProto, _technicalManuals, _technicalManualChances);
            // SpawnIntel(DiskProto, _disks, _diskChances);
            SpawnIntel(ExperimentalDevicesProto, _experimentalDevices, _experimentalDeviceChances);
            // SpawnIntel(ResearchPaperProto, _researchPapers, _researchPaperChances);
            // SpawnIntel(VialBoxProto, _vialBoxes, _vialBoxChances);

            tree.Comp.Tree.Documents.Total = _paperScraps + _progressReports + _folders + _technicalManuals;
            tree.Comp.Tree.UploadData.Total = _disks;
            tree.Comp.Tree.RetrieveItems.Total = tree.Comp.Tree.Documents.Total + tree.Comp.Tree.UploadData.Total - _disks; // TODO RMC14 remove - disks

            if (mediums.Count > 0)
            {
                foreach (var low in lows)
                {
                    var medium = _random.Pick(mediums);
                    ConnectObjectives(low, medium);
                }
            }

            if (highs.Count > 0)
            {
                foreach (var medium in mediums)
                {
                    AddRequires(medium, lows);
                    var high = _random.Pick(highs);
                    ConnectObjectives(medium, high);
                }
            }

            if (mediums.Count > 0)
            {
                foreach (var high in highs)
                {
                    AddRequires(high, mediums);
                }
            }
        }
        finally
        {
            _spawners.Clear();
        }
    }

    public void RestoreColonyCommunications()
    {
        if (_net.IsClient)
            return;

        var techTreeQuery = EntityQueryEnumerator<IntelTechTreeComponent>();
        while (techTreeQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.Tree.ColonyCommunications)
                continue;

            comp.Tree.ColonyCommunications = true;
            Dirty(uid, comp);
            // Explicitly pass the tech tree's team so AddPoints cannot accidentally credit the wrong team.
            AddPoints((uid, comp), comp.ColonyCommunicationsPoints, comp.Team);
        }
    }

    public void RestoreColonyCommunications(string team)
    {
        if (_net.IsClient)
            return;

        var tree = EnsureTechTree(team);
        if (tree.Comp.Tree.ColonyCommunications)
            return;

        tree.Comp.Tree.ColonyCommunications = true;
        AddPoints(tree, tree.Comp.ColonyCommunicationsPoints, team);
    }

    private void AddPointsToAllTeams(FixedPoint2 points)
    {
        // Ensure and award to each known team (except None)
        foreach (string t in new[] { Team.GovFor, Team.OpFor, Team.CLF })
        {
            var tree = EnsureTechTree(t);
            AddPoints(tree, points, t);
        }
    }

    private void ConnectObjectives(EntityUid unlocksId, EntityUid requiresId)
    {
        var unlocks = EnsureComp<IntelUnlocksComponent>(unlocksId);
        unlocks.Unlocks.Add(requiresId);
        Dirty(unlocksId, unlocks);

        var requires = EnsureComp<IntelRequiresComponent>(requiresId);
        requires.Requires.Add(unlocksId);
        Dirty(requiresId, requires);

        DeactivateIntel(requiresId);
    }

    private void AddRequires(Entity<IntelRequiresComponent?> requires, List<EntityUid> candidates)
    {
        requires.Comp ??= EnsureComp<IntelRequiresComponent>(requires);
        if (requires.Comp.RequiresCount <= requires.Comp.Requires.Count)
            return;

        var left = requires.Comp.RequiresCount - requires.Comp.Requires.Count;
        for (var i = 0; i < left; i++)
        {
            _random.Shuffle(candidates);
            foreach (var candidate in candidates)
            {
                if (requires.Comp.Requires.Contains(candidate))
                    continue;

                ConnectObjectives(candidate, requires);

                if (requires.Comp.RequiresCount <= requires.Comp.Requires.Count)
                    break;
            }

            if (requires.Comp.RequiresCount <= requires.Comp.Requires.Count)
                break;
        }
    }

    private void DeactivateIntel(EntityUid ent)
    {
        if (TryComp(ent, out IntelReadObjectiveComponent? read))
        {
            read.State = IntelObjectiveState.Inactive;
            Dirty(ent, read);
        }

        if (TryComp(ent, out IntelRetrieveItemObjectiveComponent? retrieve))
        {
            retrieve.State = IntelObjectiveState.Inactive;
            Dirty(ent, retrieve);
        }
    }

    private void ActivateIntel(EntityUid activatedBy, EntityUid toActivate)
    {
        if (TryComp(toActivate, out IntelReadObjectiveComponent? read) &&
            read.State == IntelObjectiveState.Inactive)
        {
            read.State = IntelObjectiveState.Active;
            Dirty(toActivate, read);
        }

        if (TryComp(toActivate, out IntelRetrieveItemObjectiveComponent? retrieve) &&
            retrieve.State == IntelObjectiveState.Inactive)
        {
            retrieve.State = IntelObjectiveState.Active;
            Dirty(toActivate, retrieve);
        }

        if (TryComp(toActivate, out IntelNumberComponent? number))
        {
            var unlocked = EnsureComp<IntelHasUnlockedComponent>(activatedBy);
            unlocked.Unlocked.Add(number.Number);
            Dirty(activatedBy, unlocked);

            _nameModifier.RefreshNameModifiers(activatedBy);
        }
    }

    public bool TryUsePoints(FixedPoint2 points)
    {
        return TryUsePoints(Team.None, points);
    }

    public bool TryUsePoints(string team, FixedPoint2 points)
    {
        // IntelSystem must not touch AU/win points. Always spend from the intel tree's internal FixedPoint2 pool.
        var tree = EnsureTechTree(team);
        if (points > tree.Comp.Tree.Points)
            return false;

        tree.Comp.Tree.Points -= points;
        Dirty(tree);
        UpdateTree(tree);
        return true;
    }

    public void AddPoints(Entity<IntelTechTreeComponent> tree, FixedPoint2 points, string teamkey = "govfor")
    {

        tree.Comp.Team = teamkey;
        var before = tree.Comp.Tree.Points;
        tree.Comp.Tree.Points += points;
        tree.Comp.Tree.TotalEarned += points;
        var after = tree.Comp.Tree.Points;
        // Log every credit so uploads from consoles or any code path are visible in server logs.
        Logger.Debug($"[IntelSystem] AddPoints: credited {points.Double()} to team='{teamkey}' before={before.Double()} after={after.Double()}");
        Dirty(tree);
        UpdateTree(tree);
    }





    public void UpdateTree(Entity<IntelTechTreeComponent> tree)
    {
        var query = EntityQueryEnumerator<TechControlConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.Team == tree.Comp.Team || (console.Team == Team.None && tree.Comp.Team == Team.None))
            {
                // Display the intel tree state as-is. Do NOT substitute AU/win points here.
                // Substitute AU win points for display: query the ObjectiveMaster if available on server
                var displayed = tree.Comp.Tree;
                displayed.Points = GetAuWinPoints(tree.Comp.Team, displayed.Points);
                console.Tree = displayed;
                Dirty(uid, console);
            }
        }
    }

    // NOTE: AU/win points are intentionally NOT present in this system. Any AU logic must live
    // in the TechSystem or ObjectiveMaster-handling systems. Do not add AU helpers here.

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        if (TryGetTechTree(out var tree) &&
            tree.Value.Comp.DoAnnouncements &&
            time >= tree.Value.Comp.LastAnnounceAt)
        {
            tree.Value.Comp.LastAnnounceAt = time + _announceEvery;
            Dirty(tree.Value);

            var ares = _ares.EnsureARES();
            // Announcements reflect the intel tree's internal FixedPoint2 points only.
            var points = tree.Value.Comp.Tree.Points;

            var last = tree.Value.Comp.LastAnnouncePoints;
            tree.Value.Comp.LastAnnouncePoints = points;
            Dirty(tree.Value);

            var change = points - last;
            foreach (var channel in tree.Value.Comp.AnnounceIn)
            {
                var announcement = change > FixedPoint2.Zero
                    ? Loc.GetString("rmc-intel-announcement-gain", ("points", points), ("change", change))
                    : Loc.GetString("rmc-intel-announcement", ("points", points));

                _marineAnnounce.AnnounceRadio(ares, announcement, channel);
            }
        }

        if (_activePositionIntels.Count > 0)
        {
            while (_activePositionIntels.TryDequeue(out var intel))
            {
                if (_timing.CurTime >= time + _maxProcessTime)
                    return;

                if (TerminatingOrDeleted(intel))
                    continue;

                if (intel.Comp.State == IntelObjectiveState.Complete)
                    continue;

                if (_readObjectiveQuery.TryComp(intel, out var read) &&
                    read.State != IntelObjectiveState.Complete)
                {
                    continue;
                }

                if (_area.TryGetArea(intel.Owner, out var area, out _) &&
                    area.Value.Comp.RetrieveItemObjective)
                {
                    intel.Comp.State = IntelObjectiveState.Complete;
                    Dirty(intel);

                    tree ??= EnsureTechTree();
                    tree.Value.Comp.Tree.RetrieveItems.Current++;
                    if (TryComp(intel, out IntelCluesComponent? cluesComp) &&
                        cluesComp.Category is { } category &&
                        tree.Value.Comp.Tree.Clues.TryGetValue(category, out var clues))
                    {
                        clues.Remove(GetNetEntity(intel));
                    }

                    // Explicitly credit the team associated with the tech tree used.
                    AddPoints(tree.Value, intel.Comp.Value, tree.Value.Comp.Team);

                    RemComp<ActiveIntelPositionComponent>(intel);
                }
            }
        }

        if (_activeSurvivorIntels.Count > 0)
        {
            while (_activeSurvivorIntels.TryDequeue(out var intel))
            {
                if (_timing.CurTime >= time + _maxProcessTime)
                    return;

                if (TerminatingOrDeleted(intel))
                    continue;

                if (_mobState.IsDead(intel))
                    continue;

                if (_area.TryGetArea(intel.Owner, out var area, out _) &&
                    HasComp<IntelRescueSurvivorAreaComponent>(area))
                {
                    tree ??= EnsureTechTree();
                    tree.Value.Comp.Tree.RescueSurvivors++;
                    // Explicitly credit the team associated with the tech tree used.
                    AddPoints(tree.Value, intel.Comp.Value, tree.Value.Comp.Team);

                    RemComp<IntelRescueSurvivorObjectiveComponent>(intel);
                }
            }
        }

        if (_activeCorpseIntels.Count > 0)
        {
            while (_activeCorpseIntels.TryDequeue(out var intel))
            {
                if (_timing.CurTime >= time + _maxProcessTime)
                    return;

                if (TerminatingOrDeleted(intel))
                    continue;

                if (!_mobState.IsDead(intel))
                    continue;

                if (_area.TryGetArea(intel.Owner, out var area, out _) &&
                    HasComp<IntelRecoverCorpsesAreaComponent>(area))
                {
                    tree ??= EnsureTechTree();
                    tree.Value.Comp.Tree.RecoverCorpses++;
                    Dirty(tree.Value);

                    RemComp<ActiveIntelCorpseComponent>(intel);

                    if (!HasComp<XenoComponent>(intel))
                    {
                        if (tree.Value.Comp.HumanoidCorpses >= _intelHumanoidsCorpsesMax)
                            continue;

                        tree.Value.Comp.HumanoidCorpses++;
                    }


                    // Explicitly credit the team associated with the tech tree used.
                    AddPoints(tree.Value, intel.Comp.Value, tree.Value.Comp.Team);
                }
            }
        }

        var activeIntelQuery = EntityQueryEnumerator<ActiveIntelPositionComponent, IntelRetrieveItemObjectiveComponent>();
        while (activeIntelQuery.MoveNext(out var uid, out _, out var retrieve))
        {
            if (retrieve.State == IntelObjectiveState.Active)
                _activePositionIntels.Enqueue((uid, retrieve));
        }

        var survivorQuery = EntityQueryEnumerator<IntelRescueSurvivorObjectiveComponent>();
        while (survivorQuery.MoveNext(out var uid, out var comp))
        {
            _activeSurvivorIntels.Enqueue((uid, comp));
        }

        var corpseQuery = EntityQueryEnumerator<ActiveIntelCorpseComponent, IntelRecoverCorpseObjectiveComponent>();
        while (corpseQuery.MoveNext(out var uid, out _, out var comp))
        {
            _activeCorpseIntels.Enqueue((uid, comp));
        }

        if (tree != null && !tree.Value.Comp.Tree.ColonyPower)
        {
            var watts = 0;
            var generatorQuery = EntityQueryEnumerator<IntelPowerObjectiveComponent, RMCFusionReactorComponent>();
            while (generatorQuery.MoveNext(out _, out var generator))
            {
                if (generator.State != RMCFusionReactorState.Working)
                    continue;

                watts += generator.Watts;
            }

            if (watts >= _powerObjectiveWattsRequired)
            {
                tree.Value.Comp.Tree.ColonyPower = true;
                // Give power points to all teams (global benefit)
                AddPointsToAllTeams(tree.Value.Comp.PowerPoints);
            }
        }
    }

    // Helper: read intel points (from the intel tech tree) for a team. Returns integer points.
    public int GetIntelPoints(string team)
    {
        if (string.IsNullOrEmpty(team) || team == Team.None)
            return 0;

        if (TryGetTechTree(team, out var tree))
        {
            return (int)Math.Floor(tree.Value.Comp.Tree.Points.Double());
        }

        return 0;
    }

    // Attempt to spend intel points (from the intel tech tree). Returns true when successful.
    public bool TrySpendIntelPoints(string team, double amount)
    {
        if (string.IsNullOrEmpty(team) || team == Team.None)
            return false;

        if (!TryGetTechTree(team, out var tree))
            return false;

        var costFp = FixedPoint2.New(amount);
        if (tree.Value.Comp.Tree.Points < costFp)
            return false;

        tree.Value.Comp.Tree.Points -= costFp;
        Dirty(tree.Value);
        UpdateTree(tree.Value);
        return true;
    }

    // New helper: resolve a team string from an entity (prefers MarineComponent.Faction)
    private string ResolveTeam(EntityUid? uid)
    {
        if (uid is null)
            return Team.None;

        if (TryComp(uid.Value, out MarineComponent? marine) && !string.IsNullOrEmpty(marine.Faction))
            return marine.Faction.ToLowerInvariant();

        return Team.None;
    }

    // Read AU win points from the ObjectiveMaster (server-side) and return as FixedPoint2 for display.
    private FixedPoint2 GetAuWinPoints(string team, FixedPoint2 fallback)
    {
        if (string.IsNullOrEmpty(team) || team == Team.None)
            return fallback;

        // Only query server-side ObjectiveMaster; shared code should not attempt this on clients.
        if (_net.IsClient)
            return fallback;

        var query = EntityQueryEnumerator<Content.Shared.AU14.Objectives.ObjectiveMasterComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            switch (team.ToLowerInvariant())
            {
                case Team.GovFor:
                    return FixedPoint2.New(comp.CurrentWinPointsGovfor);
                case Team.OpFor:
                    return FixedPoint2.New(comp.CurrentWinPointsOpfor);
                case Team.CLF:
                    return FixedPoint2.New(comp.CurrentWinPointsClf);
                default:
                    // support scientist or other factions
                    if (team.ToLowerInvariant() == "scientist")
                        return FixedPoint2.New(comp.CurrentWinPointsScientist);
                    return fallback;
            }
        }

        return fallback;
    }


    public void SetTeamTechTreeOverride(string team, string? protoId)
    {
        if (string.IsNullOrEmpty(team) || team == Team.None)
            return;

        var key = team.ToLowerInvariant();
        if (string.IsNullOrEmpty(protoId))
        {
            _teamTechTreeOverrides.Remove(key);
            return;
        }

        if (!_prototypes.TryIndex<EntityPrototype>(protoId, out _))
            return;

        _teamTechTreeOverrides[key] = protoId;
    }


}
