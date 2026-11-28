using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Tackle;
using Content.Shared.CMU14.Combat.CQC;
using Content.Shared.CMU14.Fireman;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Combat.CQC;

/// <summary>
/// Generic CQC sequences for any body with a <see cref="CMUCqcComponent"/>. Records the body's close-quarters inputs in
/// its buffer and, after each one, fires the longest listed <see cref="CMUCqcSequencePrototype"/> the buffer ends in,
/// through the effect registry (<see cref="RegisterEffect"/>). Also owns the per-target hard-CC immunity windows and
/// the one way a CQC body applies a hard CC (<see cref="ApplyHardCc(EntityUid, EntityUid, TimeSpan, CMUHardCcKind, out TimeSpan)"/>).
/// </summary>
/// <remarks>
/// Flow per input: push, match (raise <see cref="CMUCqcSequenceAttemptEvent"/>; skip if cancelled), fire (the
/// registered effect; buffer and cooldown restored when it does not land; raise <see cref="CMUCqcSequenceFiredEvent"/>),
/// then raise <see cref="CMUCqcInputEvent"/>. Matching before the input event keeps "match first, gain after": a
/// meter the input fills cannot empower the sequence that input completes.
/// Input hooks (one directed subscription per pair):
/// <list type="bullet">
/// <item>(CMUCqcComponent, MeleeHitEvent): unarmed hits, raised on the body as its own weapon. Other weapons forward
/// through <see cref="HandleMeleeHit"/> (the Early Warning Joe's machete). Never (MeleeWeaponComponent, MeleeHitEvent).</item>
/// <item>(MobStateComponent, CMDisarmEvent): a disarm attempt, raised on the target. Never sets Handled.</item>
/// <item>(MobStateComponent, PullStartedMessage), after FiremanCarrySystem, and (CMUCqcComponent, CMUAggressiveGrabbedEvent):
/// grabs. The pull is taken on the pulled end, the last of its two raises.</item>
/// <item>Each close-quarters ability calls <see cref="RegisterAbility"/> when it lands.</item>
/// </list>
/// Server only: nothing here is predicted.
/// </remarks>
public sealed class CMUCqcSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FiremanCarrySystem _fireman = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;

    /// <summary>
    /// An effect a sequence runs on <paramref name="target"/>. Returns false when it could not land at all; the
    /// sequence then keeps its steps. <paramref name="empowered"/> is true only for an effect registered with an
    /// empowered branch, when the owner asked for it (<see cref="CMUCqcSequenceAttemptEvent.Empowered"/>).
    /// </summary>
    public delegate bool CqcEffect(EntityUid user, EntityUid target, CMUCqcSequencePrototype sequence, bool empowered);

    private static readonly ProtoId<DamageTypePrototype> Slash = "Slash";
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(0.2);

    /// <summary>Every sequence, longest first, then by id so equal lengths match in a stable order.</summary>
    private readonly List<CMUCqcSequencePrototype> _sequences = new();

    // filled by every registrant's Initialize; never cleared, so the order systems initialize in does not matter
    private readonly Dictionary<string, CqcEffect> _effects = new();
    private readonly HashSet<string> _empowerable = new();

    private readonly List<EntityUid> _hitTargets = new();
    private readonly List<EntityUid> _expiredImmunity = new();
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUCqcComponent, MeleeHitEvent>(OnMeleeHit);
        // PullStartedMessage is raised on the puller, then on the pulled. FiremanCarrySystem resets the carrier's
        // grab state on both ends (OnCarrierPullStarted, OnCarriablePullStarted), which undid an instant Seize grip
        // made from the puller's raise. Taking the Grab step on the pulled end, after FiremanCarrySystem, runs it
        // after both resets.
        SubscribeLocalEvent<MobStateComponent, PullStartedMessage>(OnPullStarted, after: [typeof(FiremanCarrySystem)]);
        SubscribeLocalEvent<CMUCqcComponent, CMUAggressiveGrabbedEvent>(OnAggressiveGrabbed);
        // TransformComponent belongs to the Yautja gauntlet and the tackle components to RMC. Handled is left alone
        // so both still resolve the disarm.
        SubscribeLocalEvent<MobStateComponent, CMDisarmEvent>(OnDisarm);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        RegisterEffect("Grip", Grip);
        CacheSequences();
    }

    #region Registry

    /// <summary>
    /// Adds (or replaces) the effect <paramref name="id"/> that sequences name in <see cref="CMUCqcSequencePrototype.Effect"/>.
    /// <paramref name="usesEmpowered"/>: the effect has an empowered branch, so an owner's empower is spent on it.
    /// </summary>
    public void RegisterEffect(string id, CqcEffect handler, bool usesEmpowered = false)
    {
        _effects[id] = handler;
        if (usesEmpowered)
            _empowerable.Add(id);
        else
            _empowerable.Remove(id);
    }

    public bool HasEffect(string id)
    {
        return _effects.ContainsKey(id);
    }

    public IEnumerable<string> Effects => _effects.Keys;

    /// <summary>Whether <paramref name="id"/> has an empowered branch.</summary>
    public bool UsesEmpowered(string id)
    {
        return _empowerable.Contains(id);
    }

    /// <summary>Seize: the pull goes straight to an aggressive grab, skipping any grip windup.</summary>
    private bool Grip(EntityUid user, EntityUid target, CMUCqcSequencePrototype sequence, bool empowered)
    {
        return TryComp(user, out CanFiremanCarryComponent? carry) && _fireman.TryAggressiveGrab((user, carry), target);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<CMUCqcSequencePrototype>())
            CacheSequences();
    }

    private void CacheSequences()
    {
        _sequences.Clear();
        _sequences.AddRange(_proto.EnumeratePrototypes<CMUCqcSequencePrototype>()
            .OrderByDescending(s => s.Steps.Count)
            .ThenBy(s => s.ID, StringComparer.Ordinal));
    }

    #endregion

    #region Hooks

    private void OnMeleeHit(Entity<CMUCqcComponent> ent, ref MeleeHitEvent args)
    {
        // raised on the weapon: only a bare-handed body is its own weapon
        if (args.Weapon != ent.Owner)
            return;

        HandleMeleeHit(ent.Owner, args);
    }

    /// <summary>
    /// A melee swing by <paramref name="user"/>, from its own fists or forwarded from another weapon. An empty swing is
    /// a miss: it breaks the sequence. A hit on living bodies is a Hit (or a Wide for a wide swing). The step remembers
    /// whether it came from a blade, which a <see cref="CMUCqcSequencePrototype.RequiresBlade"/> sequence's Wide steps need.
    /// </summary>
    public void HandleMeleeHit(EntityUid user, MeleeHitEvent args)
    {
        if (args.Handled || !args.IsHit || !TryComp(user, out CMUCqcComponent? cqc))
            return;

        if (args.HitEntities.Count == 0)
        {
            cqc.Buffer.Clear();
            return;
        }

        _hitTargets.Clear();
        foreach (var hit in args.HitEntities)
        {
            if (hit != user && TryComp(hit, out MobStateComponent? mob) && _mobState.IsAlive(hit, mob))
                _hitTargets.Add(hit);
        }

        // furniture and corpses build nothing and leave the sequence as it is
        if (_hitTargets.Count == 0)
            return;

        // the body the buffer is already on keeps the sequence going through a crowd
        var target = _hitTargets[0];
        if (CurrentTarget(cqc) is { } current && _hitTargets.Contains(current))
            target = current;

        var bodies = _hitTargets.Count;
        var blade = IsBlade(user, args.Weapon);
        var input = args.Direction == null ? CMUCqcInput.Hit : CMUCqcInput.Wide;
        Register((user, cqc), input, target, blade, bodies: bodies, meleeHit: true);
    }

    /// <summary>A blade is a weapon other than the body itself that is the machete or deals Slash damage.</summary>
    public bool IsBlade(EntityUid user, EntityUid weapon)
    {
        if (weapon == user)
            return false;

        if (HasComp<EWJoeMacheteComponent>(weapon))
            return true;

        return _melee.GetDamage(weapon, user).DamageDict.TryGetValue(Slash, out var slash) && slash > 0;
    }

    private void OnDisarm(Entity<MobStateComponent> ent, ref CMDisarmEvent args)
    {
        if (args.User == ent.Owner || !TryComp(args.User, out CMUCqcComponent? cqc))
            return;

        Register((args.User, cqc), CMUCqcInput.Disarm, ent.Owner);
    }

    private void OnPullStarted(Entity<MobStateComponent> ent, ref PullStartedMessage args)
    {
        // raised on both ends of the pull; only a CQC body's grab of this mob counts
        if (args.PulledUid != ent.Owner || !TryComp(args.PullerUid, out CMUCqcComponent? cqc))
            return;

        Register((args.PullerUid, cqc), CMUCqcInput.Grab, ent.Owner);
    }

    private void OnAggressiveGrabbed(Entity<CMUCqcComponent> ent, ref CMUAggressiveGrabbedEvent args)
    {
        // a corpse (or anything not alive) must not feed the buffer
        if (args.Carrier != ent.Owner || !_mobState.IsAlive(args.Target))
            return;

        Register(ent, CMUCqcInput.AggressiveGrab, args.Target);
    }

    #endregion

    #region Inputs

    /// <summary>
    /// A close-quarters ability landed: an Ability step on <paramref name="target"/> (null for a cone). A hard CC
    /// (<paramref name="hardCc"/>) also opens the target's CC-immunity window. Does nothing without a <see cref="CMUCqcComponent"/>.
    /// </summary>
    public void RegisterAbility(EntityUid user, EntityUid? target, bool hardCc = false)
    {
        if (!TryComp(user, out CMUCqcComponent? cqc))
            return;

        if (hardCc && target is { } victim)
            RecordHardCc((user, cqc), victim);

        Register((user, cqc), CMUCqcInput.Ability, target);
    }

    /// <summary>
    /// Records one input directly (tests, admin tools and forwarders): pushes it, fires the sequence it completes, then
    /// raises <see cref="CMUCqcInputEvent"/> with <paramref name="gain"/> as an explicit override. Returns false without
    /// a <see cref="CMUCqcComponent"/>.
    /// </summary>
    public bool Register(EntityUid user, CMUCqcInput input, EntityUid? target, bool fromBlade = false, int? gain = null)
    {
        if (!TryComp(user, out CMUCqcComponent? cqc))
            return false;

        Register((user, cqc), input, target, fromBlade, gain);
        return true;
    }

    private void Register(Entity<CMUCqcComponent> ent,
        CMUCqcInput input,
        EntityUid? target,
        bool fromBlade = false,
        int? gain = null,
        int bodies = 1,
        bool meleeHit = false)
    {
        // match first, gain after: an owner's meter that the input fills must not empower its own sequence
        Push(ent, input, target, _timing.CurTime, fromBlade);

        var ev = new CMUCqcInputEvent(target, input, fromBlade, bodies, gain, meleeHit);
        RaiseLocalEvent(ent, ref ev);
    }

    /// <summary>Records one input in the buffer and fires the sequence it completes, if any.</summary>
    private void Push(Entity<CMUCqcComponent> ent, CMUCqcInput input, EntityUid? target, TimeSpan now, bool fromBlade)
    {
        var buffer = ent.Comp.Buffer;
        if (buffer.Count > 0)
        {
            if (now - buffer[^1].Time > ent.Comp.MaxGap)
                buffer.Clear();
            else if (target is { } t && CurrentTarget(ent.Comp) is { } current && current != t)
                buffer.Clear(); // the same-target rule: a new body starts a new sequence
        }

        // a pull upgraded to an aggressive grab is one grab, not two steps
        if (input == CMUCqcInput.AggressiveGrab &&
            buffer.Count > 0 &&
            buffer[^1].Input == CMUCqcInput.Grab &&
            buffer[^1].Target == target)
        {
            buffer.RemoveAt(buffer.Count - 1);
        }

        buffer.Add(new CMUCqcBufferEntry(input, target, now, fromBlade));
        while (buffer.Count > Math.Max(1, ent.Comp.BufferCap))
        {
            buffer.RemoveAt(0);
        }

        TryMatch(ent);
    }

    /// <summary>Empties the body's sequence buffer (a miss, a stun, an owner's empower spent).</summary>
    public void ClearBuffer(EntityUid user)
    {
        if (TryComp(user, out CMUCqcComponent? cqc))
            cqc.Buffer.Clear();
    }

    /// <summary>The body the buffer's steps are on: the last recorded target.</summary>
    public static EntityUid? CurrentTarget(CMUCqcComponent cqc)
    {
        for (var i = cqc.Buffer.Count - 1; i >= 0; i--)
        {
            if (cqc.Buffer[i].Target is { } target)
                return target;
        }

        return null;
    }

    #endregion

    #region Matching

    /// <summary>
    /// Fires the longest listed sequence the buffer ends in that the grab, the blade, the target's CC immunity and the
    /// owner (<see cref="CMUCqcSequenceAttemptEvent"/>) allow. Returns whether one fired.
    /// </summary>
    public bool TryMatch(Entity<CMUCqcComponent> ent)
    {
        var buffer = ent.Comp.Buffer;
        if (buffer.Count == 0 || CurrentTarget(ent.Comp) is not { } target)
            return false;

        // one shared cooldown after any sequence fires; the input is kept in the buffer meanwhile
        if (_timing.CurTime < ent.Comp.NextSequence)
            return false;

        foreach (var sequence in _sequences)
        {
            if (sequence.Steps.Count == 0 ||
                sequence.Steps.Count > buffer.Count ||
                !Knows(ent.Comp, sequence) ||
                !EndsWith(buffer, sequence, target))
            {
                continue;
            }

            if (sequence.RequiresGrab && !IsGrabbedBy(ent, target))
                continue;

            // no sequence hard CC on a body still inside its immunity window
            if (sequence.HardCc && IsCcImmune((ent.Owner, ent.Comp), target))
                continue;

            if (!_effects.ContainsKey(sequence.Effect))
                continue;

            var attempt = new CMUCqcSequenceAttemptEvent(target, sequence.ID);
            RaiseLocalEvent(ent, ref attempt);
            if (attempt.Cancelled)
                continue;

            var empowered = attempt.Empowered && UsesEmpowered(sequence.Effect);
            if (!Fire(ent, sequence, target, empowered))
                continue;

            return true;
        }

        return false;
    }

    private static bool Knows(CMUCqcComponent cqc, CMUCqcSequencePrototype sequence)
    {
        foreach (var id in cqc.Sequences)
        {
            if (id.Id == sequence.ID)
                return true;
        }

        return false;
    }

    private bool Fire(Entity<CMUCqcComponent> ent, CMUCqcSequencePrototype sequence, EntityUid target, bool empowered)
    {
        // the effect can raise new inputs (a grab), so the buffer is emptied before it runs
        var buffer = ent.Comp.Buffer;
        var saved = buffer.ToList();
        buffer.Clear();

        // the cooldown starts before the effect, so an input the effect raises (Seize's grab) cannot chain a second
        // sequence; a sequence that did not land gives it back
        var previousNext = ent.Comp.NextSequence;
        ent.Comp.NextSequence = _timing.CurTime + ent.Comp.SequenceCooldown;

        if (!_effects.TryGetValue(sequence.Effect, out var effect) || !effect(ent, target, sequence, empowered))
        {
            // nothing happened: the steps are kept for another sequence
            ent.Comp.NextSequence = previousNext;
            buffer.Clear();
            buffer.AddRange(saved);
            return false;
        }

        // eligibility (the CC-immunity rule) was settled before the effect; the effect's own hard CC has just opened
        // the target's window, so an owner spending an empower here must not check it again
        var fired = new CMUCqcSequenceFiredEvent(target, sequence.ID, empowered);
        RaiseLocalEvent(ent, ref fired);

        if (sequence.HardCc)
            RecordHardCc((ent.Owner, ent.Comp), target);

        var name = Loc.GetString(sequence.Name);
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-liturgy-sequence", ("name", name)), ent, PopupType.MediumCaution);
        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(ent):user} used the CQC sequence {sequence.ID} on {ToPrettyString(target):target}{(empowered ? " (empowered)" : string.Empty)}");
        return true;
    }

    /// <summary>
    /// The buffer ends in the sequence's steps, every step on <paramref name="target"/> (or on no one), in time, and
    /// every Wide step of a blade sequence made with a blade.
    /// </summary>
    private static bool EndsWith(List<CMUCqcBufferEntry> buffer, CMUCqcSequencePrototype sequence, EntityUid target)
    {
        var offset = buffer.Count - sequence.Steps.Count;
        for (var i = 0; i < sequence.Steps.Count; i++)
        {
            var entry = buffer[offset + i];
            if (!StepMatches(sequence.Steps[i], entry.Input))
                return false;

            // fists cannot wide-swing; a blade sequence's Wide step needs a bladed swing
            if (sequence.RequiresBlade && sequence.Steps[i] == CMUCqcInput.Wide && !entry.FromBlade)
                return false;

            if (entry.Target is { } stepTarget && stepTarget != target)
                return false;

            if (i > 0 && sequence.MaxGapOverride is { } gap && (entry.Time - buffer[offset + i - 1].Time).TotalSeconds > gap)
                return false;
        }

        return true;
    }

    /// <summary>A Grab step also accepts an aggressive grab.</summary>
    public static bool StepMatches(CMUCqcInput step, CMUCqcInput input)
    {
        return step == input || step == CMUCqcInput.Grab && input == CMUCqcInput.AggressiveGrab;
    }

    /// <summary><paramref name="user"/> is pulling <paramref name="target"/> and the grab is aggressive.</summary>
    public bool IsGrabbedBy(EntityUid user, EntityUid target)
    {
        return TryComp(user, out PullerComponent? puller)
            && puller.Pulling == target
            && _fireman.IsAggressivelyGrabbed(target);
    }

    #endregion

    #region CC immunity

    public bool IsCcImmune(Entity<CMUCqcComponent?> user, EntityUid target)
    {
        return Resolve(user, ref user.Comp, false)
            && user.Comp.CcImmuneUntil.TryGetValue(target, out var until)
            && _timing.CurTime < until;
    }

    /// <summary>Opens <paramref name="target"/>'s hard-CC immunity window against this body.</summary>
    public void RecordHardCc(Entity<CMUCqcComponent?> user, EntityUid target)
    {
        if (!Resolve(user, ref user.Comp, false))
            return;

        user.Comp.CcImmuneUntil[target] = _timing.CurTime + user.Comp.CcImmunity;
    }

    /// <summary>
    /// How long a hard CC (knockdown or paralyze) of <paramref name="duration"/> from <paramref name="user"/> lasts on
    /// <paramref name="target"/>, without applying it or opening the window. The owner's scaling
    /// (<see cref="CMUCqcHardCcDurationEvent"/>) comes first, then, inside the target's window,
    /// <see cref="CMUCqcComponent.CcWindowMultiplier"/>. A source without the component has no window.
    /// </summary>
    public TimeSpan HardCcDuration(EntityUid user, EntityUid target, TimeSpan duration)
    {
        var ev = new CMUCqcHardCcDurationEvent(target, duration);
        RaiseLocalEvent(user, ref ev);
        duration = ev.Duration;

        if (TryComp(user, out CMUCqcComponent? cqc) && IsCcImmune((user, cqc), target))
            duration *= Math.Clamp(cqc.CcWindowMultiplier, 0f, 1f);

        return duration;
    }

    /// <summary>
    /// <see cref="HardCcDuration"/>, then opens (or refreshes) the target's window. For a hard CC another system applies
    /// later, such as the RMC obstacle-slam paralyze armed for a flight: the window opens when it is armed.
    /// </summary>
    public TimeSpan TakeHardCc(EntityUid user, EntityUid target, TimeSpan duration)
    {
        duration = HardCcDuration(user, target, duration);
        RecordHardCc(user, target);
        return duration;
    }

    /// <summary>
    /// The one way a CQC body applies a hard CC (knockdown or paralyze) to a target, from any source. Scales
    /// <paramref name="duration"/> by <see cref="HardCcDuration"/>, applies it with refresh, and when it lands opens or
    /// refreshes the target's window. <paramref name="applied"/> is the duration used.
    /// </summary>
    public bool ApplyHardCc(EntityUid user, EntityUid target, TimeSpan duration, CMUHardCcKind kind, out TimeSpan applied)
    {
        applied = HardCcDuration(user, target, duration);
        var landed = kind switch
        {
            CMUHardCcKind.Knockdown => _stun.TryKnockdown(target, applied, refresh: true, autoStand: true, drop: true),
            _ => _stun.TryParalyze(target, applied, true),
        };

        if (landed)
            RecordHardCc(user, target);

        return landed;
    }

    /// <inheritdoc cref="ApplyHardCc(EntityUid, EntityUid, TimeSpan, CMUHardCcKind, out TimeSpan)"/>
    public bool ApplyHardCc(EntityUid user, EntityUid target, TimeSpan duration, CMUHardCcKind kind)
    {
        return ApplyHardCc(user, target, duration, kind, out _);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckEvery;

        var query = EntityQueryEnumerator<CMUCqcComponent>();
        while (query.MoveNext(out var uid, out var cqc))
        {
            if (cqc.Buffer.Count > 0)
            {
                // a stunned or knocked-down body loses its sequence; a pause past the max gap starts it over
                if (HasComp<StunnedComponent>(uid) || HasComp<KnockedDownComponent>(uid) || now - cqc.Buffer[^1].Time > cqc.MaxGap)
                    cqc.Buffer.Clear();
            }

            if (cqc.CcImmuneUntil.Count == 0)
                continue;

            _expiredImmunity.Clear();
            foreach (var (target, until) in cqc.CcImmuneUntil)
            {
                if (now >= until || TerminatingOrDeleted(target))
                    _expiredImmunity.Add(target);
            }

            foreach (var target in _expiredImmunity)
            {
                cqc.CcImmuneUntil.Remove(target);
            }
        }
    }
}
