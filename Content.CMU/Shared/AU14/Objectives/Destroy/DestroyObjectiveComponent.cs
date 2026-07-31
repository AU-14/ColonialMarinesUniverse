using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.Objectives.Destroy;

[RegisterComponent, NetworkedComponent]
public sealed partial class DestroyObjectiveComponent : Component
{
    [DataField("entitytodestroy", required: false)]
    public string EntityToDestroy { get; private set; } = string.Empty;

    [DataField("spawnmarker", required: false)]
    public string SpawnMarker { get; private set; } = string.Empty;

    [DataField("amounttospawn", required: false)]
    public int AmountToSpawn { get; private set; } = 1;

    [DataField("amounttodestroy", required: false)]
    public int AmountToDestroy { get; private set; } = 1;

    public int AmountDestroyed = 0;

    [DataField("useanyentity", required: false)]
    public bool UseAnyEntity { get; private set; } = false;

    public bool EntitiesSpawned = false;


}
