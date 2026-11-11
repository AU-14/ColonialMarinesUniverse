namespace Content.Shared.Storage;

public sealed partial class StorageComponent
{
    /// <summary>Nested synchronous removals compact their remaining contents only when the batch completes.</summary>
    public int CMURepackDepth;
    public bool CMURepackPending;
}
