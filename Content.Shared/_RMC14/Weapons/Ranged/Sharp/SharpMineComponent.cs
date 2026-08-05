using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Weapons.Ranged.Sharp;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SharpMineComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public SharpProjectileKind Kind;

    [DataField, AutoNetworkedField]
    public int Level = 1;

    [DataField, AutoNetworkedField]
    public int MaxLevel = 4;

    [DataField, AutoNetworkedField]
    public TimeSpan ArmDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    [DataField, AutoNetworkedField]
    public TimeSpan UpgradeEvery = TimeSpan.FromSeconds(30);

    [DataField, AutoNetworkedField]
    public float TriggerRadius = 1.5f;

    [DataField, AutoNetworkedField]
    public List<EntProtoId> Effects = new();

    [DataField, AutoNetworkedField]
    public string InactiveState = "sharp_explosive_mine";

    [DataField, AutoNetworkedField]
    public string ActiveState = "sharp_explosive_mine_active";

    [DataField, AutoNetworkedField]
    public string DisarmedState = "sharp_mine_disarmed";

    [DataField, AutoNetworkedField]
    public bool Armed;

    [DataField, AutoNetworkedField]
    public bool Disarmed;

    [DataField, AutoNetworkedField]
    public TimeSpan ArmAt;

    [DataField, AutoNetworkedField]
    public TimeSpan DespawnAt;

    [DataField, AutoNetworkedField]
    public TimeSpan NextUpgradeAt;
}

[Serializable, NetSerializable]
public enum SharpMineVisuals : byte
{
    Layer,
    State,
}

[Serializable, NetSerializable]
public enum SharpMineState : byte
{
    Inactive,
    Active1,
    Active2,
    Active3,
    Active4,
    Disarmed,
}
