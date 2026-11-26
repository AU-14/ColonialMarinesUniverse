namespace Content.Shared.CMU14.Camera;

/// <summary>
/// Goes on both ends of a camera feed. On a security monitor it lets the monitor show feeds through the television's
/// grainy picture with a hint of tape playback; on a camera it marks that camera's feed as one that gets it. The
/// monitor only shows the static while it's watching a camera that has this too, such as worn camera gear.
/// </summary>
[RegisterComponent]
public sealed partial class CMUCameraFeedStaticComponent : Component;
