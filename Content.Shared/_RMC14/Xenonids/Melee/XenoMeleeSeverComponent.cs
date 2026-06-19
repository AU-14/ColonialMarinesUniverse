using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Melee;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoMeleeSeverComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Chance = 0.4f;
}
