using System.Numerics;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>Solid assemblies for the source vehicle's independently visible RSI layers.</summary>
    [DataField]
    public List<CMU3DVehicleLayer> VehicleLayers = [];

    /// <summary>Installed turret items represented through their separate VehicleTurretVisual entity.</summary>
    [DataField]
    public string[] VehicleTurretPrototypes = [];

    [DataField]
    public Vector2 VehicleSpriteOffset;

    [DataField]
    public Vector2 VehicleSpriteScale = Vector2.One;
}

[DataDefinition]
public sealed partial class CMU3DVehicleLayer
{
    [DataField(required: true)]
    public string Rsi = string.Empty;

    [DataField(required: true)]
    public string State = string.Empty;

    /// <summary>Static solid shape for this state; wheel artwork animation does not alter the wheel volume.</summary>
    [DataField(required: true)]
    public List<CMU3DModelPart> Parts = [];
}
