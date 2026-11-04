using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Ranged.Components;

// CMU14 - Start: remote weapon routing used by gunship pilot controls.
/// <summary>
/// Lets an entity operate a remote gun through the normal gun input pipeline.
/// The owning system remains responsible for validating that the operator may
/// use the selected weapon.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RemoteWeaponOperatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Platform;

    [DataField, AutoNetworkedField]
    public EntityUid? SelectedWeapon;
}
// CMU14 - End
