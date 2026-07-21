using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;

// ReSharper disable CheckNamespace
namespace Content.Shared.Humanoid.Markings;

public partial record struct Marking
{
    public Marking(Marking other)
    {
        this = other;
        _markingColors = other._markingColors == null ? new() : new(other._markingColors);
    }

    public void SetColor(int colorIndex, Color color)
    {
        _markingColors[colorIndex] = color;
    }

    public void SetColor(Color color)
    {
        for (var i = 0; i < _markingColors.Count; i++)
            _markingColors[i] = color;
    }
}

public sealed partial class MarkingPrototype
{
    [DataField("markingCategory")]
    private MarkingCategories? _legacyMarkingCategory;

    [DataField("speciesRestriction")]
    public List<string>? SpeciesRestrictions { get; private set; }

    // Kept so legacy marking prototypes continue to deserialize. RMC's coloring
    // pipeline uses RMCFollowSkinColor and the modern coloring definitions.
    [DataField("followSkinColor")]
    public bool FollowSkinColor { get; private set; }

    public MarkingCategories MarkingCategory =>
        _legacyMarkingCategory ?? MarkingCategoriesConversion.FromHumanoidVisualLayers(BodyPart);
}

public sealed partial class MarkingManager
{
    public IReadOnlyDictionary<string, MarkingPrototype> Markings => _markings;

    public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByCategory(MarkingCategories category)
    {
        return _markings
            .Where(pair => pair.Value.MarkingCategory == category)
            .ToDictionary();
    }

    public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByCategoryAndSpecies(
        MarkingCategories category,
        string species)
    {
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        return MarkingsByCategory(category)
            .Where(pair => CanBeAppliedToLegacySpecies(species, pair.Value, prototypes))
            .ToDictionary();
    }

    public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByCategoryAndSex(
        MarkingCategories category,
        Sex sex)
    {
        return MarkingsByCategory(category)
            .Where(pair => pair.Value.SexRestriction == null || pair.Value.SexRestriction == sex)
            .ToDictionary();
    }

    public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByCategoryAndSpeciesAndSex(
        MarkingCategories category,
        string species,
        Sex sex)
    {
        return MarkingsByCategoryAndSpecies(category, species)
            .Where(pair => pair.Value.SexRestriction == null || pair.Value.SexRestriction == sex)
            .ToDictionary();
    }

    public bool CanBeApplied(
        string species,
        Sex sex,
        Marking marking,
        IPrototypeManager? prototypeManager = null)
    {
        return TryGetMarking(marking, out var prototype) &&
               CanBeApplied(species, sex, prototype, prototypeManager);
    }

    public bool CanBeApplied(
        string species,
        Sex sex,
        MarkingPrototype prototype,
        IPrototypeManager? prototypeManager = null)
    {
        IoCManager.Resolve(ref prototypeManager);
        return CanBeAppliedToLegacySpecies(species, prototype, prototypeManager) &&
               (prototype.SexRestriction == null || prototype.SexRestriction == sex);
    }

    internal static bool CanBeAppliedToLegacySpecies(
        string species,
        MarkingPrototype prototype,
        IPrototypeManager prototypeManager)
    {
        var onlyWhitelisted = false;
        if (prototypeManager.TryIndex<SpeciesPrototype>(species, out var speciesPrototype) &&
            speciesPrototype.MarkingPoints.Id is { Length: > 0 } markingPoints &&
            prototypeManager.TryIndex(markingPoints, out MarkingPointsPrototype? pointsPrototype))
        {
            onlyWhitelisted = pointsPrototype.OnlyWhitelisted ||
                              pointsPrototype.Points.GetValueOrDefault(prototype.MarkingCategory)?.OnlyWhitelisted == true;
        }

        var group = new ProtoId<MarkingsGroupPrototype>(species);
        var hasWhitelist = prototype.GroupWhitelist != null || prototype.SpeciesRestrictions != null;
        if (!hasWhitelist)
            return !onlyWhitelisted;

        return prototype.GroupWhitelist?.Contains(group) == true ||
               prototype.SpeciesRestrictions?.Contains(species) == true;
    }

    public bool MustMatchSkin(
        string species,
        HumanoidVisualLayers layer,
        out float alpha,
        IPrototypeManager? prototypeManager = null)
    {
        IoCManager.Resolve(ref prototypeManager);
        var speciesPrototype = prototypeManager.Index<SpeciesPrototype>(species);
        var spriteSet = speciesPrototype.SpriteSet.Id;
        if (string.IsNullOrEmpty(spriteSet) ||
            !prototypeManager.TryIndex(spriteSet, out HumanoidSpeciesBaseSpritesPrototype? baseSprites) ||
            !baseSprites.Sprites.TryGetValue(layer, out var spriteName) ||
            !prototypeManager.TryIndex(spriteName, out HumanoidSpeciesSpriteLayer? sprite) ||
            !sprite.MarkingsMatchSkin)
        {
            alpha = 1f;
            return false;
        }

        alpha = sprite.LayerAlpha;
        return true;
    }
}
