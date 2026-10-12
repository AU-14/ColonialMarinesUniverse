using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A spray painter for vehicles: any colour picked in its RGB window, sprayed on in one go for some
/// of its charges. Switched to its rim designs, it sprays the design picked on a vehicle's wheels, in
/// the base, accent, lip and cap colours picked for it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleSprayPainterComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#767E50");

    [DataField, AutoNetworkedField]
    public CMUVehicleSprayPainterMode Mode = CMUVehicleSprayPainterMode.Paint;

    [DataField, AutoNetworkedField]
    public ProtoId<CMURimDesignPrototype> Design = "CMURimFiveSpoke";

    /// <summary>
    /// The design's base, accent, lip and cap colours.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<Color> DesignColors = new();

    [DataField]
    public SoundSpecifier SpraySound = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");

    /// <summary>
    /// How long a fresh respray stays wet.
    /// </summary>
    [DataField]
    public TimeSpan FreshPaintDuration = TimeSpan.FromMinutes(5);
}

[Serializable, NetSerializable]
public enum CMUVehicleSprayPainterUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CMUVehicleSprayPainterMode : byte
{
    Paint,
    Design,
}

[Serializable, NetSerializable]
public sealed class CMUVehicleSprayPainterColorMessage(Color color) : BoundUserInterfaceMessage
{
    public readonly Color Color = color;
}

[Serializable, NetSerializable]
public sealed class CMUVehicleSprayPainterModeMessage(CMUVehicleSprayPainterMode mode) : BoundUserInterfaceMessage
{
    public readonly CMUVehicleSprayPainterMode Mode = mode;
}

[Serializable, NetSerializable]
public sealed class CMUVehicleSprayPainterDesignMessage(ProtoId<CMURimDesignPrototype> design, List<Color> colors)
    : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMURimDesignPrototype> Design = design;
    public readonly List<Color> Colors = colors;
}
