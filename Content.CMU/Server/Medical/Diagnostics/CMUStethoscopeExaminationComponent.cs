using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Body;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.DoAfter;

namespace Content.Server.CMU14.Medical.Diagnostics;

/// <summary>
/// Transient server-owned examination identity. Deleting the medic also releases
/// the snapshot; no clinical data or component references are sent to the client.
/// </summary>
[RegisterComponent, Access(typeof(CMUStethoscopeSystem))]
public sealed partial class CMUStethoscopeExaminationComponent : Component
{
    public ulong Attempt;
    public EntityUid Patient;
    public Entity<RMCStethoscopeComponent> Tool;
    public CMUHumanMedicalComponent Medical = default!;
    public BodyComponent Body = default!;
    public TransformComponent PatientTransform = default!;
    public TransformComponent MedicTransform = default!;
    public SkillsComponent Skills = default!;
    public DoAfterComponent? DoAfter;
    public int Skill;
    public bool FromVerb;
}
