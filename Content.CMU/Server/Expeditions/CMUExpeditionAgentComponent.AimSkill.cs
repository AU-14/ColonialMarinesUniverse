using System.Numerics;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    [DataField] public float AimSkill = 1;
    public EntityUid? AimErrorTarget;
    public Vector2 AimError;
    public TimeSpan NextAimCorrection;
}
