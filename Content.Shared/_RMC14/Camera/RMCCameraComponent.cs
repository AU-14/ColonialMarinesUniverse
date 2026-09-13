using Content.Shared._RMC14.Dropship.Weapon;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Camera;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCCameraSystem), typeof(SharedDropshipWeaponSystem))]
public sealed partial class RMCCameraComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Rename = true;

    [DataField, AutoNetworkedField]
    public string? NameOverride;
}
