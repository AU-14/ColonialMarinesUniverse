using Robust.Shared.Audio;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Screwdrivering mutes the machine's ambient hum by removing its
/// AmbientSoundComponent. The removed settings are kept here so a second
/// use of the screwdriver restores the original sound.
/// </summary>
[RegisterComponent]
public sealed partial class CMUAmbientMuteComponent : Component
{
    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float Volume = -10f;

    [DataField]
    public float Range = 2f;

    // Restored as-is: AmbientOnPowered only re-enables on a power transition, so a
    // machine unmuted mid-run would otherwise stay silent until the grid blips
    [DataField]
    public bool Enabled;
}
