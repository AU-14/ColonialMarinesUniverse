using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A spray painter for vehicles: any colour picked in its RGB window, sprayed on in one go for some
/// of its charges.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleSprayPainterComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#767E50");

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
public sealed class CMUVehicleSprayPainterColorMessage(Color color) : BoundUserInterfaceMessage
{
    public readonly Color Color = color;
}
