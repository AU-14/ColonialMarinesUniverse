using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Clothing.NightVision;

/// <summary>
///     Night vision that can't handle bright light. While it's giving someone night vision, a lit flare close by
///     whites it out and blinds the wearer for a few seconds, after which they see normally again.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
[Access(typeof(CMUNightVisionFlareBlindSystem))]
public sealed partial class CMUNightVisionFlareBlindComponent : Component
{
    /// <summary>How close, in tiles, a lit flare has to be, with a clear line to the wearer.</summary>
    [DataField]
    public float Range = 4f;

    /// <summary>How long the wearer is blinded.</summary>
    [DataField]
    public TimeSpan BlindDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     How long after the wearer can see again before another flare can blind them. Without it, standing next to
    ///     one flare would keep them blind forever.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    /// <summary>When the goggles can next blind their wearer.</summary>
    [DataField, AutoPausedField]
    public TimeSpan NextBlind;
}
