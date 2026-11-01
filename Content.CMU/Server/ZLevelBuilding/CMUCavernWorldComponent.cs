using Content.Shared.CMU14.ZLevelBuilding;

namespace Content.Server.CMU14.ZLevelBuilding;

/// <summary>One seed for all generated depths belonging to a source map.</summary>
[RegisterComponent]
public sealed partial class CMUCavernWorldComponent : Component
{
    [DataField]
    public int Seed;
}
