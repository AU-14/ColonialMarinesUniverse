using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Ordnance;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCMineCasingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PlacementDelay = 3f;

    [DataField, AutoNetworkedField]
    public bool Planted;

    [DataField, AutoNetworkedField]
    public float TriggerRange = 1.5f;
}

[Serializable, NetSerializable]
public sealed partial class RMCMinePlantDoAfterEvent : SimpleDoAfterEvent;
