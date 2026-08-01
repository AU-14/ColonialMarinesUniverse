using System.Linq;
using Content.Shared._AU14.Radio;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._AU14.Radio;

/// <summary>
///     CLF net splicing. A trained insurgent operator uses a splice kit on a mast or array, runs a do-after
///     to get the feeder junction open, and then plays the band: each hidden trunk carrier has to be found by
///     probing positions and reading the strength that comes back, then committed to with a lock. Probes are
///     a fixed budget for the whole job and each one raises a detection meter, so it is a budget problem
///     rather than a timer. Locking every carrier grafts the cell nets onto the anchor and leaves a tap
///     behind. Running out of probes or filling the meter alarms the junction instead.
///
///     The mast sparks for the duration of the job, and the tap left by a successful one is a visible entity
///     anyone can pull off (see <see cref="AU14NetSpliceTapComponent"/>).
/// </summary>
public sealed partial class AU14NetSpliceSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;

    // all played at the mast rather than to the operator: someone standing next to a junction being worked
    // should be able to hear it happening
    private static readonly SoundSpecifier ProbeSound =
        new SoundPathSpecifier("/Audio/_RMC14/Machines/click.ogg", AudioParams.Default.WithVolume(-6f));

    private static readonly SoundSpecifier CarrierLockedSound =
        new SoundPathSpecifier("/Audio/_RMC14/Effects/tick.ogg", AudioParams.Default.WithVolume(-2f));

    private static readonly SoundSpecifier MissedLockSound =
        new SoundPathSpecifier("/Audio/_RMC14/Machines/buzz_two.ogg", AudioParams.Default.WithVolume(-4f));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AU14NetSpliceKitComponent, AfterInteractEvent>(OnKitAfterInteract);
        SubscribeLocalEvent<AU14NetSpliceTargetComponent, AU14NetSpliceOpenDoAfterEvent>(OnJunctionOpened);
        SubscribeLocalEvent<AU14NetSpliceTargetComponent, ExaminedEvent>(OnTargetExamined);
        SubscribeLocalEvent<AU14NetSplicedComponent, EntityTerminatingEvent>(OnTargetTerminating);

        SubscribeLocalEvent<AU14NetSpliceTapComponent, GetVerbsEvent<AlternativeVerb>>(OnTapVerbs);
        SubscribeLocalEvent<AU14NetSpliceTapComponent, AU14NetSpliceRemoveDoAfterEvent>(OnTapRemoved);
        SubscribeLocalEvent<AU14NetSpliceTapComponent, ExaminedEvent>(OnTapExamined);
        // covers every other way a tap can stop existing - shot off the mast, gibbed, admin-deleted -
        // so the grafted nets never outlive the hardware holding them up
        SubscribeLocalEvent<AU14NetSpliceTapComponent, EntityTerminatingEvent>(OnTapTerminating);

        Subs.BuiEvents<AU14NetSpliceInProgressComponent>(AU14NetSpliceUiKey.Key, subs =>
        {
            subs.Event<AU14NetSpliceProbeMsg>(OnProbe);
            subs.Event<AU14NetSpliceLockMsg>(OnLock);
        });
    }

    // ----- starting the job ---------------------------------------------------------------------------

    private void OnKitAfterInteract(Entity<AU14NetSpliceKitComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp(target, out AU14NetSpliceTargetComponent? spliceTarget))
            return;

        args.Handled = true;
        var user = args.User;

        // the tap grafts the cell's own nets on, so it is CLF hardware and operator work both
        if (!HasComp<CLFMemberComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("au14-splice-not-clf"), target, user, PopupType.SmallCaution);
            return;
        }

        if (!HasComp<ANPRCRadioUserComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("au14-splice-untrained"), target, user, PopupType.SmallCaution);
            return;
        }

        if (HasComp<AU14NetSplicedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("au14-splice-already-tapped"), target, user, PopupType.SmallCaution);
            return;
        }

        if (HasComp<AU14NetSpliceInProgressComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("au14-splice-already-working"), target, user, PopupType.SmallCaution);
            return;
        }

        // a failed job costs the window as well as the kit, otherwise a cell with spare kits just retries
        // through the alarm and the alarm means nothing
        if (HasComp<AU14NetSpliceAlarmedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("au14-splice-alarmed"), target, user, PopupType.SmallCaution);
            return;
        }

        if (!HasComp<ANPRCRelayAnchorComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("au14-splice-no-feeder"), target, user, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.WorkSound is { } work)
            _audio.PlayPvs(work, target);

        _popup.PopupEntity(Loc.GetString("au14-splice-opening"), target, user);

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            TimeSpan.FromSeconds(spliceTarget.OpenTime),
            new AU14NetSpliceOpenDoAfterEvent(),
            target,
            target,
            ent.Owner)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnJunctionOpened(Entity<AU14NetSpliceTargetComponent> ent, ref AU14NetSpliceOpenDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } kit)
            return;

        if (!TryComp(kit, out AU14NetSpliceKitComponent? kitComp))
            return;

        args.Handled = true;

        // somebody else got there first while this one was working
        if (HasComp<AU14NetSplicedComponent>(ent) || HasComp<AU14NetSpliceInProgressComponent>(ent))
            return;

        // the kit goes into the junction here and does not come back out. otherwise you could probe until
        // the meter got dangerous, walk away, and start again for free
        QueueDel(kit);

        var session = EnsureComp<AU14NetSpliceInProgressComponent>(ent);
        session.User = args.User;
        session.Kit = kit;
        session.TapPrototype = kitComp.TapPrototype;
        session.Stage = 0;
        session.Detection = 0f;
        session.ProbesLeft = ent.Comp.Probes;
        session.Readings.Clear();
        session.Locked.Clear();
        session.Carriers = RollCarriers(ent.Comp);
        session.NextSpark = _timing.CurTime + TimeSpan.FromSeconds(session.SparkIntervalMin);

        _ui.OpenUi(ent.Owner, AU14NetSpliceUiKey.Key, args.User);
        PushState((ent.Owner, session), ent.Comp, AU14NetSpliceStatus.Running);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):player} opened the feeder junction on {ToPrettyString(ent.Owner)} and started a net splice.");
    }

    /// <summary>
    ///     Carriers sit clear of the band edges and clear of each other, so a later stage is never sitting
    ///     next to one the operator has already found.
    /// </summary>
    private List<int> RollCarriers(AU14NetSpliceTargetComponent target)
    {
        var margin = target.LockTolerance + 2;
        var minSeparation = target.CarrierRadius / 2;
        var carriers = new List<int>();

        for (var i = 0; i < target.Carriers; i++)
        {
            var position = 0;

            // bounded: on a crowded band we simply take the last roll rather than spinning
            for (var attempt = 0; attempt < 24; attempt++)
            {
                position = _random.Next(margin, Math.Max(margin + 1, target.BandSize - margin + 1));

                if (carriers.All(c => Math.Abs(c - position) >= minSeparation))
                    break;
            }

            carriers.Add(position);
        }

        return carriers;
    }

    // ----- the band game ------------------------------------------------------------------------------

    private void OnProbe(Entity<AU14NetSpliceInProgressComponent> ent, ref AU14NetSpliceProbeMsg args)
    {
        if (!TryGetSession(ent, args.Actor, out var target))
            return;

        if (ent.Comp.ProbesLeft <= 0)
            return;

        var position = Math.Clamp(args.Position, 1, target.BandSize);

        ent.Comp.ProbesLeft--;
        ent.Comp.Detection += target.ProbeDetection;
        ent.Comp.Readings.Add(new AU14NetSpliceReading(position, ReadStrength(target, position, CurrentCarrier(ent))));

        _audio.PlayPvs(ProbeSound, ent.Owner);

        // running the band dry without locking every carrier fails the job as well
        if (ent.Comp.Detection >= 100f || ent.Comp.ProbesLeft <= 0)
        {
            FailSplice((ent.Owner, ent.Comp), target,
                ent.Comp.Detection >= 100f ? "au14-splice-failed-detected" : "au14-splice-failed-probes");
            return;
        }

        PushState(ent, target, AU14NetSpliceStatus.Running);
    }

    private void OnLock(Entity<AU14NetSpliceInProgressComponent> ent, ref AU14NetSpliceLockMsg args)
    {
        if (!TryGetSession(ent, args.Actor, out var target))
            return;

        var position = Math.Clamp(args.Position, 1, target.BandSize);
        var carrier = CurrentCarrier(ent);

        if (Math.Abs(position - carrier) <= target.LockTolerance)
        {
            ent.Comp.Locked.Add(carrier);
            ent.Comp.Stage++;
            ent.Comp.Readings.Clear();

            if (ent.Comp.Stage >= ent.Comp.Carriers.Count)
            {
                CompleteSplice((ent.Owner, ent.Comp), target);
                return;
            }

            _audio.PlayPvs(CarrierLockedSound, ent.Owner);

            _popup.PopupEntity(
                Loc.GetString("au14-splice-carrier-locked",
                    ("done", ent.Comp.Stage),
                    ("total", ent.Comp.Carriers.Count)),
                ent.Owner,
                ent.Comp.User);

            PushState(ent, target, AU14NetSpliceStatus.Running);
            return;
        }

        // a miss costs probes and most of a detection meter, so guessing is worse than probing again
        ent.Comp.Detection += target.FailedLockDetection;
        ent.Comp.ProbesLeft = Math.Max(0, ent.Comp.ProbesLeft - target.FailedLockProbeCost);

        _audio.PlayPvs(MissedLockSound, ent.Owner);
        Spawn(ent.Comp.SparkEffect, Transform(ent.Owner).Coordinates);

        if (ent.Comp.Detection >= 100f || ent.Comp.ProbesLeft <= 0)
        {
            FailSplice((ent.Owner, ent.Comp), target,
                ent.Comp.Detection >= 100f ? "au14-splice-failed-detected" : "au14-splice-failed-probes");
            return;
        }

        PushState(ent, target, AU14NetSpliceStatus.Running);
    }

    /// <summary>
    ///     Signal falls off linearly to nothing at <see cref="AU14NetSpliceTargetComponent.CarrierRadius"/>,
    ///     with slop on top so the meter cannot be read straight off as a distance. Outside the radius the
    ///     reading is a clean zero, which is what a coarse opening sweep is for.
    /// </summary>
    private int ReadStrength(AU14NetSpliceTargetComponent target, int position, int carrier)
    {
        var distance = Math.Abs(position - carrier);

        if (distance >= target.CarrierRadius)
            return 0;

        var raw = 100f * (1f - (float) distance / target.CarrierRadius);
        var noised = raw + _random.Next(-target.ReadingNoise, target.ReadingNoise + 1);

        return Math.Clamp((int) MathF.Round(noised), 0, 100);
    }

    private static int CurrentCarrier(Entity<AU14NetSpliceInProgressComponent> ent)
    {
        return ent.Comp.Carriers[Math.Clamp(ent.Comp.Stage, 0, ent.Comp.Carriers.Count - 1)];
    }

    private bool TryGetSession(
        Entity<AU14NetSpliceInProgressComponent> ent,
        EntityUid actor,
        out AU14NetSpliceTargetComponent target)
    {
        target = default!;

        // only the operator who opened the junction is working it
        if (actor != ent.Comp.User)
            return false;

        if (ent.Comp.Stage >= ent.Comp.Carriers.Count)
            return false;

        return TryComp(ent.Owner, out target!);
    }

    // ----- landing it, or not -------------------------------------------------------------------------

    private void CompleteSplice(Entity<AU14NetSpliceInProgressComponent> ent, AU14NetSpliceTargetComponent target)
    {
        var user = ent.Comp.User;

        CloseSession(ent);

        if (!TryComp(ent.Owner, out ANPRCRelayAnchorComponent? anchor))
            return;

        // only the nets this tap actually adds are recorded, so pulling it later puts the anchor back
        // exactly as it was instead of stripping nets the mast owned to begin with
        var grafted = new HashSet<ProtoId<RadioChannelPrototype>>();

        foreach (var channel in target.Channels)
        {
            if (anchor.Channels.Add(channel))
                grafted.Add(channel);
        }

        var tap = Spawn(ent.Comp.TapPrototype, Transform(ent.Owner).Coordinates);

        // the prototype is already anchored:true, so Spawn has put it in the snap grid cell. anchoring it
        // a second time asserts on the duplicate - only reach for it if something spawned it loose
        var tapXform = Transform(tap);

        if (!tapXform.Anchored)
            _transform.AnchorEntity(tap, tapXform);

        var tapComp = EnsureComp<AU14NetSpliceTapComponent>(tap);
        tapComp.Target = ent.Owner;
        Dirty(tap, tapComp);

        var spliced = EnsureComp<AU14NetSplicedComponent>(ent.Owner);
        spliced.Tap = tap;
        spliced.Grafted = grafted;
        Dirty(ent.Owner, spliced);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_RMC14/Effects/tech_notification.ogg"), ent.Owner);

        if (user.IsValid())
            _popup.PopupEntity(Loc.GetString("au14-splice-success"), ent.Owner, user, PopupType.Medium);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(user):player} spliced {ToPrettyString(ent.Owner)}; grafted nets: {string.Join(", ", grafted)}.");
    }

    private void FailSplice(
        Entity<AU14NetSpliceInProgressComponent> ent,
        AU14NetSpliceTargetComponent target,
        string reason)
    {
        var user = ent.Comp.User;

        CloseSession(ent);

        var alarmed = EnsureComp<AU14NetSpliceAlarmedComponent>(ent.Owner);
        alarmed.RecoverAt = _timing.CurTime + alarmed.RecoverDelay;
        alarmed.NextSpark = _timing.CurTime;
        // the buzzer fires once here as part of the failure, so hold the recurring one off a full interval
        alarmed.NextSound = _timing.CurTime + TimeSpan.FromSeconds(alarmed.SoundIntervalMin);

        _audio.PlayPvs(alarmed.AlarmSound, ent.Owner);

        // a burst rather than the usual single spark, so the moment it goes wrong is visible from a distance
        var coordinates = Transform(ent.Owner).Coordinates;

        for (var i = 0; i < 3; i++)
            Spawn(alarmed.SparkEffect, coordinates);

        if (user.IsValid())
            _popup.PopupEntity(Loc.GetString(reason), ent.Owner, user, PopupType.LargeCaution);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} botched a net splice on {ToPrettyString(ent.Owner)} ({reason}); the junction alarmed.");
    }

    /// <summary>Tears the session down and shuts the panel, whichever way the job ended.</summary>
    private void CloseSession(Entity<AU14NetSpliceInProgressComponent> ent)
    {
        _ui.CloseUi(ent.Owner, AU14NetSpliceUiKey.Key);
        RemCompDeferred<AU14NetSpliceInProgressComponent>(ent.Owner);
    }

    private void PushState(
        Entity<AU14NetSpliceInProgressComponent> ent,
        AU14NetSpliceTargetComponent target,
        AU14NetSpliceStatus status)
    {
        _ui.SetUiState(
            ent.Owner,
            AU14NetSpliceUiKey.Key,
            new AU14NetSpliceBuiState(
                target.BandSize,
                ent.Comp.Stage,
                ent.Comp.Carriers.Count,
                ent.Comp.ProbesLeft,
                MathF.Min(ent.Comp.Detection, 100f),
                status,
                new List<AU14NetSpliceReading>(ent.Comp.Readings),
                new List<int>(ent.Comp.Locked)));
    }

    // ----- the tap, and taking it off -----------------------------------------------------------------

    private void OnTapVerbs(Entity<AU14NetSpliceTapComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-splice-verb-remove"),
            Priority = 1,
            Act = () => StartTapRemoval(ent, user),
        });
    }

    private void StartTapRemoval(Entity<AU14NetSpliceTapComponent> ent, EntityUid user)
    {
        _popup.PopupEntity(Loc.GetString("au14-splice-removing"), ent.Owner, user);

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            TimeSpan.FromSeconds(ent.Comp.RemoveTime),
            new AU14NetSpliceRemoveDoAfterEvent(),
            ent.Owner,
            ent.Owner)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnTapRemoved(Entity<AU14NetSpliceTapComponent> ent, ref AU14NetSpliceRemoveDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        UnsplicedTarget(ent);

        // what the finder gets for spotting it. the keying module reads cell traffic until CLF leadership
        // orders a recrypto, so it is worth taking without being worth the round
        if (ent.Comp.SalvagedCrypto is { } crypto)
        {
            var card = Spawn(crypto, Transform(ent.Owner).Coordinates);
            _hands.TryPickupAnyHand(args.User, card);
            _popup.PopupEntity(Loc.GetString("au14-splice-salvaged"), args.User, args.User, PopupType.Medium);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("au14-splice-removed"), args.User, args.User);
        }

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(args.User):player} pulled the net splice tap off {ToPrettyString(ent.Comp.Target ?? EntityUid.Invalid)}.");

        QueueDel(ent.Owner);
    }

    /// <summary>Puts the anchor back the way it was: removes exactly the nets this tap grafted on.</summary>
    private void UnsplicedTarget(Entity<AU14NetSpliceTapComponent> ent)
    {
        if (ent.Comp.Target is not { } target || TerminatingOrDeleted(target))
            return;

        if (!TryComp(target, out AU14NetSplicedComponent? spliced))
            return;

        if (TryComp(target, out ANPRCRelayAnchorComponent? anchor))
        {
            foreach (var channel in spliced.Grafted)
                anchor.Channels.Remove(channel);
        }

        RemComp<AU14NetSplicedComponent>(target);
    }

    private void OnTapTerminating(Entity<AU14NetSpliceTapComponent> ent, ref EntityTerminatingEvent args)
    {
        UnsplicedTarget(ent);
    }

    /// <summary>A mast that dies takes its tap with it, rather than leaving the box anchored in the rubble.</summary>
    private void OnTargetTerminating(Entity<AU14NetSplicedComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!TerminatingOrDeleted(ent.Comp.Tap))
            QueueDel(ent.Comp.Tap);
    }

    // ----- examine ------------------------------------------------------------------------------------

    private void OnTargetExamined(Entity<AU14NetSpliceTargetComponent> ent, ref ExaminedEvent args)
    {
        if (HasComp<AU14NetSpliceAlarmedComponent>(ent))
        {
            args.PushMarkup(Loc.GetString("au14-splice-examine-alarmed"));
            return;
        }

        if (HasComp<AU14NetSplicedComponent>(ent))
            args.PushMarkup(Loc.GetString("au14-splice-examine-tapped"));
    }

    private void OnTapExamined(Entity<AU14NetSpliceTapComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("au14-splice-examine-tap"));
    }

    // ----- ticking ------------------------------------------------------------------------------------

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // a job in progress sparks the whole time it is running, and breaks off if the operator wanders
        var sessions = EntityQueryEnumerator<AU14NetSpliceInProgressComponent, AU14NetSpliceTargetComponent>();
        while (sessions.MoveNext(out var uid, out var session, out var target))
        {
            if (!InWorkRange(session.User, uid, target.WorkRange))
            {
                if (session.User.IsValid())
                    _popup.PopupEntity(Loc.GetString("au14-splice-abandoned"), uid, session.User, PopupType.MediumCaution);

                _ui.CloseUi(uid, AU14NetSpliceUiKey.Key);
                RemCompDeferred<AU14NetSpliceInProgressComponent>(uid);
                continue;
            }

            if (now < session.NextSpark)
                continue;

            session.NextSpark = now + TimeSpan.FromSeconds(
                _random.NextFloat(session.SparkIntervalMin, session.SparkIntervalMax));

            Spawn(session.SparkEffect, Transform(uid).Coordinates);
        }

        var alarmed = EntityQueryEnumerator<AU14NetSpliceAlarmedComponent>();
        while (alarmed.MoveNext(out var uid, out var alarm))
        {
            if (now >= alarm.RecoverAt)
            {
                RemCompDeferred<AU14NetSpliceAlarmedComponent>(uid);
                continue;
            }

            // sparks and buzzer run on separate irregular timers: the fault should look continuous without
            // sounding like a metronome to anyone standing near it
            if (now >= alarm.NextSpark)
            {
                alarm.NextSpark = now + TimeSpan.FromSeconds(
                    _random.NextFloat(alarm.SparkIntervalMin, alarm.SparkIntervalMax));

                Spawn(alarm.SparkEffect, Transform(uid).Coordinates);
            }

            if (now < alarm.NextSound)
                continue;

            alarm.NextSound = now + TimeSpan.FromSeconds(
                _random.NextFloat(alarm.SoundIntervalMin, alarm.SoundIntervalMax));

            _audio.PlayPvs(alarm.AlarmSound, uid);
        }
    }

    private bool InWorkRange(EntityUid user, EntityUid target, float range)
    {
        if (!Exists(user) || !Exists(target))
            return false;

        var userXform = Transform(user);
        var targetXform = Transform(target);

        if (userXform.MapID != targetXform.MapID)
            return false;

        var distance = (_transform.GetWorldPosition(userXform) - _transform.GetWorldPosition(targetXform)).Length();

        return distance <= range;
    }
}
