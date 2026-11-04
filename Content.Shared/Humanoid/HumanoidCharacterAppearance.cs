using System.Numerics;
using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using static Content.Shared.Preferences.HumanoidCharacterProfile;

namespace Content.Shared.Humanoid;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class HumanoidCharacterAppearance : IEquatable<HumanoidCharacterAppearance>
{
    [DataField]
    public Color EyeColor { get; set; } = Color.Black;

    [DataField]
    public Color SkinColor { get; set; } = Color.FromHsv(new Vector4(0.07f, 0.2f, 1f, 1f));

    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> Markings { get; set; } = new();

    /// <summary>
    /// UCMJ/SOP-compliant hairstyle used when a job requires regulation appearance.
    /// </summary>
    [DataField]
    public string RegulationHairStyleId { get; set; } = HairStyles.DefaultHairStyle;

    [DataField]
    public Color RegulationHairColor { get; set; } = Color.Black;

    /// <summary>
    /// UCMJ/SOP-compliant facial hairstyle used when a job requires regulation appearance.
    /// </summary>
    [DataField]
    public string RegulationFacialHairStyleId { get; set; } = HairStyles.DefaultFacialHairStyle;

    [DataField]
    public Color RegulationFacialHairColor { get; set; } = Color.Black;

    public HumanoidCharacterAppearance(
        Color eyeColor,
        Color skinColor,
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings)
        : this(
            eyeColor,
            skinColor,
            markings,
            HairStyles.DefaultHairStyle,
            Color.Black,
            HairStyles.DefaultFacialHairStyle,
            Color.Black)
    {
    }

    public HumanoidCharacterAppearance(
        Color eyeColor,
        Color skinColor,
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings,
        string regulationHairStyleId,
        Color regulationHairColor,
        string regulationFacialHairStyleId,
        Color regulationFacialHairColor)
    {
        EyeColor = ClampColor(eyeColor);
        SkinColor = ClampColor(skinColor);
        Markings = markings;
        RegulationHairStyleId = regulationHairStyleId;
        RegulationHairColor = ClampColor(regulationHairColor);
        RegulationFacialHairStyleId = regulationFacialHairStyleId;
        RegulationFacialHairColor = ClampColor(regulationFacialHairColor);
    }

    public HumanoidCharacterAppearance(HumanoidCharacterAppearance other) :
        this(
            other.EyeColor,
            other.SkinColor,
            DeepCloneMarkings(other.Markings),
            other.RegulationHairStyleId,
            other.RegulationHairColor,
            other.RegulationFacialHairStyleId,
            other.RegulationFacialHairColor)
    {
    }

    private static Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> DeepCloneMarkings(
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings)
    {
        var clone = new Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>(markings.Count);
        foreach (var (organ, layers) in markings)
        {
            var layerClone = new Dictionary<HumanoidVisualLayers, List<Marking>>(layers.Count);
            foreach (var (layer, layerMarkings) in layers)
            {
                var markingClone = new List<Marking>(layerMarkings.Count);
                foreach (var marking in layerMarkings)
                {
                    markingClone.Add(new Marking(marking.MarkingId, marking.MarkingColors)
                    {
                        Forced = marking.Forced,
                    });
                }

                layerClone.Add(layer, markingClone);
            }

            clone.Add(organ, layerClone);
        }

        return clone;
    }

    public HumanoidCharacterAppearance WithEyeColor(Color newColor)
    {
        return new(newColor, SkinColor, Markings, RegulationHairStyleId, RegulationHairColor,
            RegulationFacialHairStyleId, RegulationFacialHairColor);
    }

    public HumanoidCharacterAppearance WithSkinColor(Color newColor)
    {
        return new(EyeColor, newColor, Markings, RegulationHairStyleId, RegulationHairColor,
            RegulationFacialHairStyleId, RegulationFacialHairColor);
    }

    public HumanoidCharacterAppearance WithMarkings(Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> newMarkings)
    {
        return new(EyeColor, SkinColor, newMarkings, RegulationHairStyleId, RegulationHairColor,
            RegulationFacialHairStyleId, RegulationFacialHairColor);
    }

    public HumanoidCharacterAppearance WithRegulationHairStyleName(string newName)
    {
        return new(EyeColor, SkinColor, Markings, newName, RegulationHairColor,
            RegulationFacialHairStyleId, RegulationFacialHairColor);
    }

