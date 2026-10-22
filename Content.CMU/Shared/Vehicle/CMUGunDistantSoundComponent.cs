using Robust.Shared.Audio;

namespace Content.Shared.CMU14.Vehicle;

/// <summary>
/// A gun heard from afar: each shot also plays <see cref="Sound"/> to everyone between
/// <see cref="MinRange"/> and <see cref="MaxRange"/> tiles away, full at the near edge and fading out
/// at the far one. Closer in, only the gun's own shot is heard; the two overlap where its own fades.
/// </summary>
[RegisterComponent]
public sealed partial class CMUGunDistantSoundComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    [DataField]
    public float MinRange = 10f;

    [DataField]
    public float MaxRange = 45f;

    /// <summary>
    /// The shortest time between two distant shots, each a sound sent to everyone in the ring. None by
    /// default, so a burst far off keeps the gun's own rate.
    /// </summary>
    [DataField]
    public TimeSpan MinInterval = TimeSpan.Zero;

    /// <summary>
    /// When the next distant shot may play.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextShot;
}
