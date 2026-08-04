using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Entrenching;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(BarricadeSystem))]
public sealed partial class EntrenchingToolComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan DigDelay = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public TimeSpan FillDelay = TimeSpan.FromSeconds(3);

    [DataField, AutoNetworkedField]
    public int LayersPerDig = 5;

    [DataField, AutoNetworkedField]
    public int TotalLayers;

    [AutoNetworkedField]
    public EntityCoordinates LastDigLocation;

    [DataField, AutoNetworkedField]
    public SoundSpecifier DigSound = new SoundCollectionSpecifier("CMEntrenchingThud", AudioParams.Default.WithVolume(-3));

    [DataField, AutoNetworkedField]
    public SoundSpecifier FillSound = new SoundCollectionSpecifier("CMEntrenchingRustle", AudioParams.Default.WithVolume(-6));

    /// <summary>
    ///     How much dug-up dirt is spent piling up a dirt mound.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MoundCost = 5;

    [DataField, AutoNetworkedField]
    public TimeSpan MoundBuildDelay = TimeSpan.FromSeconds(5);

    [DataField, AutoNetworkedField]
    public EntProtoId MoundPrototype = "AU14DirtMound";
}

[Serializable, NetSerializable]
public enum EntrenchingToolComponentVisualLayers
{
    Base,
    Folded,
    Dirt
}
