using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Content.Shared._RMC14.Xenonids.Fruit.Events;

namespace Content.Shared._RMC14.Xenonids.Fruit.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
[Access(typeof(SharedXenoFruitSystem))]
public sealed partial class XenoFruitComponent : Component
{
    [DataField]
    public XenoFruitState State = XenoFruitState.Growing;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan? GrowAt;

    [DataField]
    public TimeSpan GrowTime = TimeSpan.FromSeconds(15);

    [DataField]
    public string ItemState = "fruit_lesser_item";

    [DataField]
    public string GrowingState = "fruit_lesser_immature";

    [DataField]
    public string GrownState = "fruit_lesser";

    [DataField]
    public string EatenState = "fruit_lesser_spent";

    [DataField]
    public SoundSpecifier HarvestSound = new SoundCollectionSpecifier("XenoResinBreak")
    {
        Params = AudioParams.Default.WithVolume(-10f)
    };

    [DataField]
    public EntityUid? Hive;

    // entity who planted the given fruit
    [DataField]
    public EntityUid? Planter;

    // Fruit harvest do-after delay
    [DataField]
    public TimeSpan HarvestDelay = TimeSpan.FromSeconds(2);

    // Fruit consumption do-after delay
    [DataField]
    public TimeSpan ConsumeDelay = TimeSpan.FromSeconds(2);

    // Can this fruit be consumed at full health?
    [DataField]
    public bool CanConsumeAtFull = true;

    // Popup to display upon consumption
    [DataField]
    public LocId Popup = new LocId("rmc-xeno-fruit-effect-lesser");

    // Color for the gardener overlay
    [DataField]
    public Color? Color;

    // Color for the aura overlay
    [DataField]
    public Color OutlineColor;

    [DataField]
    public float SpentDespawnTime = 1.0f;
}

[Serializable, NetSerializable]
public enum XenoFruitState
{
    Item,
    Growing,
    Grown,
    Eaten
}

[Serializable, NetSerializable]
public enum XenoFruitLayers
{
    Base
}
