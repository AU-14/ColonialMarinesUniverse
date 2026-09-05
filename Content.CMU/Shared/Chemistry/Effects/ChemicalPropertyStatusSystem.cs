using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Pulling;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.CMU14.Medical.Injuries.Pain.Penalties;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;
using Robust.Shared.Network;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Rejuvenate;

namespace Content.Shared.CMU14.Chemistry.Effects;

[ByRefEvent]
public readonly record struct ChemicalAntiparasiticChangedEvent;

[ByRefEvent]
public readonly record struct ChemicalCardiacPacingChangedEvent(TimeSpan ExpiresAt);

[ByRefEvent]
public record struct GetChemicalStunTimeMultiplierEvent
{
    public float Multiplier = 1f;

    public GetChemicalStunTimeMultiplierEvent()
    {
    }
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), AutoGenerateComponentPause]
public sealed partial class ChemicalNerveStimulationComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Strength;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), AutoGenerateComponentPause]
public sealed partial class ChemicalMuscleStimulationComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Strength;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ChemicalCardiacPacingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Strength;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ChemicalHyperdensityComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Protection = 0.75f;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ChemicalNeuroshieldComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Protection = 0.8f;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ChemicalNeurocryogenicComponent : Component
{
    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ChemicalAntiparasiticComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Strength;

    [DataField, AutoNetworkedField]
    public float TreatmentProgress;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ChemicalFluxingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Progress;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ChemicalPainSensitivityComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1f;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ChemicalAddictionTreatmentComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Strength;

    [DataField, AutoNetworkedField]
    public float Progress;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

/// <summary>
/// Owns short-lived state supplied by generated medicinal properties. Reapplying a property
/// refreshes its duration and keeps the strongest potency instead of stacking modifiers.
/// </summary>
public sealed partial class ChemicalPropertyStatusSystem : EntitySystem
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(2);
    private const string DirectSource = "__direct";

    private readonly Dictionary<EntityUid, Dictionary<string, TimedStrength>> _nerveSources = new();
    private readonly Dictionary<EntityUid, Dictionary<string, TimedStrength>> _muscleSources = new();
    private readonly Dictionary<EntityUid, Dictionary<string, TimedStrength>> _pacingSources = new();
    private readonly Dictionary<EntityUid, Dictionary<string, TimedStrength>> _hyperdensitySources = new();
    private readonly Dictionary<EntityUid, Dictionary<string, TimedStrength>> _neuroshieldSources = new();
    private readonly Dictionary<EntityUid, Dictionary<string, TimedStrength>> _antiparasiticSources = new();
    private readonly Dictionary<EntityUid, Dictionary<string, TimedStrength>> _painSensitivitySources = new();
    private readonly Dictionary<EntityUid, Dictionary<string, TimedStrength>> _addictionTreatmentSources = new();

    [Dependency] private CMUMedicalSchedulerSystem _scheduler = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private SharedCMUMedicalSpeedSystem _medicalSpeed = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChemicalNerveStimulationComponent, RefreshMovementSpeedModifiersEvent>(OnNerveMovement);
        SubscribeLocalEvent<ChemicalNerveStimulationComponent, GetChemicalStunTimeMultiplierEvent>(OnNerveStunTime);
        SubscribeLocalEvent<ChemicalMuscleStimulationComponent, RefreshMovementSpeedModifiersEvent>(OnMuscleMovement);
        SubscribeLocalEvent<ChemicalMuscleStimulationComponent, GetMeleeDamageEvent>(OnMuscleMelee);
        SubscribeLocalEvent<ChemicalMuscleStimulationComponent, PullSlowdownAttemptEvent>(OnMusclePullSlowdown);

