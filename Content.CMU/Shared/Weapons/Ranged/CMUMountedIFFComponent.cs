using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.Ranged;

/// <summary>Visor-style firing interlock, with its setting stored on the mounted weapon.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUMountedIFFComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    public EntityUid? Operator;
}

/// <summary>Temporarily grants the current operator the weapon's IFF toggle.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUMountedIFFOperatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Weapon;

    [DataField, AutoNetworkedField]
    public EntityUid Mount;

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleAction;
}

public sealed partial class CMUToggleMountedIFFActionEvent : InstantActionEvent;
