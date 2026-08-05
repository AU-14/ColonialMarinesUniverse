using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Shared.Actions.Components;

/// <summary>
/// Keeps RMC and CMU action prototypes compatible with the sprite-based upstream action icon renderer.
/// </summary>
public sealed partial class ActionComponent
{
    [DataField, AutoNetworkedField]
    public SpriteSpecifier? Icon;

    [DataField, AutoNetworkedField]
    public SpriteSpecifier? IconOn;

    [DataField]
    public SpriteSpecifier? BackgroundOn;

    [DataField, AutoNetworkedField]
    public Color IconColor = Color.White;

    [DataField, AutoNetworkedField]
    public Color OriginalIconColor;

    [DataField]
    public Color DisabledIconColor = Color.DimGray;
}
