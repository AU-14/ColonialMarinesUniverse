using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._CMU14.Wendigo;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class WendigoHeadbiteAudioComponent : Component
{
    /// <summary>
    ///     Sound played for nearby players on every successful headbite.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? CloseSound;

    /// <summary>
    ///     Sound played map-wide for distant players, gated by <see cref="GlobalCooldown"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier? GlobalSound;

    /// <summary>
    ///     Minimum time between global sound plays.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan GlobalCooldown = TimeSpan.FromSeconds(60);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan? LastGlobalPlayed;

    /// <summary>
    ///     Volume in dB for the global screech heard by outdoor players.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float GlobalVolume = -3f;

    /// <summary>
    ///     Volume in dB for the global screech heard by indoor (roofed) players.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float GlobalIndoorVolume = -12f;
}
