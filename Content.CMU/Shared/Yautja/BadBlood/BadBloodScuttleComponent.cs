using Content.Shared.Explosion;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// a bracer counting down to its scuttle. added by BadBloodScuttleSystem.TryStartScuttle, never by a prototype,
// so honourable bracers never carry it. networked so the client predicts the pickup/unequip refusals and
// CanArmSelfDestruct sees it; only the countdown end goes over, the beeps and light stay server-side
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BadBloodScuttleComponent : Component
{
    [DataField]
    public TimeSpan Fuse = TimeSpan.FromSeconds(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan DetonateAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextWarning;

    [DataField]
    public TimeSpan WarningEvery = TimeSpan.FromSeconds(1);

    // the beeps and the light speed up for the last stretch
    [DataField]
    public TimeSpan FinalCountdown = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan FinalWarningEvery = TimeSpan.FromSeconds(0.5);

    // the last second count shown in a popup, so each second pops up once
    [DataField]
    public int LastWarnedSeconds = int.MaxValue;

    [DataField]
    public bool LightOn;

    [DataField]
    public Color LightColor = Color.Red;

    [DataField]
    public float LightRadius = 2.5f;

    [DataField]
    public float LightEnergy = 3f;

    // balance sheet: max 12 / total 120 / slope 6, small enough to gib no one
    [DataField]
    public ProtoId<ExplosionPrototype> Explosion = "RMC";

    [DataField]
    public float TotalIntensity = 120;

    [DataField]
    public float IntensitySlope = 6;

    [DataField]
    public float MaxIntensity = 12;
}
