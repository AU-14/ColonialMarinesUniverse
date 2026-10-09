using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A vehicle a spray painter can repaint in any colour. The art marks its paint pixels; clients run
/// them through the paint shader on the listed layers, which keeps their shading. Null is the
/// factory olive.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehiclePaintComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color? Color;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Spray painter charges a full respray uses.
    /// </summary>
    [DataField]
    public int ChargeCost = 5;

    /// <summary>
    /// Layer keys painted on the vehicle's sprite and on its overlay entity's sprite.
    /// </summary>
    [DataField]
    public List<string> Layers = new();

    [DataField]
    public List<string> OverlayLayers = new();
}

[Serializable, NetSerializable]
public sealed partial class CMUVehiclePaintDoAfterEvent : DoAfterEvent
{
    [DataField]
    public Color Color;

    private CMUVehiclePaintDoAfterEvent()
    {
    }

    public CMUVehiclePaintDoAfterEvent(Color color)
    {
        Color = color;
    }

    public override DoAfterEvent Clone()
    {
        return this;
    }
}
