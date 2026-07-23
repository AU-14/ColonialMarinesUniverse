using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Emp;
using Content.Server.Speech;
using Content.Shared.ActionBlocker;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Camera;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Coordinates;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaItemSystem : EntitySystem
{
    private readonly record struct RelayGroundDestination(EntityUid Entity, string Id, string Name);

    private static readonly string[] FalconReturnSlots = { "ears", "ears2" };
    private static readonly ProtoId<NpcFactionPrototype> BadBloodYautjaFaction = "CMUYautjaBadBlood";

    private readonly List<YautjaRelayBeaconCustomDestination> _relayDestinations = new();

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedRMCCameraSystem _rmcCamera = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedXenoAcidSystem _acid = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private YautjaCloakSystem _cloak = default!;
    [Dependency] private YautjaTeleportSystem _teleport = default!;
    [Dependency] private YautjaThrallSystem _thralls = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCleanerComponent, AfterInteractEvent>(OnCleanerAfterInteract);
        SubscribeLocalEvent<YautjaCleanerComponent, YautjaCleanserDoAfterEvent>(OnCleanserDoAfter);
        SubscribeLocalEvent<YautjaDissolvingComponent, BeforeMeltedEvent>(OnCleanserBeforeMelted);
        SubscribeLocalEvent<YautjaScalableRepairComponent, ExaminedEvent>(OnScalableRepairExamined);

        SubscribeLocalEvent<YautjaHivebreakerComponent, AfterInteractEvent>(OnHivebreakerAfterInteract);
        SubscribeLocalEvent<YautjaHivebreakerComponent, YautjaHivebreakerDoAfterEvent>(OnHivebreakerDoAfter);
        SubscribeLocalEvent<YautjaHivebreakerConsentAcceptedEvent>(OnHivebreakerConsentAccepted);
        SubscribeLocalEvent<YautjaHivebreakerConsentRejectedEvent>(OnHivebreakerConsentRejected);
        SubscribeLocalEvent<XenoComponent, GetVerbsEvent<AlternativeVerb>>(OnGetXenoVerbs);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<YautjaRelayBeaconComponent, UseInHandEvent>(OnRelayBeaconUse);
        SubscribeLocalEvent<YautjaRelayBeaconComponent, GetItemActionsEvent>(OnRelayBeaconGetItemActions);
        SubscribeLocalEvent<YautjaRelayBeaconComponent, YautjaAddTeleporterLocationActionEvent>(OnRelayBeaconAddLocation);
        SubscribeLocalEvent<YautjaRelayBeaconComponent, YautjaRelayBeaconNameDestinationEvent>(OnRelayBeaconNameDestination);
        SubscribeLocalEvent<YautjaRelayBeaconComponent, YautjaRelayBeaconDoAfterEvent>(OnRelayBeaconDoAfter);
        Subs.BuiEvents<YautjaRelayBeaconComponent>(YautjaRelayBeaconUIKey.Key, subs =>
        {
            subs.Event<YautjaRelayBeaconDestinationMsg>(OnRelayBeaconDestination);
        });
        SubscribeLocalEvent<YautjaFalconDroneComponent, UseInHandEvent>(OnFalconDroneUse);
        SubscribeLocalEvent<YautjaFalconDroneComponent, GetItemActionsEvent>(OnFalconDroneGetItemActions);
        SubscribeLocalEvent<YautjaFalconDroneComponent, YautjaFalconControlActionEvent>(OnFalconControl);
        SubscribeLocalEvent<YautjaFalconDroneComponent, ListenEvent>(OnFalconListen);
        SubscribeLocalEvent<InventoryComponent, ExaminedEvent>(OnFalconWearerExamined);
        SubscribeLocalEvent<YautjaFalconDroneDeployedComponent, EntityTerminatingEvent>(OnFalconDroneTerminating);
        SubscribeLocalEvent<YautjaFalconDroneDeployedComponent, EmpPulseEvent>(OnFalconEmpPulse);
        SubscribeLocalEvent<YautjaFalconDroneDeployedComponent, DestructionEventArgs>(OnFalconDestroyed);
        SubscribeLocalEvent<YautjaFalconControllerComponent, ComponentShutdown>(OnFalconControllerShutdown);
        SubscribeLocalEvent<YautjaFalconControllerComponent, EntityTerminatingEvent>(OnFalconControllerTerminating);
        SubscribeLocalEvent<YautjaFalconControllerComponent, YautjaFalconRecallActionEvent>(OnFalconRecall);
        SubscribeLocalEvent<YautjaFalconSourceBracerComponent, GotUnequippedEvent>(OnFalconSourceBracerUnequipped);
        SubscribeLocalEvent<YautjaHoundPadComponent, MapInitEvent>(OnHoundPadMapInit);
        SubscribeLocalEvent<YautjaHoundPadComponent, ComponentShutdown>(OnHoundPadShutdown);
        SubscribeLocalEvent<YautjaHoundPadComponent, UseInHandEvent>(OnHoundPadUse);
        SubscribeLocalEvent<YautjaHellhoundComponent, EntityTerminatingEvent>(OnHellhoundTerminating);
        SubscribeLocalEvent<YautjaHoundWatchingComponent, ComponentShutdown>(OnHoundWatchingShutdown);
        SubscribeLocalEvent<YautjaHoundWatchingComponent, EntityTerminatingEvent>(OnHoundWatchingTerminating);
    }

    private void OnScalableRepairExamined(Entity<YautjaScalableRepairComponent> ent, ref ExaminedEvent args)
    {
        var text = ent.Comp.Status switch
        {
            YautjaScalableRepairStatus.Damaged => ent.Comp.DamagedText,
            YautjaScalableRepairStatus.Reinforced => ent.Comp.ReinforcedText,
            _ => string.Empty,
        };

        if (text.Length > 0)
            args.PushMarkup(text);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _relayDestinations.Clear();
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaDissolvingComponent>();
        while (query.MoveNext(out var uid, out var dissolving))
        {
            if (now < dissolving.DeleteAt)
                continue;

            if (HasComp<TimedCorrodingComponent>(uid))
                continue;

            _popup.PopupCoordinates(
                Loc.GetString("cmu-yautja-cleanser-crumble", ("target", uid)),
                Transform(uid).Coordinates,
                PopupType.MediumCaution);
            QueueDel(uid);
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (!HasComp<XenoComponent>(args.Target))
            return;

        if (args.NewMobState == MobState.Dead)
        {
            var death = EnsureComp<YautjaHivebreakerDeathComponent>(args.Target);
            death.DeadAt = _timing.CurTime;
            return;
        }

        RemComp<YautjaHivebreakerDeathComponent>(args.Target);
    }

    private void OnCleanerAfterInteract(Entity<YautjaCleanerComponent> cleaner, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = TryStartCleanser(cleaner, args.User, target, args.CanReach);
    }

    private bool TryStartCleanser(Entity<YautjaCleanerComponent> cleaner, EntityUid user, EntityUid target, bool canReach)
    {
        if (_hands.GetActiveItem(user) != cleaner.Owner)
            return false;

        if (!CanUseYautjaTech(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!canReach || !CanDissolve(cleaner.Owner, target, user, true))
            return false;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            cleaner.Comp.DoAfter,
            new YautjaCleanserDoAfterEvent(),
            cleaner.Owner,
            target: target,
            used: cleaner.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
            ForceVisible = true,
            TargetEffect = "RMCEffectXenoTelegraphRedEmpower",
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _audio.PlayPvs(cleaner.Comp.StartSound, target);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-cleanser-start-others", ("user", user), ("target", target)),
            user,
            Filter.PvsExcept(user),
            true,
            PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-start-self", ("target", target)), user, user, PopupType.LargeCaution);
        return true;
    }

    private void OnCleanserDoAfter(Entity<YautjaCleanerComponent> cleaner, ref YautjaCleanserDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (args.Cancelled)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-cleanser-cancel-others", ("user", args.User), ("target", target)),
                args.User,
                Filter.PvsExcept(args.User),
                true,
                PopupType.MediumCaution);
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-cleanser-cancel-self", ("target", target)),
                args.User,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        if (_hands.GetActiveItem(args.User) != cleaner.Owner)
            return;

        if (!CanDissolve(cleaner.Owner, target, args.User, true))
            return;

        var dissolving = EnsureComp<YautjaDissolvingComponent>(target);
        dissolving.DeleteAt = _timing.CurTime + cleaner.Comp.DissolveDelay;

        _acid.ApplyAcid(
            cleaner.Comp.AcidPrototype,
            cleaner.Comp.AcidStrength,
            target,
            cleaner.Comp.AcidDps,
            cleaner.Comp.LightAcidDps,
            cleaner.Comp.DissolveDelay);

        _audio.PlayPvs(cleaner.Comp.FinishSound, target);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-cleanser-covered-others", ("user", args.User), ("target", target)),
            args.User,
            Filter.PvsExcept(args.User),
            true);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-covered", ("target", target)), args.User, args.User);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):player} covered {ToPrettyString(target):target} in Yautja dissolving gel");
    }

    private void OnCleanserBeforeMelted(Entity<YautjaDissolvingComponent> ent, ref BeforeMeltedEvent args)
    {
        _popup.PopupCoordinates(
            Loc.GetString("cmu-yautja-cleanser-crumble", ("target", ent.Owner)),
            Transform(ent.Owner).Coordinates,
            PopupType.MediumCaution);
    }

    private bool CanDissolve(EntityUid cleaner, EntityUid target, EntityUid user, bool popup)
    {
        if (Deleted(target) || !HasComp<ItemComponent>(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-invalid"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<YautjaDissolvingComponent>(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-already", ("target", target)), user, user, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<EntityActiveInvisibleComponent>(user))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-cloaked"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (_acid.IsMelted(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-already", ("target", target)), user, user, PopupType.SmallCaution);
            return false;
        }

        if (TryComp(target, out TransformComponent? xform))
        {
            if (xform.Anchored)
            {
                if (popup)
                    _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-anchored", ("target", target)), user, user, PopupType.SmallCaution);
                return false;
            }

            if (HasComp<MobStateComponent>(xform.ParentUid))
            {
                if (popup)
                    _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-held", ("target", target)), user, user, PopupType.SmallCaution);
                return false;
            }
        }

        if (target == cleaner || HasComp<YautjaCleanerComponent>(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-cleanser-fluid"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private void OnHivebreakerAfterInteract(Entity<YautjaHivebreakerComponent> hivebreaker, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = TryStartHivebreaker(hivebreaker, args.User, target, args.CanReach);
    }

    private void OnGetXenoVerbs(Entity<XenoComponent> xeno, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            _hands.GetActiveItem(args.User) is not { } held ||
            !TryComp(held, out YautjaHivebreakerComponent? hivebreaker))
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-yautja-hivebreaker-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png")),
            Priority = 3,
            Act = () => TryStartHivebreaker((held, hivebreaker), user, xeno.Owner, true),
        });
    }

    private bool TryStartHivebreaker(Entity<YautjaHivebreakerComponent> hivebreaker, EntityUid user, EntityUid target, bool canReach)
    {
        if (!canReach || !CanHivebreak(hivebreaker.Comp, user, target, true, requireCritical: true))
            return false;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            hivebreaker.Comp.DoAfter,
            new YautjaHivebreakerDoAfterEvent(),
            hivebreaker.Owner,
            target: target,
            used: hivebreaker.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
            ForceVisible = true,
            TargetEffect = "RMCEffectXenoTelegraphRedEmpower",
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _audio.PlayPvs(hivebreaker.Comp.StartSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-start-self", ("target", target)), user, user, PopupType.LargeCaution);
        return true;
    }

    private void OnHivebreakerDoAfter(Entity<YautjaHivebreakerComponent> hivebreaker, ref YautjaHivebreakerDoAfterEvent args)
    {
        if (args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-cancel-self", ("target", target)), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (!CanHivebreak(hivebreaker.Comp, args.User, target, true, requireCritical: false))
            return;

        var rejectedEvent = new YautjaHivebreakerConsentRejectedEvent(GetNetEntity(args.User));
        var options = new List<DialogOption>
        {
            new("Yes", new YautjaHivebreakerConsentAcceptedEvent(
                GetNetEntity(args.User),
                GetNetEntity(target),
                GetNetEntity(hivebreaker.Owner))),
            new("No", rejectedEvent),
        };

        _dialog.OpenOptions(
            target,
            target,
            hivebreaker.Comp.ConsentTitle,
            options,
            hivebreaker.Comp.ConsentMessage,
            rejectedEvent,
            timeout: hivebreaker.Comp.ConsentTimeout);
    }

    private void OnHivebreakerConsentAccepted(YautjaHivebreakerConsentAcceptedEvent args)
    {
        if (!TryGetEntity(args.User, out var user) ||
            !TryGetEntity(args.Target, out var target) ||
            !TryGetEntity(args.Hivebreaker, out var hivebreakerUid) ||
            !TryComp(hivebreakerUid, out YautjaHivebreakerComponent? hivebreaker))
        {
            return;
        }

        if (!CanHivebreak(hivebreaker, user.Value, target.Value, true, requireCritical: false))
            return;

        if (!_thralls.HivebreakXeno(user.Value, target.Value, hivebreakerUid.Value, hivebreaker))
            return;

        hivebreaker.Uses--;
        _audio.PlayPvs(hivebreaker.FinishSound, target.Value);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-finished-self", ("target", target.Value)), user.Value, user.Value);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-finished-target", ("hunter", user.Value)), target.Value, target.Value, PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(user.Value):hunter} enthralled xeno {ToPrettyString(target.Value):target} with a Yautja hivebreaker");

        if (hivebreaker.Uses <= 0)
            QueueDel(hivebreakerUid.Value);
    }

    private void OnHivebreakerConsentRejected(YautjaHivebreakerConsentRejectedEvent args)
    {
        if (!TryGetEntity(args.User, out var user))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-refused"), user.Value, user.Value, PopupType.SmallCaution);
    }

    private bool CanHivebreak(YautjaHivebreakerComponent hivebreaker, EntityUid user, EntityUid target, bool popup, bool requireCritical)
    {
        if (!CanUseHivebreaker(user))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (Deleted(target) || !TryComp(target, out XenoComponent? xeno))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-requires-xeno"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<YautjaThrallComponent>(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-already"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (IsBadBloodXeno(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-already"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (hivebreaker.RequireTargetActor &&
            (!TryComp(target, out ActorComponent? actor) || actor.PlayerSession == null))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-defective"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (xeno.Tier == 0 || hivebreaker.BannedXenoRoles.Contains(xeno.Role))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-caste-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (HunterHasAnotherThrall(user, target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-already-has"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (requireCritical && hivebreaker.RequireCritical && !_mobState.IsCritical(target))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-requires-recent-death"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private bool IsBadBloodXeno(EntityUid target)
    {
        if (TryComp<NpcFactionMemberComponent>(target, out var faction) &&
            faction.Factions.Contains(BadBloodYautjaFaction))
        {
            return true;
        }

        return TryComp(target, out HiveMemberComponent? hiveMember) &&
               hiveMember.Hive is { } hive &&
               !TerminatingOrDeleted(hive) &&
               _hive.HasFaction(hive, BadBloodYautjaFaction);
    }

    private bool CanUseHivebreaker(EntityUid user)
    {
        if (!HasComp<YautjaTechAuthorizedComponent>(user) && !HasComp<YautjaComponent>(user))
            return false;

        return TryComp<NpcFactionMemberComponent>(user, out var faction) &&
               faction.Factions.Contains(BadBloodYautjaFaction);
    }

    private bool HunterHasAnotherThrall(EntityUid hunter, EntityUid target)
    {
        var query = EntityQueryEnumerator<YautjaThrallComponent>();
        while (query.MoveNext(out var uid, out var thrall))
        {
            if (uid == target ||
                thrall.Master != hunter ||
                Deleted(uid) ||
                _mobState.IsDead(uid))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void OnRelayBeaconUse(Entity<YautjaRelayBeaconComponent> beacon, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryUseRelayBeacon(beacon, args.User);
    }

    private void OnRelayBeaconGetItemActions(Entity<YautjaRelayBeaconComponent> beacon, ref GetItemActionsEvent args)
    {
        if (!beacon.Comp.AllowCustomDestinations)
            return;

        if (!args.InHands || !_hands.IsHolding(args.User, beacon.Owner))
            return;

        args.AddAction(ref beacon.Comp.AddTeleporterLocationAction, beacon.Comp.AddTeleporterLocationActionId);
    }

    private void OnRelayBeaconAddLocation(Entity<YautjaRelayBeaconComponent> beacon, ref YautjaAddTeleporterLocationActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        if (!beacon.Comp.AllowCustomDestinations)
            return;

        var user = args.Performer;
        if (!CanUseYautjaTech(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-relay-add-destination-denied"), user, user, PopupType.SmallCaution);
            return;
        }

        if (!CanAddRelayDestination(user))
            return;

        var coordinates = Transform(user).Coordinates;
        if (coordinates.IsValid(EntityManager))
        {
            _dialog.OpenInput(
                beacon.Owner,
                user,
                Loc.GetString("cmu-yautja-relay-add-destination-prompt"),
                new YautjaRelayBeaconNameDestinationEvent(GetNetEntity(user), GetNetCoordinates(coordinates)),
                characterLimit: 40,
                minCharacterLimit: 1,
                smartCheck: true,
                title: "Text");
        }
    }

    private void OnRelayBeaconNameDestination(Entity<YautjaRelayBeaconComponent> beacon, ref YautjaRelayBeaconNameDestinationEvent args)
    {
        if (!TryGetEntity(args.User, out var user) || !CanAddRelayDestination(user.Value))
            return;

        var name = args.Message.Trim();
        if (name.Length == 0)
            return;

        if (!TryGetEntity(args.Coordinates.NetEntity, out var coordinateParent))
            return;

        var coordinates = new EntityCoordinates(coordinateParent.Value, args.Coordinates.Position);
        if (!coordinates.IsValid(EntityManager))
            return;

        var destination = new YautjaRelayBeaconCustomDestination
        {
            Name = name,
            Coordinates = coordinates,
        };

        _relayDestinations.Add(destination);
        beacon.Comp.CustomDestinations.Add(destination);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-relay-add-destination-success"), user.Value, user.Value, PopupType.Medium);
        var area = _areas.GetAreaName(user.Value);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user.Value):player} has created a new teleport location at {area}");
        BroadcastRelayDestinationToYautja(user.Value, name, coordinates, area);

        if (_ui.IsUiOpen(beacon.Owner, YautjaRelayBeaconUIKey.Key, user.Value))
            UpdateRelayBeaconUi(beacon, user.Value);
    }

    private void BroadcastRelayDestinationToYautja(EntityUid user, string destination, EntityCoordinates coordinates, string area)
    {
        var message = Loc.GetString(
            "cmu-yautja-relay-add-destination-broadcast",
            ("hunter", Name(user)),
            ("name", destination),
            ("location", coordinates.ToString()),
            ("area", area));
        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!Deleted(uid) && CanReceiveYautjaHuntingBroadcast(uid))
                _popup.PopupEntity(message, uid, uid, PopupType.Medium);
        }
    }

    private bool CanReceiveYautjaHuntingBroadcast(EntityUid uid)
    {
        // CMSS13 pred_can_receive_message() rejects dead hunters, and message_all_yautja() defaults to
        // YAUTJA_NET_HUNTING, which Bad Blood does not receive
        // unless another explicit received_networks path is involved.
        return !_mobState.IsDead(uid) &&
               (!TryComp<NpcFactionMemberComponent>(uid, out var faction) ||
                !faction.Factions.Contains(BadBloodYautjaFaction));
    }

    private bool TryUseRelayBeacon(Entity<YautjaRelayBeaconComponent> beacon, EntityUid user)
    {
        if (!CanReachRelayBeaconAttackSelf(user))
            return false;

        if (!CanUseRelayBeaconAttackSelf(beacon.Owner, user, true))
        {
            return false;
        }

        if (beacon.Comp.AllowedDestinations.Count == 0)
        {
            TryPlayRelayBeaconPulseSound(beacon);
            _popup.PopupEntity(Loc.GetString("cmu-yautja-relay-beacon-pulse"), user, user);
            return false;
        }

        if (beacon.Comp.AllowedDestinations.Count == 1 &&
            beacon.Comp.AllowedDestinations[0] != YautjaRelayDestinationKind.Ground)
            return TryStartRelayTeleport(beacon, user, beacon.Comp.AllowedDestinations[0]);

        _ui.TryOpenUi(beacon.Owner, YautjaRelayBeaconUIKey.Key, user);
        UpdateRelayBeaconUi(beacon, user);
        return true;
    }

    private void OnRelayBeaconDestination(Entity<YautjaRelayBeaconComponent> beacon, ref YautjaRelayBeaconDestinationMsg args)
    {
        if (TryStartRelayTeleport(beacon, args.Actor, args.Destination, args.CustomIndex, args.DestinationId))
            _ui.CloseUi(beacon.Owner, YautjaRelayBeaconUIKey.Key, args.Actor);
        else
            UpdateRelayBeaconUi(beacon, args.Actor);
    }

    private bool TryStartRelayTeleport(
        Entity<YautjaRelayBeaconComponent> beacon,
        EntityUid user,
        YautjaRelayDestinationKind destinationKind,
        int customIndex = -1,
        string? destinationId = null)
    {
        if (!CanReachRelayBeaconAttackSelf(user))
            return false;

        if (!CanUseRelayBeaconAttackSelf(beacon.Owner, user, true))
        {
            return false;
        }

        if (customIndex < 0)
        {
            var validDestination = destinationKind == YautjaRelayDestinationKind.Ground
                ? TryGetGroundRelayDestination(destinationId, out _)
                : string.IsNullOrWhiteSpace(destinationId) && TryGetRelayDestination(destinationKind, out _);
            if (!beacon.Comp.AllowedDestinations.Contains(destinationKind) || !validDestination)
                return false;
        }
        else if (!beacon.Comp.AllowCustomDestinations ||
                 destinationKind == YautjaRelayDestinationKind.Ground ||
                 !string.IsNullOrWhiteSpace(destinationId) ||
                 !TryGetCustomRelayDestination(customIndex, out _))
            return false;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            beacon.Comp.DoAfter,
            new YautjaRelayBeaconDoAfterEvent(destinationKind, customIndex, destinationId),
            beacon.Owner,
            target: user,
            used: beacon.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = false,
            DuplicateCondition = DuplicateConditions.SameEvent,
            ForceVisible = true,
            TargetEffect = "RMCEffectXenoTelegraphRedEmpower",
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        TryPlayRelayBeaconPulseSound(beacon);
        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-relay-beacon-start", ("user", Name(user))),
            user,
            PopupType.Medium);
        return true;
    }

    private void OnRelayBeaconDoAfter(Entity<YautjaRelayBeaconComponent> beacon, ref YautjaRelayBeaconDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        if (!CanReachRelayBeaconAttackSelf(args.User) ||
            !CanUseRelayBeaconAttackSelf(beacon.Owner, args.User, false))
        {
            return;
        }

        EntityCoordinates coordinates;
        if (args.CustomIndex >= 0)
        {
            if (!TryGetCustomRelayDestination(args.CustomIndex, out coordinates))
                return;
        }
        else if (args.Destination == YautjaRelayDestinationKind.Ground)
        {
            if (!TryGetGroundRelayDestination(args.DestinationId, out var destination))
                return;

            coordinates = Transform(destination).Coordinates;
        }
        else if (TryGetRelayDestination(args.Destination, out var destination))
        {
            coordinates = Transform(destination).Coordinates;
        }
        else
            return;

        _popup.PopupEntity(
            Loc.GetString("cmu-yautja-relay-beacon-disappear", ("user", Name(args.User))),
            args.User,
            PopupType.MediumCaution);
        _cloak.ForceDecloak(args.User);
        if (TryComp(args.User, out PullerComponent? puller) &&
            puller.Pulling is { } pulled &&
            HasComp<MobStateComponent>(pulled))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-relay-beacon-disappear", ("user", Name(pulled))),
                pulled,
                PopupType.MediumCaution);
            _cloak.ForceDecloak(pulled);
        }

        _teleport.TeleportTrain(args.User, _transform.ToMapCoordinates(coordinates));
    }

    private void TryPlayRelayBeaconPulseSound(Entity<YautjaRelayBeaconComponent> beacon)
    {
        if (beacon.Comp.PulseSound is SoundPathSpecifier path &&
            !_resources.ContentFileExists(path.Path))
        {
            return;
        }

        _audio.PlayPvs(beacon.Comp.PulseSound, beacon.Owner);
    }

    private void UpdateRelayBeaconUi(Entity<YautjaRelayBeaconComponent> beacon, EntityUid user)
    {
        if (!CanReachRelayBeaconAttackSelf(user) || !CanUseRelayBeacon(user))
            return;

        var entries = new List<YautjaRelayBeaconDestinationEntry>();
        foreach (var destination in beacon.Comp.AllowedDestinations)
        {
            if (destination == YautjaRelayDestinationKind.Ground)
            {
                foreach (var groundDestination in GetGroundRelayDestinations())
                {
                    entries.Add(new YautjaRelayBeaconDestinationEntry(
                        destination,
                        groundDestination.Name,
                        true,
                        destinationId: groundDestination.Id));
                }

                continue;
            }

            entries.Add(new YautjaRelayBeaconDestinationEntry(
                destination,
                RelayDestinationName(destination),
                TryGetRelayDestination(destination, out _)));
        }

        for (var i = 0; i < _relayDestinations.Count; i++)
        {
            var custom = _relayDestinations[i];
            entries.Add(new YautjaRelayBeaconDestinationEntry(
                YautjaRelayDestinationKind.YautjaShip,
                custom.Name,
                custom.Coordinates.IsValid(EntityManager),
                i));
        }

        _ui.SetUiState(beacon.Owner, YautjaRelayBeaconUIKey.Key, new YautjaRelayBeaconState(entries));
    }

    private string RelayDestinationName(YautjaRelayDestinationKind kind)
    {
        return kind switch
        {
            YautjaRelayDestinationKind.HumanShip => Loc.GetString("cmu-yautja-relay-destination-human-ship"),
            _ => Loc.GetString("cmu-yautja-relay-destination-yautja-ship"),
        };
    }

    private bool TryGetRelayDestination(YautjaRelayDestinationKind kind, out EntityUid destination)
    {
        destination = default;
        var query = EntityQueryEnumerator<YautjaRelayDestinationComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (Deleted(uid) || component.Kind != kind)
                continue;

            destination = uid;
            return true;
        }

        return false;
    }

    private List<RelayGroundDestination> GetGroundRelayDestinations()
    {
        var destinations = new List<RelayGroundDestination>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var query = EntityQueryEnumerator<YautjaRelayDestinationComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (Deleted(uid) || component.Kind != YautjaRelayDestinationKind.Ground)
                continue;

            var id = component.Id.Trim();
            var name = component.DisplayName.Trim();
            var transform = Transform(uid);
            var coordinates = transform.Coordinates;
            if (id.Length == 0 ||
                name.Length == 0 ||
                !coordinates.IsValid(EntityManager) ||
                transform.MapID == MapId.Nullspace ||
                !ids.Add(id))
                continue;

            destinations.Add(new RelayGroundDestination(uid, id, name));
        }

        destinations.Sort(static (left, right) =>
        {
            var idComparison = StringComparer.OrdinalIgnoreCase.Compare(left.Id, right.Id);
            return idComparison != 0
                ? idComparison
                : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        });
        return destinations;
    }

    private bool TryGetGroundRelayDestination(string? id, out EntityUid destination)
    {
        destination = default;
        if (string.IsNullOrWhiteSpace(id))
            return false;

        foreach (var groundDestination in GetGroundRelayDestinations())
        {
            if (!string.Equals(groundDestination.Id, id.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            destination = groundDestination.Entity;
            return true;
        }

        return false;
    }

    private bool TryGetCustomRelayDestination(int index, out EntityCoordinates coordinates)
    {
        coordinates = default;
        if (index < 0 || index >= _relayDestinations.Count)
            return false;

        coordinates = _relayDestinations[index].Coordinates;
        return coordinates.IsValid(EntityManager);
    }

    private void OnHoundPadMapInit(Entity<YautjaHoundPadComponent> pad, ref MapInitEvent args)
    {
        if (pad.Comp.InternalCamera is { } existing &&
            Exists(existing) &&
            !TerminatingOrDeleted(existing))
        {
            return;
        }

        pad.Comp.InternalCamera = Spawn(pad.Comp.InternalCameraPrototype, new EntityCoordinates(pad.Owner, Vector2.Zero));
    }

    private void OnHoundPadShutdown(Entity<YautjaHoundPadComponent> pad, ref ComponentShutdown args)
    {
        if (pad.Comp.InternalCamera is not { } internalCamera ||
            TerminatingOrDeleted(internalCamera))
        {
            return;
        }

        QueueDel(internalCamera);
    }

    private void OnHoundPadUse(Entity<YautjaHoundPadComponent> pad, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!CanUseYautjaTech(args.User))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-houndpad-denied"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var internalCamera = GetOrCreateHoundPadInternalCamera(pad);
        _rmcCamera.RebuildComputerCameras(internalCamera);
        _ui.TryOpenUi(internalCamera, RMCCameraUiKey.Key, args.User);
    }

    private EntityUid GetOrCreateHoundPadInternalCamera(Entity<YautjaHoundPadComponent> pad)
    {
        if (pad.Comp.InternalCamera is { } existing &&
            Exists(existing) &&
            !TerminatingOrDeleted(existing))
        {
            return existing;
        }

        var internalCamera = Spawn(pad.Comp.InternalCameraPrototype, new EntityCoordinates(pad.Owner, Vector2.Zero));
        pad.Comp.InternalCamera = internalCamera;
        return internalCamera;
    }

    private void OnHellhoundTerminating(Entity<YautjaHellhoundComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!TryComp(ent.Owner, out YautjaHoundWatchedComponent? watched))
            return;

        foreach (var watcher in watched.Watchers.ToArray())
        {
            if (!TryComp(watcher, out YautjaHoundWatchingComponent? watching))
                continue;

            RestoreHoundWatcher(watcher, watching, ent.Owner);
        }
    }

    private void OnHoundWatchingShutdown(Entity<YautjaHoundWatchingComponent> ent, ref ComponentShutdown args)
    {
        RemoveHoundWatcher(ent.Owner, ent.Comp.Hellhound);
    }

    private void OnHoundWatchingTerminating(Entity<YautjaHoundWatchingComponent> ent, ref EntityTerminatingEvent args)
    {
        RemoveHoundWatcher(ent.Owner, ent.Comp.Hellhound);
    }

    private bool TryGetHellhoundForWatcher(EntityUid watcher, out EntityUid hellhound)
    {
        hellhound = default;
        EntityUid? fallback = null;

        var query = EntityQueryEnumerator<YautjaHellhoundComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (Deleted(uid) || _mobState.IsDead(uid))
                continue;

            if (component.YautjaOwner == watcher)
            {
                hellhound = uid;
                return true;
            }

            fallback ??= uid;
        }

        if (fallback is not { } fallbackUid)
            return false;

        hellhound = fallbackUid;
        return true;
    }

    private void RestoreHoundWatcher(EntityUid watcher, YautjaHoundWatchingComponent watching, EntityUid? watched = null)
    {
        RemoveHoundWatcher(watcher, watched ?? watching.Hellhound);

        if (TryComp(watcher, out EyeComponent? eye))
        {
            var target = watching.PreviousEyeTarget;
            if (target == null || !Exists(target.Value) || TerminatingOrDeleted(target.Value))
                target = watcher;

            _eye.SetTarget(watcher, target.Value, eye);
        }

        RemCompDeferred<YautjaHoundWatchingComponent>(watcher);
    }

    private void RemoveHoundWatcher(EntityUid watcher, EntityUid? watched)
    {
        if (watched is not { } watchedUid ||
            !TryComp(watchedUid, out YautjaHoundWatchedComponent? watchedComp))
        {
            return;
        }

        watchedComp.Watchers.Remove(watcher);
        if (watchedComp.Watchers.Count == 0)
            RemCompDeferred<YautjaHoundWatchedComponent>(watchedUid);
    }

    private void OnFalconDroneUse(Entity<YautjaFalconDroneComponent> drone, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryControlFalconDrone(drone, args.User, allowHeldBracer: false);
    }

    private void OnFalconDroneGetItemActions(Entity<YautjaFalconDroneComponent> drone, ref GetItemActionsEvent args)
    {
        if (args.InHands ||
            args.SlotFlags == null ||
            (args.SlotFlags.Value & SlotFlags.EARS) == 0)
        {
            return;
        }

        args.AddAction(ref drone.Comp.ControlAction, drone.Comp.ControlActionId);
    }

    private void OnFalconControl(Entity<YautjaFalconDroneComponent> drone, ref YautjaFalconControlActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryControlFalconDrone(drone, args.Performer, allowHeldBracer: true);
    }

    private void OnFalconListen(Entity<YautjaFalconDroneComponent> drone, ref ListenEvent args)
    {
        if (!_containers.TryGetContainingContainer((drone.Owner, null, null), out var container) ||
            !TryComp(container.Owner, out YautjaFalconDroneDeployedComponent? deployed) ||
            deployed.DroneItem != drone.Owner ||
            deployed.Controller is not { } controller ||
            args.Source == controller ||
            !HasComp<HumanoidAppearanceComponent>(args.Source) ||
            !TryComp(controller, out YautjaFalconControllerComponent? controlling) ||
            controlling.Drone != container.Owner ||
            !TryComp(controller, out ActorComponent? actor))
        {
            return;
        }

        var speech = _chatSystem.GetSpeechVerb(args.Source, args.Message);
        var nameEv = new TransformSpeakerNameEvent(args.Source, Name(args.Source));
        RaiseLocalEvent(args.Source, nameEv);
        if (nameEv.SpeechVerb != null && _prototype.TryIndex(nameEv.SpeechVerb, out var overrideSpeech))
            speech = overrideSpeech;

        var sourceName = FormattedMessage.EscapeText(nameEv.VoiceName);
        var speechVerb = Loc.GetString(_random.Pick(speech.SpeechVerbStrings));
        var relay = $"Falcon Relay: {sourceName} {speechVerb}, \"{FormattedMessage.EscapeText(args.Message)}\"";
        _chat.ChatMessageToOne(ChatChannel.Radio, relay, relay, drone.Owner, false, actor.PlayerSession.Channel);
    }

    private void OnFalconWearerExamined(Entity<InventoryComponent> ent, ref ExaminedEvent args)
    {
        if (!HasComp<HumanoidAppearanceComponent>(ent))
            return;

        var slots = _inventory.GetSlotEnumerator(ent.Owner, SlotFlags.EARS);
        while (slots.NextItem(out var item))
        {
            if (!HasComp<YautjaFalconDroneComponent>(item))
                continue;

            var name = FormattedMessage.EscapeText(Name(item));
            args.PushMarkup($"{name} is on their shoulder.");
        }
    }

    private void TryControlFalconDrone(
        Entity<YautjaFalconDroneComponent> drone,
        EntityUid user,
        bool allowHeldBracer)
    {
        if (!_actionBlocker.CanConsciouslyPerformAction(user))
            return;

        if (!HasComp<HumanoidAppearanceComponent>(user) || !CanUseYautjaTech(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-falcon-drone-denied"), user, user, PopupType.SmallCaution);
            return;
        }

        if (!TryGetFalconBracer(user, allowHeldBracer, out var bracer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-falcon-drone-bracer-required", ("item", drone.Owner)), user, user, PopupType.SmallCaution);
            return;
        }

        if (TryComp(user, out YautjaFalconControllerComponent? existing))
            CleanupFalconController(user, existing, deleteDrone: true, restoreEye: true);

        EntityUid? previousEyeTarget = user;
        if (TryComp(user, out EyeComponent? eye))
            previousEyeTarget = eye.Target ?? user;

        var deployed = Spawn(drone.Comp.DeployedPrototype, user.ToCoordinates());
        var deployedComp = EnsureComp<YautjaFalconDroneDeployedComponent>(deployed);
        deployedComp.DroneItem = drone.Owner;
        deployedComp.Controller = user;
        deployedComp.PreviousEyeTarget = previousEyeTarget;

        var itemContainer = _containers.EnsureContainer<ContainerSlot>(deployed, deployedComp.DroneItemContainerId);
        if (!_containers.Insert(drone.Owner, itemContainer, force: true))
        {
            QueueDel(deployed);
            _popup.PopupEntity(Loc.GetString("cmu-yautja-falcon-drone-denied"), user, user, PopupType.SmallCaution);
            return;
        }

        var controller = EnsureComp<YautjaFalconControllerComponent>(user);
        controller.Drone = deployed;
        controller.SourceBracer = bracer.Owner;
        controller.PreviousEyeTarget = previousEyeTarget;
        var sourceBracer = EnsureComp<YautjaFalconSourceBracerComponent>(bracer.Owner);
        sourceBracer.Controller = user;
        _actions.AddAction(user, ref controller.RecallAction, controller.RecallActionId);

        _transform.AttachToGridOrMap(deployed);
        EnsureComp<InputMoverComponent>(deployed);
        _mover.SetRelay(user, deployed);
        if (eye != null && TryComp(user, out ActorComponent? actor) && actor.PlayerSession != null)
            _eye.SetTarget(user, deployed, eye);
        _audio.PlayPvs(drone.Comp.DeploySound, deployed);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-falcon-drone-deployed"), user, user);
    }

    private bool TryGetFalconBracer(
        EntityUid user,
        bool allowHeldBracer,
        out Entity<YautjaBracerComponent> bracer)
    {
        if (_power.TryGetWornBracer(user, out bracer))
            return true;

        if (allowHeldBracer &&
            _hands.GetActiveItem(user) is { } held &&
            TryComp(held, out YautjaBracerComponent? heldBracer))
        {
            bracer = (held, heldBracer);
            return true;
        }

        return false;
    }

    private void OnFalconRecall(Entity<YautjaFalconControllerComponent> ent, ref YautjaFalconRecallActionEvent args)
    {
        if (args.Handled || !_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        CleanupFalconController(ent.Owner, ent.Comp, deleteDrone: true, restoreEye: true);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-falcon-drone-recalled"), args.Performer, args.Performer);
    }

    private void OnFalconSourceBracerUnequipped(Entity<YautjaFalconSourceBracerComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.Controller != args.Equipee ||
            !TryComp(args.Equipee, out YautjaFalconControllerComponent? controlling) ||
            controlling.SourceBracer != ent.Owner)
        {
            return;
        }

        CleanupFalconController(args.Equipee, controlling, deleteDrone: true, restoreEye: true);
    }

    private void OnFalconDroneTerminating(Entity<YautjaFalconDroneDeployedComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.ReturnEyeOnDelete ||
            ent.Comp.Controller is not { } controller ||
            !TryComp(controller, out YautjaFalconControllerComponent? controlling) ||
            controlling.Drone != ent.Owner)
        {
            return;
        }

        CleanupFalconController(controller, controlling, deleteDrone: false, restoreEye: true);
    }

    private void OnFalconEmpPulse(Entity<YautjaFalconDroneDeployedComponent> ent, ref EmpPulseEvent args)
    {
        if (ConvertFalconDroneToWreckage(ent, ent.Comp.DisabledPrototype))
            args.Affected = true;
    }

    private void OnFalconDestroyed(Entity<YautjaFalconDroneDeployedComponent> ent, ref DestructionEventArgs args)
    {
        ConvertFalconDroneToWreckage(ent, ent.Comp.DestroyedPrototype);
    }

    private void OnFalconControllerShutdown(Entity<YautjaFalconControllerComponent> ent, ref ComponentShutdown args)
    {
        CleanupFalconDrone(ent.Comp.Drone);
    }

    private void OnFalconControllerTerminating(Entity<YautjaFalconControllerComponent> ent, ref EntityTerminatingEvent args)
    {
        CleanupFalconDrone(ent.Comp.Drone);
    }

    private void CleanupFalconController(
        EntityUid controller,
        YautjaFalconControllerComponent controlling,
        bool deleteDrone,
        bool restoreEye)
    {
        var drone = controlling.Drone;
        RestoreFalconDroneItem(controller, drone);

        if (deleteDrone)
            SuppressFalconReturn(drone);

        if (restoreEye && TryComp(controller, out EyeComponent? eye))
            RestoreFalconEye(controller, controlling.PreviousEyeTarget, eye);

        RemComp<RelayInputMoverComponent>(controller);
        _actions.RemoveAction(controller, controlling.RecallAction);
        if (controlling.SourceBracer is { } bracer &&
            TryComp(bracer, out YautjaFalconSourceBracerComponent? source) &&
            source.Controller == controller)
        {
            source.Controller = null;
            RemCompDeferred<YautjaFalconSourceBracerComponent>(bracer);
        }
        RemCompDeferred<YautjaFalconControllerComponent>(controller);

        if (deleteDrone)
            CleanupFalconDrone(drone);
    }

    private void CleanupFalconDrone(EntityUid drone)
    {
        SuppressFalconReturn(drone);
        if (Exists(drone) && !TerminatingOrDeleted(drone))
            QueueDel(drone);
    }

    private bool ConvertFalconDroneToWreckage(
        Entity<YautjaFalconDroneDeployedComponent> drone,
        EntProtoId wreckagePrototype)
    {
        if (drone.Comp.ConvertingToWreckage)
            return false;

        drone.Comp.ConvertingToWreckage = true;
        drone.Comp.ReturnDroneItemOnDelete = false;
        drone.Comp.ReturnEyeOnDelete = false;

        var coordinates = Transform(drone).Coordinates;
        if (drone.Comp.DroneItem is { } droneItem && Exists(droneItem) && !TerminatingOrDeleted(droneItem))
        {
            drone.Comp.DroneItem = null;
            QueueDel(droneItem);
        }

        if (drone.Comp.Controller is { } controller &&
            TryComp(controller, out YautjaFalconControllerComponent? controlling) &&
            controlling.Drone == drone.Owner)
        {
            CleanupFalconController(controller, controlling, deleteDrone: false, restoreEye: true);
        }

        Spawn(wreckagePrototype, coordinates);
        if (Exists(drone) && !TerminatingOrDeleted(drone))
            QueueDel(drone);

        return true;
    }

    private void SuppressFalconReturn(EntityUid drone)
    {
        if (TryComp(drone, out YautjaFalconDroneDeployedComponent? deployed))
        {
            deployed.ReturnEyeOnDelete = false;
            deployed.ReturnDroneItemOnDelete = false;
        }
    }

    private void RestoreFalconDroneItem(EntityUid controller, EntityUid drone)
    {
        if (!TryComp(drone, out YautjaFalconDroneDeployedComponent? deployed) ||
            !deployed.ReturnDroneItemOnDelete ||
            deployed.DroneItem is not { } droneItem ||
            Deleted(droneItem) ||
            !Exists(controller) ||
            TerminatingOrDeleted(controller))
        {
            return;
        }

        deployed.DroneItem = null;
        deployed.ReturnDroneItemOnDelete = false;

        foreach (var slot in FalconReturnSlots)
        {
            if (_inventory.TryGetSlotEntity(controller, slot, out _))
                continue;

            if (_inventory.TryEquip(
                    controller,
                    controller,
                    droneItem,
                    slot,
                    silent: true,
                    force: true,
                    checkDoafter: false,
                    triggerHandContact: false,
                    doRangeCheck: false))
            {
                return;
            }
        }

        _hands.PickupOrDrop(controller, droneItem, checkActionBlocker: false, animate: false, dropNear: true);
    }

    private void RestoreFalconEye(EntityUid controller, EntityUid? previousTarget, EyeComponent eye)
    {
        var target = previousTarget;
        if (target == null || !Exists(target.Value) || TerminatingOrDeleted(target.Value))
            target = controller;

        _eye.SetTarget(controller, target.Value, eye);
    }

    private bool CanUseYautjaTech(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) || HasComp<YautjaTechAuthorizedComponent>(user);
    }

    private bool CanReachRelayBeaconAttackSelf(EntityUid user)
    {
        return HasComp<HumanoidAppearanceComponent>(user);
    }

    public static bool CanUseRelayBeacon(bool yautja, bool youngblood, bool techAuthorized)
    {
        return !youngblood && (yautja || techAuthorized);
    }

    private bool CanUseRelayBeacon(EntityUid user)
    {
        return CanUseRelayBeacon(
               HasComp<YautjaComponent>(user),
               HasComp<YautjaYoungbloodComponent>(user),
               HasComp<YautjaTechAuthorizedComponent>(user)) &&
               !_mobState.IsDead(user);
    }

    private bool CanUseRelayBeaconAttackSelf(EntityUid beacon, EntityUid user, bool popup)
    {
        var hasTech = HasComp<YautjaComponent>(user) || HasComp<YautjaTechAuthorizedComponent>(user);
        if (!hasTech || !CanInteractWithRelayBeacon(beacon, user))
        {
            if (popup)
                PopupRelayBeaconDenied(user, youngblood: false);
            return false;
        }

        if (HasComp<YautjaYoungbloodComponent>(user))
        {
            if (popup)
                PopupRelayBeaconDenied(user, youngblood: true);
            return false;
        }

        if (_mobState.IsDead(user))
            return false;

        return true;
    }

    private bool CanInteractWithRelayBeacon(EntityUid beacon, EntityUid user)
    {
        return _actionBlocker.CanConsciouslyPerformAction(user) &&
               _actionBlocker.CanUseHeldEntity(user, beacon);
    }

    private bool CanAddRelayDestination(EntityUid user)
    {
        return CanUseYautjaTech(user) &&
               _mobState.IsAlive(user) &&
               IsGroundLevel(user) &&
               !HasComp<XenoNestedComponent>(user);
    }

    private bool IsGroundLevel(EntityUid user)
    {
        var xform = Transform(user);
        return xform.GridUid is { } grid && HasComp<RMCPlanetComponent>(grid) ||
               xform.MapUid is { } map && HasComp<RMCPlanetComponent>(map);
    }

    private void PopupRelayBeaconDenied(EntityUid user, bool? youngblood = null)
    {
        var isYoungblood = youngblood ?? HasComp<YautjaYoungbloodComponent>(user);
        var message = isYoungblood
            ? "cmu-yautja-relay-beacon-youngblood-denied"
            : "cmu-yautja-relay-beacon-denied";

        _popup.PopupEntity(Loc.GetString(message), user, user, PopupType.SmallCaution);
    }
}