    public HumanoidCharacterAppearance WithRegulationHairColor(Color newColor)
    {
        return new(EyeColor, SkinColor, Markings, RegulationHairStyleId, newColor,
            RegulationFacialHairStyleId, RegulationFacialHairColor);
    }

    public HumanoidCharacterAppearance WithRegulationFacialHairStyleName(string newName)
    {
        return new(EyeColor, SkinColor, Markings, RegulationHairStyleId, RegulationHairColor,
            newName, RegulationFacialHairColor);
    }

    public HumanoidCharacterAppearance WithRegulationFacialHairColor(Color newColor)
    {
        return new(EyeColor, SkinColor, Markings, RegulationHairStyleId, RegulationHairColor,
            RegulationFacialHairStyleId, newColor);
    }

    public static HumanoidCharacterAppearance DefaultWithSpecies(ProtoId<SpeciesPrototype> species, Sex sex)
    {
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var speciesPrototype = protoMan.Index(species);
        var skinColoration = protoMan.Index(speciesPrototype.SkinColoration).Strategy;
        var skinColor = skinColoration.InputType switch
        {
            SkinColorationStrategyInput.Unary => skinColoration.FromUnary(speciesPrototype.DefaultHumanSkinTone),
            SkinColorationStrategyInput.Color => skinColoration.ClosestSkinColor(speciesPrototype.DefaultSkinTone),
            _ => skinColoration.ClosestSkinColor(speciesPrototype.DefaultSkinTone),
        };

        var appearance = new HumanoidCharacterAppearance(Color.Black, skinColor, new());
        return EnsureValid(appearance, species, sex);
    }

    private static readonly IReadOnlyList<Color> RealisticEyeColors =
    [
        Color.Brown,
        Color.Gray,
        Color.Azure,
        Color.SteelBlue,
        Color.Black
    ];

