using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Light;

/// <summary>
/// A light that the wearer can toggle while it is stored in the helmet on their head.
/// </summary>
[RegisterComponent]
public sealed partial class CMUHelmetMountedLightComponent : Component
{
    [DataField]
    public EntProtoId Action = "CMUActionToggleHelmetLight";

    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>
    /// Whoever currently has the toggle action, while the light is in a worn helmet.
    /// </summary>
    [ViewVariables]
    public EntityUid? Wearer;
}

public sealed partial class CMUToggleHelmetLightActionEvent : InstantActionEvent;
