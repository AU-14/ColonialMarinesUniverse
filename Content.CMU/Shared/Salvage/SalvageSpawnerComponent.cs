using Robust.Shared.Prototypes;

namespace Content.Shared.AU14.Salvage;

[RegisterComponent]
public sealed partial class SalvageSpawnerComponent : Component
{
    [DataField]
    public List<EntProtoId> Loot = new();

    [DataField]
    public float DoAfterTime = 10f;
}
