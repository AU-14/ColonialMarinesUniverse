using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// Optical Zoom (playtest 3): the yautja bio-mask's zoom on the Joe itself. The view widens to
/// <see cref="ZoomLevel"/> and is pushed toward the facing direction at toggle time, and stays there
/// until toggled off; moving and turning do not end it. Owner-only, like the rest of the antag.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EWJoeOpticalZoomComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>Same as the bio-mask devices (masks.yml: zoomLevel 1.6).</summary>
    [DataField, AutoNetworkedField]
    public float ZoomLevel = 1.6f;

    /// <summary>Same as the bio-mask devices (masks.yml: zoomOffset 14).</summary>
    [DataField, AutoNetworkedField]
    public float ZoomOffset = 14f;

    [DataField, AutoNetworkedField]
    public bool Zoomed;

    /// <summary>The eye offset chosen at toggle time.</summary>
    [DataField, AutoNetworkedField]
    public Vector2 Offset;

    [DataField, AutoNetworkedField]
    public EntityUid? Action;
}
