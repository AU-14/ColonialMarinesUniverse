using Robust.Shared.Utility;

namespace Content.Shared._CMU14.Yautja;

[RegisterComponent]
public sealed partial class YautjaBracerTacticalMapMarkerComponent : Component
{
    public bool HadIcon;

    public SpriteSpecifier.Rsi? PreviousIcon;

    public SpriteSpecifier.Rsi? PreviousBackground;
}
