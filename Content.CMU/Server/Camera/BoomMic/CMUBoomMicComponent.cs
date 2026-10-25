using Content.Shared.Whitelist;

namespace Content.Server.CMU14.Camera.BoomMic;

/// <summary>
/// A handheld microphone that is synced to a broadcasting camera by clicking the camera with it. While someone
/// holds it within <see cref="CameraRange"/> of that camera, speech and emotes within <see cref="ListenRange"/> of
/// the mic are repeated by every TV watching the camera.
/// </summary>
[RegisterComponent, Access(typeof(CMUBoomMicSystem))]
public sealed partial class CMUBoomMicComponent : Component
{
    /// <summary>
    /// Which entities the mic can be synced to.
    /// </summary>
    [DataField(required: true)]
    public EntityWhitelist CameraWhitelist = new();

    /// <summary>
    /// How far, in tiles, the mic can be from its camera and still broadcast.
    /// </summary>
    [DataField]
    public float CameraRange = 20f;

    /// <summary>
    /// How far, in tiles, the mic picks up speech and emotes.
    /// </summary>
    [DataField]
    public float ListenRange = 10f;

    [ViewVariables]
    public EntityUid? Camera;
}
