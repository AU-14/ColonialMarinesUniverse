using Content.Server._RMC14.Emote;
using Content.Server.Administration.Logs;
using Content.Server.NPC.HTN;
using Content.Shared.ActionBlocker;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Tether;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Alert;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.StatusEffect;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaTrapSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private RMCEmoteSystem _emote = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StatusEffectQuerySystem _status = default!;
    [Dependency] private StepTriggerSystem _stepTrigger = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private YautjaRitualSystem _ritual = default!;

    private const string YautjaInterferenceStatus = "YautjaInterference";
    private const string HuntingTrapInactiveState = "yauttrap0";
    private const string HuntingTrapArmedState = "yauttrap1";
    private const string HuntingTrapGrassState = "yauttrapgrass";
    private const string HuntingTrapDirtState = "yauttrapdirt";

    private static readonly ProtoId<NpcFactionPrototype> DefaultYautjaFaction = "CMUYautja";
    private static readonly ProtoId<NpcFactionPrototype> BadBloodYautjaFaction = "CMUYautjaBadBlood";

    private static readonly DamageSpecifier HuntingTrapAnimalDamage = new()
    {
        DamageDict = new()
        {
            { "Blunt", 20 },
        },
    };

    private static readonly ProtoId<EmotePrototype> XenoHelpEmote = "XenoHelp";
    private static readonly ProtoId<EmotePrototype> HumanPainEmote = "Scream";
    private static readonly HashSet<string> HuntingTrapGrassTiles =
    [
        "FloorGrass",
        "FloorGrassJungle",
        "FloorGrassDark",
        "FloorGrassLight",
    ];

    private bool _restoringTetheredPosition;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaTrapComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<YautjaTrapComponent, YautjaTrapArmDoAfterEvent>(OnArmDoAfter);
        SubscribeLocalEvent<YautjaTrapComponent, YautjaTrapRangeSelectedEvent>(OnTrapRangeSelected);
        SubscribeLocalEvent<YautjaTrapComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<YautjaTrapComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        SubscribeLocalEvent<YautjaTrapComponent, GettingPickedUpAttemptEvent>(OnGettingPickedUpAttempt);
        SubscribeLocalEvent<YautjaTrapComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<YautjaTrapComponent, StepTriggeredOnEvent>(OnStepTriggeredOn);
        SubscribeLocalEvent<YautjaTrapComponent, ComponentShutdown>(OnTrapShutdown);
        SubscribeLocalEvent<RMCTetherComponent, XenoHealAttemptEvent>(OnTetheredXenoHealAttempt);
        SubscribeLocalEvent<RMCTetherComponent, MoveEvent>(OnTetheredMove);
        SubscribeLocalEvent<RMCTetherComponent, YautjaTrapBreakFreeAlertEvent>(OnBreakFreeAlert);
        SubscribeLocalEvent<RMCTetherComponent, YautjaTrapBreakFreeDoAfterEvent>(OnBreakFreeDoAfter);
        SubscribeLocalEvent<RMCTetherComponent, ComponentShutdown>(OnTetherShutdown);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaTrapComponent>();
        while (query.MoveNext(out var uid, out var trap))
        {
            if (trap.TrappedMob is not { } trapped)
                continue;

            if (Deleted(trapped) ||
                now >= trap.ReleaseAt ||
                !IsWithinTetherRange(uid, trapped, trap.TetherRange))
            {
                ReleaseTrappedMob((uid, trap));
            }
        }
    }

    private void OnUseInHand(Entity<YautjaTrapComponent> trap, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStartArmTrap(trap, args.User);
    }

    private bool TryStartArmTrap(Entity<YautjaTrapComponent> trap, EntityUid user)
    {
        if (!CanUseYautjaTech(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-arm-denied"), user, user, PopupType.SmallCaution);
            return true;
        }

        if (trap.Comp.Armed)
            return true;

        if (!CanArmTrap(user, trap.Owner))
            return true;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            trap.Comp.ArmDelay,
            new YautjaTrapArmDoAfterEvent(),
            trap.Owner,
            target: trap.Owner,
            used: trap.Owner)
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

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnArmDoAfter(Entity<YautjaTrapComponent> trap, ref YautjaTrapArmDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        TryArmTrap(trap, args.User);
    }

    private void OnInteractHand(Entity<YautjaTrapComponent> trap, ref InteractHandEvent args)
    {
        if (args.Handled || !IsActive(trap.Comp))
            return;

        if (trap.Comp.TrappedMob == args.User && trap.Comp.TrappedMobInteractResists)
        {
            args.Handled = TryStartBreakFree(args.User, trap);
            return;
        }

        if (CanUseYautjaTech(args.User))
        {
            args.Handled = TryRecoverTrap(trap, args.User);
            return;
        }

        if (trap.Comp.TrappedMob != null && trap.Comp.TrappedMobInteractResists)
        {
            args.Handled = true;
            return;
        }

        if (!trap.Comp.Armed)
            return;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-non-yautja-trigger", ("trap", trap.Owner)), args.User, args.User, PopupType.SmallCaution);
        args.Handled = TryTriggerTrap(trap, args.User);
    }

    private void OnGetInteractionVerbs(Entity<YautjaTrapComponent> trap, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess ||
            !args.CanInteract ||
            !CanUseYautjaTech(args.User))
        {
            return;
        }

        var user = args.User;
        if (IsActive(trap.Comp))
        {
            args.Verbs.Add(new InteractionVerb
            {
                Text = Loc.GetString("cmu-yautja-trap-recover-verb"),
                Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/pickup.svg.192dpi.png")),
                Act = () => TryRecoverTrap(trap, user),
            });
        }

        if (!trap.Comp.CanConfigureRange)
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-yautja-trap-configure-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () => OpenConfigureTrapDialog(trap, user),
        });
    }

    private void OpenConfigureTrapDialog(Entity<YautjaTrapComponent> trap, EntityUid user)
    {
        if (!CanUseYautjaTech(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-configure-denied"), user, user, PopupType.SmallCaution);
            return;
        }

        var userNet = GetNetEntity(user);
        var options = new List<DialogOption>
        {
            new("2", new YautjaTrapRangeSelectedEvent(userNet, 2)),
            new("3", new YautjaTrapRangeSelectedEvent(userNet, 3)),
            new("4", new YautjaTrapRangeSelectedEvent(userNet, 4)),
            new("5", new YautjaTrapRangeSelectedEvent(userNet, 5)),
            new("6", new YautjaTrapRangeSelectedEvent(userNet, 6)),
            new("7", new YautjaTrapRangeSelectedEvent(userNet, 7)),
        };

        _dialog.OpenOptions(
            trap.Owner,
            user,
            Loc.GetString("cmu-yautja-trap-configure-title"),
            options,
            Loc.GetString("cmu-yautja-trap-configure-message"));
    }

    private void OnTrapRangeSelected(Entity<YautjaTrapComponent> trap, ref YautjaTrapRangeSelectedEvent args)
    {
        if (!TryGetEntity(args.User, out var user))
            return;

        if (!CanUseYautjaTech(user.Value))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-configure-denied"), user.Value, user.Value, PopupType.SmallCaution);
            return;
        }

        if (args.Range != 2)
            return;

        trap.Comp.TetherRange = 2f;
        Dirty(trap);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-configure-set", ("range", 2)), user.Value, user.Value);
    }

    private void OnGettingPickedUpAttempt(Entity<YautjaTrapComponent> trap, ref GettingPickedUpAttemptEvent args)
    {
        if (!IsActive(trap.Comp))
            return;

        if (CanUseYautjaTech(args.User))
        {
            TryDisarmTrap(trap, args.User);
            return;
        }

        args.Cancel();
        if (trap.Comp.Armed)
            TryTriggerTrap(trap, args.User);
    }

    private void OnStepTriggerAttempt(Entity<YautjaTrapComponent> trap, ref StepTriggerAttemptEvent args)
    {
        args.Continue = true;

        if (IsBuckled(args.Tripper))
        {
            args.Cancelled = true;
            return;
        }

        if (TryAvoidTrapStep(trap, args.Tripper))
        {
            args.Cancelled = true;
            return;
        }

        if (!CanTriggerTrap(trap, args.Tripper))
            args.Cancelled = true;
    }

    private void OnStepTriggeredOn(Entity<YautjaTrapComponent> trap, ref StepTriggeredOnEvent args)
    {
        if (IsBuckled(args.Tripper))
            return;

        if (TryAvoidTrapStep(trap, args.Tripper))
            return;

        if (TrySpringAnimalTrap(trap, args.Tripper))
            return;

        if (TryTriggerTrap(trap, args.Tripper))
            PopupStepTriggerObservers(trap, args.Tripper);
    }

    private void OnTrapShutdown(Entity<YautjaTrapComponent> trap, ref ComponentShutdown args)
    {
        var trapped = trap.Comp.TrappedMob;
        trap.Comp.TrappedMob = null;
        trap.Comp.ReleaseAt = TimeSpan.Zero;

        if (trapped is { } trappedUid && !Deleted(trappedUid))
        {
            ClearBreakFreeAlert(trappedUid, trap.Comp);
            CancelBreakFreeDoAfters(trappedUid);
            RemComp<RMCTetherComponent>(trappedUid);
        }
    }

    private void OnTetheredXenoHealAttempt(Entity<RMCTetherComponent> ent, ref XenoHealAttemptEvent args)
    {
        if (ent.Comp.TetherOrigin is not { } origin ||
            !TryComp<YautjaTrapComponent>(origin, out var trap) ||
            !trap.BlocksXenoHeal ||
            trap.TrappedMob != ent.Owner)
        {
            return;
        }

        args.Cancelled = true;
    }

    private void OnBreakFreeAlert(Entity<RMCTetherComponent> ent, ref YautjaTrapBreakFreeAlertEvent args)
    {
        if (args.Handled || !TryGetTetheredTrap(ent, out var trap))
            return;

        args.Handled = TryStartBreakFree(ent.Owner, trap);
    }

    private bool TryStartBreakFree(EntityUid trapped, Entity<YautjaTrapComponent> trap)
    {
        if (!TryComp<RMCTetherComponent>(trapped, out var tether) ||
            tether.TetherOrigin != trap.Owner ||
            trap.Comp.TrappedMob != trapped)
        {
            return false;
        }

        if (IsBreakingFree(trapped))
            return true;

        var doAfter = new DoAfterArgs(
            EntityManager,
            trapped,
            trap.Comp.BreakFreeDelay,
            new YautjaTrapBreakFreeDoAfterEvent(),
            trapped,
            target: trapped,
            used: trap.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            BlockDuplicate = true,
            CancelDuplicate = false,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            ForceVisible = true,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnBreakFreeDoAfter(Entity<RMCTetherComponent> ent, ref YautjaTrapBreakFreeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !TryGetTetheredTrap(ent, out _))
            return;

        args.Handled = true;
        RemComp<RMCTetherComponent>(ent.Owner);
    }

    private void OnTetherShutdown(Entity<RMCTetherComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.TetherOrigin is not { } origin ||
            !TryComp<YautjaTrapComponent>(origin, out var trap) ||
            trap.TrappedMob != ent.Owner)
        {
            return;
        }

        ReleaseTrappedMob((origin, trap), removeTether: false);
    }

    private void OnTetheredMove(Entity<RMCTetherComponent> ent, ref MoveEvent args)
    {
        if (_restoringTetheredPosition ||
            TerminatingOrDeleted(ent.Owner) ||
            !TryGetTetheredTrap(ent, out var trap) ||
            IsWithinTetherRange(trap.Owner, ent.Owner, trap.Comp.TetherRange))
        {
            return;
        }

        // CMSS13's tether cancels a move that would cross tether_range. MoveEvent
        // is raised after the transform changes, so restore the previous local
        // coordinates and keep the tether active instead of freeing the victim.
        _restoringTetheredPosition = true;
        try
        {
            _transform.SetCoordinates(
                (ent.Owner, Transform(ent.Owner), MetaData(ent.Owner)),
                args.OldPosition,
                unanchor: false);
        }
        finally
        {
            _restoringTetheredPosition = false;
        }
    }

    public bool TryArmTrap(Entity<YautjaTrapComponent> trap, EntityUid user)
    {
        if (!CanUseYautjaTech(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-arm-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (trap.Comp.Armed)
            return true;

        if (!CanArmTrap(user, trap.Owner))
            return false;

        if (_hands.IsHolding(user, trap.Owner) && !_hands.TryDrop(user, trap.Owner))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-arm-failed"), user, user, PopupType.SmallCaution);
            return false;
        }

        trap.Comp.TrapOwner = user;
        trap.Comp.ArmedFaction = ResolveArmedFaction(user);
        trap.Comp.Armed = true;
        Dirty(trap);

        var xform = Transform(trap);
        _transform.AnchorEntity(trap, xform);

        if (TryComp<PhysicsComponent>(trap, out var physics))
            _physics.SetBodyType(trap, BodyType.Static, body: physics);

        if (TryComp<StepTriggerComponent>(trap, out var trigger))
            _stepTrigger.SetActive(trap, true, trigger);

        _appearance.SetData(trap, ToggleableVisuals.Enabled, true);
        _appearance.SetData(trap, ToggleableVisuals.Layer, GetArmedTrapVisualState(trap));
        _audio.PlayPvs(trap.Comp.ArmSound, trap);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-armed"), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} has armed {AdminLogArticleName(trap.Owner)} {ToPrettyString(trap.Owner):trap}");
        return true;
    }

    private bool CanArmTrap(EntityUid user, EntityUid trap)
    {
        return _actionBlocker.CanConsciouslyPerformAction(user) &&
               _actionBlocker.CanUseHeldEntity(user, trap);
    }

    private bool CanUseYautjaTech(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               HasComp<YautjaTechAuthorizedComponent>(user);
    }

    public bool TryDisarmTrap(Entity<YautjaTrapComponent> trap, EntityUid user)
    {
        if (!IsActive(trap.Comp))
            return true;

        if (!CanUseYautjaTech(user))
            return false;

        ReleaseTrappedMob(trap);

        trap.Comp.Armed = false;
        Dirty(trap);

        _transform.Unanchor(trap);

        if (TryComp<PhysicsComponent>(trap, out var physics))
            _physics.SetBodyType(trap, BodyType.Dynamic, body: physics);

        if (TryComp<StepTriggerComponent>(trap, out var trigger))
            _stepTrigger.SetActive(trap, false, trigger);

        SetInactiveTrapVisual(trap);
        _audio.PlayPvs(trap.Comp.DisarmSound, trap);
        _popup.PopupEntity(Loc.GetString(trap.Comp.DisarmPopup, ("trap", trap.Owner)), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(user):player} has disarmed {AdminLogArticleName(trap.Owner)} {ToPrettyString(trap.Owner):trap}");
        return true;
    }

    public bool TryRecoverTrap(Entity<YautjaTrapComponent> trap, EntityUid user)
    {
        if (!IsActive(trap.Comp) || !CanUseYautjaTech(user))
            return false;

        if (!TryDisarmTrap(trap, user))
            return false;

        if (!_hands.TryGetEmptyHand(user, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-recover-no-hand"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_hands.TryPickupAnyHand(user, trap.Owner, checkActionBlocker: false))
            return false;

        if (trap.Comp.ShowRecoverPopup)
            _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-recovered"), user, user);

        return true;
    }

    public bool TryTriggerTrap(Entity<YautjaTrapComponent> trap, EntityUid tripper)
    {
        if (!CanTriggerTrap(trap, tripper))
            return false;

        trap.Comp.Armed = false;
        trap.Comp.TrappedMob = tripper;
        trap.Comp.ReleaseAt = _timing.CurTime + trap.Comp.TrapDuration;
        Dirty(trap);

        var xform = Transform(trap);
        if (!xform.Anchored)
            _transform.AnchorEntity(trap, xform);

        if (TryComp<StepTriggerComponent>(trap, out var trigger))
            _stepTrigger.SetActive(trap, false, trigger);

        SetInactiveTrapVisual(trap);

        var tether = EnsureComp<RMCTetherComponent>(tripper);
        tether.TetherOrigin = trap;
        tether.StaticTetherOrigin = _transform.GetMapCoordinates(trap);
        Dirty(tripper, tether);
        ShowBreakFreeAlert(tripper, trap.Comp);

        if (trap.Comp.TrapOwner is { } trapOwner)
            _ritual.TryClaimCaptive(trapOwner, tripper, true);

        ApplyTriggerEffects(trap, tripper);
        if (trap.Comp.BroadcastOnTrigger)
            BroadcastTriggerToYautja(trap);

        _audio.PlayPvs(trap.Comp.TriggerSound, trap);
        _popup.PopupEntity(Loc.GetString(trap.Comp.TriggerPopup, ("trap", trap.Owner)), tripper, tripper, PopupType.MediumCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(tripper):target} was caught in {AdminLogArticleName(trap.Owner)} {ToPrettyString(trap.Owner):trap}");

        return true;
    }

    private ProtoId<NpcFactionPrototype> ResolveArmedFaction(EntityUid user)
    {
        if (!HasComp<YautjaComponent>(user))
            return DefaultYautjaFaction;

        if (TryComp<NpcFactionMemberComponent>(user, out var faction) &&
            faction.Factions.Contains(BadBloodYautjaFaction))
        {
            return BadBloodYautjaFaction;
        }

        return DefaultYautjaFaction;
    }

    private bool TryAvoidTrapStep(Entity<YautjaTrapComponent> trap, EntityUid tripper)
    {
        if (!trap.Comp.Armed)
            return false;

        if (HasComp<YautjaComponent>(tripper))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-yautja-avoid"), tripper, tripper);
            return true;
        }

        if (!IsBadBloodHiveAvoidingBadBloodTrap(trap, tripper))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-trap-badblood-hive-avoid"), tripper, tripper);
        return true;
    }

    private bool IsBadBloodHiveAvoidingBadBloodTrap(Entity<YautjaTrapComponent> trap, EntityUid tripper)
    {
        if (trap.Comp.ArmedFaction != BadBloodYautjaFaction ||
            !HasComp<XenoComponent>(tripper) ||
            !TryComp<HiveMemberComponent>(tripper, out var hiveMember) ||
            hiveMember.Hive is not { } hiveUid)
        {
            return false;
        }

        return _hive.HasFaction(hiveUid, BadBloodYautjaFaction);
    }

    private bool TrySpringAnimalTrap(Entity<YautjaTrapComponent> trap, EntityUid tripper)
    {
        if (!trap.Comp.Armed ||
            trap.Comp.TrappedMob != null ||
            Deleted(tripper) ||
            !IsSimpleAnimal(tripper) ||
            !TryComp<MobStateComponent>(tripper, out var mobState) ||
            !_mobState.IsAlive(tripper, mobState))
        {
            return false;
        }

        trap.Comp.Armed = false;
        Dirty(trap);

        if (TryComp<StepTriggerComponent>(trap, out var trigger))
            _stepTrigger.SetActive(trap, false, trigger);

        SetInactiveTrapVisual(trap);

        if (TryComp<DamageableComponent>(tripper, out var damageable))
            _damage.AddDamage(tripper, damageable, new DamageSpecifier(HuntingTrapAnimalDamage));

        return true;
    }

    private bool IsSimpleAnimal(EntityUid uid)
    {
        return HasComp<ButcherableComponent>(uid) &&
               HasComp<HTNComponent>(uid) &&
               !HasComp<HumanoidAppearanceComponent>(uid) &&
               !HasComp<XenoComponent>(uid);
    }

    private bool IsBuckled(EntityUid uid)
    {
        return TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled;
    }

    private void BroadcastTriggerToYautja(Entity<YautjaTrapComponent> trap)
    {
        var message = Loc.GetString("cmu-yautja-trap-trigger-broadcast", ("area", _areas.GetAreaName(trap.Owner)));
        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!Deleted(uid) && IsYautjaInTrapBroadcastFaction(uid, trap.Comp.ArmedFaction))
                _popup.PopupEntity(message, uid, uid, PopupType.Medium);
        }
    }

    private bool IsYautjaInTrapBroadcastFaction(EntityUid uid, ProtoId<NpcFactionPrototype> faction)
    {
        if (TryComp<NpcFactionMemberComponent>(uid, out var member))
        {
            if (member.Factions.Contains(faction))
                return true;

            return faction == DefaultYautjaFaction &&
                   !member.Factions.Contains(BadBloodYautjaFaction);
        }

        return faction == DefaultYautjaFaction;
    }

    private void PopupStepTriggerObservers(Entity<YautjaTrapComponent> trap, EntityUid tripper)
    {
        var message = Loc.GetString("cmu-yautja-trap-observer-triggered", ("target", tripper), ("trap", trap.Owner));
        _popup.PopupEntity(message, tripper, Filter.PvsExcept(tripper), true, PopupType.MediumCaution);
    }

    private void ApplyTriggerEffects(Entity<YautjaTrapComponent> trap, EntityUid tripper)
    {
        if (trap.Comp.ForceHumanPainEmote && HasComp<HumanoidAppearanceComponent>(tripper))
            _emote.TryEmoteWithChat(tripper, HumanPainEmote, forceEmote: true);

        if (!HasComp<XenoComponent>(tripper))
            return;

        if (trap.Comp.ForceXenoHelpEmote)
            _emote.TryEmoteWithChat(tripper, XenoHelpEmote, forceEmote: true);

        if (trap.Comp.XenoInterferenceDuration > TimeSpan.Zero)
            _status.TryAddStatusEffect(tripper, YautjaInterferenceStatus, trap.Comp.XenoInterferenceDuration, true);
    }

    private bool CanTriggerTrap(Entity<YautjaTrapComponent> trap, EntityUid tripper)
    {
        if (!trap.Comp.Armed ||
            trap.Comp.TrappedMob != null ||
            Deleted(tripper) ||
            (!trap.Comp.CanTriggerYautja && HasComp<YautjaComponent>(tripper)) ||
            !TryComp<MobStateComponent>(tripper, out var mobState) ||
            !_mobState.IsAlive(tripper, mobState))
        {
            return false;
        }

        return true;
    }

    private bool IsWithinTetherRange(EntityUid trap, EntityUid trapped, float range)
    {
        if (range <= 0)
            return true;

        return Transform(trap).Coordinates.TryDistance(EntityManager, _transform, Transform(trapped).Coordinates, out var distance) &&
               distance <= range;
    }

    private void ReleaseTrappedMob(Entity<YautjaTrapComponent> trap, bool removeTether = true)
    {
        var trapped = trap.Comp.TrappedMob;

        trap.Comp.TrappedMob = null;
        trap.Comp.ReleaseAt = TimeSpan.Zero;
        Dirty(trap);

        if (trapped is { } trappedUid && !Deleted(trappedUid))
        {
            if (trap.Comp.LogTrappedMobFreed)
                _adminLog.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(trappedUid):target} was freed from {AdminLogArticleName(trap.Owner)} {ToPrettyString(trap.Owner):trap}");

            ClearBreakFreeAlert(trappedUid, trap.Comp);
            CancelBreakFreeDoAfters(trappedUid);

            if (removeTether)
                RemComp<RMCTetherComponent>(trappedUid);
        }

        _transform.Unanchor(trap);

        if (TryComp<PhysicsComponent>(trap, out var physics))
            _physics.SetBodyType(trap, BodyType.Dynamic, body: physics);

        SetInactiveTrapVisual(trap);
    }

    private void ShowBreakFreeAlert(EntityUid trapped, YautjaTrapComponent trap)
    {
        if (trap.BreakFreeAlert is { } alert)
            _alerts.ShowAlert(trapped, alert);
    }

    private void ClearBreakFreeAlert(EntityUid trapped, YautjaTrapComponent trap)
    {
        if (trap.BreakFreeAlert is { } alert)
            _alerts.ClearAlert(trapped, alert);
    }

    private bool IsBreakingFree(EntityUid trapped)
    {
        if (!TryComp<DoAfterComponent>(trapped, out var doAfter))
            return false;

        foreach (var active in doAfter.DoAfters.Values)
        {
            if (IsActiveBreakFreeDoAfter(active))
                return true;
        }

        return false;
    }

    private void CancelBreakFreeDoAfters(EntityUid trapped)
    {
        if (!TryComp<DoAfterComponent>(trapped, out var doAfter))
            return;

        var pending = new List<ushort>();
        foreach (var active in doAfter.DoAfters.Values)
        {
            if (IsActiveBreakFreeDoAfter(active))
                pending.Add(active.Index);
        }

        foreach (var index in pending)
            _doAfter.Cancel(trapped, index, doAfter);
    }

    private static bool IsActiveBreakFreeDoAfter(Content.Shared.DoAfter.DoAfter doAfter)
    {
        return !doAfter.Cancelled &&
               !doAfter.Completed &&
               doAfter.Args.Event is YautjaTrapBreakFreeDoAfterEvent;
    }

    private bool TryGetTetheredTrap(Entity<RMCTetherComponent> ent, out Entity<YautjaTrapComponent> trap)
    {
        trap = default;
        if (ent.Comp.TetherOrigin is not { } origin ||
            !TryComp<YautjaTrapComponent>(origin, out var trapComp) ||
            trapComp.TrappedMob != ent.Owner)
        {
            return false;
        }

        trap = (origin, trapComp);
        return true;
    }

    private string GetArmedTrapVisualState(EntityUid trap)
    {
        var xform = Transform(trap);
        if (xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return HuntingTrapArmedState;
        }

        var indices = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
        if (!_map.TryGetTile(grid, indices, out var tile) ||
            tile.IsEmpty ||
            !_tileDefs.TryGetDefinition(tile.TypeId, out var tileDef))
        {
            return HuntingTrapArmedState;
        }

        if (tileDef.ID == "FloorDirt")
            return HuntingTrapDirtState;

        if (HuntingTrapGrassTiles.Contains(tileDef.ID))
            return HuntingTrapGrassState;

        return HuntingTrapArmedState;
    }

    private void SetInactiveTrapVisual(EntityUid trap)
    {
        _appearance.SetData(trap, ToggleableVisuals.Enabled, false);
        _appearance.SetData(trap, ToggleableVisuals.Layer, HuntingTrapInactiveState);
    }

    private static bool IsActive(YautjaTrapComponent trap)
    {
        return trap.Armed || trap.TrappedMob != null;
    }

    private string AdminLogArticleName(EntityUid trap)
    {
        var name = MetaData(trap).EntityName;
        return StartsWithVowelSound(name) ? $"an {name}" : $"a {name}";
    }

    private static bool StartsWithVowelSound(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.TrimStart()[0] switch
        {
            'a' or 'e' or 'i' or 'o' or 'u' or 'A' or 'E' or 'I' or 'O' or 'U' => true,
            _ => false,
        };
    }
}
