using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Events;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Chemistry;
using Content.Shared.CMU14.Round.Antags.Cannibal;
using Content.Shared.CMU14.Threats.Mobs.Wendigo.Lab;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Prototypes;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Zombies;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Threats.Mobs.Wendigo.Lab;

/// <summary>
/// Runs the Weyland-Yutani Wendigo procedure on a human test subject:
/// starving + human meat, human meat, Stabilized Mutagen, human meat, then MH-32 (feral) or MH-33 (tamed).
/// Each input is only accepted once the previous stage's timer has finished.
/// </summary>
public sealed partial class CMUWendigoTransformationSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private GhostRoleSystem _ghostRole = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;

    public static readonly ProtoId<ReagentPrototype> StabilizedMutagen = "CMUStabilizedMutagen";
    public static readonly ProtoId<ReagentPrototype> MH32 = "CMUMH32";
    public static readonly ProtoId<ReagentPrototype> MH33 = "CMUMH33";

    public static readonly EntProtoId FullWendigo = "AU14Wendigo";
    public static readonly EntProtoId LesserWendigo = "CMUWendigoLesser";

    private static readonly EntProtoId MindRoleFeral = "MindRoleCMUWendigoLab";
    private static readonly EntProtoId MindRoleTamed = "MindRoleCMUWendigoLabTamed";
    private static readonly ProtoId<EmotePrototype> Cough = "Cough";
    private static readonly ProtoId<TagPrototype> MeatTag = "Meat";
    private static readonly SatiationValue Starving = "Starving";
    private const string HumanSpecies = "Human";

    public static readonly FixedPoint2 RequiredDose = 15;

    private static readonly TimeSpan FeedMin = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FeedMax = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MutagenMin = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MutagenMax = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan GestationTime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MutationTime = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan ReadyCueMin = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ReadyCueMax = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan FlavorMin = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan FlavorMax = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan GestationSeizureMin = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan GestationSeizureMax = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan MinorSeizure = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MutationPulseInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MutationGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long a recorded injector stays attributable to the reagent that follows it.
    /// The injector event and the reagent reaction happen in the same tick.
    /// </summary>
    private static readonly TimeSpan InjectorWindow = TimeSpan.FromSeconds(1);

    private const int GestationFlavorLines = 6;
    private const int MutationWarningLines = 4;

    private readonly List<string> _roundEndLines = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, IngestingEvent>(OnIngesting);

        SubscribeLocalEvent<CMUWendigoSubjectComponent, TargetBeforeInjectEvent>(OnBeforeInject);
        SubscribeLocalEvent<CMUWendigoSubjectComponent, CMUBeforeHyposprayInjectEvent>(OnBeforeHyposprayInject);
        SubscribeLocalEvent<CMUWendigoSubjectComponent, ReactionEntityEvent>(OnReaction);
        SubscribeLocalEvent<CMUWendigoSubjectComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CMUWendigoSubjectComponent, CMUWendigoTransformDoAfterEvent>(OnTransformDoAfter);
        SubscribeLocalEvent<CMUWendigoSubjectComponent, ExaminedEvent>(OnSubjectExamined);

        SubscribeLocalEvent<CMUWendigoTamedComponent, ExaminedEvent>(OnTamedExamined);

        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    #region Eligibility

    public bool IsEligibleSubject(EntityUid uid)
    {
        return IsHumanSubject(uid) && _mobState.IsAlive(uid);
    }

    /// <summary>
    /// Species and body checks only; a critical subject still completes the change, since only death cancels it.
    /// </summary>
    private bool IsHumanSubject(EntityUid uid)
    {
        return TryComp(uid, out HumanoidProfileComponent? humanoid)
            && humanoid.Species == HumanSpecies
            && !HasComp<SynthComponent>(uid)
            && !HasComp<ZombieComponent>(uid)
            && !HasComp<XenoComponent>(uid)
            && !HasComp<CMUWendigoLabMadeComponent>(uid);
    }

    public bool IsStarving(EntityUid uid)
    {
        return TryComp(uid, out SatiationComponent? satiation)
            && _satiation.IsValueInRange((uid, satiation), SatiationSystem.Hunger, below: Starving);
    }

    #endregion

    #region Feeding

    private void OnIngesting(Entity<HumanoidProfileComponent> ent, ref IngestingEvent args)
    {
        // Drinks and pills never count; only solid food is a feeding.
        if (!TryComp(args.Food, out EdibleComponent? edible) || edible.Edible != IngestionSystem.Food)
            return;

        var humanMeat = _tag.HasTag(args.Food, MeatTag)
            && CMUHumanMeat.IsHumanStock(MetaData(args.Food).EntityPrototype);

        if (!TryComp(ent, out CMUWendigoSubjectComponent? subject))
        {
            // Nutrition from this bite has not been applied yet, so the starving check sees the pre-meal value.
            if (humanMeat && IsEligibleSubject(ent) && IsStarving(ent))
                BeginProcedure(ent);

            return;
        }

        var now = _timing.CurTime;
        if (!humanMeat)
        {
            // Before the Stabilized Mutagen stage, other food breaks the hunger cycle.
            if (subject.Stage is CMUWendigoSubjectStage.Fed1 or CMUWendigoSubjectStage.Fed2)
                Abort((ent, subject));

            return;
        }

        if (now < subject.StageEndsAt)
        {
            PopupNotReady(ent);
            return;
        }

        switch (subject.Stage)
        {
            case CMUWendigoSubjectStage.Fed1:
                SetStage((ent, subject), CMUWendigoSubjectStage.Fed2, _random.Next(FeedMin, FeedMax));
                _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-fed-again"), ent, ent, PopupType.Medium);
                break;
            case CMUWendigoSubjectStage.Mutagen:
                SetStage((ent, subject), CMUWendigoSubjectStage.Gestation, GestationTime);
                var now2 = _timing.CurTime;
                subject.NextFlavorAt = now2 + _random.Next(FlavorMin, FlavorMax);
                subject.NextSeizureAt = now2 + _random.Next(GestationSeizureMin, GestationSeizureMax);
                _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-gestation-start"), ent, ent, PopupType.LargeCaution);
                break;
            default:
                // Fed2 waits for Stabilized Mutagen, Gestation for MH-32/MH-33; meat does nothing more.
                PopupNotReady(ent);
                break;
        }
    }

    private void BeginProcedure(EntityUid uid)
    {
        var subject = EnsureComp<CMUWendigoSubjectComponent>(uid);
        SetStage((uid, subject), CMUWendigoSubjectStage.Fed1, _random.Next(FeedMin, FeedMax));
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-first-meal"), uid, uid, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(uid):subject} began the Wendigo procedure by eating human meat while starving");
    }

    #endregion

    #region Injection

    // Injections are never blocked: a wrong-stage, early or undersized dose is simply wasted.
    // These only record who injected, so MH-33 knows its master.
    private void OnBeforeInject(Entity<CMUWendigoSubjectComponent> ent, ref TargetBeforeInjectEvent args)
    {
        RecordInjector(ent, args.EntityUsingInjector);
    }

    private void OnBeforeHyposprayInject(Entity<CMUWendigoSubjectComponent> ent, ref CMUBeforeHyposprayInjectEvent args)
    {
        RecordInjector(ent, args.User);
    }

    private void RecordInjector(Entity<CMUWendigoSubjectComponent> ent, EntityUid user)
    {
        ent.Comp.LastInjector = user;
        ent.Comp.LastInjectorAt = _timing.CurTime;
    }

    private bool CanAcceptReagent(CMUWendigoSubjectComponent subject, ProtoId<ReagentPrototype> reagent)
    {
        if (_timing.CurTime < subject.StageEndsAt)
            return false;

        if (reagent == StabilizedMutagen)
            return subject.Stage == CMUWendigoSubjectStage.Fed2;

        if (reagent == MH32 || reagent == MH33)
            return subject.Stage == CMUWendigoSubjectStage.Gestation;

        return false;
    }

    private void OnReaction(Entity<CMUWendigoSubjectComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Injection)
            return;

        ProtoId<ReagentPrototype> reagent = args.Reagent.ID;
        if (reagent != StabilizedMutagen && reagent != MH32 && reagent != MH33)
            return;

        if (!CanAcceptReagent(ent.Comp, reagent))
        {
            // Wrong stage or too early: the dose is wasted.
            PopupNotReady(ent);
            return;
        }

        if (args.ReagentQuantity.Quantity < RequiredDose)
        {
            _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-dose-too-small"), ent, ent, PopupType.Small);
            return;
        }

        if (reagent == StabilizedMutagen)
        {
            SetStage(ent, CMUWendigoSubjectStage.Mutagen, _random.Next(MutagenMin, MutagenMax));
            _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-mutagen"), ent, ent, PopupType.Medium);
            return;
        }

        StartMutation(ent, reagent == MH33);
    }

    private void StartMutation(Entity<CMUWendigoSubjectComponent> ent, bool tamed)
    {
        var now = _timing.CurTime;
        var subject = ent.Comp;
        subject.Stage = CMUWendigoSubjectStage.Mutating;
        subject.StageEndsAt = now + MutationTime;
        subject.Tamed = tamed;
        subject.NextMutationPulseAt = now;

        if (tamed
            && subject.LastInjector is { } injector
            && now - subject.LastInjectorAt <= InjectorWindow
            && !TerminatingOrDeleted(injector))
        {
            subject.Master = injector;
            subject.MasterName = Name(injector);
        }

        // The subject is the do-after user: the change happens to them and is paralysed throughout,
        // so nothing but death may interrupt it.
        var args = new DoAfterArgs(EntityManager, ent, MutationTime, new CMUWendigoTransformDoAfterEvent(), ent, ent)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            BreakOnWeightlessMove = false,
            NeedHand = false,
            RequireCanInteract = false,
            Hidden = false,
        };

        if (_doAfter.TryStartDoAfter(args, out var doAfter))
            subject.DoAfter = doAfter;

        MutationPulse(ent);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(ent):subject} began turning into a {(tamed ? "tamed " : string.Empty)}Wendigo (injector: {ToPrettyString(subject.LastInjector)})");
    }

    #endregion

    #region Abort and conversion

    private void OnMobStateChanged(Entity<CMUWendigoSubjectComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        // Death is the only cancel once Stabilized Mutagen is in; revival does not restore progress.
        _doAfter.Cancel(ent.Comp.DoAfter);
        RemCompDeferred<CMUWendigoSubjectComponent>(ent);
    }

    private void Abort(Entity<CMUWendigoSubjectComponent> ent)
    {
        RemCompDeferred<CMUWendigoSubjectComponent>(ent);
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-aborted"), ent, ent, PopupType.Medium);
    }

    private void OnTransformDoAfter(Entity<CMUWendigoSubjectComponent> ent, ref CMUWendigoTransformDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        Convert(ent);
    }

    private void Convert(Entity<CMUWendigoSubjectComponent> ent)
    {
        var subject = ent.Comp;
        if (subject.Stage != CMUWendigoSubjectStage.Mutating || !IsHumanSubject(ent) || _mobState.IsDead(ent))
        {
            RemCompDeferred<CMUWendigoSubjectComponent>(ent);
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(ent):subject} was no longer a valid Wendigo subject when the mutation finished");
            return;
        }

        var variant = HasComp<CannibalComponent>(ent) ? FullWendigo : LesserWendigo;
        var subjectName = Name(ent);
        var wendigo = SpawnAtPosition(variant, Transform(ent).Coordinates);

        EnsureComp<CMUWendigoLabMadeComponent>(wendigo);

        DropEverything(ent);

        RemCompDeferred<IntelRecoverCorpseObjectiveOnDeathComponent>(wendigo);

        var hasMind = _mind.TryGetMind(ent, out var mindId, out var mind);
        TryComp(wendigo, out GhostRoleComponent? ghostRole);
        if (ghostRole != null && hasMind)
        {
            // The body is claimed by the subject's mind; never offer it to ghosts.
            _ghostRole.UnregisterGhostRole((wendigo, ghostRole));
            RemCompDeferred<GhostRoleComponent>(wendigo);
            RemCompDeferred<GhostTakeoverAvailableComponent>(wendigo);
        }

        if (subject.Tamed)
        {
            var tamed = EnsureComp<CMUWendigoTamedComponent>(wendigo);
            tamed.Master = subject.Master;
            tamed.MasterName = subject.MasterName ?? Loc.GetString("cmu-wendigo-lab-unknown-master");
        }

        if (hasMind)
        {
            if (ghostRole is { MakeSentient: true })
                _mind.MakeSentient(wendigo, ghostRole.AllowMovement, ghostRole.AllowSpeech);

            _mind.TransferTo(mindId, wendigo, mind: mind);

            if (ghostRole?.JobProto is { } job)
                _role.MindAddJobRole(mindId, silent: true, jobPrototype: job);

            _role.MindAddRole(mindId, subject.Tamed ? MindRoleTamed : MindRoleFeral, mind, silent: true);
        }

        var briefing = subject.Tamed
            ? Loc.GetString("cmu-wendigo-lab-briefing-tamed",
                ("master", subject.MasterName ?? Loc.GetString("cmu-wendigo-lab-unknown-master")))
            : Loc.GetString("cmu-wendigo-lab-briefing");
        _antag.SendBriefing(wendigo, briefing, Color.Red, null);
        _popup.PopupEntity(briefing, wendigo, wendigo, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-transformed-others", ("subject", subjectName)),
            wendigo, Filter.PvsExcept(wendigo), true, PopupType.LargeCaution);

        _roundEndLines.Add(subject.Tamed
            ? Loc.GetString("cmu-wendigo-lab-round-end-tamed",
                ("subject", subjectName),
                ("master", subject.MasterName ?? Loc.GetString("cmu-wendigo-lab-unknown-master")))
            : Loc.GetString("cmu-wendigo-lab-round-end", ("subject", subjectName)));

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(ent):subject} turned into {ToPrettyString(wendigo):wendigo} ({variant}, tamed: {subject.Tamed}, master: {subject.MasterName ?? "none"})");

        // Deleting the old body keeps it out of colonist counts, as if the subject left the round.
        RemCompDeferred<CMUWendigoSubjectComponent>(ent);
        QueueDel(ent);
    }

    private void DropEverything(EntityUid uid)
    {
        if (_inventory.TryGetContainerSlotEnumerator(uid, out var enumerator))
        {
            while (enumerator.MoveNext(out var slot))
            {
                _inventory.TryUnequip(uid, slot.ID, true, true);
            }
        }

        foreach (var held in _hands.EnumerateHeld(uid))
        {
            _hands.TryDrop(uid, held, checkActionBlocker: false);
        }
    }

    #endregion

    #region Timers and cues

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUWendigoSubjectComponent>();
        while (query.MoveNext(out var uid, out var subject))
        {
            if (!_mobState.IsAlive(uid))
                continue;

            var ent = (uid, subject);
            if (subject.Stage == CMUWendigoSubjectStage.Mutating)
            {
                // Only death may cancel the change; finish it if the do-after was lost some other way.
                if (now > subject.StageEndsAt + MutationGrace && !_doAfter.IsRunning(subject.DoAfter))
                {
                    Convert(ent);
                    continue;
                }

                if (now >= subject.NextMutationPulseAt)
                    MutationPulse(ent);

                continue;
            }

            if (now < subject.StageEndsAt)
            {
                if (subject.Stage == CMUWendigoSubjectStage.Gestation)
                    GestationTick(ent, now);

                continue;
            }

            if (!subject.ReadyCuePlayed)
            {
                subject.ReadyCuePlayed = true;
                subject.NextReadyCueAt = now + _random.Next(ReadyCueMin, ReadyCueMax);
                ExpiryCue(ent);
                continue;
            }

            if (now >= subject.NextReadyCueAt)
            {
                subject.NextReadyCueAt = now + _random.Next(ReadyCueMin, ReadyCueMax);
                ReminderCue(ent);
            }
        }
    }

    private void SetStage(Entity<CMUWendigoSubjectComponent> ent, CMUWendigoSubjectStage stage, TimeSpan duration)
    {
        ent.Comp.Stage = stage;
        ent.Comp.StageEndsAt = _timing.CurTime + duration;
        ent.Comp.ReadyCuePlayed = false;
    }

    private void GestationTick(Entity<CMUWendigoSubjectComponent> ent, TimeSpan now)
    {
        if (now >= ent.Comp.NextFlavorAt)
        {
            ent.Comp.NextFlavorAt = now + _random.Next(FlavorMin, FlavorMax);
            var line = _random.Next(1, GestationFlavorLines + 1);
            _popup.PopupEntity(Loc.GetString($"cmu-wendigo-lab-gestation-flavor-{line}"), ent, ent, PopupType.Medium);
        }

        if (now >= ent.Comp.NextSeizureAt)
        {
            ent.Comp.NextSeizureAt = now + _random.Next(GestationSeizureMin, GestationSeizureMax);
            Seizure(ent, MinorSeizure);
        }
    }

    /// <summary>
    /// Plays once when a stage's timer finishes, so the scientists can tell the subject is ready.
    /// </summary>
    private void ExpiryCue(Entity<CMUWendigoSubjectComponent> ent)
    {
        switch (ent.Comp.Stage)
        {
            case CMUWendigoSubjectStage.Fed1:
            case CMUWendigoSubjectStage.Fed2:
                Seizure(ent, MinorSeizure);
                break;
            case CMUWendigoSubjectStage.Mutagen:
                _jitter.DoJitter(ent, TimeSpan.FromSeconds(3), true, 8, 5);
                _emote.TryEmoteWithChat(ent, Cough, forceEmote: true, cooldown: TimeSpan.Zero);
                _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-ready-mutagen"), ent, ent, PopupType.Medium);
                break;
            case CMUWendigoSubjectStage.Gestation:
                _jitter.DoJitter(ent, TimeSpan.FromSeconds(4), true, 12, 6);
                _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-ready-gestation"), ent, ent, PopupType.LargeCaution);
                break;
        }
    }

    /// <summary>
    /// Repeats while a ready subject waits for its next input.
    /// </summary>
    private void ReminderCue(Entity<CMUWendigoSubjectComponent> ent)
    {
        var shiver = ent.Comp.Stage == CMUWendigoSubjectStage.Mutagen;
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(ent.Comp.Stage == CMUWendigoSubjectStage.Gestation ? 3 : 2), true, 6, 4);

        if (shiver)
        {
            _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-shiver-others", ("subject", ent.Owner)),
                ent, Filter.PvsExcept(ent), true, PopupType.Small);
            return;
        }

        _emote.TryEmoteWithChat(ent, Cough, forceEmote: true, cooldown: TimeSpan.Zero);
    }

    private void MutationPulse(Entity<CMUWendigoSubjectComponent> ent)
    {
        ent.Comp.NextMutationPulseAt = _timing.CurTime + MutationPulseInterval;
        var duration = MutationPulseInterval + TimeSpan.FromSeconds(1);
        _jitter.DoJitter(ent, duration, true, 12, 8);
        _stun.TryParalyze(ent, duration, true);

        var line = _random.Next(1, MutationWarningLines + 1);
        _popup.PopupEntity(Loc.GetString($"cmu-wendigo-lab-mutation-warning-{line}"), ent, ent, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-mutation-others", ("subject", ent.Owner)),
            ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
    }

    private void Seizure(EntityUid uid, TimeSpan duration)
    {
        _jitter.DoJitter(uid, duration, true, 10, 6);
        _stun.TryParalyze(uid, duration, true);
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-seizure"), uid, uid, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-seizure-others", ("subject", uid)),
            uid, Filter.PvsExcept(uid), true, PopupType.MediumCaution);
    }

    private void PopupNotReady(EntityUid uid)
    {
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-not-ready"), uid, uid, PopupType.Small);
    }

    #endregion

    #region Examine and round end

    private void OnSubjectExamined(Entity<CMUWendigoSubjectComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Stage == CMUWendigoSubjectStage.Mutating)
        {
            args.PushMarkup(Loc.GetString("cmu-wendigo-lab-examine-mutating"));
            return;
        }

        if (_timing.CurTime >= ent.Comp.StageEndsAt)
            args.PushMarkup(Loc.GetString("cmu-wendigo-lab-examine-ready"));
    }

    private void OnTamedExamined(Entity<CMUWendigoTamedComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-wendigo-lab-examine-tamed", ("master", ent.Comp.MasterName)));
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        if (_roundEndLines.Count == 0)
            return;

        ev.AddLine(Loc.GetString("cmu-wendigo-lab-round-end-header"));
        foreach (var line in _roundEndLines)
        {
            ev.AddLine(line);
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _roundEndLines.Clear();
    }

    #endregion
}
