using System.Numerics;
using Content.Server.Administration;
using Content.Server.Chat.Systems;
using Content.Server.Mind;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Server.Radio.EntitySystems;
using Content.Server._RMC14.Humanoid.Markings;
using Content.Shared._CMU14.DroneOperator;
using Content.Shared._CMU14.Threats.Mobs.Xeno.Caste.Warlock;
using Content.Shared._RMC14.Humanoid.Markings;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Synth;
using Content.Shared.ActionBlocker;
using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Coordinates;
using Content.Shared.Dataset;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.NPC;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Radio.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Tools;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using static Content.Server.Chat.Systems.ChatSystem;

namespace Content.Server._CMU14.DroneOperator;

public sealed partial class CMUDroneOperatorSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedCombatModeSystem _combatMode = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _netMan = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedStatusEffectsSystem _statusEffects = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUXenoWarlockSystem _warlockParticles = default!;

    private static readonly EntProtoId EndControlActionId = "CMUActionDroneEndControl";
    private static readonly TimeSpan LeashCheckInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan BodyMoveGrace = TimeSpan.FromSeconds(0.75);
    private const float TransferParticlePixelsPerMeter = 32f;
    private const float TransferParticleMinVelocity = 4f;
    private const float TransferParticleMaxVelocity = 65f;
    private const string FollowCompoundTask = "CMUDroneFollowCompound";
    private const string FollowCloseRangeKey = "FollowCloseRange";
    private const float BodyMoveGraceDistance = 0.25f;
    private TimeSpan _nextLeashCheck;
    private readonly Dictionary<EntityUid, string> _pendingOperatorEndControls = new();
    private readonly List<(EntityUid Entity, string Reason)> _pendingOperatorEndControlBuffer = new();
    private readonly Dictionary<EntityUid, string> _pendingSessionEndControls = new();
    private readonly List<(EntityUid Entity, string Reason)> _pendingSessionEndControlBuffer = new();
    private EntityQuery<ActorComponent> _actorQuery;
    private EntityQuery<GhostHearingComponent> _ghostHearingQuery;

    public override void Initialize()
    {
        base.Initialize();

        _actorQuery = GetEntityQuery<ActorComponent>();
        _ghostHearingQuery = GetEntityQuery<GhostHearingComponent>();

        SubscribeLocalEvent<CMUDroneOperatorComponent, ComponentStartup>(OnOperatorStartup);
        SubscribeLocalEvent<CMUDroneOperatorComponent, ComponentShutdown>(OnOperatorShutdown);
        SubscribeLocalEvent<CMUDroneOperatorComponent, CMUDroneFollowActionEvent>(OnDroneFollowAction);
        SubscribeLocalEvent<CMUDroneOperatorComponent, CMUDroneStopFollowActionEvent>(OnDroneStopFollowAction);

        SubscribeLocalEvent<CMUDroneFrameComponent, ComponentInit>(OnFrameInit);
        SubscribeLocalEvent<CMUDroneFrameComponent, InteractUsingEvent>(OnFrameInteractUsing);
        SubscribeLocalEvent<CMUDroneFrameComponent, CMUDroneFrameOpenPortsDoAfterEvent>(OnFrameOpenPortsComplete);
        SubscribeLocalEvent<CMUDroneFrameComponent, CMUDroneFrameInstallPartDoAfterEvent>(OnFrameInstallPartComplete);
        SubscribeLocalEvent<CMUDroneFrameComponent, CMUDroneFrameClampPartDoAfterEvent>(OnFrameClampPartComplete);
        SubscribeLocalEvent<CMUDroneFrameComponent, CMUDroneFrameWeldPartDoAfterEvent>(OnFrameWeldPartComplete);
        SubscribeLocalEvent<CMUDroneFrameComponent, CMUDroneFrameActivateDoAfterEvent>(OnFrameActivateComplete);

        SubscribeLocalEvent<CMUDroneControlTabletComponent, UseInHandEvent>(OnTabletUseInHand);
        SubscribeLocalEvent<CMUDroneControlTabletComponent, GotUnequippedEvent>(OnTabletGotUnequipped);
        SubscribeLocalEvent<CMUDroneControlTabletComponent, ComponentShutdown>(OnTabletShutdown);
        SubscribeLocalEvent<CMUDroneControlTabletComponent, EntityTerminatingEvent>(OnTabletTerminating);

        SubscribeLocalEvent<CMUDroneAndroidComponent, ComponentInit>(OnDroneInit);
        SubscribeLocalEvent<CMUDroneAndroidComponent, InteractUsingEvent>(OnDroneInteractUsing);
        SubscribeLocalEvent<CMUDroneAndroidComponent, CMUDroneModuleInstallDoAfterEvent>(OnDroneModuleInstallComplete);
        SubscribeLocalEvent<CMUDroneAndroidComponent, CMUDroneModuleUninstallDoAfterEvent>(OnDroneModuleUninstallComplete);
        SubscribeLocalEvent<CMUDroneAndroidComponent, EntitySpokeEvent>(OnDroneSpoke);
        SubscribeLocalEvent<CMUDroneAndroidComponent, MobStateChangedEvent>(OnDroneMobStateChanged);
        SubscribeLocalEvent<CMUDroneAndroidComponent, RMCSynthRepairToolUseAttemptEvent>(OnDroneRepairAttempt);
        SubscribeLocalEvent<CMUDroneAndroidComponent, GetAdditionalAccessEvent>(OnDroneGetAdditionalAccess);
        SubscribeLocalEvent<CMUDroneAndroidComponent, GetVerbsEvent<Verb>>(OnDroneGetVerbs);
        SubscribeLocalEvent<CMUDroneAndroidComponent, MapInitEvent>(OnDroneMapInit, after: [typeof(SSDIndicatorSystem), typeof(RMCIntentsEyeColorSystem)]);
        SubscribeLocalEvent<CMUDroneAndroidComponent, PlayerAttachedEvent>(OnDronePlayerAttached, after: [typeof(SSDIndicatorSystem)]);
        SubscribeLocalEvent<CMUDroneAndroidComponent, PlayerDetachedEvent>(OnDronePlayerDetached, after: [typeof(SSDIndicatorSystem)]);
        SubscribeLocalEvent<CMUDroneAndroidComponent, ComponentShutdown>(OnDroneShutdown);
        SubscribeLocalEvent<CMUDroneAndroidComponent, EntityTerminatingEvent>(OnDroneTerminating);

        SubscribeLocalEvent<CMUDroneControlSessionComponent, CMUDroneEndControlActionEvent>(OnDroneEndControlAction);

        SubscribeLocalEvent<CMURemotePilotingComponent, ComponentStartup>(OnPilotingStartup);
        SubscribeLocalEvent<CMURemotePilotingComponent, ComponentShutdown>(OnPilotingShutdown);
        SubscribeLocalEvent<CMURemotePilotingComponent, PlayerAttachedEvent>(OnPilotingPlayerAttached, after: [typeof(SSDIndicatorSystem)]);
        SubscribeLocalEvent<CMURemotePilotingComponent, PlayerDetachedEvent>(OnPilotingPlayerDetached, after: [typeof(SSDIndicatorSystem)]);
        SubscribeLocalEvent<CMURemotePilotingComponent, HeadsetRadioReceiveRelayEvent>(OnPilotingHeadsetReceive);
        SubscribeLocalEvent<CMURemotePilotingComponent, UpdateCanMoveEvent>(OnPilotingUpdateCanMove);
        SubscribeLocalEvent<CMURemotePilotingComponent, MoveEvent>(OnPilotingMove);
        SubscribeLocalEvent<CMURemotePilotingComponent, MobStateChangedEvent>(OnPilotingMobStateChanged);
        SubscribeLocalEvent<CMURemotePilotingComponent, UseAttemptEvent>(OnPilotingAttempt);
        SubscribeLocalEvent<CMURemotePilotingComponent, PickupAttemptEvent>(OnPilotingAttempt);
        SubscribeLocalEvent<CMURemotePilotingComponent, DropAttemptEvent>(OnPilotingAttempt);
        SubscribeLocalEvent<CMURemotePilotingComponent, ThrowAttemptEvent>(OnPilotingAttempt);
        SubscribeLocalEvent<CMURemotePilotingComponent, AttackAttemptEvent>(OnPilotingAttempt);
        SubscribeLocalEvent<CMURemotePilotingComponent, ChangeDirectionAttemptEvent>(OnPilotingAttempt);
        SubscribeLocalEvent<CMURemotePilotingComponent, InteractionAttemptEvent>(OnPilotingInteractionAttempt);
        SubscribeLocalEvent<CMURemotePilotingComponent, PullAttemptEvent>(OnPilotingPullAttempt);

        SubscribeLocalEvent<ExpandICChatRecipientsEvent>(OnExpandChatRecipients);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        FlushPendingOperatorEndControls();
        FlushPendingSessionEndControls();

        if (_timing.CurTime < _nextLeashCheck)
            return;

        _nextLeashCheck = _timing.CurTime + LeashCheckInterval;

        var query = EntityQueryEnumerator<CMUDroneControlSessionComponent>();
        while (query.MoveNext(out var drone, out var session))
        {
            ValidateSession((drone, session));
        }
    }

    private void OnOperatorStartup(Entity<CMUDroneOperatorComponent> ent, ref ComponentStartup args)
    {
        AddOperatorActions(ent);
    }

    private void OnOperatorShutdown(Entity<CMUDroneOperatorComponent> ent, ref ComponentShutdown args)
    {
        RemoveOperatorActions(ent);
        SetOperatorTransferEffect(ent, false);

        if (ent.Comp.Drone is { } droneUid &&
            !TerminatingOrDeleted(droneUid) &&
            TryComp<CMUDroneAndroidComponent>(droneUid, out var drone))
        {
            StopDroneFollowing((droneUid, drone), null, false);
        }
    }

    private void OnDroneFollowAction(Entity<CMUDroneOperatorComponent> ent, ref CMUDroneFollowActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        args.Handled = true;

        if (!TryGetLinkedDroneForCommand(ent, out var drone, out var tablet, out var reason) ||
            !CanCommandLinkedDrone(drone, tablet, ent.Owner, out reason))
        {
            _popup.PopupEntity(reason, ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        if (drone.Comp.FollowingOperator)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-drone-follow-already", ("drone", drone.Owner)),
                ent.Owner,
                ent.Owner,
                PopupType.SmallCaution);
            return;
        }

        StartDroneFollowing(ent.Owner, drone);
    }

    private void OnDroneStopFollowAction(Entity<CMUDroneOperatorComponent> ent, ref CMUDroneStopFollowActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        args.Handled = true;

        if (!TryGetLinkedDroneForCommand(ent, out var drone, out var tablet, out var reason) ||
            !CanCommandLinkedDrone(drone, tablet, ent.Owner, out reason))
        {
            _popup.PopupEntity(reason, ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        StopDroneFollowing(drone, ent.Owner, true);
    }

    private void OnFrameInit(Entity<CMUDroneFrameComponent> ent, ref ComponentInit args)
    {
        EnsureFramePartsContainer(ent);
        EnsureFramePartStates(ent);
    }

    private void OnFrameInteractUsing(Entity<CMUDroneFrameComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!CanWorkOnFrame(ent, args.User, out var reason))
        {
            _popup.PopupEntity(reason, ent.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        if (IsEntityContainedBy(ent.Owner, args.User))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-drone-frame-must-place"),
                ent.Owner,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        if (TryComp<CMUDroneAssemblyPartComponent>(args.Used, out var part))
        {
            TryStartFramePartInstall(ent, (args.Used, part), args.User);
            return;
        }

        if (TryComp<CMUDroneSynthKeyComponent>(args.Used, out var key))
        {
            TryStartFrameActivation(ent, (args.Used, key), args.User);
            return;
        }

        if (TryComp<ToolComponent>(args.Used, out var tool))
        {
            TryStartFrameToolStep(ent, (args.Used, tool), args.User);
            return;
        }

        args.Handled = false;
    }

    private void OnFrameOpenPortsComplete(Entity<CMUDroneFrameComponent> ent, ref CMUDroneFrameOpenPortsDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled ||
            ent.Comp.PortsOpen ||
            !CanWorkOnFrame(ent, args.User, out _))
        {
            return;
        }

        ent.Comp.PortsOpen = true;
        _popup.PopupEntity(Loc.GetString("cmu-drone-frame-open-finish", ("frame", ent.Owner)), ent.Owner, args.User);
    }

    private void OnFrameInstallPartComplete(Entity<CMUDroneFrameComponent> ent, ref CMUDroneFrameInstallPartDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled ||
            args.Used is not { } partUid ||
            !TryComp<CMUDroneAssemblyPartComponent>(partUid, out var part) ||
            !CanInstallFramePart(ent, (partUid, part), args.User, out _))
        {
            return;
        }

        var container = EnsureFramePartsContainer(ent);
        if (!_containers.Insert(partUid, container))
            return;

        ent.Comp.PartStates[part.Part] = CMUDroneAssemblyPartState.Installed;
        ent.Comp.InstalledParts[part.Part] = partUid;

        _popup.PopupEntity(
            Loc.GetString("cmu-drone-frame-part-install-finish", ("part", partUid), ("frame", ent.Owner)),
            ent.Owner,
            args.User);
    }

    private void OnFrameClampPartComplete(Entity<CMUDroneFrameComponent> ent, ref CMUDroneFrameClampPartDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled ||
            !CanAdvanceFramePart(ent, args.Part, CMUDroneAssemblyPartState.Installed, args.User, out _))
        {
            return;
        }

        ent.Comp.PartStates[args.Part] = CMUDroneAssemblyPartState.Clamped;
        _popup.PopupEntity(
            Loc.GetString("cmu-drone-frame-part-clamp-finish", ("part", GetFramePartName(args.Part)), ("frame", ent.Owner)),
            ent.Owner,
            args.User);
    }

    private void OnFrameWeldPartComplete(Entity<CMUDroneFrameComponent> ent, ref CMUDroneFrameWeldPartDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled ||
            !CanAdvanceFramePart(ent, args.Part, CMUDroneAssemblyPartState.Clamped, args.User, out _))
        {
            return;
        }

        ent.Comp.PartStates[args.Part] = CMUDroneAssemblyPartState.Welded;
        _popup.PopupEntity(
            Loc.GetString("cmu-drone-frame-part-weld-finish", ("part", GetFramePartName(args.Part)), ("frame", ent.Owner)),
            ent.Owner,
            args.User);
    }

    private void OnFrameActivateComplete(Entity<CMUDroneFrameComponent> ent, ref CMUDroneFrameActivateDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        string? reason = null;
        if (args.Cancelled ||
            args.Used is not { } key ||
            !TryComp<CMUDroneSynthKeyComponent>(key, out _) ||
            !CanActivateFrame(ent, args.User, out reason))
        {
            if (!args.Cancelled && !string.IsNullOrEmpty(reason))
                _popup.PopupEntity(reason, ent.Owner, args.User, PopupType.SmallCaution);

            return;
        }

        ActivateFrame(ent, args.User, key);
    }

    private void OnTabletUseInHand(Entity<CMUDroneControlTabletComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (TryGetActiveSession(ent.Owner, out var active))
        {
            EndControl(active, Loc.GetString("cmu-drone-control-ended-manual"));
            return;
        }

        TryStartControl(ent, args.User);
    }

    private void OnTabletGotUnequipped(Entity<CMUDroneControlTabletComponent> ent, ref GotUnequippedEvent args)
    {
        EndControlForTablet(ent.Owner, Loc.GetString("cmu-drone-control-ended-tablet-removed"));
    }

    private void OnTabletShutdown(Entity<CMUDroneControlTabletComponent> ent, ref ComponentShutdown args)
    {
        EndControlForTablet(ent.Owner, Loc.GetString("cmu-drone-control-ended-tablet-lost"));
    }

    private void OnTabletTerminating(Entity<CMUDroneControlTabletComponent> ent, ref EntityTerminatingEvent args)
    {
        EndControlForTablet(ent.Owner, Loc.GetString("cmu-drone-control-ended-tablet-lost"));
    }

    private void OnDroneInit(Entity<CMUDroneAndroidComponent> ent, ref ComponentInit args)
    {
        EnsureModuleContainer(ent);
    }

    private void OnDroneInteractUsing(Entity<CMUDroneAndroidComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<CMUDroneControlTabletComponent>(args.Used, out var tabletComp))
        {
            args.Handled = true;
            TryLinkTablet((args.Used, tabletComp), args.User, ent.Owner);
            return;
        }

        if (TryComp<CMUDroneModuleComponent>(args.Used, out var module))
        {
            args.Handled = TryStartModuleInstall(ent, (args.Used, module), args.User);
            return;
        }

        if (ent.Comp.InstalledModule != null && TryComp<ToolComponent>(args.Used, out var tool))
            args.Handled = TryStartModuleUninstall(ent, (args.Used, tool), args.User);
    }

    private void OnDroneModuleInstallComplete(Entity<CMUDroneAndroidComponent> ent, ref CMUDroneModuleInstallDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled ||
            args.Used is not { } moduleUid ||
            !TryComp<CMUDroneModuleComponent>(moduleUid, out var module) ||
            !CanFinishModuleInstall(ent, (moduleUid, module), args.User))
        {
            return;
        }

        InstallDroneModule(ent, (moduleUid, module), args.User);
    }

    private void OnDroneModuleUninstallComplete(Entity<CMUDroneAndroidComponent> ent, ref CMUDroneModuleUninstallDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled ||
            args.Used is not { } tool ||
            !TryComp<ToolComponent>(tool, out var toolComp) ||
            !TryGetInstalledDroneModule(ent, out var module) ||
            !_tool.HasQuality(tool, module.Comp.InstallTool, toolComp))
        {
            return;
        }

        _tool.PlayToolSound(tool, toolComp, args.User);
        UninstallDroneModule(ent, args.User, module);
    }

    private void OnDroneSpoke(Entity<CMUDroneAndroidComponent> ent, ref EntitySpokeEvent args)
    {
        if (args.Source != ent.Owner ||
            !TryComp<CMUDroneControlSessionComponent>(ent.Owner, out var session) ||
            TerminatingOrDeleted(session.Operator))
        {
            return;
        }

        if (args.Channel != null &&
            TryComp<WearingHeadsetComponent>(session.Operator, out var wearing) &&
            TryComp<EncryptionKeyHolderComponent>(wearing.Headset, out var keys) &&
            keys.Channels.Contains(args.Channel.ID) &&
            !keys.ReadOnlyChannels.Contains(args.Channel.ID))
        {
            _radio.SendRadioMessage(session.Operator, args.Message, args.Channel, wearing.Headset, args.Language);
            args.Channel = null;
            return;
        }

        _chat.TrySendInGameICMessage(
            session.Operator,
            args.Message,
            InGameICChatType.Whisper,
            ChatTransmitRange.GhostRangeLimit,
            hideLog: true,
            checkRadioPrefix: false,
            ignoreActionBlocker: true);
    }

    private void OnDroneMobStateChanged(Entity<CMUDroneAndroidComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        SetDroneDormantEffect(ent, false);
        StopDroneFollowing(ent, null, false);
        EndControlForDrone(ent.Owner, Loc.GetString("cmu-drone-control-ended-drone-disabled"));
    }

    private void OnDroneRepairAttempt(Entity<CMUDroneAndroidComponent> ent, ref RMCSynthRepairToolUseAttemptEvent args)
    {
        if (args.User != ent.Owner)
            return;

        args.Handled = true;
        _popup.PopupEntity(
            Loc.GetString("cmu-drone-self-repair-blocked"),
            ent.Owner,
            ent.Owner,
            PopupType.SmallCaution);
    }

    private void OnDroneGetAdditionalAccess(Entity<CMUDroneAndroidComponent> ent, ref GetAdditionalAccessEvent args)
    {
        if (!TryComp<CMUDroneControlSessionComponent>(ent.Owner, out var session) ||
            TerminatingOrDeleted(session.Operator))
        {
            return;
        }

        args.Entities.Add(session.Operator);
    }

    private void OnDroneGetVerbs(Entity<CMUDroneAndroidComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || !CanRenameDrone(ent, args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-drone-rename-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/pencil.png")),
            Priority = -4,
            Act = () => OpenDroneRenameDialog(ent, user),
        });
    }

    private void OnDroneMapInit(Entity<CMUDroneAndroidComponent> ent, ref MapInitEvent args)
    {
        AssignRandomDroneName(ent);
        SuppressSsdIndicator(ent.Owner);
        RefreshDroneDormantEffect(ent);
    }

    private void OnDronePlayerAttached(Entity<CMUDroneAndroidComponent> ent, ref PlayerAttachedEvent args)
    {
        SuppressSsdIndicator(ent.Owner);
        RefreshDroneDormantEffect(ent);
    }

    private void OnDronePlayerDetached(Entity<CMUDroneAndroidComponent> ent, ref PlayerDetachedEvent args)
    {
        SuppressSsdIndicator(ent.Owner);
        RefreshDroneDormantEffect(ent);
    }

    private void OnDroneShutdown(Entity<CMUDroneAndroidComponent> ent, ref ComponentShutdown args)
    {
        SetDroneDormantEffect(ent, false);
        StopDroneFollowing(ent, null, false);
        EndControlForDrone(ent.Owner, Loc.GetString("cmu-drone-control-ended-drone-lost"));
    }

    private void OnDroneTerminating(Entity<CMUDroneAndroidComponent> ent, ref EntityTerminatingEvent args)
    {
        SetDroneDormantEffect(ent, false);
        StopDroneFollowing(ent, null, false);
        EndControlForDrone(ent.Owner, Loc.GetString("cmu-drone-control-ended-drone-lost"));
        SpawnRuinedCore(ent);
    }

    private void OnDroneEndControlAction(Entity<CMUDroneControlSessionComponent> ent, ref CMUDroneEndControlActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        EndControl(ent, Loc.GetString("cmu-drone-control-ended-manual"));
    }

    private bool TryStartModuleInstall(
        Entity<CMUDroneAndroidComponent> drone,
        Entity<CMUDroneModuleComponent> module,
        EntityUid user)
    {
        if (!TryGetHeldTool(user, module.Comp.InstallTool, module.Owner, out _))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-drone-module-tool-required", ("module", module.Owner)),
                user,
                user,
                PopupType.SmallCaution);
            return true;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            module.Comp.InstallDelay,
            new CMUDroneModuleInstallDoAfterEvent(),
            drone.Owner,
            target: drone.Owner,
            used: module.Owner)
        {
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget | DuplicateConditions.SameTool,
            ExtraCheck = () => CanFinishModuleInstall(drone, module, user),
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return true;

        _popup.PopupEntity(
            Loc.GetString("cmu-drone-module-install-start", ("module", module.Owner), ("drone", drone.Owner)),
            user,
            user);
        return true;
    }

    private bool TryStartModuleUninstall(
        Entity<CMUDroneAndroidComponent> drone,
        Entity<ToolComponent> tool,
        EntityUid user)
    {
        if (!TryGetInstalledDroneModule(drone, out var module) ||
            !_tool.HasQuality(tool.Owner, module.Comp.InstallTool, tool.Comp))
        {
            return false;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            module.Comp.InstallDelay,
            new CMUDroneModuleUninstallDoAfterEvent(),
            drone.Owner,
            target: drone.Owner,
            used: tool.Owner)
        {
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            NeedHand = tool.Owner != user,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget | DuplicateConditions.SameTool,
            ExtraCheck = () =>
                !TerminatingOrDeleted(drone.Owner) &&
                TryGetInstalledDroneModule(drone, out var installed) &&
                installed.Owner == module.Owner &&
                _hands.IsHolding(user, tool.Owner) &&
                _tool.HasQuality(tool.Owner, installed.Comp.InstallTool, tool.Comp),
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return true;

        _popup.PopupEntity(
            Loc.GetString("cmu-drone-module-remove-start", ("module", module.Owner), ("drone", drone.Owner)),
            user,
            user);
        return true;
    }

    private bool CanFinishModuleInstall(
        Entity<CMUDroneAndroidComponent> drone,
        Entity<CMUDroneModuleComponent> module,
        EntityUid user)
    {
        return !TerminatingOrDeleted(drone.Owner) &&
               !TerminatingOrDeleted(module.Owner) &&
               _hands.IsHolding(user, module.Owner) &&
               TryGetHeldTool(user, module.Comp.InstallTool, module.Owner, out _);
    }

    private void InstallDroneModule(
        Entity<CMUDroneAndroidComponent> drone,
        Entity<CMUDroneModuleComponent> module,
        EntityUid user)
    {
        var container = EnsureModuleContainer(drone);
        if (container.ContainedEntity is { } installed)
            EjectDroneModule(drone, installed, user);

        if (!_containers.Insert(module.Owner, container))
            return;

        drone.Comp.InstalledModule = module.Owner;

        if (TryGetHeldTool(user, module.Comp.InstallTool, module.Owner, out var tool))
            _tool.PlayToolSound(tool.Owner, tool.Comp, user);

        _popup.PopupEntity(
            Loc.GetString("cmu-drone-module-install-finish", ("module", module.Owner), ("drone", drone.Owner)),
            user,
            user);

        if (TryComp<CMUDroneControlSessionComponent>(drone.Owner, out var session))
            RefreshDroneSkills((drone.Owner, session));
    }

    private void UninstallDroneModule(
        Entity<CMUDroneAndroidComponent> drone,
        EntityUid user,
        Entity<CMUDroneModuleComponent> module)
    {
        EjectDroneModule(drone, module.Owner, user);
        _popup.PopupEntity(
            Loc.GetString("cmu-drone-module-remove-finish", ("module", module.Owner), ("drone", drone.Owner)),
            user,
            user);

        if (TryComp<CMUDroneControlSessionComponent>(drone.Owner, out var session))
            RefreshDroneSkills((drone.Owner, session));
    }

    private void EjectDroneModule(Entity<CMUDroneAndroidComponent> drone, EntityUid module, EntityUid user)
    {
        var container = EnsureModuleContainer(drone);
        if (container.ContainedEntity == module)
            _containers.Remove(module, container, force: true);

        if (drone.Comp.InstalledModule == module)
            drone.Comp.InstalledModule = null;

        _hands.PickupOrDrop(user, module, dropNear: true);
    }

    private ContainerSlot EnsureModuleContainer(Entity<CMUDroneAndroidComponent> drone)
    {
        drone.Comp.ModuleContainer ??= _containers.EnsureContainer<ContainerSlot>(drone.Owner, drone.Comp.ModuleContainerId);
        drone.Comp.ModuleContainer.OccludesLight = false;
        return drone.Comp.ModuleContainer;
    }

    private bool TryGetInstalledDroneModule(
        Entity<CMUDroneAndroidComponent> drone,
        out Entity<CMUDroneModuleComponent> module)
    {
        var container = EnsureModuleContainer(drone);
        var moduleUid = drone.Comp.InstalledModule;
        if ((moduleUid == null || container.ContainedEntity != moduleUid) &&
            container.ContainedEntity is { } contained)
        {
            moduleUid = contained;
        }

        if (moduleUid is { } uid &&
            !TerminatingOrDeleted(uid) &&
            TryComp<CMUDroneModuleComponent>(uid, out var moduleComp))
        {
            drone.Comp.InstalledModule = uid;
            module = (uid, moduleComp);
            return true;
        }

        drone.Comp.InstalledModule = null;
        module = default;
        return false;
    }

    private bool TryGetHeldTool(
        EntityUid user,
        ProtoId<ToolQualityPrototype> quality,
        EntityUid except,
        out Entity<ToolComponent> heldTool)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (held == except ||
                !TryComp<ToolComponent>(held, out var tool) ||
                !_tool.HasQuality(held, quality, tool))
            {
                continue;
            }

            heldTool = (held, tool);
            return true;
        }

        heldTool = default;
        return false;
    }

    private void OnPilotingStartup(Entity<CMURemotePilotingComponent> ent, ref ComponentStartup args)
    {
        _blocker.UpdateCanMove(ent.Owner);
    }

    private void OnPilotingShutdown(Entity<CMURemotePilotingComponent> ent, ref ComponentShutdown args)
    {
        RestoreOperatorSsdIndicator(ent);
        if (TryComp<CMUDroneOperatorComponent>(ent.Owner, out var operatorComp))
            SetOperatorTransferEffect((ent.Owner, operatorComp), false);

        _blocker.UpdateCanMove(ent.Owner);
    }

    private void OnPilotingPlayerAttached(Entity<CMURemotePilotingComponent> ent, ref PlayerAttachedEvent args)
    {
        SuppressSsdIndicator(ent.Owner);
    }

    private void OnPilotingPlayerDetached(Entity<CMURemotePilotingComponent> ent, ref PlayerDetachedEvent args)
    {
        SuppressSsdIndicator(ent.Owner);
    }

    private void OnPilotingHeadsetReceive(Entity<CMURemotePilotingComponent> ent, ref HeadsetRadioReceiveRelayEvent args)
    {
        if (_actorQuery.TryComp(ent.Comp.Drone, out var actor))
            _netMan.ServerSendMessage(args.RelayedEvent.ChatMsg, actor.PlayerSession.Channel);
    }

    private void OnPilotingUpdateCanMove(Entity<CMURemotePilotingComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.BlocksInput)
            args.Cancel();
    }

    private void OnPilotingMove(Entity<CMURemotePilotingComponent> ent, ref MoveEvent args)
    {
        if (!ent.Comp.BlocksInput)
            return;

        if (args.OldPosition == args.NewPosition && !args.ParentChanged)
            return;

        if (ShouldIgnoreBodyMove(ent, args))
            return;

        QueueEndControlForOperator(ent.Owner, Loc.GetString("cmu-drone-control-ended-operator-moved"));
    }

    private void OnPilotingMobStateChanged(Entity<CMURemotePilotingComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        EndControlForOperator(ent.Owner, Loc.GetString("cmu-drone-control-ended-operator-disabled"));
    }

    private void OnPilotingAttempt(EntityUid uid, CMURemotePilotingComponent component, CancellableEntityEventArgs args)
    {
        if (component.BlocksInput)
            args.Cancel();
    }

    private void OnPilotingInteractionAttempt(Entity<CMURemotePilotingComponent> ent, ref InteractionAttemptEvent args)
    {
        if (ent.Comp.BlocksInput)
            args.Cancelled = true;
    }

    private void OnPilotingPullAttempt(Entity<CMURemotePilotingComponent> ent, ref PullAttemptEvent args)
    {
        if (ent.Comp.BlocksInput)
            args.Cancelled = true;
    }

    private void OnExpandChatRecipients(ExpandICChatRecipientsEvent ev)
    {
        var query = EntityQueryEnumerator<CMUDroneControlSessionComponent>();
        while (query.MoveNext(out var drone, out var session))
        {
            if (!_actorQuery.TryComp(drone, out var actor) ||
                TerminatingOrDeleted(session.Operator))
            {
                continue;
            }

            if (!TryDistance(ev.Source, session.Operator, out var distance) ||
                distance > ev.VoiceRange)
            {
                continue;
            }

            ev.Recipients.TryAdd(actor.PlayerSession, new ICChatRecipientData(distance, _ghostHearingQuery.HasComp(drone)));
        }
    }

    private bool TryStartFramePartInstall(
        Entity<CMUDroneFrameComponent> frame,
        Entity<CMUDroneAssemblyPartComponent> part,
        EntityUid user)
    {
        if (!CanInstallFramePart(frame, part, user, out var reason))
        {
            _popup.PopupEntity(reason, frame.Owner, user, PopupType.SmallCaution);
            return false;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            frame.Comp.InstallDelay,
            new CMUDroneFrameInstallPartDoAfterEvent(),
            frame.Owner,
            target: frame.Owner,
            used: part.Owner)
        {
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget | DuplicateConditions.SameTool,
            ExtraCheck = () => CanInstallFramePart(frame, part, user, out _),
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(
            Loc.GetString("cmu-drone-frame-part-install-start", ("part", part.Owner), ("frame", frame.Owner)),
            frame.Owner,
            user);
        return true;
    }

    private bool TryStartFrameActivation(
        Entity<CMUDroneFrameComponent> frame,
        Entity<CMUDroneSynthKeyComponent> key,
        EntityUid user)
    {
        if (!CanActivateFrame(frame, user, out var reason))
        {
            _popup.PopupEntity(reason, frame.Owner, user, PopupType.SmallCaution);
            return false;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            frame.Comp.ActivateDelay,
            new CMUDroneFrameActivateDoAfterEvent(),
            frame.Owner,
            target: frame.Owner,
            used: key.Owner)
        {
            BreakOnDropItem = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget | DuplicateConditions.SameTool,
            ExtraCheck = () => CanActivateFrame(frame, user, out _) && _hands.IsHolding(user, key.Owner),
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(
            Loc.GetString("cmu-drone-frame-activate-start", ("frame", frame.Owner)),
            frame.Owner,
            user);
        return true;
    }

    private bool TryStartFrameToolStep(
        Entity<CMUDroneFrameComponent> frame,
        Entity<ToolComponent> tool,
        EntityUid user)
    {
        EnsureFramePartStates(frame);

        if (!frame.Comp.PortsOpen)
        {
            if (!_tool.HasQuality(tool.Owner, frame.Comp.OpenTool, tool.Comp))
            {
                _popup.PopupEntity(Loc.GetString("cmu-drone-frame-open-tool-required"), frame.Owner, user, PopupType.SmallCaution);
                return true;
            }

            var started = _tool.UseTool(
                tool.Owner,
                user,
                frame.Owner,
                (float) frame.Comp.OpenDelay.TotalSeconds,
                frame.Comp.OpenTool,
                new CMUDroneFrameOpenPortsDoAfterEvent(),
                duplicateCondition: DuplicateConditions.SameEvent | DuplicateConditions.SameTarget | DuplicateConditions.SameTool);

            if (started)
                _popup.PopupEntity(Loc.GetString("cmu-drone-frame-open-start", ("frame", frame.Owner)), frame.Owner, user);

            return true;
        }

        if (_tool.HasQuality(tool.Owner, frame.Comp.ClampTool, tool.Comp) &&
            TryGetFramePartWithState(frame, CMUDroneAssemblyPartState.Installed, out var clampPart))
        {
            var started = _tool.UseTool(
                tool.Owner,
                user,
                frame.Owner,
                (float) frame.Comp.ClampDelay.TotalSeconds,
                frame.Comp.ClampTool,
                new CMUDroneFrameClampPartDoAfterEvent(clampPart),
                duplicateCondition: DuplicateConditions.SameEvent | DuplicateConditions.SameTarget | DuplicateConditions.SameTool);

            if (started)
            {
                _popup.PopupEntity(
                    Loc.GetString("cmu-drone-frame-part-clamp-start", ("part", GetFramePartName(clampPart)), ("frame", frame.Owner)),
                    frame.Owner,
                    user);
            }

            return true;
        }

        if (_tool.HasQuality(tool.Owner, frame.Comp.WeldTool, tool.Comp) &&
            TryGetFramePartWithState(frame, CMUDroneAssemblyPartState.Clamped, out var weldPart))
        {
            var started = _tool.UseTool(
                tool.Owner,
                user,
                frame.Owner,
                (float) frame.Comp.WeldDelay.TotalSeconds,
                frame.Comp.WeldTool,
                new CMUDroneFrameWeldPartDoAfterEvent(weldPart),
                frame.Comp.WeldFuel,
                duplicateCondition: DuplicateConditions.SameEvent | DuplicateConditions.SameTarget | DuplicateConditions.SameTool);

            if (started)
            {
                _popup.PopupEntity(
                    Loc.GetString("cmu-drone-frame-part-weld-start", ("part", GetFramePartName(weldPart)), ("frame", frame.Owner)),
                    frame.Owner,
                    user);
            }

            return true;
        }

        if (TryGetFramePartWithState(frame, CMUDroneAssemblyPartState.Installed, out var loosePart))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-drone-frame-clamp-tool-required", ("part", GetFramePartName(loosePart))),
                frame.Owner,
                user,
                PopupType.SmallCaution);
            return true;
        }

        if (TryGetFramePartWithState(frame, CMUDroneAssemblyPartState.Clamped, out var clampedPart))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-drone-frame-weld-tool-required", ("part", GetFramePartName(clampedPart))),
                frame.Owner,
                user,
                PopupType.SmallCaution);
            return true;
        }

        _popup.PopupEntity(
            IsFrameComplete(frame)
                ? Loc.GetString("cmu-drone-frame-ready-key")
                : Loc.GetString("cmu-drone-frame-missing-parts"),
            frame.Owner,
            user,
            PopupType.SmallCaution);
        return true;
    }

    private bool CanWorkOnFrame(Entity<CMUDroneFrameComponent> frame, EntityUid user, out string reason)
    {
        reason = string.Empty;

        if (TerminatingOrDeleted(frame.Owner))
        {
            reason = Loc.GetString("cmu-drone-control-ended-link-lost");
            return false;
        }

        if (!TryComp<CMUDroneOperatorComponent>(user, out var operatorComp))
        {
            reason = Loc.GetString("cmu-drone-operator-required");
            return false;
        }

        if (HasExistingDrone((user, operatorComp)))
        {
            reason = Loc.GetString("cmu-drone-assembly-existing");
            return false;
        }

        return true;
    }

    private bool CanInstallFramePart(
        Entity<CMUDroneFrameComponent> frame,
        Entity<CMUDroneAssemblyPartComponent> part,
        EntityUid user,
        out string reason)
    {
        EnsureFramePartStates(frame);
        reason = string.Empty;

        if (!CanWorkOnFrame(frame, user, out reason))
            return false;

        if (IsEntityContainedBy(frame.Owner, user))
        {
            reason = Loc.GetString("cmu-drone-frame-must-place");
            return false;
        }

        if (!frame.Comp.PortsOpen)
        {
            reason = Loc.GetString("cmu-drone-frame-ports-closed");
            return false;
        }

        if (!frame.Comp.RequiredParts.Contains(part.Comp.Part))
        {
            reason = Loc.GetString("cmu-drone-frame-part-invalid");
            return false;
        }

        if (frame.Comp.PartStates.GetValueOrDefault(part.Comp.Part) != CMUDroneAssemblyPartState.Missing)
        {
            reason = Loc.GetString("cmu-drone-frame-part-occupied", ("part", GetFramePartName(part.Comp.Part)));
            return false;
        }

        if (!_hands.IsHolding(user, part.Owner))
        {
            reason = Loc.GetString("cmu-drone-frame-part-must-hold", ("part", part.Owner));
            return false;
        }

        return true;
    }

    private bool CanAdvanceFramePart(
        Entity<CMUDroneFrameComponent> frame,
        CMUDroneAssemblyPartSlot part,
        CMUDroneAssemblyPartState requiredState,
        EntityUid user,
        out string reason)
    {
        EnsureFramePartStates(frame);
        reason = string.Empty;

        if (!CanWorkOnFrame(frame, user, out reason))
            return false;

        if (!frame.Comp.PartStates.TryGetValue(part, out var state) || state != requiredState)
        {
            reason = Loc.GetString("cmu-drone-frame-incomplete");
            return false;
        }

        return true;
    }

    private bool CanActivateFrame(Entity<CMUDroneFrameComponent> frame, EntityUid user, out string reason)
    {
        if (!CanWorkOnFrame(frame, user, out reason))
            return false;

        if (IsEntityContainedBy(frame.Owner, user))
        {
            reason = Loc.GetString("cmu-drone-frame-must-place");
            return false;
        }

        if (!IsFrameComplete(frame))
        {
            reason = Loc.GetString("cmu-drone-frame-incomplete");
            return false;
        }

        return true;
    }

    private void ActivateFrame(Entity<CMUDroneFrameComponent> frame, EntityUid user, EntityUid key)
    {
        if (!TryComp<CMUDroneOperatorComponent>(user, out var operatorComp))
            return;

        var xform = Transform(frame.Owner);
        var drone = Spawn(frame.Comp.DronePrototype, xform.Coordinates);
        _transform.SetLocalRotation(drone, xform.LocalRotation);

        var droneComp = EnsureComp<CMUDroneAndroidComponent>(drone);
        droneComp.Operator = user;
        SuppressSsdIndicator(drone);
        RefreshDroneDormantEffect((drone, droneComp));

        operatorComp.Drone = drone;

        if (TryFindCarriedTablet(user, out var tablet))
            TryLinkTablet(tablet, user, drone, silent: true);

        _popup.PopupEntity(
            Loc.GetString("cmu-drone-assembly-finish", ("drone", drone)),
            drone,
            user);

        QueueDel(key);
        QueueDel(frame.Owner);
    }

    private Container EnsureFramePartsContainer(Entity<CMUDroneFrameComponent> frame)
    {
        frame.Comp.PartsContainer ??= _containers.EnsureContainer<Container>(frame.Owner, frame.Comp.PartsContainerId);
        return frame.Comp.PartsContainer;
    }

    private void EnsureFramePartStates(Entity<CMUDroneFrameComponent> frame)
    {
        foreach (var part in frame.Comp.RequiredParts)
        {
            frame.Comp.PartStates.TryAdd(part, CMUDroneAssemblyPartState.Missing);
        }
    }

    private bool TryGetFramePartWithState(
        Entity<CMUDroneFrameComponent> frame,
        CMUDroneAssemblyPartState state,
        out CMUDroneAssemblyPartSlot part)
    {
        EnsureFramePartStates(frame);

        foreach (var required in frame.Comp.RequiredParts)
        {
            if (frame.Comp.PartStates.GetValueOrDefault(required) != state)
                continue;

            part = required;
            return true;
        }

        part = default;
        return false;
    }

    private bool IsFrameComplete(Entity<CMUDroneFrameComponent> frame)
    {
        EnsureFramePartStates(frame);

        foreach (var part in frame.Comp.RequiredParts)
        {
            if (frame.Comp.PartStates.GetValueOrDefault(part) != CMUDroneAssemblyPartState.Welded)
                return false;
        }

        return true;
    }

    private string GetFramePartName(CMUDroneAssemblyPartSlot part)
    {
        return Loc.GetString(part switch
        {
            CMUDroneAssemblyPartSlot.Head => "cmu-drone-frame-part-head",
            CMUDroneAssemblyPartSlot.LeftArm => "cmu-drone-frame-part-left-arm",
            CMUDroneAssemblyPartSlot.RightArm => "cmu-drone-frame-part-right-arm",
            CMUDroneAssemblyPartSlot.LeftLeg => "cmu-drone-frame-part-left-leg",
            CMUDroneAssemblyPartSlot.RightLeg => "cmu-drone-frame-part-right-leg",
            _ => "cmu-drone-frame-part-unknown",
        });
    }

    private void AddOperatorActions(Entity<CMUDroneOperatorComponent> ent)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.FollowAction, ent.Comp.FollowActionId);
        _actions.AddAction(ent.Owner, ref ent.Comp.StopFollowAction, ent.Comp.StopFollowActionId);
    }

    private void RemoveOperatorActions(Entity<CMUDroneOperatorComponent> ent)
    {
        if (ent.Comp.FollowAction is { } followAction)
            _actions.RemoveAction(ent.Owner, followAction);

        if (ent.Comp.StopFollowAction is { } stopFollowAction)
            _actions.RemoveAction(ent.Owner, stopFollowAction);

        ent.Comp.FollowAction = null;
        ent.Comp.StopFollowAction = null;
    }

    private bool TryGetLinkedDroneForCommand(
        Entity<CMUDroneOperatorComponent> ent,
        out Entity<CMUDroneAndroidComponent> drone,
        out Entity<CMUDroneControlTabletComponent> tablet,
        out string reason)
    {
        drone = default;
        tablet = default;
        reason = Loc.GetString("cmu-drone-follow-no-link");

        EntityUid? tabletUid = null;
        CMUDroneControlTabletComponent? tabletComp = null;
        if (ent.Comp.Tablet is { } storedTablet &&
            Exists(storedTablet) &&
            TryComp<CMUDroneControlTabletComponent>(storedTablet, out var storedTabletComp))
        {
            tabletUid = storedTablet;
            tabletComp = storedTabletComp;
        }
        else if (TryFindCarriedTablet(ent.Owner, out var carriedTablet))
        {
            tabletUid = carriedTablet.Owner;
            tabletComp = carriedTablet.Comp;
        }

        if (tabletUid is not { } resolvedTablet ||
            tabletComp == null)
        {
            return false;
        }

        if (tabletComp.Operator is { } tabletOperator &&
            tabletOperator != ent.Owner &&
            Exists(tabletOperator))
        {
            reason = Loc.GetString("cmu-drone-tablet-bound");
            return false;
        }

        var droneUid = tabletComp.LinkedDrone;
        if (droneUid is not { } resolvedDrone ||
            !Exists(resolvedDrone))
        {
            if (ent.Comp.Drone is not { } storedDrone ||
                !Exists(storedDrone))
            {
                reason = Loc.GetString("cmu-drone-tablet-no-link");
                return false;
            }

            resolvedDrone = storedDrone;
        }

        if (!TryComp<CMUDroneAndroidComponent>(resolvedDrone, out var droneComp))
        {
            reason = Loc.GetString("cmu-drone-link-invalid");
            return false;
        }

        if (ent.Comp.Drone is { } existingDrone &&
            existingDrone != resolvedDrone &&
            Exists(existingDrone) &&
            !TerminatingOrDeleted(existingDrone))
        {
            reason = Loc.GetString("cmu-drone-assembly-existing");
            return false;
        }

        if (droneComp.Operator is { } droneOperator &&
            droneOperator != ent.Owner &&
            Exists(droneOperator))
        {
            reason = Loc.GetString("cmu-drone-link-bound");
            return false;
        }

        ent.Comp.Tablet = resolvedTablet;
        ent.Comp.Drone = resolvedDrone;
        tabletComp.Operator = ent.Owner;
        tabletComp.LinkedDrone = resolvedDrone;
        droneComp.Operator = ent.Owner;
        droneComp.Tablet = resolvedTablet;

        tablet = (resolvedTablet, tabletComp);
        drone = (resolvedDrone, droneComp);
        return true;
    }

    private bool CanCommandLinkedDrone(
        Entity<CMUDroneAndroidComponent> drone,
        Entity<CMUDroneControlTabletComponent> tablet,
        EntityUid user,
        out string reason)
    {
        reason = string.Empty;

        if (!IsEntityContainedBy(tablet.Owner, user))
        {
            reason = Loc.GetString("cmu-drone-tablet-must-carry");
            return false;
        }

        if (TerminatingOrDeleted(drone.Owner) || !_mobState.IsAlive(drone.Owner))
        {
            reason = Loc.GetString("cmu-drone-follow-drone-dead");
            return false;
        }

        if (!_mobState.IsAlive(user))
        {
            reason = Loc.GetString("cmu-drone-control-operator-disabled");
            return false;
        }

        if (drone.Comp.Operator is { } operatorUid &&
            operatorUid != user &&
            Exists(operatorUid))
        {
            reason = Loc.GetString("cmu-drone-link-bound");
            return false;
        }

        if (HasComp<CMUDroneControlSessionComponent>(drone.Owner))
        {
            reason = Loc.GetString("cmu-drone-control-drone-busy");
            return false;
        }

        if (!IsSameMapInRange(user, drone.Owner, tablet.Comp.Range))
        {
            reason = Loc.GetString("cmu-drone-follow-out-of-range");
            return false;
        }

        return true;
    }

    private void StartDroneFollowing(EntityUid user, Entity<CMUDroneAndroidComponent> drone)
    {
        var htn = EnsureDroneFollowHtn(drone.Owner);
        _npc.SleepNPC(drone.Owner, htn);
        _htn.SetHTNEnabled((drone.Owner, htn), false);
        ClearDroneFollowBlackboard(htn);
        StopEntityMotion(drone.Owner);
        EnsureDroneFollowMovement(drone.Owner);

        htn.Blackboard.SetValue(NPCBlackboard.Owner, drone.Owner);
        htn.Blackboard.SetValue(NPCBlackboard.OwnerCoordinates, Transform(drone.Owner).Coordinates);
        _npc.SetBlackboard(drone.Owner, NPCBlackboard.FollowTarget, new EntityCoordinates(user, Vector2.Zero), htn);
        htn.Blackboard.SetValue(FollowCloseRangeKey, drone.Comp.FollowCloseRange);

        drone.Comp.FollowingOperator = true;
        _htn.SetHTNEnabled((drone.Owner, htn), true);
        _npc.WakeNPC(drone.Owner, htn);
        _htn.Replan(htn);

        _popup.PopupEntity(
            Loc.GetString("cmu-drone-follow-start", ("drone", drone.Owner)),
            user,
            user);
    }

    private void StopDroneFollowing(
        Entity<CMUDroneAndroidComponent> drone,
        EntityUid? user,
        bool popup,
        bool showNotFollowing = true)
    {
        if (!drone.Comp.FollowingOperator)
        {
            if (popup && showNotFollowing && user is { } popupUser)
            {
                _popup.PopupEntity(
                    Loc.GetString("cmu-drone-follow-not-following", ("drone", drone.Owner)),
                    popupUser,
                    popupUser,
                    PopupType.SmallCaution);
            }

            return;
        }

        drone.Comp.FollowingOperator = false;

        if (TryComp<HTNComponent>(drone.Owner, out var htn))
        {
            _htn.SetHTNEnabled((drone.Owner, htn), false);
            _npc.SleepNPC(drone.Owner, htn);
            ClearDroneFollowBlackboard(htn);
        }

        StopEntityMotion(drone.Owner);

        if (popup && user is { } userUid)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-drone-follow-stop", ("drone", drone.Owner)),
                userUid,
                userUid);
        }
    }

    private void EnsureDroneFollowMovement(EntityUid drone)
    {
        EnsureComp<InputMoverComponent>(drone);
        EnsureComp<MobMoverComponent>(drone);
    }

    private HTNComponent EnsureDroneFollowHtn(EntityUid drone)
    {
        var htn = EnsureComp<HTNComponent>(drone);
        if (htn.RootTask == null ||
            htn.RootTask.Task != FollowCompoundTask)
        {
            htn.RootTask = new HTNCompoundTask
            {
                Task = FollowCompoundTask,
            };
        }

        return htn;
    }

    private void ClearDroneFollowBlackboard(HTNComponent htn)
    {
        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.FollowTarget);
        htn.Blackboard.Remove<EntityCoordinates>(NPCBlackboard.OwnerCoordinates);
        htn.Blackboard.Remove<PathResultEvent>(NPCBlackboard.PathfindKey);
        htn.Blackboard.Remove<float>(FollowCloseRangeKey);
    }

    private bool TryStartControl(Entity<CMUDroneControlTabletComponent> tablet, EntityUid user)
    {
        if (!TryComp<CMUDroneOperatorComponent>(user, out var operatorComp))
        {
            _popup.PopupEntity(Loc.GetString("cmu-drone-operator-required"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!IsEntityContainedBy(tablet.Owner, user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-drone-tablet-must-carry"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (tablet.Comp.LinkedDrone == null &&
            operatorComp.Drone is { } operatorDrone &&
            Exists(operatorDrone))
        {
            TryLinkTablet(tablet, user, operatorDrone, silent: true);
        }

        if (tablet.Comp.LinkedDrone is not { } linkedDrone ||
            !TryComp<CMUDroneAndroidComponent>(linkedDrone, out var droneComp))
        {
            _popup.PopupEntity(Loc.GetString("cmu-drone-tablet-no-link"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!CanUseLinkedDrone((linkedDrone, droneComp), tablet, user, out var reason))
        {
            _popup.PopupEntity(reason, user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_mind.TryGetMind(user, out var resolvedMind, out var mind) ||
            mind.OwnedEntity != user)
        {
            _popup.PopupEntity(Loc.GetString("cmu-drone-control-no-mind"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (mind.VisitingEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-drone-control-already-visiting"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<CMUDroneControlSessionComponent>(linkedDrone))
        {
            _popup.PopupEntity(Loc.GetString("cmu-drone-control-drone-busy"), user, user, PopupType.SmallCaution);
            return false;
        }

        StopDroneFollowing((linkedDrone, droneComp), user, false);
        StopEntityMotion(user);
        SetDroneDormantEffect((linkedDrone, droneComp), false);

        var pilot = EnsureComp<CMURemotePilotingComponent>(user);
        pilot.Drone = linkedDrone;
        pilot.Tablet = tablet.Owner;
        pilot.MindId = resolvedMind;
        pilot.BlocksInput = true;
        pilot.BodyMoveGraceUntil = _timing.CurTime + BodyMoveGrace;
        RemoveOperatorSsdIndicator((user, pilot));

        var session = EnsureComp<CMUDroneControlSessionComponent>(linkedDrone);
        session.Operator = user;
        session.Tablet = tablet.Owner;
        session.MindId = resolvedMind;
        RefreshDroneSkills((linkedDrone, session));
        AddEndControlAction((linkedDrone, session));

        operatorComp.ControlledDrone = linkedDrone;
        operatorComp.Drone = linkedDrone;
        operatorComp.Tablet = tablet.Owner;
        SetOperatorTransferEffect((user, operatorComp), true);
        SpawnDroneConnectVisuals((user, operatorComp), (linkedDrone, droneComp));

        tablet.Comp.Operator = user;
        tablet.Comp.LinkedDrone = linkedDrone;

        droneComp.Operator = user;
        droneComp.Tablet = tablet.Owner;

        _mind.Visit(resolvedMind, linkedDrone, mind);
        SuppressSsdIndicator(linkedDrone);
        _blocker.UpdateCanMove(user);

        _popup.PopupEntity(
            Loc.GetString("cmu-drone-control-start", ("drone", linkedDrone)),
            linkedDrone,
            linkedDrone);
        return true;
    }

    private bool TryLinkTablet(
        Entity<CMUDroneControlTabletComponent> tablet,
        EntityUid user,
        EntityUid drone,
        bool silent = false)
    {
        if (!TryComp<CMUDroneOperatorComponent>(user, out var operatorComp))
        {
            if (!silent)
                _popup.PopupEntity(Loc.GetString("cmu-drone-operator-required"), user, user, PopupType.SmallCaution);

            return false;
        }

        if (!TryComp<CMUDroneAndroidComponent>(drone, out var droneComp))
        {
            if (!silent)
                _popup.PopupEntity(Loc.GetString("cmu-drone-link-invalid"), user, user, PopupType.SmallCaution);

            return false;
        }

        if (tablet.Comp.Operator is { } tabletOperator &&
            tabletOperator != user &&
            Exists(tabletOperator))
        {
            if (!silent)
                _popup.PopupEntity(Loc.GetString("cmu-drone-tablet-bound"), user, user, PopupType.SmallCaution);

            return false;
        }

        if (droneComp.Operator is { } droneOperator &&
            droneOperator != user &&
            Exists(droneOperator))
        {
            if (!silent)
                _popup.PopupEntity(Loc.GetString("cmu-drone-link-bound"), user, user, PopupType.SmallCaution);

            return false;
        }

        if (operatorComp.Drone is { } existingDrone &&
            existingDrone != drone &&
            Exists(existingDrone) &&
            !TerminatingOrDeleted(existingDrone))
        {
            if (!silent)
                _popup.PopupEntity(Loc.GetString("cmu-drone-assembly-existing"), user, user, PopupType.SmallCaution);

            return false;
        }

        tablet.Comp.LinkedDrone = drone;
        tablet.Comp.Operator = user;

        droneComp.Operator = user;
        droneComp.Tablet = tablet.Owner;

        operatorComp.Drone = drone;
        operatorComp.Tablet = tablet.Owner;

        if (!silent)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-drone-link-finish", ("drone", drone)),
                user,
                user);
        }

        return true;
    }

    private bool CanUseLinkedDrone(
        Entity<CMUDroneAndroidComponent> drone,
        Entity<CMUDroneControlTabletComponent> tablet,
        EntityUid user,
        out string reason)
    {
        reason = string.Empty;

        if (TerminatingOrDeleted(drone.Owner) || !_mobState.IsAlive(drone.Owner))
        {
            reason = Loc.GetString("cmu-drone-control-drone-dead");
            return false;
        }

        if (!_mobState.IsAlive(user))
        {
            reason = Loc.GetString("cmu-drone-control-operator-disabled");
            return false;
        }

        if (drone.Comp.Operator is { } operatorUid &&
            operatorUid != user &&
            Exists(operatorUid))
        {
            reason = Loc.GetString("cmu-drone-link-bound");
            return false;
        }

        if (!IsSameMapInRange(user, drone.Owner, tablet.Comp.Range))
        {
            reason = Loc.GetString("cmu-drone-control-out-of-range");
            return false;
        }

        return true;
    }

    private void ValidateSession(Entity<CMUDroneControlSessionComponent> drone)
    {
        if (TerminatingOrDeleted(drone.Owner) ||
            TerminatingOrDeleted(drone.Comp.Operator) ||
            TerminatingOrDeleted(drone.Comp.Tablet))
        {
            QueueEndControl(drone, Loc.GetString("cmu-drone-control-ended-link-lost"));
            return;
        }

        if (!TryComp<CMUDroneControlTabletComponent>(drone.Comp.Tablet, out var tablet))
        {
            QueueEndControl(drone, Loc.GetString("cmu-drone-control-ended-tablet-lost"));
            return;
        }

        if (!IsEntityContainedBy(drone.Comp.Tablet, drone.Comp.Operator))
        {
            QueueEndControl(drone, Loc.GetString("cmu-drone-control-ended-tablet-removed"));
            return;
        }

        if (!_mobState.IsAlive(drone.Owner))
        {
            QueueEndControl(drone, Loc.GetString("cmu-drone-control-ended-drone-disabled"));
            return;
        }

        if (!_mobState.IsAlive(drone.Comp.Operator))
        {
            QueueEndControl(drone, Loc.GetString("cmu-drone-control-ended-operator-disabled"));
            return;
        }

        if (!TryGetSameMapDistance(drone.Comp.Operator, drone.Owner, out var distance) ||
            distance > tablet.Range)
        {
            QueueEndControl(drone, Loc.GetString("cmu-drone-control-ended-leash"));
            return;
        }

        WarnLeashRange(drone, tablet, distance);
    }

    private void EndControlForTablet(EntityUid tablet, string reason)
    {
        var query = EntityQueryEnumerator<CMUDroneControlSessionComponent>();
        while (query.MoveNext(out var drone, out var session))
        {
            if (session.Tablet == tablet)
                EndControl((drone, session), reason);
        }
    }

    private void EndControlForDrone(EntityUid drone, string reason)
    {
        if (TryComp<CMUDroneControlSessionComponent>(drone, out var session))
            EndControl((drone, session), reason);
    }

    private void EndControlForOperator(EntityUid user, string reason)
    {
        var query = EntityQueryEnumerator<CMUDroneControlSessionComponent>();
        while (query.MoveNext(out var drone, out var session))
        {
            if (session.Operator == user)
                EndControl((drone, session), reason);
        }
    }

    private void DisableOperatorInputBlock(EntityUid user)
    {
        if (!TryComp<CMURemotePilotingComponent>(user, out var piloting))
            return;

        piloting.BlocksInput = false;
        _blocker.UpdateCanMove(user);
    }

    private void StopEntityMotion(EntityUid uid)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics))
            return;

        _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        _physics.SetAngularVelocity(uid, 0f, body: physics);
        _physics.SetBodyStatus(uid, physics, BodyStatus.OnGround);
    }

    private void RefreshDroneDormantEffect(Entity<CMUDroneAndroidComponent> drone)
    {
        SetDroneDormantEffect(
            drone,
            !TerminatingOrDeleted(drone.Owner) &&
            _mobState.IsAlive(drone.Owner) &&
            !HasComp<CMUDroneControlSessionComponent>(drone.Owner));
    }

    private void SetDroneDormantEffect(Entity<CMUDroneAndroidComponent> drone, bool enabled)
    {
        var effect = drone.Comp.DormantEffect;
        SetAttachedEffect(drone.Owner, ref effect, drone.Comp.DormantEffectId, enabled);
        drone.Comp.DormantEffect = effect;
        SetDroneDormantEyeColor(drone, enabled);
    }

    private void SetDroneDormantEyeColor(Entity<CMUDroneAndroidComponent> drone, bool dormant)
    {
        if (TerminatingOrDeleted(drone.Owner) ||
            !TryComp<HumanoidAppearanceComponent>(drone.Owner, out var humanoid))
        {
            return;
        }

        humanoid.EyeColor = dormant
            ? drone.Comp.DormantEyeColor
            : GetDroneActiveEyeColor(drone.Owner, humanoid.EyeColor);
        Dirty(drone.Owner, humanoid);
    }

    private Color GetDroneActiveEyeColor(EntityUid drone, Color fallback)
    {
        if (!TryComp<RMCIntentsEyeColorComponent>(drone, out var eyes))
            return fallback;

        if (_mobState.IsDead(drone))
            return eyes.DeadEyeColor;

        return _combatMode.IsInCombatMode(drone)
            ? eyes.EyeColorHarm
            : eyes.EyeColorHelp;
    }

    private void SetOperatorTransferEffect(Entity<CMUDroneOperatorComponent> operatorEnt, bool enabled)
    {
        var effect = operatorEnt.Comp.TransferEffect;
        SetAttachedEffect(operatorEnt.Owner, ref effect, operatorEnt.Comp.TransferEffectId, enabled);
        operatorEnt.Comp.TransferEffect = effect;
    }

    private void SpawnDroneConnectVisuals(
        Entity<CMUDroneOperatorComponent> operatorEnt,
        Entity<CMUDroneAndroidComponent> drone)
    {
        SpawnDirectedDroneTransferEffect(operatorEnt.Owner, drone.Owner, operatorEnt.Comp.ConnectBeamEffectId);
        PlayDroneTransferShake(drone);
    }

    private void SpawnDroneDisconnectVisuals(EntityUid operatorUid, Entity<CMUDroneAndroidComponent> drone)
    {
        SpawnDirectedDroneTransferEffect(drone.Owner, operatorUid, drone.Comp.DisconnectBeamEffectId);
        PlayDroneTransferShake(drone);
    }

    private void SpawnDirectedDroneTransferEffect(EntityUid source, EntityUid target, EntProtoId prototype)
    {
        if (TerminatingOrDeleted(source) || TerminatingOrDeleted(target))
            return;

        var sourceMap = _transform.GetMapCoordinates(source);
        var targetMap = _transform.GetMapCoordinates(target);
        if (sourceMap.MapId != targetMap.MapId)
            return;

        var holder = Spawn(prototype, sourceMap);
        if (!TryComp(holder, out CMUXenoWarlockParticleEmitterComponent? particles))
            return;

        var distance = Vector2.Distance(sourceMap.Position, targetMap.Position);
        var velocity = GetDroneTransferParticleVelocity(distance, particles.Effect);
        _warlockParticles.TrySetWarlockDirectedParticleMotion(
            (holder, particles),
            sourceMap.Position,
            targetMap.Position,
            velocity);
    }

    private static float GetDroneTransferParticleVelocity(float distance, CMUXenoWarlockParticleEffect effect)
    {
        var profile = CMUXenoWarlockSystem.GetWarlockParticleProfile(effect);
        var rawAge = MathF.Max(0.05f, profile.Lifespan);
        var travelFactor = 0.5f * (rawAge + rawAge * rawAge);
        if (travelFactor <= 0f)
            return TransferParticleMinVelocity;

        var distancePixels = distance * TransferParticlePixelsPerMeter;
        return Math.Clamp(distancePixels / travelFactor, TransferParticleMinVelocity, TransferParticleMaxVelocity);
    }

    private void PlayDroneTransferShake(Entity<CMUDroneAndroidComponent> drone)
    {
        if (TerminatingOrDeleted(drone.Owner) || drone.Comp.TransferShakeDuration <= TimeSpan.Zero)
            return;

        RaiseNetworkEvent(
            new CMUDroneAndroidShakeEvent(
                GetNetEntity(drone.Owner),
                (float) drone.Comp.TransferShakeDuration.TotalSeconds),
            Filter.Pvs(drone.Owner));
    }

    private void SetAttachedEffect(EntityUid owner, ref EntityUid? effect, EntProtoId prototype, bool enabled)
    {
        if (!enabled || TerminatingOrDeleted(owner))
        {
            DeleteAttachedEffect(ref effect);
            return;
        }

        if (effect is { } existing &&
            Exists(existing) &&
            !TerminatingOrDeleted(existing))
        {
            return;
        }

        effect = SpawnAttachedTo(prototype, owner.ToCoordinates());
    }

    private void DeleteAttachedEffect(ref EntityUid? effect)
    {
        if (effect is { } existing &&
            Exists(existing) &&
            !TerminatingOrDeleted(existing))
        {
            QueueDel(existing);
        }

        effect = null;
    }

    private bool ShouldIgnoreBodyMove(Entity<CMURemotePilotingComponent> ent, MoveEvent args)
    {
        if (_timing.CurTime >= ent.Comp.BodyMoveGraceUntil ||
            args.ParentChanged)
        {
            return false;
        }

        if (!args.OldPosition.TryDistance(EntityManager, args.NewPosition, out var distance))
            return false;

        return distance <= BodyMoveGraceDistance;
    }

    private void WarnLeashRange(
        Entity<CMUDroneControlSessionComponent> drone,
        CMUDroneControlTabletComponent tablet,
        float distance)
    {
        if (tablet.RangeWarningBuffer <= 0f)
            return;

        var warningDistance = MathF.Max(tablet.Range - tablet.RangeWarningBuffer, 0f);
        if (distance < warningDistance)
        {
            drone.Comp.NextLeashWarning = TimeSpan.Zero;
            return;
        }

        var now = _timing.CurTime;
        if (now < drone.Comp.NextLeashWarning)
            return;

        var remaining = MathF.Max(tablet.Range - distance, 0f).ToString("0");
        _popup.PopupEntity(
            Loc.GetString("cmu-drone-control-leash-warning", ("remaining", remaining)),
            drone.Owner,
            drone.Owner,
            PopupType.MediumCaution);
        drone.Comp.NextLeashWarning = now + tablet.RangeWarningInterval;
    }

    private void PopupControlEndReason(EntityUid operatorUid, string reason)
    {
        if (string.IsNullOrEmpty(reason))
            return;

        if (!TerminatingOrDeleted(operatorUid))
            _popup.PopupEntity(reason, operatorUid, operatorUid, PopupType.SmallCaution);
    }

    private void RemoveOperatorSsdIndicator(Entity<CMURemotePilotingComponent> ent)
    {
        if (!TryComp<SSDIndicatorComponent>(ent.Owner, out var ssd))
        {
            ent.Comp.HadSsdIndicator = false;
            return;
        }

        ent.Comp.HadSsdIndicator = true;
        ent.Comp.SsdIndicatorIcon = ssd.Icon;
        RemComp<SSDIndicatorComponent>(ent.Owner);
        _statusEffects.TryRemoveStatusEffect(ent.Owner, SSDIndicatorSystem.StatusEffectSSDSleeping);
    }

    private void RestoreOperatorSsdIndicator(Entity<CMURemotePilotingComponent> ent)
    {
        if (!ent.Comp.HadSsdIndicator || TerminatingOrDeleted(ent.Owner))
            return;

        var ssd = EnsureComp<SSDIndicatorComponent>(ent.Owner);
        ssd.Icon = ent.Comp.SsdIndicatorIcon;
        SuppressSsdIndicator(ent.Owner);
    }

    private void QueueEndControlForOperator(EntityUid user, string reason)
    {
        _pendingOperatorEndControls[user] = reason;
    }

    private void QueueEndControl(Entity<CMUDroneControlSessionComponent> drone, string reason)
    {
        _pendingSessionEndControls[drone.Owner] = reason;
    }

    private void FlushPendingOperatorEndControls()
    {
        if (!DrainPendingEndControls(_pendingOperatorEndControls, _pendingOperatorEndControlBuffer))
            return;

        foreach (var (operatorUid, reason) in _pendingOperatorEndControlBuffer)
        {
            EndControlForOperator(operatorUid, reason);
        }

        _pendingOperatorEndControlBuffer.Clear();
    }

    private void FlushPendingSessionEndControls()
    {
        if (!DrainPendingEndControls(_pendingSessionEndControls, _pendingSessionEndControlBuffer))
            return;

        foreach (var (droneUid, reason) in _pendingSessionEndControlBuffer)
        {
            if (TryComp<CMUDroneControlSessionComponent>(droneUid, out var session))
                EndControl((droneUid, session), reason);
        }

        _pendingSessionEndControlBuffer.Clear();
    }

    private static bool DrainPendingEndControls(
        Dictionary<EntityUid, string> pending,
        List<(EntityUid Entity, string Reason)> buffer)
    {
        if (pending.Count == 0)
            return false;

        buffer.Clear();

        foreach (var (entity, reason) in pending)
        {
            buffer.Add((entity, reason));
        }

        pending.Clear();
        return true;
    }

    private void EndControl(Entity<CMUDroneControlSessionComponent> drone, string reason)
    {
        RemoveEndControlAction(drone);
        RestoreDroneSkills(drone);

        var operatorUid = drone.Comp.Operator;
        var mindId = drone.Comp.MindId;
        var operatorExists = !TerminatingOrDeleted(operatorUid);
        var droneExists = !TerminatingOrDeleted(drone.Owner);

        _pendingOperatorEndControls.Remove(operatorUid);
        _pendingSessionEndControls.Remove(drone.Owner);

        StopEntityMotion(drone.Owner);

        if (operatorExists)
            DisableOperatorInputBlock(operatorUid);

        if (TryComp<MindComponent>(mindId, out var mind) &&
            mind.VisitingEntity == drone.Owner)
        {
            _mind.UnVisit(mindId, mind);
        }

        PopupControlEndReason(operatorUid, reason);

        if (droneExists)
            SuppressSsdIndicator(drone.Owner);

        if (droneExists &&
            TryComp<CMUDroneAndroidComponent>(drone.Owner, out var droneAndroid))
        {
            SpawnDroneDisconnectVisuals(operatorUid, (drone.Owner, droneAndroid));
            SetDroneDormantEffect((drone.Owner, droneAndroid), _mobState.IsAlive(drone.Owner));
        }

        if (operatorExists &&
            TryComp<CMUDroneOperatorComponent>(operatorUid, out var operatorComp))
        {
            SetOperatorTransferEffect((operatorUid, operatorComp), false);

            if (operatorComp.ControlledDrone == drone.Owner)
                operatorComp.ControlledDrone = null;
        }

        if (operatorExists &&
            TryComp<CMURemotePilotingComponent>(operatorUid, out var piloting))
        {
            RestoreOperatorSsdIndicator((operatorUid, piloting));
        }

        if (operatorExists)
            RemCompDeferred<CMURemotePilotingComponent>(operatorUid);

        if (droneExists)
            RemCompDeferred<CMUDroneControlSessionComponent>(drone.Owner);
    }

    private void AddEndControlAction(Entity<CMUDroneControlSessionComponent> drone)
    {
        drone.Comp.EndControlAction ??= _actions.AddAction(drone.Owner, EndControlActionId);
    }

    private void RemoveEndControlAction(Entity<CMUDroneControlSessionComponent> drone)
    {
        if (drone.Comp.EndControlAction is not { } action)
            return;

        if (!TerminatingOrDeleted(drone.Owner))
            _actions.RemoveAction(drone.Owner, action);

        drone.Comp.EndControlAction = null;
    }

    private void SuppressSsdIndicator(EntityUid uid)
    {
        if (!TryComp<SSDIndicatorComponent>(uid, out var ssd))
            return;

        ssd.IsSSD = false;
        _statusEffects.TryRemoveStatusEffect(uid, SSDIndicatorSystem.StatusEffectSSDSleeping);
        Dirty(uid, ssd);
    }

    private void AssignRandomDroneName(Entity<CMUDroneAndroidComponent> ent)
    {
        if (!_prototypes.TryIndex<LocalizedDatasetPrototype>(ent.Comp.NameDataset, out var dataset))
            return;

        var name = _random.Pick(dataset);
        _metaData.SetEntityName(ent.Owner, name);
    }

    private bool CanRenameDrone(Entity<CMUDroneAndroidComponent> drone, EntityUid user)
    {
        if (drone.Comp.Operator == user)
            return true;

        return user == drone.Owner &&
               TryComp<CMUDroneControlSessionComponent>(drone.Owner, out var session) &&
               session.Operator == drone.Comp.Operator &&
               !TerminatingOrDeleted(session.Operator);
    }

    private void OpenDroneRenameDialog(Entity<CMUDroneAndroidComponent> drone, EntityUid user)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        var netDrone = GetNetEntity(drone.Owner);
        var netUser = GetNetEntity(user);

        _quickDialog.OpenDialog(actor.PlayerSession,
            Loc.GetString("cmu-drone-rename-title"),
            Loc.GetString("cmu-drone-rename-prompt", ("max", drone.Comp.MaxNameLength)),
            (string name) =>
            {
                if (!TryGetEntity(netDrone, out var droneUid) ||
                    !TryGetEntity(netUser, out var userUid) ||
                    !TryComp<CMUDroneAndroidComponent>(droneUid.Value, out var droneComp) ||
                    !CanRenameDrone((droneUid.Value, droneComp), userUid.Value))
                {
                    return;
                }

                if (!TryNormalizeDroneName(name, droneComp.MaxNameLength, out var normalized))
                {
                    _popup.PopupEntity(
                        Loc.GetString("cmu-drone-rename-invalid", ("max", droneComp.MaxNameLength)),
                        userUid.Value,
                        userUid.Value,
                        PopupType.SmallCaution);
                    return;
                }

                _metaData.SetEntityName(droneUid.Value, normalized);
                _popup.PopupEntity(
                    Loc.GetString("cmu-drone-rename-success", ("drone", droneUid.Value)),
                    userUid.Value,
                    userUid.Value);
            });
    }

    private static bool TryNormalizeDroneName(string name, int maxLength, out string normalized)
    {
        normalized = FormattedMessage.RemoveMarkupPermissive(name).Trim();
        if (normalized.Length == 0 || normalized.Length > maxLength)
            return false;

        foreach (var character in normalized)
        {
            if (char.IsWhiteSpace(character))
                return false;
        }

        return true;
    }

    private void RefreshDroneSkills(Entity<CMUDroneControlSessionComponent> drone)
    {
        SnapshotDroneSkills(drone);

        var skills = new Dictionary<EntProtoId<SkillDefinitionComponent>, int>();

        if (!TerminatingOrDeleted(drone.Comp.Operator) &&
            TryComp<SkillsComponent>(drone.Comp.Operator, out var operatorSkills))
        {
            MergeSkills(skills, operatorSkills.Skills);
        }

        if (TryComp<CMUDroneAndroidComponent>(drone.Owner, out var droneComp) &&
            TryGetInstalledDroneModule((drone.Owner, droneComp), out var module))
        {
            MergeSkills(skills, module.Comp.Skills);
        }

        _skills.RemoveAllSkills(drone.Owner);
        if (skills.Count > 0)
            _skills.SetSkills(drone.Owner, skills);
    }

    private void SnapshotDroneSkills(Entity<CMUDroneControlSessionComponent> drone)
    {
        if (drone.Comp.SkillsSnapshotTaken)
            return;

        drone.Comp.SkillsSnapshotTaken = true;

        if (TryComp<SkillsComponent>(drone.Owner, out var previousSkills))
        {
            drone.Comp.HadSkills = true;
            drone.Comp.PreviousSkills = new Dictionary<EntProtoId<SkillDefinitionComponent>, int>(previousSkills.Skills);
        }
    }

    private static void MergeSkills(
        Dictionary<EntProtoId<SkillDefinitionComponent>, int> target,
        Dictionary<EntProtoId<SkillDefinitionComponent>, int> source)
    {
        foreach (var (skill, level) in source)
        {
            if (!target.TryGetValue(skill, out var current) || level > current)
                target[skill] = level;
        }
    }

    private void RestoreDroneSkills(Entity<CMUDroneControlSessionComponent> drone)
    {
        if (TerminatingOrDeleted(drone.Owner))
            return;

        if (!drone.Comp.HadSkills)
        {
            RemCompDeferred<SkillsComponent>(drone.Owner);
            return;
        }

        _skills.RemoveAllSkills(drone.Owner);
        if (drone.Comp.PreviousSkills is { } previous)
            _skills.SetSkills(drone.Owner, new Dictionary<EntProtoId<SkillDefinitionComponent>, int>(previous));
    }

    private bool TryGetActiveSession(EntityUid tablet, out Entity<CMUDroneControlSessionComponent> session)
    {
        var query = EntityQueryEnumerator<CMUDroneControlSessionComponent>();
        while (query.MoveNext(out var drone, out var control))
        {
            if (control.Tablet != tablet)
                continue;

            session = (drone, control);
            return true;
        }

        session = default;
        return false;
    }

    private bool TryFindCarriedTablet(EntityUid user, out Entity<CMUDroneControlTabletComponent> tablet)
    {
        var query = EntityQueryEnumerator<CMUDroneControlTabletComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!IsEntityContainedBy(uid, user))
                continue;

            tablet = (uid, comp);
            return true;
        }

        tablet = default;
        return false;
    }

    private bool HasExistingDrone(Entity<CMUDroneOperatorComponent> ent)
    {
        return ent.Comp.Drone is { } drone &&
               Exists(drone) &&
               !TerminatingOrDeleted(drone);
    }

    private bool IsSameMapInRange(EntityUid first, EntityUid second, float range)
    {
        return TryGetSameMapDistance(first, second, out var distance) && distance <= range;
    }

    private bool TryGetSameMapDistance(EntityUid first, EntityUid second, out float distance)
    {
        distance = 0f;

        if (TerminatingOrDeleted(first) || TerminatingOrDeleted(second))
            return false;

        var firstCoords = _transform.GetMapCoordinates(first);
        var secondCoords = _transform.GetMapCoordinates(second);

        if (firstCoords.MapId != secondCoords.MapId ||
            firstCoords.MapId == MapId.Nullspace)
        {
            return false;
        }

        distance = (firstCoords.Position - secondCoords.Position).Length();
        return true;
    }

    private bool TryDistance(EntityUid first, EntityUid second, out float distance)
    {
        distance = 0f;

        if (TerminatingOrDeleted(first) || TerminatingOrDeleted(second))
            return false;

        return Transform(first).Coordinates.TryDistance(EntityManager, Transform(second).Coordinates, out distance);
    }

    private bool IsEntityContainedBy(EntityUid child, EntityUid parent)
    {
        if (child == parent)
            return true;

        if (!TryComp(child, out TransformComponent? xform))
            return false;

        var current = xform.ParentUid;
        for (var i = 0; i < 32 && current.IsValid(); i++)
        {
            if (current == parent)
                return true;

            if (!TryComp(current, out xform))
                return false;

            current = xform.ParentUid;
        }

        return false;
    }

    private void SpawnRuinedCore(Entity<CMUDroneAndroidComponent> drone)
    {
        if (drone.Comp.RuinedCoreSpawned)
            return;

        drone.Comp.RuinedCoreSpawned = true;

        var coords = _transform.GetMapCoordinates(drone.Owner);
        if (coords.MapId == MapId.Nullspace)
            return;

        Spawn(drone.Comp.RuinedCorePrototype, coords);

        if (drone.Comp.Operator is { } operatorUid &&
            !TerminatingOrDeleted(operatorUid) &&
            TryComp<CMUDroneOperatorComponent>(operatorUid, out var operatorComp) &&
            operatorComp.Drone == drone.Owner)
        {
            operatorComp.Drone = null;
            operatorComp.ControlledDrone = null;
        }
    }
}
