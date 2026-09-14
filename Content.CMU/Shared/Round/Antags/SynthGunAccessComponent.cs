using Robust.Shared.GameObjects;

namespace Content.Shared.CMU14.Round.Antags;

/// <summary>
/// Grants gun usage to the holder if they are a synth. Added by antag specifiers so
/// synth antags keep firearm access without turning human antags into synths.
/// </summary>
[RegisterComponent]
public sealed partial class SynthGunAccessComponent : Component;
