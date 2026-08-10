using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;

[Serializable, NetSerializable]
public enum OvermindVisuals : byte
{
    VisualState,
}

[Serializable, NetSerializable]
public enum OvermindVisualState : byte
{
    Incorporeal,
    Appearing,
    Disappearing,
    Manifested,
    ManifestedStrengthened,
    Dying,
}

/// <summary>
/// Sprite layer keys for the Overmind, matching the `map:` entries in
/// overmind's entity prototype and overmind.rsi's states.
/// </summary>
public enum OvermindVisualLayers : byte
{
    /// <summary>Main body sprite (overmind_eye / overmind_manifested).</summary>
    Base,

    /// <summary>One-shot appear/disappear transition flick.</summary>
    Transition,

    /// <summary>Pulsing eye glow overlay, visible only while incorporeal.</summary>
    EyeGlow,

    /// <summary>Strengthened tint/aura overlay, visible only when manifested + strengthened.</summary>
    Strengthen,
}