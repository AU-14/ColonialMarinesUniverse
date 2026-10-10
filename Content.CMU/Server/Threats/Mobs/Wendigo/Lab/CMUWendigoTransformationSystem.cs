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
using Content.Shared.CMU14.Medical.Diagnostics;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
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
public sealed class CMUWendigoTransformationSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedRMCEmoteSystem _emote = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRole = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly SatiationSystem _satiation = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public static readonly ProtoId<ReagentPrototype> StabilizedMutagen = "CMUStabilizedMutagen";
    public static readonly ProtoId<ReagentPrototype> MH32 = "CMUMH32";
    public static readonly ProtoId<ReagentPrototype> MH33 = "CMUMH33";

    public static readonly EntProtoId FullWendigo = "CMUWendigoLab";
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
    /// How long a ready subject waits for its next input before relapsing a stage.
    /// </summary>
    public static readonly TimeSpan ReadyWindow = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan WindowWarning = TimeSpan.FromSeconds(60);

    /// <summary>
    /// From this instability on, MH-33 no longer binds the Wendigo to its master.
    /// </summary>
    public const int UnstableThreshold = 3;

    /// <summary>
    /// At this instability the subject's body gives out and the procedure ends in death.
    /// </summary>
    public const int MaxInstability = 5;

    private const string CollapseDamageType = "Poison";
    private static readonly FixedPoint2 CollapseMargin = 5;

    /// <summary>
    /// How long a recorded injector stays attributable to the reagent that follows it.
    /// The injector event and the reagent reaction happen in the same tick.
    /// </summary>
    private static readonly TimeSpan InjectorWindow = TimeSpan.FromSeconds(1);

    private const int GestationFlavorLines = 6;
    private const int MutationWarningLines = 4;

    private readonly List<string> _roundEndLines = new();
    private readonly HashSet<EntityUid> _pendingCollapse = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanoidProfileComponent, IngestingEvent>(OnIngesting);
        SubscribeLocalEvent<CMUWendigoMealComponent, FullyEatenEvent>(OnMealEaten);

        SubscribeLocalEvent<CMUWendigoSubjectComponent, TargetBeforeInjectEvent>(OnBeforeInject);
        SubscribeLocalEvent<CMUWendigoSubjectComponent, CMUBeforeHyposprayInjectEvent>(OnBeforeHyposprayInject);
        SubscribeLocalEvent<CMUWendigoSubjectComponent, ReactionEntityEvent>(OnReaction);
        SubscribeLocalEvent<CMUWendigoSubjectComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CMUWendigoSubjectComponent, CMUWendigoTransformDoAfterEvent>(OnTransformDoAfter);
        SubscribeLocalEvent<CMUWendigoSubjectComponent, ExaminedEvent>(OnSubjectExamined);
        SubscribeLocalEvent<CMUWendigoSubjectComponent, CMUHealthScannerReadingEvent>(OnScannerReading);

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

        if (humanMeat)
        {
            // Human meat only counts once the piece is finished; remember the eater and their hunger at the first bite.
            var meal = EnsureComp<CMUWendigoMealComponent>(args.Food);
            if (meal.Eater != ent.Owner)
            {
                meal.Eater = ent;
                meal.StartedStarving = IsStarving(ent);
            }

            return;
        }

        // Before the Stabilized Mutagen stage, other food breaks the hunger cycle.
        if (TryComp(ent, out CMUWendigoSubjectComponent? subject)
            && subject.Stage is CMUWendigoSubjectStage.Fed1 or CMUWendigoSubjectStage.Fed2)
        {
            Abort((ent, subject));
        }
    }

    private void OnMealEaten(Entity<CMUWendigoMealComponent> food, ref FullyEatenEvent args)
    {
        if (food.Comp.Eater is not { } eater || TerminatingOrDeleted(eater))
            return;

        if (!TryComp(eater, out CMUWendigoSubjectComponent? subject))
        {
            if (food.Comp.StartedStarving && IsEligibleSubject(eater))
                BeginProcedure(eater);

            return;
        }

        var ent = (eater, subject);
        if (_timing.CurTime < subject.StageEndsAt)
        {
            PopupNotReady(eater);
            AddInstability(ent, $"finished human meat too early in stage {subject.Stage}");
            return;
        }

        switch (subject.Stage)
        {
            case CMUWendigoSubjectStage.Fed1:
                SetStage(ent, CMUWendigoSubjectStage.Fed2, _random.Next(FeedMin, FeedMax));
                _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-fed-again"), eater, eater, PopupType.Medium);
                break;
            case CMUWendigoSubjectStage.Mutagen:
                SetStage(ent, CMUWendigoSubjectStage.Gestation, GestationTime);
                var now = _timing.CurTime;
                subject.NextFlavorAt = now + _random.Next(FlavorMin, FlavorMax);
                subject.NextSeizureAt = now + _random.Next(GestationSeizureMin, GestationSeizureMax);
                _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-gestation-start"), eater, eater, PopupType.LargeCaution);
                break;
            default:
                // Fed2 waits for Stabilized Mutagen, Gestation for MH-32/MH-33; meat does nothing more.
                PopupNotReady(eater);
                AddInstability(ent, $"finished human meat in stage {subject.Stage}, which does not take meat");
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
            AddInstability(ent, $"injected with {args.ReagentQuantity.Quantity}u {reagent} in stage {ent.Comp.Stage} before it was ready");
            return;
        }

        if (args.ReagentQuantity.Quantity < RequiredDose)
        {
            _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-dose-too-small"), ent, ent, PopupType.Small);
            AddInstability(ent, $"injected with {args.ReagentQuantity.Quantity}u {reagent}, under the {RequiredDose}u dose");
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
        subject.StageStartedAt = now;
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
        if (args.NewMobState == MobState.Critical)
        {
            // Letting the subject fall into crit strains the procedure, but does not stop it.
            AddInstability(ent, "fell into critical condition");
            return;
        }

        if (args.NewMobState != MobState.Dead)
            return;

        // Death is the only cancel once Stabilized Mutagen is in; revival does not restore progress.
        _doAfter.Cancel(ent.Comp.DoAfter);
        RemCompDeferred<CMUWendigoSubjectComponent>(ent);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(ent):subject} died in Wendigo stage {ent.Comp.Stage}; the procedure was cancelled");
    }

    private void Abort(Entity<CMUWendigoSubjectComponent> ent)
    {
        RemCompDeferred<CMUWendigoSubjectComponent>(ent);
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-aborted"), ent, ent, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(ent):subject} ate other food in Wendigo stage {ent.Comp.Stage}; the procedure was aborted");
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

        if (subject.Tamed && subject.Instability >= UnstableThreshold)
        {
            // An unstable body rejects MH-33's conditioning: the Wendigo comes out feral.
            subject.Tamed = false;
            _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-bond-failed-others", ("subject", ent.Owner)),
                ent, PopupType.LargeCaution);
            _adminLog.Add(LogType.Action, LogImpact.High,
                $"{ToPrettyString(ent):subject}'s MH-33 bond failed at instability {subject.Instability}/{MaxInstability}; the Wendigo is feral");
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
            Dirty(wendigo, tamed);
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

        foreach (var collapsed in _pendingCollapse)
        {
            if (TerminatingOrDeleted(collapsed)
                || !TryComp(collapsed, out DamageableComponent? damageable)
                || !_thresholds.TryGetDeadThreshold(collapsed, out var dead))
            {
                continue;
            }

            // Exactly past the dead threshold, so the body dies without being gibbed.
            var needed = dead.Value - _damageable.GetTotalDamage((collapsed, damageable)) + CollapseMargin;
            if (needed <= FixedPoint2.Zero)
                continue;

            var damage = new DamageSpecifier();
            damage.DamageDict[CollapseDamageType] = needed;
            _damageable.TryChangeDamage(collapsed, damage, ignoreResistances: true, interruptsDoAfters: false);
        }

        _pendingCollapse.Clear();

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUWendigoSubjectComponent>();
        while (query.MoveNext(out var uid, out var subject))
        {
            if (!_mobState.IsAlive(uid))
            {
                // A ready window is paused while the subject is down, so revival never lands on an instant relapse.
                if (subject.ReadyCuePlayed && subject.Stage != CMUWendigoSubjectStage.Mutating)
                    subject.WindowEndsAt += TimeSpan.FromSeconds(frameTime);

                continue;
            }

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

            if (now >= subject.WindowEndsAt)
            {
                MissWindow(ent);
                continue;
            }

            if (!subject.WindowWarned && now >= subject.WindowEndsAt - WindowWarning)
            {
                subject.WindowWarned = true;
                WindowClosingCue(ent);
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
        ent.Comp.StageStartedAt = _timing.CurTime;
        ent.Comp.StageEndsAt = _timing.CurTime + duration;
        ent.Comp.ReadyCuePlayed = false;
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(ent):subject} entered Wendigo stage {stage}, ready in {duration.TotalSeconds:F0}s");
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
    /// Plays once when a stage's timer finishes: a seizure with red text the subject and everyone nearby can see.
    /// </summary>
    private void ExpiryCue(Entity<CMUWendigoSubjectComponent> ent)
    {
        LocId self, others;
        switch (ent.Comp.Stage)
        {
            case CMUWendigoSubjectStage.Fed1:
                self = "cmu-wendigo-lab-ready-fed";
                others = "cmu-wendigo-lab-ready-fed-others";
                break;
            case CMUWendigoSubjectStage.Fed2:
                self = "cmu-wendigo-lab-ready-fed2";
                others = "cmu-wendigo-lab-ready-fed2-others";
                break;
            case CMUWendigoSubjectStage.Mutagen:
                self = "cmu-wendigo-lab-ready-mutagen";
                others = "cmu-wendigo-lab-ready-mutagen-others";
                break;
            case CMUWendigoSubjectStage.Gestation:
                self = "cmu-wendigo-lab-ready-gestation";
                others = "cmu-wendigo-lab-ready-gestation-others";
                break;
            default:
                return;
        }

        ent.Comp.WindowEndsAt = _timing.CurTime + ReadyWindow;
        ent.Comp.WindowWarned = false;
        Seizure(ent, MinorSeizure, self, others);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(ent):subject} completed Wendigo stage {ent.Comp.Stage}; the next step must land within {ReadyWindow.TotalMinutes} minutes");
    }

    /// <summary>
    /// One minute before a ready subject relapses.
    /// </summary>
    private void WindowClosingCue(Entity<CMUWendigoSubjectComponent> ent)
    {
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(3), true, 12, 6);
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-window-closing"), ent, ent, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-window-closing-others", ("subject", ent.Owner)),
            ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(ent):subject}'s Wendigo stage {ent.Comp.Stage} window closes in {WindowWarning.TotalSeconds}s");
    }

    /// <summary>
    /// The ready window passed without the next input: the subject loses a stage and the procedure destabilises.
    /// </summary>
    private void MissWindow(Entity<CMUWendigoSubjectComponent> ent)
    {
        var missed = ent.Comp.Stage;
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-window-missed"), ent, ent, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-window-missed-others", ("subject", ent.Owner)),
            ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);

        CMUWendigoSubjectStage previous;
        switch (missed)
        {
            case CMUWendigoSubjectStage.Fed2:
                previous = CMUWendigoSubjectStage.Fed1;
                break;
            case CMUWendigoSubjectStage.Mutagen:
                previous = CMUWendigoSubjectStage.Fed2;
                break;
            case CMUWendigoSubjectStage.Gestation:
                previous = CMUWendigoSubjectStage.Mutagen;
                break;
            default:
                // Nothing to fall back to from the first meal: the hunger cycle breaks.
                RemCompDeferred<CMUWendigoSubjectComponent>(ent);
                _adminLog.Add(LogType.Action, LogImpact.Medium,
                    $"{ToPrettyString(ent):subject} missed the Wendigo stage {missed} window; the procedure ended");
                return;
        }

        // Back to the previous stage, already ready: the lost step has to be repeated.
        SetStage(ent, previous, TimeSpan.Zero);
        AddInstability(ent, $"missed the stage {missed} window and relapsed to {previous}");
    }

    /// <summary>
    /// Records a mistake. Each one is announced to the subject and onlookers; the maximum kills the subject.
    /// </summary>
    private void AddInstability(Entity<CMUWendigoSubjectComponent> ent, string reason)
    {
        if (ent.Comp.Instability >= MaxInstability)
            return;

        ent.Comp.Instability++;
        var level = ent.Comp.Instability;
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(ent):subject} Wendigo instability {level}/{MaxInstability}: {reason}");

        if (level >= MaxInstability)
        {
            Collapse(ent);
            return;
        }

        var severe = level >= UnstableThreshold;
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(2), true, 14, 8);
        _popup.PopupEntity(
            Loc.GetString(severe ? "cmu-wendigo-lab-instability-severe" : "cmu-wendigo-lab-instability",
                ("level", level), ("max", MaxInstability)),
            ent, ent, PopupType.LargeCaution);
        _popup.PopupEntity(
            Loc.GetString(severe ? "cmu-wendigo-lab-instability-severe-others" : "cmu-wendigo-lab-instability-others",
                ("subject", ent.Owner), ("level", level), ("max", MaxInstability)),
            ent, Filter.PvsExcept(ent), true, PopupType.MediumCaution);
    }

    private void Collapse(Entity<CMUWendigoSubjectComponent> ent)
    {
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-collapse"), ent, ent, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cmu-wendigo-lab-collapse-others", ("subject", ent.Owner)),
            ent, Filter.PvsExcept(ent), true, PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(ent):subject}'s body gave out at Wendigo instability {MaxInstability}; the subject died");

        _doAfter.Cancel(ent.Comp.DoAfter);
        RemCompDeferred<CMUWendigoSubjectComponent>(ent);

        // Applied next update: a collapse can start inside a mob state change (falling into crit).
        _pendingCollapse.Add(ent);
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

    private void Seizure(EntityUid uid, TimeSpan duration,
        LocId self = default, LocId others = default)
    {
        if (self == default)
            self = "cmu-wendigo-lab-seizure";
        if (others == default)
            others = "cmu-wendigo-lab-seizure-others";

        _jitter.DoJitter(uid, duration, true, 10, 6);
        _stun.TryParalyze(uid, duration, true);
        // Caution popups render red.
        _popup.PopupEntity(Loc.GetString(self), uid, uid, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString(others, ("subject", uid)),
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

        if (IsReadyForFinalDose(ent.Comp))
            args.PushMarkup(Loc.GetString("cmu-wendigo-lab-examine-ready"));

        if (ent.Comp.Instability >= UnstableThreshold)
            args.PushMarkup(Loc.GetString("cmu-wendigo-lab-examine-unstable-severe"));
        else if (ent.Comp.Instability > 0)
            args.PushMarkup(Loc.GetString("cmu-wendigo-lab-examine-unstable"));
    }

    private void OnScannerReading(Entity<CMUWendigoSubjectComponent> ent, ref CMUHealthScannerReadingEvent args)
    {
        args.State.CMUWendigoReading = BuildScannerReading(ent.Comp);
    }

    /// <summary>
    /// Health analyzer line: stage, overall progress, what the subject is waiting for, and instability.
    /// </summary>
    public string BuildScannerReading(CMUWendigoSubjectComponent subject)
    {
        var now = _timing.CurTime;
        var stageIndex = (int) subject.Stage;
        var stageLength = subject.StageEndsAt - subject.StageStartedAt;
        var stageFraction = stageLength <= TimeSpan.Zero
            ? 1f
            : Math.Clamp((float) ((now - subject.StageStartedAt) / stageLength), 0f, 1f);
        var stageCount = Enum.GetValues<CMUWendigoSubjectStage>().Length;
        var progress = (int) MathF.Round((stageIndex + stageFraction) / stageCount * 100f);

        string status;
        if (subject.Stage == CMUWendigoSubjectStage.Mutating)
            status = Loc.GetString("cmu-wendigo-lab-scanner-mutating");
        else if (now < subject.StageEndsAt)
            status = Loc.GetString("cmu-wendigo-lab-scanner-developing", ("time", FormatTime(subject.StageEndsAt - now)));
        else
            status = Loc.GetString("cmu-wendigo-lab-scanner-ready");

        return Loc.GetString("cmu-wendigo-lab-scanner",
            ("stage", Loc.GetString($"cmu-wendigo-lab-stage-{subject.Stage.ToString().ToLowerInvariant()}")),
            ("progress", progress),
            ("status", status),
            ("instability", subject.Instability),
            ("max", MaxInstability));
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        return $"{(int) time.TotalMinutes}:{time.Seconds:D2}";
    }

    /// <summary>
    /// Gestation is over and the subject will take MH-32 or MH-33.
    /// </summary>
    public bool IsReadyForFinalDose(CMUWendigoSubjectComponent subject)
    {
        return subject.Stage == CMUWendigoSubjectStage.Gestation && _timing.CurTime >= subject.StageEndsAt;
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
        _pendingCollapse.Clear();
    }

    #endregion
}
