using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Leap;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(XenoLeapSystem), typeof(Content.Shared._CMU14.Threats.Mobs.Ape.ApeLeapSystem))]
public sealed partial class LeapIncapacitatedComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan RecoverAt;
}
