using Content.Shared.Radio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.ShotAlert;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUShotAlertComponent : Component
{
    [DataField]
    public float Range = 25f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextAlert;

    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "radioCMB";
}