        SubscribeLocalEvent<ChemicalNerveStimulationComponent, ComponentStartup>(OnMovementStatusChanged);
        SubscribeLocalEvent<ChemicalNerveStimulationComponent, ComponentShutdown>(OnMovementStatusChanged);
        SubscribeLocalEvent<ChemicalMuscleStimulationComponent, ComponentStartup>(OnMuscleStatusChanged);
        SubscribeLocalEvent<ChemicalMuscleStimulationComponent, ComponentShutdown>(OnMuscleStatusChanged);
        SubscribeLocalEvent<MetaDataComponent, EntityUnpausedEvent>(OnEntityUnpaused);
        RegisterStrengthStatus<ChemicalNerveStimulationComponent>(_nerveSources,
            comp => (comp.Strength, comp.ExpiresAt),
            (comp, strength, expires) => (comp.Strength, comp.ExpiresAt) = (strength, expires), RefreshMovement);
        RegisterStrengthStatus<ChemicalMuscleStimulationComponent>(_muscleSources,
            comp => (comp.Strength, comp.ExpiresAt),
            (comp, strength, expires) => (comp.Strength, comp.ExpiresAt) = (strength, expires), RefreshMovement);
        RegisterStrengthStatus<ChemicalCardiacPacingComponent>(_pacingSources,
            comp => (comp.Strength, comp.ExpiresAt),
            (comp, strength, expires) => (comp.Strength, comp.ExpiresAt) = (strength, expires));
        RegisterStrengthStatus<ChemicalHyperdensityComponent>(_hyperdensitySources,
            comp => (comp.Protection, comp.ExpiresAt),
            (comp, strength, expires) => (comp.Protection, comp.ExpiresAt) = (strength, expires));
        RegisterStrengthStatus<ChemicalNeuroshieldComponent>(_neuroshieldSources,
            comp => (comp.Protection, comp.ExpiresAt),
            (comp, strength, expires) => (comp.Protection, comp.ExpiresAt) = (strength, expires));
        RegisterStrengthStatus<ChemicalAntiparasiticComponent>(_antiparasiticSources,
            comp => (comp.Strength, comp.ExpiresAt),
            (comp, strength, expires) => (comp.Strength, comp.ExpiresAt) = (strength, expires), RefreshAntiparasitic);
        RegisterStrengthStatus<ChemicalPainSensitivityComponent>(_painSensitivitySources,
            comp => (comp.Multiplier, comp.ExpiresAt),
            (comp, strength, expires) => (comp.Multiplier, comp.ExpiresAt) = (strength, expires),
            beforeExpiry: SettlePain);
        RegisterStrengthStatus<ChemicalAddictionTreatmentComponent>(_addictionTreatmentSources,
            comp => (comp.Strength, comp.ExpiresAt),
            (comp, strength, expires) => (comp.Strength, comp.ExpiresAt) = (strength, expires));
        RegisterExpiry<ChemicalNeurocryogenicComponent>(comp => comp.ExpiresAt);
        RegisterExpiry<ChemicalFluxingComponent>(comp => comp.ExpiresAt);
        SubscribeLocalEvent<ChemicalNerveStimulationComponent, AfterAutoHandleStateEvent>(OnNerveState);
        SubscribeLocalEvent<ChemicalMuscleStimulationComponent, AfterAutoHandleStateEvent>(OnMuscleState);
        SubscribeLocalEvent<ChemicalPainSensitivityComponent, ComponentShutdown>(OnSensitivityShutdown);

    }

    public void ApplyNerveStimulation(EntityUid target, float strength, string source = DirectSource)
    {
        if (_net.IsClient)
            return;

        var comp = EnsureComp<ChemicalNerveStimulationComponent>(target);
        var previousStrength = comp.Strength;
        RecordSource(_nerveSources, target, comp, source, strength, out comp.Strength, out comp.ExpiresAt);
        Dirty(target, comp);
        if (previousStrength != comp.Strength)
            RefreshMovement(target);
    }

    public void ApplyMuscleStimulation(EntityUid target, float strength, string source = DirectSource)
    {
        if (_net.IsClient)
            return;

        var comp = EnsureComp<ChemicalMuscleStimulationComponent>(target);
        var previousStrength = comp.Strength;
        RecordSource(_muscleSources, target, comp, source, strength, out comp.Strength, out comp.ExpiresAt);
        Dirty(target, comp);
        if (previousStrength != comp.Strength)
            RefreshMovement(target);
    }

    public void ApplyCardiacPacing(EntityUid target, float strength, string source = DirectSource)
    {
        if (_net.IsClient)
            return;

        var comp = EnsureComp<ChemicalCardiacPacingComponent>(target);
        RecordSource(_pacingSources, target, comp, source, strength, out comp.Strength, out comp.ExpiresAt);
        Dirty(target, comp);
        var changed = new ChemicalCardiacPacingChangedEvent(comp.ExpiresAt);
        RaiseLocalEvent(target, ref changed);
    }

    public void ApplyHyperdensity(EntityUid target, float protection = 0.75f, string source = DirectSource)
    {
        if (_net.IsClient)
            return;

        var comp = EnsureComp<ChemicalHyperdensityComponent>(target);
        RecordSource(_hyperdensitySources, target, comp, source, protection, out comp.Protection, out comp.ExpiresAt);
        Dirty(target, comp);
    }

    public void ApplyNeuroshield(EntityUid target, float protection = 0.8f, string source = DirectSource)
    {
        if (_net.IsClient)
            return;

        var comp = EnsureComp<ChemicalNeuroshieldComponent>(target);
        RecordSource(_neuroshieldSources, target, comp, source, protection, out comp.Protection, out comp.ExpiresAt);
        Dirty(target, comp);
    }

    public void ApplyNeurocryogenic(EntityUid target)
    {
        if (_net.IsClient)
            return;

        var comp = EnsureComp<ChemicalNeurocryogenicComponent>(target);
        comp.ExpiresAt = MathHelper.Max(comp.ExpiresAt, StatusTime(target) + DefaultDuration);
        Dirty(target, comp);
        ScheduleExpiry<ChemicalNeurocryogenicComponent>(target, comp.ExpiresAt);
    }

    public ChemicalAntiparasiticComponent? ApplyAntiparasitic(EntityUid target,
        float strength,
        float progress,
        string source = DirectSource)
    {
        if (_net.IsClient)
            return null;

        var comp = EnsureComp<ChemicalAntiparasiticComponent>(target);
        var previousStrength = comp.Strength;
        RecordSource(_antiparasiticSources, target, comp, source, strength, out comp.Strength, out comp.ExpiresAt);
        comp.TreatmentProgress += MathF.Max(0f, progress);
        Dirty(target, comp);
        if (previousStrength != comp.Strength)
            RefreshAntiparasitic(target);
        return comp;
    }

    public ChemicalFluxingComponent? ApplyFluxing(EntityUid target, float progress)
    {
        if (_net.IsClient)
            return null;

        var comp = EnsureComp<ChemicalFluxingComponent>(target);
        comp.Progress += MathF.Max(0f, progress);
        comp.ExpiresAt = MathHelper.Max(comp.ExpiresAt, StatusTime(target) + DefaultDuration);
        Dirty(target, comp);
        ScheduleExpiry<ChemicalFluxingComponent>(target, comp.ExpiresAt);
        return comp;
    }

    public void ApplyPainSensitivity(EntityUid target, float multiplier, string source = DirectSource)
    {
        if (_net.IsClient)
            return;

        SettlePain(target);
        var comp = EnsureComp<ChemicalPainSensitivityComponent>(target);
        RecordSource(_painSensitivitySources, target, comp, source, multiplier, out comp.Multiplier, out comp.ExpiresAt);
        Dirty(target, comp);
    }

    /// <summary>
    /// Reads the sensitivity over a historical active-time interval. Sources are
    /// retained until pain consumes their preceding interval before mutation/expiry.
    /// </summary>
    public float GetPainSensitivity(EntityUid target, TimeSpan at, out TimeSpan nextChange)
    {
        nextChange = TimeSpan.MaxValue;
        var multiplier = 1f;
        if (_painSensitivitySources.TryGetValue(target, out var sources))
        {
            foreach (var entry in sources.Values)
            {
                if (entry.ExpiresAt <= at)
                    continue;
                multiplier = MathF.Max(multiplier, entry.Strength);
                nextChange = MathHelper.Min(nextChange, entry.ExpiresAt);
            }
            return multiplier;
        }

        if (TryComp<ChemicalPainSensitivityComponent>(target, out var component) && component.ExpiresAt > at)
        {
            nextChange = component.ExpiresAt;
            return MathF.Max(1, component.Multiplier);
        }
        return multiplier;
    }

    private void SettlePain(EntityUid target) => _pain.SettlePainBeforeModifierChange(target);

    private void OnSensitivityShutdown(Entity<ChemicalPainSensitivityComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsServer && !TerminatingOrDeleted(ent.Owner))
            SettlePain(ent.Owner);
    }

    public ChemicalAddictionTreatmentComponent? ApplyAddictionTreatment(EntityUid target,
        float strength,
        float progress,
        string source = DirectSource)
    {
        if (_net.IsClient)
            return null;

        var comp = EnsureComp<ChemicalAddictionTreatmentComponent>(target);
        RecordSource(_addictionTreatmentSources, target, comp, source, strength, out comp.Strength, out comp.ExpiresAt);
        comp.Progress += MathF.Max(0f, progress);
        Dirty(target, comp);
        return comp;
    }

    private void OnNerveMovement(Entity<ChemicalNerveStimulationComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;
        var bonus = MathF.Min(0.30f, ent.Comp.Strength * 0.10f);
        args.ModifySpeed(1f + bonus, 1f + bonus);
    }

    private void OnNerveStunTime(Entity<ChemicalNerveStimulationComponent> ent, ref GetChemicalStunTimeMultiplierEvent args)
    {
        args.Multiplier *= MathF.Max(0.5f, 1f - ent.Comp.Strength * 0.15f);
    }

    private void OnMuscleMovement(Entity<ChemicalMuscleStimulationComponent> ent,
        ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;
        var bonus = MathF.Min(0.30f, ent.Comp.Strength * 0.05f);
        args.ModifySpeed(1f + bonus, 1f + bonus);
    }

    private void OnMuscleMelee(Entity<ChemicalMuscleStimulationComponent> ent, ref GetMeleeDamageEvent args)
    {
        args.Damage *= (FixedPoint2)(1f + MathF.Min(0.75f, ent.Comp.Strength * 0.15f));
    }

    private void OnMusclePullSlowdown(Entity<ChemicalMuscleStimulationComponent> ent, ref PullSlowdownAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnMovementStatusChanged(Entity<ChemicalNerveStimulationComponent> ent, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers((ent.Owner, null));
        _medicalSpeed.RefreshAggregatedPenalties(ent);
    }

    private void OnMovementStatusChanged(Entity<ChemicalNerveStimulationComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _movement.RefreshMovementSpeedModifiers((ent.Owner, null));
        _medicalSpeed.RefreshAggregatedPenalties(ent);
    }

    private void OnMuscleStatusChanged(Entity<ChemicalMuscleStimulationComponent> ent, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers((ent.Owner, null));
        _medicalSpeed.RefreshAggregatedPenalties(ent);
    }

    private void OnMuscleStatusChanged(Entity<ChemicalMuscleStimulationComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _movement.RefreshMovementSpeedModifiers((ent.Owner, null));
        _medicalSpeed.RefreshAggregatedPenalties(ent);
    }

    private void OnEntityUnpaused(Entity<MetaDataComponent> ent, ref EntityUnpausedEvent args)
    {
        ShiftSources(_nerveSources, ent, args.PausedTime);
        ShiftSources(_muscleSources, ent, args.PausedTime);
        ShiftSources(_pacingSources, ent, args.PausedTime);
        ShiftSources(_hyperdensitySources, ent, args.PausedTime);
        ShiftSources(_neuroshieldSources, ent, args.PausedTime);
        ShiftSources(_antiparasiticSources, ent, args.PausedTime);
        ShiftSources(_painSensitivitySources, ent, args.PausedTime);
        ShiftSources(_addictionTreatmentSources, ent, args.PausedTime);
    }

    private void RefreshMovement(EntityUid target)
    {
        _movement.RefreshMovementSpeedModifiers(target);
        _medicalSpeed.RefreshAggregatedPenalties(target);
    }

    private void RefreshAntiparasitic(EntityUid target)
    {
        var changed = new ChemicalAntiparasiticChangedEvent();
        RaiseLocalEvent(target, ref changed);
    }

    private void OnNerveState(Entity<ChemicalNerveStimulationComponent> ent, ref AfterAutoHandleStateEvent args)
        => RefreshMovement(ent.Owner);

    private void OnMuscleState(Entity<ChemicalMuscleStimulationComponent> ent, ref AfterAutoHandleStateEvent args)
        => RefreshMovement(ent.Owner);

    private static CMUMedicalWorkKey StatusKey<T>() where T : Component => new(typeof(T).Name);

    // AutoPaused fields use the start of the current pause as their clock until
    // unpause shifts them. Sources applied mid-pause must use that same clock.
    private TimeSpan StatusTime(EntityUid target) => _timing.CurTime - _metadata.GetPauseTime(target);

    private void ScheduleExpiry<T>(EntityUid target, TimeSpan dueAt) where T : Component
        => _scheduler.Schedule(target, StatusKey<T>(), dueAt + _metadata.GetPauseTime(target));

    private void RecordSource<T>(Dictionary<EntityUid, Dictionary<string, TimedStrength>> statuses,
        EntityUid target,
        T component,
        string source,
        float strength,
        out float strongest,
        out TimeSpan expiresAt) where T : Component
    {
        if (!statuses.TryGetValue(target, out var sources))
        {
            sources = new Dictionary<string, TimedStrength>();
            statuses.Add(target, sources);
        }

        var now = StatusTime(target);
        RemoveExpiredSources(sources, now);
        sources[source] = new TimedStrength(strength, now + DefaultDuration);
        AggregateSources(sources, out strongest, out expiresAt);
        ScheduleSources<T>(target, sources);
    }

    // These component/event pairs are owned here. In particular, Antiparasitic's
    // ComponentShutdown is owned by the parasite system; removal only retires our sources.
    private void RegisterStrengthStatus<T>(Dictionary<EntityUid, Dictionary<string, TimedStrength>> statuses,
        Func<T, (float Strength, TimeSpan ExpiresAt)> read,
        Action<T, float, TimeSpan> update,
        Action<EntityUid>? afterStrengthChange = null,
        Action<EntityUid>? beforeExpiry = null) where T : Component
    {
        SubscribeLocalEvent<T, RejuvenateEvent>(OnRejuvenate<T>);
        SubscribeLocalEvent<T, ComponentInit>((Entity<T> ent, ref ComponentInit args) =>
        {
            if (_net.IsClient)
                return;

            // Aggregate fields cannot reconstruct separate source lifetimes after
            // load. Retire unsupported source-less state as the old scan did; a
            // real application below replaces this provisional deadline.
            ScheduleExpiry<T>(ent.Owner, StatusTime(ent.Owner));
        });
        SubscribeLocalEvent<T, CMUMedicalWorkDueEvent>((Entity<T> ent, ref CMUMedicalWorkDueEvent args) =>
        {
            if (args.Key != StatusKey<T>() || _net.IsClient)
                return;

            beforeExpiry?.Invoke(ent.Owner);
            if (statuses.TryGetValue(ent.Owner, out var sources))
                RemoveExpiredSources(sources, _timing.CurTime);
            if (sources == null || sources.Count == 0)
            {
                statuses.Remove(ent.Owner);
                RemComp<T>(ent.Owner);
                return;
            }

            AggregateSources(sources, out var strongest, out var expiresAt);
            var previous = read(ent.Comp);
            if (previous != (strongest, expiresAt))
            {
                update(ent.Comp, strongest, expiresAt);
                Dirty(ent);
                if (previous.Strength != strongest)
                    afterStrengthChange?.Invoke(ent.Owner);
            }

            ScheduleSources<T>(ent.Owner, sources);
        });
        SubscribeLocalEvent<T, ComponentRemove>((Entity<T> ent, ref ComponentRemove args) =>
        {
            statuses.Remove(ent.Owner);
            _scheduler.Cancel(ent.Owner, StatusKey<T>());
            if (!TerminatingOrDeleted(ent.Owner))
                afterStrengthChange?.Invoke(ent.Owner);
        });
    }

    private void RegisterExpiry<T>(Func<T, TimeSpan> expiresAt) where T : Component
    {
        SubscribeLocalEvent<T, RejuvenateEvent>(OnRejuvenate<T>);
        SubscribeLocalEvent<T, ComponentInit>((Entity<T> ent, ref ComponentInit args) =>
        {
            if (!_net.IsClient)
                ScheduleExpiry<T>(ent.Owner, expiresAt(ent.Comp));
        });
        SubscribeLocalEvent<T, CMUMedicalWorkDueEvent>((Entity<T> ent, ref CMUMedicalWorkDueEvent args) =>
        {
            if (args.Key == StatusKey<T>() && !_net.IsClient && expiresAt(ent.Comp) <= _timing.CurTime)
                RemComp<T>(ent.Owner);
        });
        SubscribeLocalEvent<T, ComponentRemove>((Entity<T> ent, ref ComponentRemove args) =>
            _scheduler.Cancel(ent.Owner, StatusKey<T>()));
    }

    private void OnRejuvenate<T>(Entity<T> ent, ref RejuvenateEvent args) where T : Component
    {
        // Removal also retires source history, queued expiry and cached effects.
        if (!_net.IsClient)
            RemComp<T>(ent.Owner);
    }

    private void ScheduleSources<T>(EntityUid target, Dictionary<string, TimedStrength> sources) where T : Component
    {
        var next = TimeSpan.MaxValue;
        foreach (var entry in sources.Values)
            next = MathHelper.Min(next, entry.ExpiresAt);
        ScheduleExpiry<T>(target, next);
    }

    private static void RemoveExpiredSources(Dictionary<string, TimedStrength> sources, TimeSpan now)
    {
        // Dictionary removal during enumeration is supported by the target runtime.
        foreach (var (source, entry) in sources)
        {
            if (entry.ExpiresAt <= now)
                sources.Remove(source);
        }
    }

    private static void AggregateSources(Dictionary<string, TimedStrength> sources,
        out float strongest,
        out TimeSpan expiresAt)
    {
        strongest = 0f;
        expiresAt = TimeSpan.Zero;
        foreach (var entry in sources.Values)
        {
            strongest = MathF.Max(strongest, entry.Strength);
            expiresAt = MathHelper.Max(expiresAt, entry.ExpiresAt);
        }
    }

    private static void ShiftSources(Dictionary<EntityUid, Dictionary<string, TimedStrength>> statuses,
        EntityUid target,
        TimeSpan pausedTime)
    {
        if (!statuses.TryGetValue(target, out var sources))
            return;

        foreach (var source in new List<string>(sources.Keys))
        {
            var entry = sources[source];
            sources[source] = entry with { ExpiresAt = entry.ExpiresAt + pausedTime };
        }
    }

    private readonly record struct TimedStrength(float Strength, TimeSpan ExpiresAt);
}
