using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Weapons;

/// <summary>
/// Strikes lightning from the sky wherever the holder clicks. Debug weapon support.
/// </summary>
[RegisterComponent]
public sealed partial class DebugLightningComponent : Component
{
    /// <summary>
    /// Beam prototype spawned for the strike. Keep it harmless, this is an attention getter.
    /// </summary>
    [DataField]
    public EntProtoId LightningPrototype = "CMUDebugLightningBolt";

    /// <summary>
    /// How far above the click point the bolt's top anchor hangs, in tiles.
    /// </summary>
    [DataField]
    public float SkyOffset = 6f;

    /// <summary>
    /// Map-wide thunder roll played on strike.
    /// </summary>
    [DataField]
    public SoundSpecifier ThunderSound = new SoundCollectionSpecifier("RMCThunder");

    /// <summary>
    /// Radius around the strike where players get a camera shake.
    /// </summary>
    [DataField]
    public float ScreenShakeRange = 8f;

    /// <summary>
    /// Camera shake shakes and strength for players in range. Purely visual.
    /// </summary>
    [DataField]
    public int ScreenShakeShakes = 3;

    [DataField]
    public int ScreenShakeStrength = 2;
}
