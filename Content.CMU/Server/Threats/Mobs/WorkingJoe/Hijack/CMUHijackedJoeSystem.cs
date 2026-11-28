using Content.Server._RMC14.Ghost;
using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.CMU14.Threats.GrandMachine;
using Content.Server.CMU14.Threats.Mobs.WorkingJoe.Shipment;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.Synth;
using Content.Shared.Actions;
using Content.Shared.Antag;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.CMU14.SynthRepairer;
using Content.Shared.CMU14.Threats.Mobs.SubvertedSynth;
using Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Traits.Assorted;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Threats.Mobs.WorkingJoe.Hijack;

/// <summary>
/// Hijacked Working Joes: their kit (actions and the Grand Machine radio, applied once), the conversion of a dead
/// synthetic by Offer, and the cure by a reset key. A conversion applies the <see cref="CMUJoeConversionComponent.Specifier"/>
/// as data and records exactly what it added on <see cref="CMUHijackedJoeComponent"/>; the cure takes back that
/// record and nothing else. Owns the <see cref="CMUHijackedJoeComponent"/> map-init and shutdown hooks.
/// </summary>
public sealed partial class CMUHijackedJoeSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private GrandMachineRadioSystem _radio = default!;
    [Dependency] private SharedRoleSystem _role = default!;

    /// <summary>The job of a hijacked Joe ghost role; its playtime tracker records hijacked-Joe minutes.</summary>
    public static readonly ProtoId<JobPrototype> HijackedJoeJob = "CMUJobHijackedJoe";

    /// <summary>The shipment body whose ghost role a converted body copies.</summary>
    private static readonly EntProtoId ShipmentBody = "CMUMobHijackedJoe";

    public override void Initialize()
    {
        base.Initialize();

        // after the ghost-role special, which applies the job's round profile and with it a fresh radio
        SubscribeLocalEvent<CMUHijackedJoeComponent, MapInitEvent>(OnMapInit, after: [typeof(GhostRoleApplySpecialSystem)]);
        SubscribeLocalEvent<CMUHijackedJoeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUHijackedJoeComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<CMUHijackedJoeComponent, EWJoeOfferActionEvent>(OnOffer);
        SubscribeLocalEvent<CMUHijackedJoeComponent, TargetDefibrillatedEvent>(OnDefibrillated);
        // shared by Early Warning Joes and hijacked Joes: the do-after is raised on the dead synthetic
        SubscribeLocalEvent<SynthComponent, CMUJoeHijackDoAfterEvent>(OnHijackDoAfter);
    }

    #region Kit

    private void OnMapInit(Entity<CMUHijackedJoeComponent> ent, ref MapInitEvent args)
    {
        GrantKit(ent);
    }

    /// <summary>Grants the kit's actions and the Grand Machine radio, once.</summary>
    private void GrantKit(Entity<CMUHijackedJoeComponent> ent)
    {
        if (ent.Comp.KitGranted)
            return;

        ent.Comp.KitGranted = true;
        foreach (var proto in ent.Comp.GrantActions)
        {
            EntityUid? action = null;
            if (_actions.AddAction(ent, ref action, proto) && action is { } added)
                ent.Comp.AddedActions.Add(added);
        }

        if (!_radio.HasChannel(ent))
        {
            _radio.GrantChannel(ent);
            ent.Comp.RadioGranted = true;
        }
    }

    /// <summary>
    /// Takes back the overlay record: the granted actions, the recorded components and, when asked, the radio
    /// channel. The record is cleared first, so a later shutdown has nothing left to undo.
    /// </summary>
    private void ReleaseOverlay(Entity<CMUHijackedJoeComponent> ent, bool removeComponents, bool revokeRadio, bool deferred)
    {
        var actions = ent.Comp.AddedActions;
        var components = ent.Comp.AddedComponents;
        var radio = ent.Comp.RadioGranted;
        ent.Comp.AddedActions = new();
        ent.Comp.AddedComponents = new();
        ent.Comp.RadioGranted = false;

        foreach (var action in actions)
        {
            // RemoveAction only detaches a lasting action; delete it so no orphan stays in the container
            _actions.RemoveAction(ent.Owner, action);
            QueueDel(action);
        }

        if (removeComponents)
        {
            foreach (var name in components)
            {
                if (!EntityManager.ComponentFactory.TryGetRegistration(name, out var registration)
                    || !EntityManager.TryGetComponent(ent, registration.Type, out var component))
                    continue;

                if (deferred)
                    RemCompDeferred(ent, component);
                else
                    RemComp(ent, component);
            }
        }

        if (radio && revokeRadio)
            _radio.RevokeChannel(ent);
    }

    /// <summary>
    /// Safety net for a hijack removed some other way than the cure (an admin, a promotion). A promoted Early
    /// Warning Joe keeps the machine channel and its own kit; the promotion released the record already.
    /// </summary>
    private void OnShutdown(Entity<CMUHijackedJoeComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var earlyWarning = HasComp<EWJoeComponent>(ent);
        ReleaseOverlay(ent, removeComponents: !earlyWarning, revokeRadio: !earlyWarning, deferred: true);
    }

    /// <summary>Whoever takes a hijacked body, by seat or ghost role, is told what they are.</summary>
    private void OnMindAdded(Entity<CMUHijackedJoeComponent> ent, ref MindAddedMessage args)
    {
        // a leader being promoted is briefed as an Early Warning Joe instead
        if (HasComp<EWJoeComponent>(ent) || HasComp<CMUPendingEWJoeComponent>(ent))
            return;

        if (_player.TryGetSessionById(args.Mind.Comp.UserId, out var session))
            _antag.SendBriefing(session, Loc.GetString("cmu-hijacked-joe-briefing"), Color.DarkOrange, null);
    }

    /// <summary>
    /// A hijacked Joe that is promoted to an Early Warning Joe: the hijack's own actions and components go now, so
    /// the Early Warning kit is applied fresh, and the hijacked mind role is replaced. The radio channel stays,
    /// because the Early Warning Joe uses the same one. The caller then removes the component.
    /// </summary>
    public void ReleaseForPromotion(EntityUid body)
    {
        if (!TryComp(body, out CMUHijackedJoeComponent? hijack))
            return;

        ReleaseOverlay((body, hijack), removeComponents: true, revokeRadio: false, deferred: false);

        if (_mind.TryGetMind(body, out var mindId, out _) && _role.MindHasRole<CMUHijackedJoeRoleComponent>(mindId))
            _role.MindRemoveRole<CMUHijackedJoeRoleComponent>(mindId);
    }

    #endregion

    #region Conversion

    private void OnOffer(Entity<CMUHijackedJoeComponent> ent, ref EWJoeOfferActionEvent args)
    {
        // an Early Warning Joe's own Offer handler covers it, conversion included
        if (args.Handled || HasComp<EWJoeComponent>(ent))
            return;

        args.Handled = TryStartConversion(ent, args.Target);
    }

    /// <summary>
    /// Whether <paramref name="user"/> may convert <paramref name="target"/>: a dead synthetic that serves no other
    /// master and is not a drone android, lying in the open, offered by a conscious Joe able to convert.
    /// </summary>
    public bool CanConvert(EntityUid user, EntityUid target)
    {
        if (user == target || TerminatingOrDeleted(target) || TerminatingOrDeleted(user))
            return false;

        if (!HasComp<CMUJoeConversionComponent>(user) || _mobState.IsIncapacitated(user))
            return false;

        if (!HasComp<SynthComponent>(target) || !_mobState.IsDead(target))
            return false;

        if (HasComp<EWJoeComponent>(target)
            || HasComp<CMUHijackedJoeComponent>(target)
            || HasComp<RiverMeldedComponent>(target)
            || HasComp<RiverMeldingComponent>(target)
            || HasComp<SubvertedSynthComponent>(target)
            || HasComp<CMUDroneAndroidComponent>(target))
            return false;

        return !_container.IsEntityInContainer(target);
    }

    /// <summary>Starts the conversion do-after. Called by both Offer handlers (Early Warning and hijacked).</summary>
    public bool TryStartConversion(EntityUid user, EntityUid target)
    {
        if (!CanConvert(user, target) || !TryComp(user, out CMUJoeConversionComponent? conversion))
        {
            _popup.PopupEntity(Loc.GetString("cmu-hijacked-joe-convert-invalid"), user, user);
            return false;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, conversion.ConversionDelay, new CMUJoeHijackDoAfterEvent(), target, target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 1.5f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-hijacked-joe-convert-start", ("target", target)), user, user);
        return true;
    }

    private void OnHijackDoAfter(Entity<SynthComponent> ent, ref CMUJoeHijackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (!Convert(args.User, ent))
            _popup.PopupEntity(Loc.GetString("cmu-hijacked-joe-convert-invalid"), args.User, args.User);
    }

    /// <summary>
    /// Converts <paramref name="target"/> into a hijacked Joe for <paramref name="user"/>: the specifier's
    /// components (recorded), the actions and the radio, threat-unit status, a revival just under critical, and
    /// then the mind: a connected player gets the role, the briefing and a prompt to return to the body; otherwise
    /// the body becomes a ghost role. Public so tests can skip the do-after.
    /// </summary>
    public bool Convert(EntityUid user, EntityUid target)
    {
        if (!CanConvert(user, target) || !TryComp(user, out CMUJoeConversionComponent? conversion))
            return false;

        if (!_proto.TryIndex(conversion.Specifier, out var specifier))
        {
            Log.Error($"Hijacked Joe conversion specifier {conversion.Specifier} is missing");
            return false;
        }

        var added = new HashSet<string>();
        foreach (var name in specifier.Components.Keys)
        {
            if (EntityManager.ComponentFactory.TryGetRegistration(name, out var registration)
                && !HasComp(target, registration.Type))
                added.Add(name);
        }

        EntityManager.AddComponents(target, specifier.Components, removeExisting: false);

        var hijack = new CMUHijackedJoeComponent
        {
            AddedComponents = added,
            GrantActions = new List<EntProtoId>(conversion.Actions),
            ConvertedAt = _timing.CurTime,
        };
        AddComp(target, hijack);
        GrantKit((target, hijack));
        EnsureComp<CMUJoeThreatUnitComponent>(target);
        EnsureComp<CMUJoeConversionComponent>(target);

        Revive(target);

        _popup.PopupEntity(Loc.GetString("cmu-hijacked-joe-convert-done", ("target", target)), user, user);

        if (_mind.TryGetMind(target, out var mindId, out var mind))
        {
            if (_player.TryGetSessionById(mind.UserId, out var session))
            {
                _role.MindAddRoles(mindId, specifier.MindRoles, mind, silent: true);
                if (specifier.Briefing is { Text: { } text } briefing)
                    _antag.SendBriefing(session, Loc.GetString(text), briefing.Color, briefing.Sound);

                if (mind.CurrentEntity != target || mind.IsVisitingEntity)
                    _eui.OpenEui(new ReturnToBodyEui(mind, _mind, _player), session);

                _adminLogger.Add(LogType.Mind, LogImpact.High,
                    $"{ToPrettyString(user):user} converted {ToPrettyString(target):target} into a hijacked Working Joe");
                return true;
            }

            // a disconnected player: the body goes to someone who is here
            _mind.TransferTo(mindId, null);
        }

        OfferGhostRole(target, specifier);
        _adminLogger.Add(LogType.Mind, LogImpact.High,
            $"{ToPrettyString(user):user} converted {ToPrettyString(target):target} into a hijacked Working Joe, offered as a ghost role");
        return true;
    }

    /// <summary>Heals the body to just under critical and stands it up, the way a reset key revives a synthetic.</summary>
    private void Revive(EntityUid target)
    {
        RemComp<UnrevivableComponent>(target);

        if (!TryComp(target, out DamageableComponent? damageable)
            || !TryComp(target, out MobThresholdsComponent? thresholds)
            || !TryComp(target, out MobStateComponent? mobState))
            return;

        if (!_mobThreshold.TryGetThresholdForState(target, MobState.Critical, out var limit, thresholds)
            && !_mobThreshold.TryGetThresholdForState(target, MobState.Dead, out limit, thresholds))
            return;

        var goal = FixedPoint2.Max(FixedPoint2.Zero, limit.Value - FixedPoint2.New(1));
        var damage = _damageable.GetAllDamage((target, damageable));
        var excess = damage.GetTotal() - goal;
        if (excess > FixedPoint2.Zero)
        {
            var heal = new DamageSpecifier();
            foreach (var (type, value) in damage.DamageDict)
            {
                if (excess <= FixedPoint2.Zero)
                    break;

                if (value <= FixedPoint2.Zero)
                    continue;

                var amount = FixedPoint2.Min(value, excess);
                heal.DamageDict[type] = -amount;
                excess -= amount;
            }

            _damageable.TryChangeDamage(target, heal, ignoreResistances: true, interruptsDoAfters: false, damageable: damageable);
        }

        _mobState.ChangeMobState(target, MobState.Alive, mobState);
        _mobThreshold.VerifyThresholds(target, thresholds, mobState, damageable);
    }

    /// <summary>Opens the body to any ghost, with no whitelist, playtime or job gate; role bans still apply.</summary>
    private void OfferGhostRole(EntityUid target, AntagSpecifierPrototype specifier)
    {
        // A fresh ghost role: an earlier one may already be taken, and only GhostRoleSystem may reset that.
        // It is copied from the shipment body's own ghost role, so the hijacked Joe job and its explicit empty
        // requirement override (no whitelist, playtime or trait gate) carry over unchanged.
        if (TryComp<GhostRoleComponent>(target, out var old))
        {
            _ghostRole.UnregisterGhostRole((target, old));
            RemComp<GhostRoleComponent>(target);
        }

        GhostRoleComponent ghostRole;
        if (_proto.Index(ShipmentBody).TryGetComponent<GhostRoleComponent>(out var template, _componentFactory))
        {
            ghostRole = _serialization.CreateCopy(template, notNullableOverride: true);
            AddComp(target, ghostRole);
        }
        else
        {
            ghostRole = AddComp<GhostRoleComponent>(target);
        }

        ghostRole.RoleName = "cmu-hijacked-joe-ghost-role-name";
        ghostRole.RoleDescription = "cmu-hijacked-joe-ghost-role-description";
        ghostRole.RoleRules = "cmu-hijacked-joe-ghost-role-rules";
        ghostRole.JobProto = HijackedJoeJob;
        ghostRole.MindRoles = specifier.MindRoles is { } roles ? new List<EntProtoId>(roles) : new List<EntProtoId>();

        EnsureComp<GhostTakeoverAvailableComponent>(target);
        _ghostRole.RegisterGhostRole((target, ghostRole));
    }

    #endregion

    #region Cure

    /// <summary>Any synthetic reset key used by someone else (the Working Joe reboot verb zaps with one too) cures.</summary>
    private void OnDefibrillated(Entity<CMUHijackedJoeComponent> ent, ref TargetDefibrillatedEvent args)
    {
        if (!IsResetKeyByOther(ent, args))
            return;

        if (ent.Comp.ConvertedAt != TimeSpan.Zero && _timing.CurTime < ent.Comp.ConvertedAt + ent.Comp.ReconversionCooldown)
            return;

        Cure(ent, $"reset key {ToPrettyString(args.Defibrillator.Owner)} used by {ToPrettyString(args.User)}");
    }

    private bool IsResetKeyByOther(EntityUid target, TargetDefibrillatedEvent args)
    {
        // a subversion key inherits the reset key's component but never acts as a reset (SubvertedSynthRuleSystem)
        return args.User != target && HasComp<SynthRepairerComponent>(args.Defibrillator)
            && !HasComp<SynthSubverterComponent>(args.Defibrillator);
    }

    /// <summary>
    /// Repairs a hijacked Joe: everything the hijack added goes (recorded components and actions, the mind role,
    /// the radio channel, threat-unit status and the conversion), then the hijack itself. Public for tests and admins.
    /// </summary>
    public void Cure(Entity<CMUHijackedJoeComponent> ent, string why)
    {
        ReleaseOverlay(ent, removeComponents: true, revokeRadio: true, deferred: false);

        // an unclaimed hijack ghost role is withdrawn; the body is a plain Joe again
        if (TryComp(ent, out GhostRoleComponent? ghostRole) && !ghostRole.Taken && ghostRole.JobProto == HijackedJoeJob)
        {
            _ghostRole.UnregisterGhostRole((ent, ghostRole));
            RemCompDeferred<GhostRoleComponent>(ent);
            RemCompDeferred<GhostTakeoverAvailableComponent>(ent);
        }

        RemComp<CMUJoeThreatUnitComponent>(ent);
        RemComp<CMUJoeConversionComponent>(ent);
        RemCompDeferred<CMUHijackedJoeComponent>(ent);

        _popup.PopupEntity(Loc.GetString("cmu-hijacked-joe-repaired-popup"), ent, ent, PopupType.Large);

        if (_mind.TryGetMind(ent, out var mindId, out var mind))
        {
            if (_role.MindHasRole<CMUHijackedJoeRoleComponent>(mindId))
                _role.MindRemoveRole<CMUHijackedJoeRoleComponent>(mindId);

            if (_player.TryGetSessionById(mind.UserId, out var session))
                _antag.SendBriefing(session, Loc.GetString("cmu-hijacked-joe-repaired-briefing"), Color.CornflowerBlue, null);
        }

        _adminLogger.Add(LogType.Mind, LogImpact.Medium,
            $"Hijacked Working Joe {ToPrettyString(ent):joe} was repaired: {why}");
    }

    #endregion
}
