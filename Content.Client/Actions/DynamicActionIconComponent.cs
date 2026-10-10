using Robust.Client.Graphics;

namespace Content.Client.Actions;

/// <summary>
/// Preserves a toggled layer's original image while an action supplies a dynamic override.
/// </summary>
[RegisterComponent]
public sealed partial class DynamicActionIconComponent : Component
{
    public bool CreatedLayer;
    public bool OverrideApplied;
    public Texture? OriginalTexture;
    public RSI? OriginalRsi;
    public RSI.StateId OriginalState;
}
