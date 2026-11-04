using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>Dust and impact audio for a vehicle crate delivered by free fall.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUVehicleDropImpactComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? ImpactAt;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_RMC14/Effects/metal_crash.ogg");
}