    /// <summary>
    /// Picks a random eye color.
    /// </summary>
    public static Color RandomEyes()
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        return random.Pick(RealisticEyeColors);
    }

    /// <summary>
    /// Picks a random skin color using species.
    /// </summary>
    public static Color RandomSkin(ProtoId<SpeciesPrototype> species)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var protoMan = IoCManager.Resolve<IPrototypeManager>();

        var speciesProto = protoMan.Index(species);
        var strategy = protoMan.Index(speciesProto.SkinColoration).Strategy;

        return strategy.InputType switch
        {
            SkinColorationStrategyInput.Unary => strategy.FromUnary(random.NextFloat(0f, 100f)),
            SkinColorationStrategyInput.Color => strategy.ClosestSkinColor(new Color(random.NextFloat(1), random.NextFloat(1), random.NextFloat(1), 1)),
            _ => strategy.ClosestSkinColor(new Color(random.NextFloat(1), random.NextFloat(1), random.NextFloat(1), 1)),
        };
    }

    /// <summary>
    /// Generates a randomized character appearance.
    /// </summary>
    public static HumanoidCharacterAppearance Random(
        SpeciesPrototype species,
        Sex sex,
        RandomizeCfg? charEditorRandomizeConfig = null,
        HumanoidCharacterAppearance? baseAppearance = null)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var protoMan = IoCManager.Resolve<IPrototypeManager>();

        var skinType = protoMan.Index(species.SkinColoration);
        var palette = GetRandomClampedPalette(skinType, random);

        palette = palette with
        {
            SkinColor = (charEditorRandomizeConfig & RandomizeCfg.Skin) != 0 || baseAppearance is null
                ? palette.SkinColor : baseAppearance.SkinColor,
            EyeColor = (charEditorRandomizeConfig & RandomizeCfg.Eyes) != 0 || baseAppearance is null
                ? palette.EyeColor : baseAppearance.EyeColor
        };

        var markings = ((charEditorRandomizeConfig & RandomizeCfg.Markings) != 0 || baseAppearance is null)
            ? RandomizeMarkings(species, sex, palette, protoMan, random)
            : baseAppearance.Markings;

        var appearance = new HumanoidCharacterAppearance(
            palette.EyeColor,
            palette.SkinColor,
            markings,
            baseAppearance?.RegulationHairStyleId ?? HairStyles.DefaultHairStyle,
            baseAppearance?.RegulationHairColor ?? Color.Black,
            baseAppearance?.RegulationFacialHairStyleId ?? HairStyles.DefaultFacialHairStyle,
            baseAppearance?.RegulationFacialHairColor ?? Color.Black);

        return EnsureValid(appearance, species, sex);
    }

    public static Color ClampColor(Color color)
    {
        return new(color.RByte, color.GByte, color.BByte);
    }

    public static HumanoidCharacterAppearance EnsureValid(HumanoidCharacterAppearance appearance, ProtoId<SpeciesPrototype> species, Sex sex)
    {
        var proto = IoCManager.Resolve<IPrototypeManager>();
        var markingManager = IoCManager.Resolve<MarkingManager>();

        var skinColor = appearance.SkinColor;
        var eyeColor = ClampColor(appearance.EyeColor);
        var validatedMarkings = appearance.Markings.ShallowClone();

        if (proto.TryIndex(species, out var speciesProto))
        {
            var coloration = proto.Index(speciesProto.SkinColoration);
            var organs = markingManager.GetOrgans(species);
            skinColor = coloration.Strategy.EnsureVerified(skinColor);

            foreach (var (organ, _) in appearance.Markings)
            {
                if (!organs.ContainsKey(organ))
                    validatedMarkings.Remove(organ);
            }

            foreach (var (organ, organProtoID) in organs)
            {
                if (!markingManager.TryGetMarkingData(organProtoID, out var organData))
                {
                    validatedMarkings.Remove(organ);
                    continue;
                }

                var actualMarkings = appearance.Markings.GetValueOrDefault(organ)?.ShallowClone() ?? new();

                markingManager.EnsureValidColors(actualMarkings);
                markingManager.EnsureValidGroupAndSex(actualMarkings, organData.Value.Group, sex);
                markingManager.EnsureValidLayers(actualMarkings, organData.Value.Layers);
                markingManager.EnsureValidLimits(actualMarkings, organData.Value.Group, organData.Value.Layers, skinColor, eyeColor);

                validatedMarkings[organ] = actualMarkings;
            }
        }

        var regulationHairStyleId = appearance.RegulationHairStyleId;
        if (!HairStyles.RegulationHairStyles.Contains(regulationHairStyleId))
            regulationHairStyleId = HairStyles.DefaultHairStyle;

        var regulationFacialHairStyleId = appearance.RegulationFacialHairStyleId;
        if (!HairStyles.RegulationFacialHairStyles.Contains(regulationFacialHairStyleId))
            regulationFacialHairStyleId = HairStyles.DefaultFacialHairStyle;

        var regulationHairColor = ClampColor(appearance.RegulationHairColor);
        if (!HairStyles.RegulationHairColors.Any(c => c.Color == regulationHairColor))
            regulationHairColor = HairStyles.RegulationHairColors[0].Color;

        var regulationFacialHairColor = ClampColor(appearance.RegulationFacialHairColor);
        if (!HairStyles.RegulationHairColors.Any(c => c.Color == regulationFacialHairColor))
            regulationFacialHairColor = HairStyles.RegulationHairColors[0].Color;

        return new HumanoidCharacterAppearance(
            eyeColor,
            skinColor,
            validatedMarkings,
            regulationHairStyleId,
            regulationHairColor,
            regulationFacialHairStyleId,
            regulationFacialHairColor);
    }

    public bool Equals(HumanoidCharacterAppearance? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return EyeColor.Equals(other.EyeColor) &&
               SkinColor.Equals(other.SkinColor) &&
               MarkingManager.MarkingsAreEqual(Markings, other.Markings) &&
               RegulationHairStyleId == other.RegulationHairStyleId &&
               RegulationHairColor.Equals(other.RegulationHairColor) &&
               RegulationFacialHairStyleId == other.RegulationFacialHairStyleId &&
               RegulationFacialHairColor.Equals(other.RegulationFacialHairColor);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is HumanoidCharacterAppearance other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(EyeColor);
        hashCode.Add(SkinColor);
        hashCode.Add(Markings);
        hashCode.Add(RegulationHairStyleId);
        hashCode.Add(RegulationHairColor);
        hashCode.Add(RegulationFacialHairStyleId);
        hashCode.Add(RegulationFacialHairColor);
        return hashCode.ToHashCode();
    }

    public HumanoidCharacterAppearance Clone()
    {
        return new(this);
    }
}
