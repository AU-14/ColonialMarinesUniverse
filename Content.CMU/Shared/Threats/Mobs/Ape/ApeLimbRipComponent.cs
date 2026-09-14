using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Ape;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(ApeLimbRipSystem))]
public sealed partial class ApeLimbRipComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);

    [DataField]
    public LocId InvalidTargetPopup = "cmu-ape-limb-rip-invalid";

    [DataField]
    public LocId FinishedPopup = "cmu-ape-limb-rip-finish";

    [DataField, AutoNetworkedField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/CMU14/Ape/aperoarshort.ogg");
}
