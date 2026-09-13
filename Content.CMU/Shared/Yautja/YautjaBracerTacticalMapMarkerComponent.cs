using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Yautja;

[RegisterComponent]
public sealed partial class YautjaBracerTacticalMapMarkerComponent : Component
{
    public bool HadIcon;

    public bool HadYautjaTracking;

    public bool WasYautjaUser;

    public SpriteSpecifier.Rsi? PreviousIcon;

    public SpriteSpecifier.Rsi? PreviousBackground;
}
