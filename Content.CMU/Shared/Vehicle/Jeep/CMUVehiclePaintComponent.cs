using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A vehicle a spray painter can repaint in any colour. The art marks its paint pixels; clients run
/// them through the paint shader on the listed layers, which keeps their shading. Null is the
/// factory colour. The body outside, the tub inside, the seats and the wheels each take their own
/// paint, sprayed where the painter is used on them.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehiclePaintComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color? Color;

    [DataField, AutoNetworkedField]
    public Color? InteriorColor;

    [DataField, AutoNetworkedField]
    public Color? SeatColor;

    [DataField, AutoNetworkedField]
    public Color WheelColor = Robust.Shared.Maths.Color.FromHex("#C8CDD2");

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan InteriorDelay = TimeSpan.FromSeconds(6);

    [DataField]
    public TimeSpan SeatDelay = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan WheelDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Spray painter charges a full respray uses.
    /// </summary>
    [DataField]
    public int ChargeCost = 5;

    [DataField]
    public int InteriorChargeCost = 3;

    [DataField]
    public int SeatChargeCost = 1;

    [DataField]
    public int WheelChargeCost = 1;

    /// <summary>
    /// The inside of the tub in body voxels (f forward, r right, z up), told from the outside where
    /// the painter is used on the body.
    /// </summary>
    [DataField]
    public Vector3 InteriorMin = new(-31, -19, 6);

    [DataField]
    public Vector3 InteriorMax = new(5, 18, 40);

    /// <summary>
    /// Layer keys painted on the vehicle's sprite and on its overlay entity's sprite, the tub inside
    /// and the seats included.
    /// </summary>
    [DataField]
    public List<string> Layers = new();

    [DataField]
    public List<string> OverlayLayers = new();

    /// <summary>
    /// Layer keys only painted in the body colour: their own art uses the alphas the tub inside and
    /// seats are marked with.
    /// </summary>
    [DataField]
    public List<string> PlainLayers = new();

    [DataField]
    public List<string> PlainOverlayLayers = new();

    /// <summary>
    /// Layer keys painted in the wheel colour.
    /// </summary>
    [DataField]
    public List<string> WheelLayers = new();

    [DataField]
    public List<string> WheelOverlayLayers = new();
}

[Serializable, NetSerializable]
public enum CMUVehiclePaintZone : byte
{
    Body,
    Interior,
    Seats,
    Wheels,
}

[Serializable, NetSerializable]
public sealed partial class CMUVehiclePaintDoAfterEvent : DoAfterEvent
{
    [DataField]
    public Color Color;

    [DataField]
    public CMUVehiclePaintZone Zone;

    private CMUVehiclePaintDoAfterEvent()
    {
    }

    public CMUVehiclePaintDoAfterEvent(Color color, CMUVehiclePaintZone zone)
    {
        Color = color;
        Zone = zone;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}
