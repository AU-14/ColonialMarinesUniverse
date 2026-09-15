using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Systems;
using Content.Server.DoAfter;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.UserInterface;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Synth;
using Content.Shared.Actions;
using Content.Shared.Administration;
using Content.Shared.Bed.Sleep;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Round.Antags.Rider;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.Forensics.Components;
using Content.Shared.Inventory;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Linq;

namespace Content.Server.CMU14.Round.Antags.Rider;

/// <summary>
/// The Rider antag: a hatchling that latches onto unconscious or willing hosts,
/// rides them from an internal container, and trades grip for leverage over them.
/// </summary>
public sealed partial class RiderSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly INetManager _netMan = default!;

    [Dependency] private readonly SharedCMChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPainShockSystem _pain = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private const string RiderContainerSlot = "rider_hatchling_slot";
    private const string CrawlResidue = "traces of an unknown biological film";
    private static readonly ProtoId<DamageTypePrototype> PunishDamage = "Blunt";

    private int _characterLimit = 1000;

    public override void Initialize()
    {
        Subs.CVar(_config, CCVars.ChatMaxMessageLength, limit => _characterLimit = limit, true);

        SubscribeLocalEvent<RiderComponent, ComponentStartup>(OnHatchlingStartup);
        SubscribeLocalEvent<RiderComponent, RiderLatchActionEvent>(OnLatchAction);
        SubscribeLocalEvent<RiderComponent, RiderLatchDoAfterEvent>(OnLatchDoAfter);
        SubscribeLocalEvent<RiderComponent, RiderVoiceActionEvent>(OnVoiceAction);
        SubscribeLocalEvent<RiderComponent, RiderVoiceMessage>(OnVoiceMessage);
        SubscribeLocalEvent<RiderComponent, RiderPunishActionEvent>(OnPunishAction);
        SubscribeLocalEvent<RiderComponent, RiderSeizeActionEvent>(OnSeizeAction);
        SubscribeLocalEvent<RiderComponent, RiderExitActionEvent>(OnExitAction);
        SubscribeLocalEvent<RiderComponent, GetVerbsEvent<Verb>>(OnHatchlingVerbs);
        SubscribeLocalEvent<RiderComponent, ComponentShutdown>(OnHatchlingShutdown);

        SubscribeLocalEvent<RiddenComponent, HostResistActionEvent>(OnHostResist);
        SubscribeLocalEvent<RiddenComponent, MobStateChangedEvent>(OnHostMobState);
        SubscribeLocalEvent<RiddenComponent, ComponentShutdown>(OnHostShutdown);
        SubscribeLocalEvent<RiddenComponent, GetVerbsEvent<Verb>>(OnHostAdminVerbs);
        SubscribeLocalEvent<RiddenComponent, HeadsetRadioReceiveRelayEvent>(OnHostRadio);

        SubscribeLocalEvent<RiderSurgeryConditionComponent, CMSurgeryValidEvent>(OnParasiteSurgeryValid);
        SubscribeLocalEvent<RiderSurgeryStepEffectComponent, CMSurgeryStepEvent>(OnParasiteSurgeryStep);
        SubscribeLocalEvent<RiddenComponent, ExaminedEvent>(OnRiddenExamined);
        SubscribeLocalEvent<RiderComponent, RiderSurgeActionEvent>(OnSurgeAction);
    }

    private void OnHatchlingStartup(Entity<RiderComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.LatchAction, "ActionRiderLatch");
        _actions.AddAction(ent, ref ent.Comp.VoiceAction, "ActionRiderVoice");
        _actions.AddAction(ent, ref ent.Comp.PunishAction, "ActionRiderPunish");
        _actions.AddAction(ent, ref ent.Comp.SeizeAction, "ActionRiderSeize");
        _actions.AddAction(ent, ref ent.Comp.ExitAction, "ActionRiderExit");
        _actions.AddAction(ent, ref ent.Comp.SurgeAction, "ActionRiderSurge");
        ent.Comp.NextCrawlResidueAt = _timing.CurTime + TimeSpan.FromSeconds(8);
    }

    #region Latch

    private void OnLatchAction(Entity<RiderComponent> ent, ref RiderLatchActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Host != null)
        {
            _popup.PopupEntity(Loc.GetString("rider-latch-riding"), ent, ent);
            return;
        }

        var target = args.Target;
        if (!IsRideable(target))
        {
            _popup.PopupEntity(Loc.GetString("rider-latch-invalid"), target, ent);
            return;
        }

        if (!IsUnconscious(target))
        {
            _popup.PopupEntity(Loc.GetString("rider-latch-awake"), target, ent);
            return;
        }

        args.Handled = true;
        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.LatchDuration,
            new RiderLatchDoAfterEvent(), ent, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        _popup.PopupEntity(Loc.GetString("rider-latch-start"), target, ent);
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnLatchDoAfter(Entity<RiderComponent> ent, ref RiderLatchDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!IsRideable(target) || !IsUnconscious(target))
        {
            _popup.PopupEntity(Loc.GetString("rider-latch-failed"), target, ent);
            return;
        }

        LatchOnto(ent, target, willing: false);
    }

    private void OnHatchlingVerbs(Entity<RiderComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == ent.Owner)
            return;

        // The willing door: a conscious host offers themselves
        if (!IsRideable(args.User) || IsUnconscious(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("rider-verb-accept"),
            Act = () => LatchOnto(ent, user, willing: true),
        });
    }

    private void LatchOnto(Entity<RiderComponent> ent, EntityUid host, bool willing)
    {
        if (ent.Comp.Host != null || !IsRideable(host))
            return;

        var container = _container.EnsureContainer<ContainerSlot>(host, RiderContainerSlot);
        if (!_container.Insert(ent.Owner, container))
            return;

        var ridden = EnsureComp<RiddenComponent>(host);
        ridden.Rider = ent.Owner;
        ridden.RideStart = _timing.CurTime;
        ridden.Willing = willing;
        _actions.AddAction(host, ref ridden.ResistAction, "ActionHostResist");

        ent.Comp.Host = host;
        ent.Comp.Grip = ent.Comp.GripStart;
        ent.Comp.HostsRidden++;
        // A willing host is interaction from the first second; they count
        ent.Comp.RideCredited = willing;
        ent.Comp.NextTellAt = _timing.CurTime + TimeSpan.FromMinutes(10);
        ent.Comp.NextShedAt = _timing.CurTime + TimeSpan.FromMinutes(20);

        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"{ToPrettyString(ent):rider} latched onto {ToPrettyString(host):host} ({(willing ? "willing" : "unconscious")})");

        SendToHost(ent.Owner, host, Loc.GetString("rider-host-latched"),
            Loc.GetString("rider-host-latched-wrap"));
    }

    private bool IsRideable(EntityUid uid)
    {
        if (!TryComp<MobStateComponent>(uid, out var state)
            || !_mobState.IsAlive(uid, state) && !_mobState.IsCritical(uid, state))
        {
            return false;
        }

        return HasComp<HumanoidProfileComponent>(uid)
            && !HasComp<SynthComponent>(uid)
            && !HasComp<RiddenComponent>(uid)
            && !HasComp<RiderComponent>(uid);
    }

    private bool IsUnconscious(EntityUid uid)
        => HasComp<SleepingComponent>(uid)
           || HasComp<ForcedSleepingStatusEffectComponent>(uid)
           || _mobState.IsCritical(uid);

    #endregion

    #region Levers

    private void OnVoiceAction(Entity<RiderComponent> ent, ref RiderVoiceActionEvent args)
    {
        if (ent.Comp.Host == null)
        {
            _popup.PopupEntity(Loc.GetString("rider-no-host"), ent, ent);
            return;
        }

        if (TryComp<ActorComponent>(args.Performer, out var actor))
            _ui.OpenUi(ent.Owner, RiderVoiceUiKey.Key, actor.PlayerSession);
    }

    private void OnVoiceMessage(Entity<RiderComponent> ent, ref RiderVoiceMessage args)
    {
        if (ent.Comp.Host is not { } host || string.IsNullOrWhiteSpace(args.Text))
            return;

        var raw = args.Text.Trim();
        if (raw.Length > _characterLimit)
            raw = raw[.._characterLimit].Trim();

        if (raw.Length == 0)
            return;

        if (args.SpeakThrough)
        {
            if (!SpendGrip(ent, ent.Comp.SpeakCost))
            {
                _popup.PopupEntity(Loc.GetString("rider-grip-low"), ent, ent);
                return;
            }

            // Chat renders markup in wrapped messages and the text is client-supplied
            var text = FormattedMessage.EscapeText(raw);
            // Disabled: the trailing pause is a learnable tell players metagame
            // var name = FormattedMessage.EscapeText(MetaData(host).EntityName);
            // foreach (var session in GetLocalRecipients(host))
            // {
            //     _chat.ChatMessageToOne(ChatChannel.Local,
            //         $"{text} ...",
            //         $"{name} says: \"{text} …\"",
            //         host, false, session.Channel);
            // }

            SendToHost(ent.Owner, host, text,
                Loc.GetString("rider-speak-host-wrap", ("text", text)));
            _adminLogger.Add(LogType.Chat, LogImpact.Medium,
                $"{ToPrettyString(ent):rider} spoke through {ToPrettyString(host):host}: {raw}");
            return;
        }

        // Whisper: free, private, garbled for sleepers
        var heard = HasComp<SleepingComponent>(host)
            ? Garble(raw)
            : raw;
        var shown = FormattedMessage.EscapeText(heard);

        SendToHost(ent.Owner, host, shown, Loc.GetString("rider-whisper-wrap", ("text", shown)));
    }

    private void OnPunishAction(Entity<RiderComponent> ent, ref RiderPunishActionEvent args)
    {
        if (ent.Comp.Host is not { } host)
            return;

        if (!SpendGrip(ent, ent.Comp.PunishCost))
        {
            _popup.PopupEntity(Loc.GetString("rider-grip-low"), ent, ent);
            return;
        }

        var severity = _random.NextFloat(10, 20);
        var betrayal = CompOrNull<RiddenComponent>(host)?.Willing == true;
        if (betrayal)
        {
            // Betraying a willing host ends the cooperation and hits hard
            Comp<RiddenComponent>(host).Willing = false;
            severity *= 2;
            _popup.PopupEntity(Loc.GetString("rider-betrayal"), host, host, PopupType.LargeCaution);
        }

        _damageable.TryChangeDamage(host, new DamageSpecifier(_proto.Index(PunishDamage), severity));
        _popup.PopupEntity(Loc.GetString("rider-punish-host"), host, host, PopupType.LargeCaution);
        _adminLogger.Add(LogType.Damaged, LogImpact.Medium,
            $"{ToPrettyString(ent):rider} punished {ToPrettyString(host):host}{(betrayal ? " (betrayal)" : string.Empty)}");
    }

    private void OnSeizeAction(Entity<RiderComponent> ent, ref RiderSeizeActionEvent args)
    {
        if (ent.Comp.SeizeActive || ent.Comp.Host is not { } host)
            return;

        if (ent.Comp.Grip < ent.Comp.SeizeGate)
        {
            _popup.PopupEntity(Loc.GetString("rider-grip-low"), ent, ent);
            return;
        }

        if (!_mind.TryGetMind(ent.Owner, out var riderMindId, out _))
            return;

        if (_mind.TryGetMind(host, out var hostMindId, out _))
        {
            var proxy = EnsureSeizeProxy(ent, host);
            _mind.Visit(hostMindId, proxy);
        }

        _mind.Visit(riderMindId, host);

        ent.Comp.SeizeActive = true;
        ent.Comp.SeizeEndsAt = _timing.CurTime + ent.Comp.SeizeDuration;
        _popup.PopupEntity(Loc.GetString("rider-seize-host"), host, host, PopupType.LargeCaution);
        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"{ToPrettyString(ent):rider} seized {ToPrettyString(host):host}");
    }

    private EntityUid EnsureSeizeProxy(Entity<RiderComponent> ent, EntityUid host)
    {
        if (ent.Comp.SeizeProxy is { } existing && !TerminatingOrDeleted(existing))
            return existing;

        var proxy = Spawn("CMURiderSeizeProxy", _xform.GetMapCoordinates(host));
        ent.Comp.SeizeProxy = proxy;

        // Living players never draw the spectator; the host's own eye carries
        // the Rider bit so they watch the shape hovering over their body
        _visibility.AddLayer(proxy, (int) VisibilityFlags.Rider, false);
        _visibility.RemoveLayer(proxy, (int) VisibilityFlags.Normal, false);
        _visibility.RefreshVisibility(proxy);
        return proxy;
    }

    private void EndSeize(Entity<RiderComponent> ent, bool natural)
    {
        if (!ent.Comp.SeizeActive || ent.Comp.Host is not { } host)
            return;

        if (_mind.TryGetMind(ent.Owner, out var riderMindId, out _))
            _mind.UnVisit(riderMindId);

        if (_mind.TryGetMind(host, out var hostMindId, out _))
            _mind.UnVisit(hostMindId);

        if (ent.Comp.SeizeProxy is { } proxy)
        {
            Del(proxy);
            ent.Comp.SeizeProxy = null;
        }

        ent.Comp.SeizeActive = false;
        ent.Comp.Grip = MathF.Max(ent.Comp.Grip - ent.Comp.SeizeCost, ent.Comp.SeizeFloor);

        if (natural)
            _popup.PopupEntity(Loc.GetString("rider-seize-end-host"), host, host, PopupType.Medium);

        _adminLogger.Add(LogType.AntagSelection, LogImpact.Medium,
            $"{ToPrettyString(ent):rider} ended a seizure of {ToPrettyString(host):host}");
    }

    private bool SpendGrip(Entity<RiderComponent> ent, float cost)
    {
        if (ent.Comp.Grip < ent.Comp.GripDisableBelow)
            return false;

        ent.Comp.Grip = MathF.Max(0, ent.Comp.Grip - cost);
        return true;
    }

    #endregion

    #region Exits

    private void OnExitAction(Entity<RiderComponent> ent, ref RiderExitActionEvent args)
    {
        if (ent.Comp.Host is not { } host)
        {
            _popup.PopupEntity(Loc.GetString("rider-no-host"), ent, ent);
            return;
        }

        EndSeize(ent, true);
        Eject(ent, host, loud: !IsUnconscious(host));
    }

    private void OnHostMobState(Entity<RiddenComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !TryComp<RiderComponent>(ent.Comp.Rider, out var rider))
            return;

        // The rider cannot die on host death; it is ejected alive and stunned
        var riderEnt = new Entity<RiderComponent>(ent.Comp.Rider, rider);
        EndSeize(riderEnt, false);
        Eject(riderEnt, ent.Owner, loud: true, stunned: true);
    }

    private void OnHostShutdown(Entity<RiddenComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<RiderComponent>(ent.Comp.Rider, out var rider))
            return;

        // Host body going away (cryo, deletion); leave at its last position
        var riderEnt = new Entity<RiderComponent>(ent.Comp.Rider, rider);
        EndSeize(riderEnt, false);
        Eject(riderEnt, ent.Owner, loud: false);
    }

    private void Eject(Entity<RiderComponent> ent, EntityUid host, bool loud, bool stunned = false)
    {
        if (ent.Comp.Host != host)
            return;

        if (TerminatingOrDeleted(host) || TerminatingOrDeleted(ent.Owner))
        {
            // The host body is going away and would delete its transform children.
            // Detach first; its components die with it and touching them would re-enter shutdown.
            _xform.AttachToGridOrMap(ent.Owner);
            ent.Comp.Host = null;
            return;
        }

        if (_container.TryGetContainer(host, RiderContainerSlot, out var container))
            _container.Remove(ent.Owner, container, force: true);

        if (loud)
            _popup.PopupEntity(Loc.GetString("rider-eject-loud", ("host", host)),
                host, Filter.PvsExcept(ent.Owner), true);

        if (stunned)
            _stun.TryStun(ent.Owner, TimeSpan.FromSeconds(3), true);

        ent.Comp.Host = null;
        if (ent.Comp.RideCredited)
            ent.Comp.CreditedHosts++;

        ent.Comp.RideCredited = false;
        _adminLogger.Add(LogType.AntagSelection, LogImpact.Medium,
            $"{ToPrettyString(ent):rider} left {ToPrettyString(host):host} ({(loud ? "loud" : "silent")})");

        if (TryComp<RiddenComponent>(host, out var ridden))
        {
            if (ridden.ResistAction is { } action)
                _actions.RemoveAction(host, action);

            RemComp<RiddenComponent>(host);
        }
    }

    #endregion

    #region Host side

    private void OnHatchlingShutdown(Entity<RiderComponent> ent, ref ComponentShutdown args)
    {
        EndSeize(ent, false);

        if (ent.Comp.Host is not { } host)
            return;

        ent.Comp.Host = null;

        if (TryComp<RiddenComponent>(host, out var ridden))
        {
            if (ridden.ResistAction is { } action)
                _actions.RemoveAction(host, action);

            RemComp<RiddenComponent>(host);
        }
    }

    private void OnHostResist(Entity<RiddenComponent> ent, ref HostResistActionEvent args)
    {
        ent.Comp.ResistActive = !ent.Comp.ResistActive;
        if (!ent.Comp.ResistActive)
            return;

        _popup.PopupEntity(Loc.GetString("rider-resist-start", ("host", ent.Owner)),
            ent.Owner, Filter.PvsExcept(ent.Owner), true);
        _stamina.TakeStaminaDamage(ent.Owner, 4);
    }

    private void OnHostRadio(Entity<RiddenComponent> ent, ref HeadsetRadioReceiveRelayEvent args)
    {
        // Sensory parity: what the host's headset receives, the rider hears too.
        // RadioReceiveEvent itself is raised on the headset, only the relay
        // reaches the wearer
        if (!TryComp<RiderComponent>(ent.Comp.Rider, out var rider)
            || !_mind.TryGetMind(ent.Comp.Rider, out _, out var riderMind)
            || !_players.TryGetSessionById(riderMind.UserId, out var session))
        {
            return;
        }

        _netMan.ServerSendMessage(args.RelayedEvent.ChatMsg, session.Channel);
    }

    private void OnHostAdminVerbs(Entity<RiddenComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!_admin.HasAdminFlag(args.User, AdminFlags.Admin))
            return;

        var riderUid = ent.Comp.Rider;
        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("rider-verb-inspect"),
            Category = VerbCategory.Admin,
            Act = () =>
            {
                if (!TryComp<RiderComponent>(riderUid, out var rider))
                    return;

                var minutes = (int) rider.TotalRideTime.TotalMinutes;
                var message = Loc.GetString("rider-admin-inspect",
                    ("rider", riderUid),
                    ("host", ent.Owner),
                    ("grip", (int) rider.Grip),
                    ("hosts", rider.HostsRidden),
                    ("minutes", minutes));
                _popup.PopupCursor(message, user);
            },
        });

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("rider-verb-eject"),
            Category = VerbCategory.Admin,
            Act = () =>
            {
                if (TryComp<RiderComponent>(riderUid, out var rider))
                {
                    EndSeize((riderUid, rider), false);
                    Eject((riderUid, rider), ent.Owner, loud: true);
                }
            },
        });
    }

    #endregion

    #region Tells and medicine (phase 2)

    private void OnParasiteSurgeryValid(Entity<RiderSurgeryConditionComponent> ent, ref CMSurgeryValidEvent args)
    {
        if (!HasComp<RiddenComponent>(args.Body))
            args.Cancelled = true;
    }

    private void OnParasiteSurgeryStep(Entity<RiderSurgeryStepEffectComponent> ent, ref CMSurgeryStepEvent args)
    {
        if (!TryComp<RiddenComponent>(args.Body, out var ridden)
            || !TryComp<RiderComponent>(ridden.Rider, out var rider))
        {
            return;
        }

        var riderEnt = new Entity<RiderComponent>(ridden.Rider, rider);
        EndSeize(riderEnt, false);
        Eject(riderEnt, args.Body, loud: true);
        _popup.PopupEntity(Loc.GetString("rider-extracted"), args.Body, Filter.Pvs(args.Body), true);
        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"{ToPrettyString(args.User):surgeon} cut {ToPrettyString(ridden.Rider):rider} out of {ToPrettyString(args.Body):host}");
    }

    private void OnRiddenExamined(Entity<RiddenComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryComp<RiderComponent>(ent.Comp.Rider, out var rider))
            return;

        var ride = rider.TotalRideTime;
        if (ride >= TimeSpan.FromMinutes(45))
            args.PushText(Loc.GetString("rider-examine-heavy"));
        else if (ride >= TimeSpan.FromMinutes(25))
            args.PushText(Loc.GetString("rider-examine-medium"));
        else if (ride >= TimeSpan.FromMinutes(10))
            args.PushText(Loc.GetString("rider-examine-mild"));
    }

    private void OnSurgeAction(Entity<RiderComponent> ent, ref RiderSurgeActionEvent args)
    {
        if (ent.Comp.Host is not { } host || CompOrNull<RiddenComponent>(host) is not { } ridden)
            return;

        if (!ridden.Willing)
        {
            _popup.PopupEntity(Loc.GetString("rider-surge-refused"), ent, ent);
            return;
        }

        if (!_solutions.TryGetSolution(host, "bloodstream", out var solution)
            || !_solutions.TryAddReagent(solution.Value, "Epinephrine", FixedPoint2.New(5)))
        {
            return;
        }

        _popup.PopupEntity(Loc.GetString("rider-surge-host"), host, host);
        _adminLogger.Add(LogType.ChemicalReaction, LogImpact.Low,
            $"{ToPrettyString(ent):rider} granted resilience to {ToPrettyString(host):host}");
    }

    private void DepositCrawlResidue(EntityUid hatchling)
    {
        foreach (var uid in _lookup.GetEntitiesInRange(hatchling, 0.75f))
        {
            // FixturesComponent: shared-side stand-in for the client-only sprite check
            if (uid == hatchling || HasComp<MobStateComponent>(uid) || !HasComp<FixturesComponent>(uid))
                continue;

            var forensics = EnsureComp<ForensicsComponent>(uid);
            forensics.Residues.Add(CrawlResidue);
            break;
        }
    }

    private void ShedResidue(EntityUid host)
    {
        if (!_inventory.TryGetSlotEntity(host, "outerClothing", out var clothing)
            && !_inventory.TryGetSlotEntity(host, "jumpsuit", out clothing))
        {
            return;
        }

        var forensics = EnsureComp<ForensicsComponent>(clothing.Value);
        forensics.Residues.Add(CrawlResidue);
    }

    #endregion

    public override void Update(float frameTime)
    {
        var enumerator = EntityQueryEnumerator<RiderComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            if (comp.Host == null)
            {
                // Crawl residue: the hatchling's trail feeds the scanner's unused
                // Residues field and gives marshals a colony-first discovery path
                if (_timing.CurTime >= comp.NextCrawlResidueAt)
                {
                    comp.NextCrawlResidueAt = _timing.CurTime + TimeSpan.FromSeconds(8);
                    DepositCrawlResidue(uid);
                }

                continue;
            }

            var host = comp.Host.Value;
            if (TerminatingOrDeleted(host))
                continue;

            if (comp.SeizeActive)
            {
                if (_timing.CurTime >= comp.SeizeEndsAt)
                {
                    EndSeize((uid, comp), true);
                }
                else if (comp.SeizeProxy is { } proxy && !TerminatingOrDeleted(proxy))
                {
                    // The spectator drifts with the body; pull it back inside the leash
                    var hostPos = _xform.GetWorldPosition(host);
                    var delta = _xform.GetWorldPosition(proxy) - hostPos;
                    if (Transform(proxy).MapID != Transform(host).MapID)
                        _xform.SetCoordinates(proxy, _xform.GetMoverCoordinates(host));
                    else if (delta.Length() > comp.SeizeProxyLeash)
                        _xform.SetWorldPosition(proxy, hostPos + delta.Normalized() * comp.SeizeProxyLeash);
                }
            }

            comp.TotalRideTime += TimeSpan.FromSeconds(frameTime);

            if (!IsUnconscious(host))
                comp.RideCredited = true;

            if (_timing.CurTime >= comp.NextChoirAt)
            {
                comp.NextChoirAt = _timing.CurTime + TimeSpan.FromSeconds(30);
                ChoirHum((uid, comp));
            }

            var ridden = CompOrNull<RiddenComponent>(host);
            var resisting = ridden?.ResistActive == true;

            var perMinute = resisting
                ? 0
                : ridden?.Willing == true
                    ? comp.GripCoopRegenPerMinute
                    : comp.GripRegenPerMinute;

            comp.GripAccumulator += perMinute * frameTime / 60f;
            if (comp.GripAccumulator >= 1)
            {
                var whole = MathF.Floor(comp.GripAccumulator);
                comp.GripAccumulator -= whole;
                comp.Grip = MathF.Min(100, comp.Grip + whole);
            }

            if (resisting)
            {
                comp.Grip -= comp.ResistDrainPerSecond * frameTime;
                _stamina.TakeStaminaDamage(host, 5 * frameTime, visual: false);
                if (comp.Grip <= 0)
                {
                    comp.Grip = 0;
                    Eject((uid, comp), host, loud: true);
                    continue;
                }
            }

            if (comp.Grip >= comp.SootheThreshold)
            {
                _stamina.TakeStaminaDamage(host, -0.5f * frameTime, visual: false);
                if (!comp.Soothing)
                {
                    comp.Soothing = true;
                    _popup.PopupEntity(Loc.GetString("rider-soothe"), host, host);
                }
            }
            else if (comp.Soothing)
            {
                comp.Soothing = false;
                _popup.PopupEntity(Loc.GetString("rider-soothe-end"), host, host);
            }

            if (comp.Soothing || ridden?.Willing == true)
            {
                if (_timing.CurTime >= comp.NextSoothePainAt)
                {
                    comp.NextSoothePainAt = _timing.CurTime + comp.SoothePainRefresh;
                    _pain.AddAdditivePainSuppressionProfile(host,
                        comp.SoothePainAccumulation,
                        comp.SoothePainTier,
                        comp.SoothePainDecayBonus,
                        comp.SoothePainRefresh);
                }
            }

            if (_timing.CurTime >= comp.NextTellAt)
            {
                var ride = comp.TotalRideTime;
                if (ride >= TimeSpan.FromMinutes(45))
                {
                    // TODO: fauna agitation
                    _popup.PopupEntity(Loc.GetString("rider-tell-heavy", ("entName", host)), host, Filter.Pvs(host), true);
                    comp.NextTellAt += TimeSpan.FromSeconds(25);
                }
                else if (ride >= TimeSpan.FromMinutes(25))
                {
                    _popup.PopupEntity(Loc.GetString("rider-tell-medium", ("entName", host)), host, Filter.Pvs(host), true);
                    comp.NextTellAt += TimeSpan.FromSeconds(40);
                }
                else
                {
                    _popup.PopupEntity(Loc.GetString("rider-tell-mild", ("entName", host)), host, Filter.Pvs(host), true);
                    comp.NextTellAt += TimeSpan.FromSeconds(60);
                }
            }

            // Bedding and clothing traces after twenty minutes
            if (_timing.CurTime >= comp.NextShedAt)
            {
                comp.NextShedAt += TimeSpan.FromMinutes(2);
                ShedResidue(host);
            }

            // The host reads grip qualitatively and only hears when the band changes
            if (ridden == null)
                continue;

            int band;
            if (comp.Grip >= 70)
                band = 2;
            else if (comp.Grip < comp.GripDisableBelow)
                band = 0;
            else
                band = 1;

            if (band == ridden.GripBand)
                continue;

            ridden.GripBand = band;
            var key = band switch
            {
                2 => "rider-grip-tight",
                0 => "rider-grip-thread",
                _ => "rider-grip-loose",
            };
            _popup.PopupEntity(Loc.GetString(key), host, host);
        }
    }

    #region Choir and objectives (phase 3)

    private void ChoirHum(Entity<RiderComponent> ent)
    {
        var myPos = _xform.GetWorldPosition(ent);
        var myMap = Transform(ent).MapID;

        EntityUid? nearest = null;
        var best = float.MaxValue;
        var others = EntityQueryEnumerator<RiderComponent, TransformComponent>();
        while (others.MoveNext(out var other, out _, out var xform))
        {
            if (other == ent.Owner || xform.MapID != myMap)
                continue;

            var distance = (_xform.GetWorldPosition(other) - myPos).Length();
            if (distance < best)
            {
                best = distance;
                nearest = other;
            }
        }

        if (nearest is not { } otherRider)
            return;

        var delta = _xform.GetWorldPosition(otherRider) - myPos;
        var angle = Math.Atan2(delta.Y, delta.X) * 180 / Math.PI;
        var sector = ((int) Math.Round(angle / 45) + 8) % 8;
        var direction = sector switch
        {
            0 => "rider-choir-east",
            1 => "rider-choir-northeast",
            2 => "rider-choir-north",
            3 => "rider-choir-northwest",
            4 => "rider-choir-west",
            5 => "rider-choir-southwest",
            6 => "rider-choir-south",
            _ => "rider-choir-southeast",
        };

        var distanceKey = best switch
        {
            < 15 => "rider-choir-close",
            < 60 => "rider-choir-near",
            _ => "rider-choir-far",
        };

        _popup.PopupEntity(Loc.GetString("rider-choir-hum",
            ("direction", Loc.GetString(direction)),
            ("distance", Loc.GetString(distanceKey))), ent, ent);
    }

    /// <summary>
    /// Round-end win check by flavor: still riding (Hitchhiker), three credited
    /// hosts (Leapfrog), or riding a host who carries the target (Puppeteer).
    /// </summary>
    public bool EvaluateWin(Entity<RiderComponent> ent)
        => ent.Comp.Flavor switch
        {
            RiderFlavor.Leapfrog => ent.Comp.CreditedHosts >= 3,
            RiderFlavor.Puppeteer => ent.Comp.Host is { } host && HostCarries(host, ent.Comp.PuppeteerItem),
            _ => ent.Comp.Host != null,
        };

    private bool HostCarries(EntityUid host, EntProtoId? item)
    {
        if (item is not { } proto)
            return false;

        var target = _proto.Index(proto);
        var slots = _inventory.GetSlotEnumerator(host);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } contained
                && MetaData(contained).EntityPrototype == target)
            {
                return true;
            }
        }

        foreach (var held in _hands.EnumerateHeld(host))
        {
            if (MetaData(held).EntityPrototype == target)
                return true;
        }

        return false;
    }

    #endregion

    private void SendToHost(EntityUid rider, EntityUid host, string message, string wrapped)
    {
        if (!_mind.TryGetMind(host, out _, out var mind)
            || !_players.TryGetSessionById(mind.UserId, out var session))
        {
            return;
        }

        _chat.ChatMessageToOne(ChatChannel.Local, message, wrapped, rider, false, session.Channel);
    }

    private IEnumerable<ICommonSession> GetLocalRecipients(EntityUid source)
    {
        var sourcePos = _xform.GetWorldPosition(source);
        var sourceMap = _xform.GetMapId(source);
        var enumerator = EntityManager.AllEntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (enumerator.MoveNext(out var viewer, out var actor, out var xform))
        {
            if (xform.MapID != sourceMap
                || (_xform.GetWorldPosition(viewer) - sourcePos).Length() > 10)
            {
                continue;
            }

            yield return actor.PlayerSession;
        }
    }

    /// <summary>
    /// Dream garble: sleepers get fragments, not transcripts. Sleep must never
    /// be a perfectly silent, perfectly readable channel.
    /// </summary>
    private string Garble(string text)
    {
        var kept = text.Split(' ').Where(w => w.Length > 0 && _random.Prob(0.4f)).ToArray();
        return kept.Length == 0 ? "…" : string.Join(" … ", kept);
    }
}
