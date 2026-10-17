using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Dropship.Weapon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedDropshipWeaponSystem))]
public sealed partial class DropshipMixedBeltComponent : Component
{
    public static readonly string[] Slots = ["belt1", "belt2", "belt3"];

    [DataField, AutoNetworkedField]
    public int NextSlot;
}
