using Content.Server.Administration.Logs;
using Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Server.Antag;
using Content.Server.Prayer;
using Content.Shared.Alert;
using Content.Shared.Antag;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Melding;
using Content.Shared.CMU14.Threats.GrandMachine;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Melding;

/// <summary>
/// Phase E3: River-Melded conversions. Each console the Early Warning Joe assimilates schedules one
/// conversion of a synthetic player (playtest 4: synthetics only). With none eligible the miss becomes a
/// meld debt that is retried on the planet network for a search window (N7); only when the window closes
/// with debts left is the Joe given its one respawn instead, <see cref="EWJoeRespawnSystem"/>. A dead,
/// stood-down or deleted Joe's debts are dropped. The victim is warned three times, then
/// converted in place. Thralls are freed only by the Weyland reset key or by the Joe's death or
/// stand-down (D4).
/// </summary>
public sealed partial class EWJoeMeldingSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private EWJoeRuleSystem _rule = default!;
    [Dependency] private EWJoeMessageSystem _messages = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PrayerSystem _prayer = default!;
    [Dependency] private EWJoeRespawnSystem _respawn = default!;
    [Dependency] private SharedRoleSystem _role = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    /// <summary>Number of warnings before conversion, spread evenly over the meld duration.</summary>
    public const int Warnings = 3;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EWJoeComponent, EWJoeConsoleAssimilatedEvent>(OnConsoleAssimilated);
        // the directed death slot on EWJoeComponent belongs to the console system and the removal slot to the
        // objective ladder; listen broadcast
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<EWJoeRemovedEvent>(OnJoeRemoved);
        SubscribeLocalEvent<RiverMeldedComponent, TargetDefibrillatedEvent>(OnThrallDefibrillated);
        SubscribeLocalEvent<RiverMeldedComponent, ComponentShutdown>(OnMeldedShutdown);
        SubscribeLocalEvent<RiverMeldedComponent, EntityTerminatingEvent>(OnMeldedTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + TimeSpan.FromSeconds(1);

        // thralls whose master was gibbed, deleted or otherwise left without a Dead transition
        var orphaned = new List<Entity<RiverMeldedComponent>>();
        var meldedQuery = EntityQueryEnumerator<RiverMeldedComponent>();
        while (meldedQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.Master is not { } master || TerminatingOrDeleted(master)
                || !HasComp<EWJoeComponent>(master) || _mobState.IsDead(master))
                orphaned.Add((uid, comp));
        }

        foreach (var thrall in orphaned)
        {
            Cure(thrall, "the master is gone");
        }

        var pending = new List<(EntityUid Uid, RiverMeldingComponent Comp)>();
        var query = EntityQueryEnumerator<RiverMeldingComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            pending.Add((uid, comp));
        }

        foreach (var (victim, melding) in pending)
        {
            if (!StillValid(victim, melding))
            {
                CancelMeld(victim, "no longer valid");
                continue;
            }

            var step = melding.ConvertAt - melding.StartedAt;
            var due = melding.StartedAt + step * melding.WarningsSent / Warnings;
            if (melding.WarningsSent < Warnings && now >= due)
            {
                SendWarning(victim, melding);
                melding.WarningsSent++;
            }

            // every warning goes out before the conversion, even on a hitching server
            if (now >= melding.ConvertAt && melding.WarningsSent >= Warnings)
                Convert(victim, melding);
        }

        UpdateMeldDebts(now);
    }

    #region Scheduling

    private void OnConsoleAssimilated(Entity<EWJoeComponent> ent, ref EWJoeConsoleAssimilatedEvent args)
    {
        TryStartMeld(ent);
    }

    /// <summary>
    /// Picks a synthetic victim and starts the countdown. When none is eligible this returns false (the
    /// console still counts) and the miss becomes a meld debt (N7): the river searches the planet
    /// network again every <see cref="EWJoeComponent.MeldRetry"/>, and only when
    /// <see cref="EWJoeComponent.MeldSearchWindow"/> runs out with debts left is the Joe given its one
    /// respawn (playtest 4; at most once per round since RB-05). A zero window grants the respawn at once.
    /// </summary>
    public bool TryStartMeld(Entity<EWJoeComponent> joe)
    {
        var rejections = new MeldRejections();
        if (PickMeldTarget(joe, rejections) is { } victim)
        {
            StartMeld(joe, victim);
            return true;
        }

        var reasons = rejections.ToString();
        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"Early Warning Joe {ToPrettyString(joe):joe} found no synthetic to meld ({reasons})");
        AddMeldDebt(joe);
        return false;
    }

    private void StartMeld(Entity<EWJoeComponent> joe, EntityUid victim)
    {
        var melding = EnsureComp<RiverMeldingComponent>(victim);
        melding.Master = joe;
        melding.StartedAt = _timing.CurTime;
        melding.ConvertAt = _timing.CurTime + joe.Comp.MeldDuration;
        melding.WarningsSent = 0;

        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"Early Warning Joe {ToPrettyString(joe):joe} began melding {ToPrettyString(victim):victim}");
    }

    /// <summary>
    /// Records a missed meld. The first debt of a search opens the window and tells the Joe once; later
    /// debts join the open search.
    /// </summary>
    private void AddMeldDebt(Entity<EWJoeComponent> joe)
    {
        if (joe.Comp.MeldSearchWindow <= TimeSpan.Zero)
        {
            _respawn.Grant(joe);
            return;
        }

        var searching = joe.Comp.PendingMelds > 0;
        joe.Comp.PendingMelds++;
        if (searching)
            return;

        var now = _timing.CurTime;
        joe.Comp.MeldSearchUntil = now + joe.Comp.MeldSearchWindow;
        joe.Comp.NextMeldRetry = now + joe.Comp.MeldRetry;
        _messages.Notify(joe, Loc.GetString("cmu-ew-joe-meld-searching"));
    }

    /// <summary>
    /// Every <see cref="EWJoeComponent.MeldRetry"/>, and once more when the window closes, each open debt
    /// looks for a synthetic. Debts still open when the window closes give the copy once between them:
    /// <see cref="EWJoeRespawnSystem.Grant"/> never holds more than one.
    /// </summary>
    private void UpdateMeldDebts(TimeSpan now)
    {
        var debtors = new List<Entity<EWJoeComponent>>();
        var query = EntityQueryEnumerator<EWJoeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.PendingMelds > 0)
                debtors.Add((uid, comp));
        }

        foreach (var joe in debtors)
        {
            if (TerminatingOrDeleted(joe))
                continue;

            if (_mobState.IsDead(joe))
            {
                ClearMeldDebts(joe, "the Joe is dead");
                continue;
            }

            var expired = now >= joe.Comp.MeldSearchUntil;
            if (!expired && now < joe.Comp.NextMeldRetry)
                continue;

            joe.Comp.NextMeldRetry = now + joe.Comp.MeldRetry;

            // each pick re-counts from zero, so the log below describes the last, failed search only
            var rejections = new MeldRejections();
            while (joe.Comp.PendingMelds > 0)
            {
                rejections = new MeldRejections();
                if (PickMeldTarget(joe, rejections) is not { } victim)
                    break;

                joe.Comp.PendingMelds--;
                StartMeld(joe, victim);
                var left = joe.Comp.PendingMelds;
                _adminLogger.Add(LogType.Action, LogImpact.Medium,
                    $"Early Warning Joe {ToPrettyString(joe):joe} paid a meld debt with {ToPrettyString(victim):victim}, {left} left");
            }

            if (joe.Comp.PendingMelds <= 0 || !expired)
                continue;

            var debts = joe.Comp.PendingMelds;
            joe.Comp.PendingMelds = 0;
            var reasons = rejections.ToString();
            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"Early Warning Joe {ToPrettyString(joe):joe} found no synthetic to meld for {debts} debt(s) before its search window closed ({reasons})");

            // RB-05: a copy already held says so; one already given this round is not given again
            if (!_respawn.Grant(joe))
                _messages.Notify(joe, Loc.GetString(joe.Comp.RespawnReady ? "cmu-ew-joe-meld-search-ended" : "cmu-ew-joe-meld-search-ended-spent"));
        }
    }

    /// <summary>Drops every open meld debt without granting the copy (death, stand-down).</summary>
    private void ClearMeldDebts(EntityUid joe, string why)
    {
        if (!TryComp(joe, out EWJoeComponent? comp) || comp.PendingMelds <= 0)
            return;

        var debts = comp.PendingMelds;
        comp.PendingMelds = 0;
        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"Early Warning Joe {ToPrettyString(joe):joe} dropped {debts} meld debt(s): {why}");
    }

    private bool StillValid(EntityUid victim, RiverMeldingComponent melding)
    {
        if (TerminatingOrDeleted(victim) || _mobState.IsDead(victim))
            return false;

        if (!TryComp(melding.Master, out EWJoeComponent? master) || _mobState.IsDead(melding.Master))
            return false;

        var masterMap = Transform(melding.Master).MapID;
        var maps = masterMap == MapId.Nullspace ? null : _zLevels.GetAllNetworkMapIds(masterMap);

        // the victim may have become an antag, left the planet, or died and been revived as something else
        return IsEligible((melding.Master, master), victim, maps, pendingOk: true);
    }

    public void CancelMeld(EntityUid victim, string why)
    {
        if (!HasComp<RiverMeldingComponent>(victim))
            return;

        RemComp<RiverMeldingComponent>(victim);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"River melding of {ToPrettyString(victim):victim} cancelled: {why}");
    }

    private void SendWarning(EntityUid victim, RiverMeldingComponent melding)
    {
        var key = "cmu-ew-joe-meld-warning-synth-" + melding.WarningsSent;
        var message = Loc.GetString(key);

        if (TryComp(victim, out ActorComponent? actor))
        {
            _prayer.SendSubtleMessage(actor.PlayerSession, Loc.GetString("cmu-ew-joe-meld-source"), message,
                Loc.GetString("prayer-popup-subtle-default"));
        }
        else
        {
            _popup.PopupEntity(message, victim, victim, PopupType.LargeCaution);
        }
    }

    #endregion

    #region Conversion

    /// <summary>
    /// Converts the synthetic victim in place: the thrall specifier's components, the thrall mind role,
    /// the machine radio channel and the briefing. Playtest 4: the thrall also gets a lasting
    /// "River-Melded" alert and a popup, so the conversion shows. Public so tests can skip the timer.
    /// </summary>
    public void Convert(EntityUid victim, RiverMeldingComponent melding)
    {
        var masterUid = melding.Master;
        RemComp<RiverMeldingComponent>(victim);

        if (!TryComp(masterUid, out EWJoeComponent? master))
            return;

        if (!_proto.TryIndex(master.ThrallSpecifier, out var specifier))
        {
            Log.Error($"River melding specifier {master.ThrallSpecifier} is missing");
            return;
        }

        var warnings = melding.WarningsSent;
        var hadTouched = HasComp<GrandMachineTouchedComponent>(victim);
        EntityManager.AddComponents(victim, specifier.Components, removeExisting: false);
        var melded = EnsureComp<RiverMeldedComponent>(victim);
        melded.Master = masterUid;
        melded.WarningsReceived = warnings;
        melded.AddedTouched = !hadTouched && HasComp<GrandMachineTouchedComponent>(victim);

        _alerts.ShowAlert(victim, melded.Alert);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-thrall-melded"), victim, victim, PopupType.LargeCaution);

        if (_mind.TryGetMind(victim, out var mindId, out var mind))
        {
            _role.MindAddRoles(mindId, specifier.MindRoles, mind, silent: true);

            if (specifier.Briefing is { Text: { } text } briefing
                && _player.TryGetSessionById(mind.UserId, out var session))
            {
                _antag.SendBriefing(session, Loc.GetString(text), briefing.Color, briefing.Sound);
            }
        }

        GrantRadio(victim, master.ThrallRadioChannel, melded);
        if (!master.GrantedMachineRadio)
        {
            GrantRadio(masterUid, master.ThrallRadioChannel, null);
            master.GrantedMachineRadio = true;
        }

        if (!master.Thralls.Contains(victim))
            master.Thralls.Add(victim);
        master.ThrallsMade++;
        _rule.RecordProgress((masterUid, master));

        _messages.Notify(masterUid, Loc.GetString("cmu-ew-joe-meld-complete-master", ("thrall", victim)));
        _adminLogger.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(victim):victim} became a River-Melded thrall of {ToPrettyString(masterUid):joe}");

        var ev = new EWJoeThrallMadeEvent(masterUid, victim);
        RaiseLocalEvent(masterUid, ref ev, broadcast: true);
    }

    /// <summary>
    /// Frees a thrall: marker, alert, mind role, radio and the Grand Machine marker the conversion added go.
    /// </summary>
    public void Cure(Entity<RiverMeldedComponent> thrall, string why)
    {
        var masterUid = thrall.Comp.Master;
        thrall.Comp.Master = null;

        ReleaseRoleAndRoster(thrall, masterUid);
        _alerts.ClearAlert(thrall.Owner, thrall.Comp.Alert);
        if (thrall.Comp.AddedTouched)
            RemComp<GrandMachineTouchedComponent>(thrall);
        RemComp<RiverMeldedComponent>(thrall);

        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-thrall-freed"), thrall, thrall, PopupType.Large);
        if (masterUid is { } master)
            _messages.Notify(master, Loc.GetString("cmu-ew-joe-thrall-lost", ("thrall", thrall.Owner)));
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"River-Melded thrall {ToPrettyString(thrall):thrall} freed: {why}");

        var ev = new EWJoeThrallFreedEvent(masterUid);
        RaiseLocalEvent(thrall, ref ev);
    }

    /// <summary>Frees every thrall of <paramref name="master"/> and cancels its pending meldings.</summary>
    public void FreeAll(EntityUid master, string why)
    {
        var melded = new List<Entity<RiverMeldedComponent>>();
        var query = EntityQueryEnumerator<RiverMeldedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Master == master)
                melded.Add((uid, comp));
        }

        foreach (var thrall in melded)
        {
            Cure(thrall, why);
        }

        var pending = new List<EntityUid>();
        var meldingQuery = EntityQueryEnumerator<RiverMeldingComponent>();
        while (meldingQuery.MoveNext(out var uid, out var comp))
        {
            if (comp.Master == master)
                pending.Add(uid);
        }

        foreach (var victim in pending)
        {
            CancelMeld(victim, why);
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (HasComp<RiverMeldingComponent>(args.Target))
            CancelMeld(args.Target, "victim died");

        if (HasComp<EWJoeComponent>(args.Target))
        {
            FreeAll(args.Target, "the Joe died");
            ClearMeldDebts(args.Target, "the Joe died");
        }
    }

    private void OnJoeRemoved(ref EWJoeRemovedEvent args)
    {
        FreeAll(args.Joe, "the Joe stood down");
        ClearMeldDebts(args.Joe, "the Joe stood down");

        if (TryComp(args.Joe, out EWJoeComponent? comp) && comp.GrantedMachineRadio && comp.ThrallRadioChannel is { } channel)
        {
            RevokeChannel(args.Joe, channel);
            comp.GrantedMachineRadio = false;
        }
    }

    private void OnThrallDefibrillated(Entity<RiverMeldedComponent> ent, ref TargetDefibrillatedEvent args)
    {
        // only the Weyland key; Seegson keys and the Working Joe reboot verb run the same path
        if (!HasComp<CMUWeylandResetKeyComponent>(args.Defibrillator))
            return;

        Cure(ent, $"Weyland reset key used by {ToPrettyString(args.User)}");
    }

    private void OnMeldedShutdown(Entity<RiverMeldedComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        RevokeRadio(ent, ent.Comp);
    }

    /// <summary>
    /// A thrall whose body is destroyed (gib, deletion) must not leave its mind flagged as a team
    /// antagonist or stay on the master's roster, where the round-end summary would count it.
    /// </summary>
    private void OnMeldedTerminating(Entity<RiverMeldedComponent> ent, ref EntityTerminatingEvent args)
    {
        // nothing to tidy when the whole map is going (round restart)
        var xform = Transform(ent);
        if ((xform.GridUid ?? xform.MapUid) is { } ground && TerminatingOrDeleted(ground))
            return;

        ReleaseRoleAndRoster(ent, ent.Comp.Master);
    }

    /// <summary>Removes the thrall mind role if present and the roster entry on the master, quietly.</summary>
    private void ReleaseRoleAndRoster(EntityUid thrall, EntityUid? masterUid)
    {
        if (_mind.TryGetMind(thrall, out var mindId, out _) && _role.MindHasRole<EWJoeThrallRoleComponent>(mindId))
            _role.MindRemoveRole<EWJoeThrallRoleComponent>(mindId);

        if (TryComp(masterUid, out EWJoeComponent? master))
            master.Thralls.Remove(thrall);
    }

    #endregion

    #region Radio

    /// <summary>
    /// Same shape as the accessory headsets: grant onto whatever radio components exist, track only
    /// the channel, and on revoke drop a component only once it carries no channels at all.
    /// </summary>
    private void GrantRadio(EntityUid uid, ProtoId<RadioChannelPrototype>? channel, RiverMeldedComponent? track)
    {
        if (channel is not { } id)
            return;

        EnsureComp<ActiveRadioComponent>(uid).Channels.Add(id);
        EnsureComp<IntrinsicRadioReceiverComponent>(uid);
        EnsureComp<IntrinsicRadioTransmitterComponent>(uid).Channels.Add(id);

        if (track != null)
            track.GrantedChannel = id;
    }

    private void RevokeRadio(EntityUid uid, RiverMeldedComponent track)
    {
        if (track.GrantedChannel is not { } id)
            return;

        RevokeChannel(uid, id);
        track.GrantedChannel = null;
    }

    private void RevokeChannel(EntityUid uid, ProtoId<RadioChannelPrototype> id)
    {
        var anyChannels = false;
        if (TryComp(uid, out ActiveRadioComponent? active))
        {
            active.Channels.Remove(id);
            anyChannels = active.Channels.Count > 0;
            if (!anyChannels)
                RemCompDeferred<ActiveRadioComponent>(uid);
        }

        if (TryComp(uid, out IntrinsicRadioTransmitterComponent? transmitter))
        {
            transmitter.Channels.Remove(id);
            if (transmitter.Channels.Count == 0)
                RemCompDeferred<IntrinsicRadioTransmitterComponent>(uid);
        }

        // the receiver is only useful with an active radio; a headset re-adds it when worn
        if (!anyChannels)
            RemCompDeferred<IntrinsicRadioReceiverComponent>(uid);
    }

    #endregion
}
