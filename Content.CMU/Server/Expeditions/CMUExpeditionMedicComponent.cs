using Content.Shared.DoAfter;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

[RegisterComponent]
public sealed partial class CMUExpeditionMedicComponent : Component
{
    [DataField] public float SearchRange = 14;
    [DataField] public float TreatDamage = 20;
    public EntityUid? Patient;
    public CMUExpeditionMedicalPhase Phase;
    public EntityCoordinates? Shelter;
    public EntityUid? Item;
    public DoAfterId? DoAfter;
    public TimeSpan NextTriage;
    public TimeSpan NextAction;
    public TimeSpan Deadline;
    public TimeSpan LastPatientSeen;
    public TimeSpan CoverLostAt;
    public TimeSpan NextCoverCheck;
    public bool Covered;
    public int Doses;
    public int Revivals;
    public int Extractions;
    public int Shocks;
    public int PatientShocks;
    public int PatientDoses;
    public float InjectionVolume;
    public bool ShockCompleted;
    public string Decision = "idle";
    public readonly Dictionary<EntityUid, TimeSpan> RetryAfter = new();
}

public enum CMUExpeditionMedicalPhase : byte { Idle, Approach, Extract, Treat, Revive, Inject }

/// <summary>A live claim and native treatment callback destination, including for friendly players.</summary>
[RegisterComponent]
public sealed partial class CMUExpeditionPatientComponent : Component
{
    public EntityUid Medic;
    public TimeSpan LastWound;
}

/// <summary>Native devices retain their normal effects, charge and interaction rules.</summary>
[RegisterComponent]
public sealed partial class CMUExpeditionMedicalToolComponent : Component;
