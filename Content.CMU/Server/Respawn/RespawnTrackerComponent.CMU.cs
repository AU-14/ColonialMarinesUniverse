using Robust.Shared.Network;

namespace Content.Server.GameTicking.Rules.Components;

public sealed partial class RespawnTrackerComponent
{
    /// <summary>The death that scheduled each respawn; a replacement body must never be deleted by its timer.</summary>
    public readonly Dictionary<NetUserId, (EntityUid Mind, EntityUid Body)> CMUQueuedDeaths = new();
}
