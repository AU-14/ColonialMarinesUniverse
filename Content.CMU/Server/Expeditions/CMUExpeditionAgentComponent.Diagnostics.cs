using Content.Shared.DoAfter;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public CMUExpeditionDiagnosticSnapshot? LastLivingDiagnostics;
    public TimeSpan? DiagnosticsStoppedAt;
    public string[] FrozenDecisionHistory = Array.Empty<string>();
}

// Only the volatile diagnostics erased by stopping are sampled. Inventory and
// controller state after death must not be presented as the last combat decision.
public readonly record struct CMUExpeditionDiagnosticSnapshot(TimeSpan At, CMUExpeditionAgentState State,
    CMUSquadDuty Duty, string Doctrine, string Phase, string Owner, string Reason, string FireCheck,
    string WeaponDecision, string? WeaponName, int Ammo, float Damage, float Stress, int FireLanes,
    string CornerDecision, string HeardKind, TimeSpan HeardAt, double NativeFireWait, double AimWait,
    string SquadDecision, string TrafficDecision, string DoorDecision, string VaultDecision,
    string FiringMovement, bool SustainedFire, int Volley,
    DoAfterStatus? TreatmentStatus, bool PreparingTreatment, double TreatmentWait,
    CMUExpeditionMedicalPhase? MedicalPhase, string MedicalDecision, int MedicalDoses, int MedicalShocks);
