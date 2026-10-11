namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentComponent
{
    public EntityUid? UtilityCleanupItem;
    public readonly Queue<EntityUid> UtilityCleanupQueue = new();
    public TimeSpan NextUtilityCleanup;
}
