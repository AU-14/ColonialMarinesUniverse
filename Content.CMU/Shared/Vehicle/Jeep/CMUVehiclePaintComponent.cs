using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A vehicle a spray painter can repaint in any colour. The art marks its paint pixels; clients run
/// them through the paint shader on the listed layers, which keeps their shading. Null is the
/// factory olive. The wheels have their own paint, sprayed on their own.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehiclePaintComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color? Color;

    [DataField, AutoNetworkedField]
    public Color WheelColor = Robust.Shared.Maths.Color.FromHex("#C8CDD2");

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan WheelDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Spray painter charges a full respray uses.
    /// </summary>
    [DataField]
    public int ChargeCost = 5;

    [DataField]
    public int WheelChargeCost = 1;

    /// <summary>
    /// Layer keys painted on the vehicle's sprite and on its overlay entity's sprite.
    /// </summary>
    [DataField]
    public List<string> Layers = new();

    [DataField]
    public List<string> OverlayLayers = new();

    /// <summary>
    /// Layer keys painted in the wheel colour.
    /// </summary>
    [DataField]
    public List<string> WheelLayers = new();

    [DataField]
    public List<string> WheelOverlayLayers = new();
}

[Serializable, NetSerializable]
public sealed partial class CMUVehiclePaintDoAfterEvent : DoAfterEvent
{
    [DataField]
    public Color Color;

    [DataField]
    public bool Wheels;

    private CMUVehiclePaintDoAfterEvent()
    {
    }

    public CMUVehiclePaintDoAfterEvent(Color color, bool wheels)
    {
        Color = color;
        Wheels = wheels;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}
