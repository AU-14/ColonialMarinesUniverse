namespace Content.Server.CMU14.ZLevelBuilding;

/// <summary>
/// Makes stone streaming and border mirroring follow this entity as they follow a player (an actor), without a
/// session. Use it for non-player explorers such as a remote eye or a scripted builder.
///
/// Each anchor near a chunk edge generates that edge column synchronously, so keep a few anchors per map.
/// </summary>
[RegisterComponent]
public sealed partial class CMUZStreamAnchorComponent : Component;
