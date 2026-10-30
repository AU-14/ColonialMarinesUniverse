using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// A wheel set's paint and rim design. It belongs to the wheels, so a set taken off keeps its look on
/// the next vehicle it goes on, and a spare keeps its own.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleWheelPaintComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.FromHex("#C8CDD2");

    /// <summary>
    /// The rim design on the wheels, or null for the plain rims they came with.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CMURimDesignPrototype>? RimDesign;

    /// <summary>
    /// The rim design's accent, lip and cap colours, or none for the design's own.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<Color> RimColors = new();
}
