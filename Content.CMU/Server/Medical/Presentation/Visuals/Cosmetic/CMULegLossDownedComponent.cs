namespace Content.Server.CMU14.Medical.Presentation.Visuals.Cosmetic;

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

// this mob lost the legs to stand on, so getting them back should stand it up again.
// part-added also fires for every limb at spawn, and that must not stand up a mob laid out on purpose
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(CMUSeveranceCosmeticSystem))]
public sealed partial class CMULegLossDownedComponent : Component
{
    // a failed stand (buckle, rest, crit) backs off instead of retrying every tick
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextStandRetry;
}
