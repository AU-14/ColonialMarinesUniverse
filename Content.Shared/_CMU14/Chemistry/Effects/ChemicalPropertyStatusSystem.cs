using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Pulling;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._CMU14.Medical.Injuries.Pain.Penalties;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Chemistry.Effects;

[ByRefEvent]
public record struct GetChemicalStunTimeMultiplierEvent
{
    public float Multiplier = 1f;

    public GetChemicalStunTimeMultiplierEvent()
    {
    }
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ChemicalNerveStimulationComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Strength;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
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
public sealed class ChemicalPropertyStatusSystem : EntitySystem
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

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedCMUMedicalSpeedSystem _medicalSpeed = default!;

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
    }

    public void ApplyNerveStimulation(EntityUid target, float strength, string source = DirectSource)
    {
        var comp = EnsureComp<ChemicalNerveStimulationComponent>(target);
        RecordSource(_nerveSources, target, source, strength, out comp.Strength, out comp.ExpiresAt);
        Dirty(target, comp);
        _movement.RefreshMovementSpeedModifiers(target);
        _medicalSpeed.RefreshAggregatedPenalties(target);
    }

    public void ApplyMuscleStimulation(EntityUid target, float strength, string source = DirectSource)
    {
        var comp = EnsureComp<ChemicalMuscleStimulationComponent>(target);
        RecordSource(_muscleSources, target, source, strength, out comp.Strength, out comp.ExpiresAt);
        Dirty(target, comp);
        _movement.RefreshMovementSpeedModifiers(target);
        _medicalSpeed.RefreshAggregatedPenalties(target);
    }

    public void ApplyCardiacPacing(EntityUid target, float strength, string source = DirectSource)
    {
        var comp = EnsureComp<ChemicalCardiacPacingComponent>(target);
        RecordSource(_pacingSources, target, source, strength, out comp.Strength, out comp.ExpiresAt);
        Dirty(target, comp);
    }

    public void ApplyHyperdensity(EntityUid target, float protection = 0.75f, string source = DirectSource)
    {
        var comp = EnsureComp<ChemicalHyperdensityComponent>(target);
        RecordSource(_hyperdensitySources, target, source, protection, out comp.Protection, out comp.ExpiresAt);
        Dirty(target, comp);
    }

    public void ApplyNeuroshield(EntityUid target, float protection = 0.8f, string source = DirectSource)
    {
        var comp = EnsureComp<ChemicalNeuroshieldComponent>(target);
        RecordSource(_neuroshieldSources, target, source, protection, out comp.Protection, out comp.ExpiresAt);
        Dirty(target, comp);
    }

    public void ApplyNeurocryogenic(EntityUid target)
    {
        var comp = EnsureComp<ChemicalNeurocryogenicComponent>(target);
        comp.ExpiresAt = MathHelper.Max(comp.ExpiresAt, _timing.CurTime + DefaultDuration);
        Dirty(target, comp);
    }

    public ChemicalAntiparasiticComponent ApplyAntiparasitic(EntityUid target,
        float strength,
        float progress,
        string source = DirectSource)
    {
        var comp = EnsureComp<ChemicalAntiparasiticComponent>(target);
        RecordSource(_antiparasiticSources, target, source, strength, out comp.Strength, out comp.ExpiresAt);
        comp.TreatmentProgress += MathF.Max(0f, progress);
        Dirty(target, comp);
        return comp;
    }

    public ChemicalFluxingComponent ApplyFluxing(EntityUid target, float progress)
    {
        var comp = EnsureComp<ChemicalFluxingComponent>(target);
        comp.Progress += MathF.Max(0f, progress);
        comp.ExpiresAt = MathHelper.Max(comp.ExpiresAt, _timing.CurTime + DefaultDuration);
        Dirty(target, comp);
        return comp;
    }

    public void ApplyPainSensitivity(EntityUid target, float multiplier, string source = DirectSource)
    {
        var comp = EnsureComp<ChemicalPainSensitivityComponent>(target);
        RecordSource(_painSensitivitySources, target, source, multiplier, out comp.Multiplier, out comp.ExpiresAt);
        Dirty(target, comp);
    }

    public ChemicalAddictionTreatmentComponent ApplyAddictionTreatment(EntityUid target,
        float strength,
        float progress,
        string source = DirectSource)
    {
        var comp = EnsureComp<ChemicalAddictionTreatmentComponent>(target);
        RecordSource(_addictionTreatmentSources, target, source, strength, out comp.Strength, out comp.ExpiresAt);
        comp.Progress += MathF.Max(0f, progress);
        Dirty(target, comp);
        return comp;
    }

    private void OnNerveMovement(Entity<ChemicalNerveStimulationComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
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
        _movement.RefreshMovementSpeedModifiers(ent);
        _medicalSpeed.RefreshAggregatedPenalties(ent);
    }

    private void OnMovementStatusChanged(Entity<ChemicalNerveStimulationComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _movement.RefreshMovementSpeedModifiers(ent);
        _medicalSpeed.RefreshAggregatedPenalties(ent);
    }

    private void OnMuscleStatusChanged(Entity<ChemicalMuscleStimulationComponent> ent, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
        _medicalSpeed.RefreshAggregatedPenalties(ent);
    }

    private void OnMuscleStatusChanged(Entity<ChemicalMuscleStimulationComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _movement.RefreshMovementSpeedModifiers(ent);
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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        RefreshStrengthStatuses<ChemicalNerveStimulationComponent>(_nerveSources,
            now,
            (comp, strength, expires) => (comp.Strength, comp.ExpiresAt) = (strength, expires),
            uid =>
            {
                _movement.RefreshMovementSpeedModifiers(uid);
                _medicalSpeed.RefreshAggregatedPenalties(uid);
            });
        RefreshStrengthStatuses<ChemicalMuscleStimulationComponent>(_muscleSources,
            now,
            (comp, strength, expires) => (comp.Strength, comp.ExpiresAt) = (strength, expires),
            uid =>
            {
                _movement.RefreshMovementSpeedModifiers(uid);
                _medicalSpeed.RefreshAggregatedPenalties(uid);
            });
        RefreshStrengthStatuses<ChemicalCardiacPacingComponent>(_pacingSources,
            now,
            (comp, strength, expires) => (comp.Strength, comp.ExpiresAt) = (strength, expires));
        RefreshStrengthStatuses<ChemicalHyperdensityComponent>(_hyperdensitySources,
            now,
            (comp, strength, expires) => (comp.Protection, comp.ExpiresAt) = (strength, expires));
        RefreshStrengthStatuses<ChemicalNeuroshieldComponent>(_neuroshieldSources,
            now,
            (comp, strength, expires) => (comp.Protection, comp.ExpiresAt) = (strength, expires));
        Expire<ChemicalNeurocryogenicComponent>(now, c => c.ExpiresAt);
        RefreshStrengthStatuses<ChemicalAntiparasiticComponent>(_antiparasiticSources,
            now,
            (comp, strength, expires) => (comp.Strength, comp.ExpiresAt) = (strength, expires));
        Expire<ChemicalFluxingComponent>(now, c => c.ExpiresAt);
        RefreshStrengthStatuses<ChemicalPainSensitivityComponent>(_painSensitivitySources,
            now,
            (comp, strength, expires) => (comp.Multiplier, comp.ExpiresAt) = (strength, expires));
        RefreshStrengthStatuses<ChemicalAddictionTreatmentComponent>(_addictionTreatmentSources,
            now,
            (comp, strength, expires) => (comp.Strength, comp.ExpiresAt) = (strength, expires));
    }

    private void RecordSource(Dictionary<EntityUid, Dictionary<string, TimedStrength>> statuses,
        EntityUid target,
        string source,
        float strength,
        out float strongest,
        out TimeSpan expiresAt)
    {
        if (!statuses.TryGetValue(target, out var sources))
        {
            sources = new Dictionary<string, TimedStrength>();
            statuses.Add(target, sources);
        }

        sources[source] = new TimedStrength(strength, _timing.CurTime + DefaultDuration);
        AggregateSources(sources, out strongest, out expiresAt);
    }

    private void RefreshStrengthStatuses<T>(Dictionary<EntityUid, Dictionary<string, TimedStrength>> statuses,
        TimeSpan now,
        Action<T, float, TimeSpan> update,
        Action<EntityUid>? afterChange = null)
        where T : Component
    {
        var query = EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!statuses.TryGetValue(uid, out var sources))
            {
                RemCompDeferred<T>(uid);
                continue;
            }

            List<string>? expired = null;
            foreach (var (source, entry) in sources)
            {
                if (entry.ExpiresAt > now)
                    continue;

                expired ??= new List<string>();
                expired.Add(source);
            }

            if (expired != null)
            {
                foreach (var source in expired)
                {
                    sources.Remove(source);
                }
            }

            if (sources.Count == 0)
            {
                statuses.Remove(uid);
                RemCompDeferred<T>(uid);
                continue;
            }

            AggregateSources(sources, out var strongest, out var expiresAt);
            update(comp, strongest, expiresAt);
            Dirty(uid, comp);
            afterChange?.Invoke(uid);
        }

        List<EntityUid>? orphaned = null;
        foreach (var uid in statuses.Keys)
        {
            if (Exists(uid) && HasComp<T>(uid))
                continue;

            orphaned ??= new List<EntityUid>();
            orphaned.Add(uid);
        }

        if (orphaned == null)
            return;

        foreach (var uid in orphaned)
        {
            statuses.Remove(uid);
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

    private void Expire<T>(TimeSpan now, Func<T, TimeSpan> expiresAt) where T : Component
    {
        var query = EntityQueryEnumerator<T>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (expiresAt(comp) <= now)
                RemCompDeferred<T>(uid);
        }
    }

    private readonly record struct TimedStrength(float Strength, TimeSpan ExpiresAt);
}
