using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Storage.Components;

// CMU14: StorageFill remains supported by CMU prototypes and deferred shipment filling.
// [Obsolete("Use ContainerFillComponent or EntityTableContainerFillComponent instead")]
[RegisterComponent, NetworkedComponent, Access(typeof(SharedStorageSystem))]
public sealed partial class StorageFillComponent : Component
{
    [DataField("contents")] public List<EntitySpawnEntry> Contents = new();
}
