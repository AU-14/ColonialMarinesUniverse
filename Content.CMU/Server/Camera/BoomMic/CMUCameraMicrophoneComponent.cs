namespace Content.Server.CMU14.Camera.BoomMic;

/// <summary>
/// A camera's built-in microphone. Speech and emotes within <see cref="ListenRange"/> of the camera are repeated by
/// the TVs watching it, the same as a <see cref="CMUBoomMicComponent"/> synced to it, and never twice when both hear.
/// </summary>
[RegisterComponent, Access(typeof(CMUBoomMicSystem))]
public sealed partial class CMUCameraMicrophoneComponent : Component
{
    /// <summary>
    /// How far, in tiles, the camera picks up speech and emotes.
    /// </summary>
    [DataField]
    public float ListenRange = 5f;
}
