namespace Content.Shared.CMU14.Spawners;

public enum CMUGovforRankSpawnGroup : byte
{
    Enlisted,
    Officer,
}

/// <summary>
/// A shared GOVFOR spawn marker. The server assigns each arrival to the least-used matching marker.
/// </summary>
[RegisterComponent]
public sealed partial class CMUGovforRankSpawnPointComponent : Component
{
    [DataField(required: true)]
    public CMUGovforRankSpawnGroup Group;

    public int AssignedCount;
}
