using Content.Shared.CMU14.Expeditions;
using Content.Shared.Maps;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Expeditions;

/// <summary>Palette and atmosphere for a CMU-generated expedition, configured on a profile entity.</summary>
[RegisterComponent]
public sealed partial class CMUExpeditionProfileComponent : Component
{
    [DataField] public CMUExpeditionBiome Biome;
    [DataField] public int Size = CMUExpeditionPlan.DefaultSize;
    [DataField] public Color AmbientLight = Color.FromHex("#BAC8AE");
    [DataField(required: true)] public Dictionary<CMUExpeditionTerrain, ProtoId<ContentTileDefinition>> Tiles = new();
    [DataField(required: true)] public Dictionary<CMUExpeditionProp, List<EntProtoId>> Props = new();
    [DataField(required: true)] public Dictionary<CMUExpeditionDetail, List<EntProtoId>> Details = new();
    [DataField] public Dictionary<CMUExpeditionWaterKind, EntProtoId> Water = new()
    {
        { CMUExpeditionWaterKind.Shallow, "RMCEntityDesertWaterShallow" },
        { CMUExpeditionWaterKind.Deep, "RMCEntityDesertWaterDeep" },
        { CMUExpeditionWaterKind.Edge, "RMCEntityDesertWaterShallowEdge" },
        { CMUExpeditionWaterKind.Corner, "RMCEntityDesertWaterShallowCorner" },
        { CMUExpeditionWaterKind.InnerCorner, "RMCEntityDesertWaterShallowCornerEdge" },
    };
    [DataField] public EntProtoId LandingBeacon = "CMUExpeditionLandingBeacon";
    [DataField] public EntProtoId Wildfire = "CMUExpeditionWildfire";
}
