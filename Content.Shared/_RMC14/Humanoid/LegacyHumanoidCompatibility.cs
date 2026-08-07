using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Humanoid.Prototypes
{
    public sealed partial class SpeciesPrototype
    {
        [DataField("sprites")]
        public ProtoId<HumanoidSpeciesBaseSpritesPrototype>? SpriteSet { get; private set; }

        [DataField("markingLimits")]
        public ProtoId<MarkingPointsPrototype>? MarkingPoints { get; private set; }
    }
}

// ReSharper disable once CheckNamespace
namespace Content.Shared.Humanoid
{
    public sealed partial class HumanoidCharacterAppearance
    {
        public ProtoId<MarkingPrototype> HairStyleId =>
            GetLegacyMarking(HumanoidVisualLayers.Hair)?.MarkingId ?? HairStyles.DefaultHairStyle;

        public Color HairColor => GetLegacyColor(HumanoidVisualLayers.Hair);

        public ProtoId<MarkingPrototype> FacialHairStyleId =>
            GetLegacyMarking(HumanoidVisualLayers.FacialHair)?.MarkingId ?? HairStyles.DefaultFacialHairStyle;

        public Color FacialHairColor => GetLegacyColor(HumanoidVisualLayers.FacialHair);

        private Marking? GetLegacyMarking(HumanoidVisualLayers layer)
        {
            foreach (var organMarkings in Markings.Values)
            {
                if (organMarkings.TryGetValue(layer, out var markings) && markings.Count > 0)
                    return markings[0];
            }

            return null;
        }

        private Color GetLegacyColor(HumanoidVisualLayers layer)
        {
            var marking = GetLegacyMarking(layer);
            return marking is { } value && value.MarkingColors.Count > 0
                ? value.MarkingColors[0]
                : Color.Black;
        }
    }
}
