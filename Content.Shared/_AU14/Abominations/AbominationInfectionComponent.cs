using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._AU14.Abominations;

/// <summary>
/// Applied to a humanoid hit by an abomination. Symptoms ramp progressively
/// from coughs and slight drunkenness up to constant seizures + vomiting as
/// the infection runs its course. Any death while infected polymorphs the
/// victim into an AU14AbominationMimic and seeds flesh kudzu at the corpse.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class AbominationInfectionComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan InfectedAt;

    /// <summary>How long until the infection reaches its peak (full seizures).</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CrescendoAfter = TimeSpan.FromMinutes(8);

    /// <summary>Has the crescendo phase been entered? (set when severity = 1).</summary>
    [DataField, AutoNetworkedField]
    public bool HasCrescendoed;

    /// <summary>How often the main symptom tick runs.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(6);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextTickAt;

    /// <summary>Damage applied each symptom tick. Scaled by severity (0..1).</summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier TickDamage = new();

    /// <summary>Drunk duration applied each tick. Scaled by severity.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DrunkPerTick = TimeSpan.FromSeconds(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextCoughAt;

    /// <summary>Cough interval at the very beginning of the infection.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CoughIntervalEarly = TimeSpan.FromSeconds(20);

    /// <summary>Cough interval at peak severity.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CoughIntervalLate = TimeSpan.FromSeconds(5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextJitterAt;

    /// <summary>Jitter interval at severity 0 — early infection.
    /// Aggressive cadence so the seizures land within seconds of infection.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan JitterIntervalEarly = TimeSpan.FromSeconds(8);

    /// <summary>Jitter interval at peak severity — basically constant.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan JitterIntervalLate = TimeSpan.FromSeconds(0.5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextVomitAt;

    /// <summary>How often the late-phase vomiting fires once severity is past <see cref="VomitSeverityThreshold"/>.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan VomitInterval = TimeSpan.FromSeconds(10);

    /// <summary>Severity threshold below which vomiting is suppressed entirely.</summary>
    [DataField, AutoNetworkedField]
    public float VomitSeverityThreshold = 0.6f;

    /// <summary>True once symptoms have begun (severity > 0). Used to gate the death-into-mimic trigger.</summary>
    [DataField, AutoNetworkedField]
    public bool HasShownSymptoms;
}
