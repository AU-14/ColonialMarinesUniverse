using Content.Shared.Actions;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Dropship.AttachmentPoint;

/// <summary>On a helmet only while the advanced crew visor is lowered.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUAdvancedGunshipCrewVisorComponent : Component;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUGunshipCrewIFFComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Helmet;

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleAction;
}

public sealed partial class CMUToggleGunshipCrewIFFActionEvent : InstantActionEvent;
