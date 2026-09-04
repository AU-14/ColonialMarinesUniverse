using System.Linq;
using Content.Server.Body;
using Content.Server.Humanoid.Components;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Humanoid.Systems;

public sealed partial class RandomHumanoidAppearanceSystem : EntitySystem
{
    [Dependency] private HumanoidProfileSystem _humanoidProfile = default!;
    [Dependency] private MarkingManager _markingManager = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomHumanoidAppearanceComponent, MapInitEvent>(OnMapInit,
            after: [typeof(InitialBodySystem), typeof(VisualBodySystem)]);
    }

    private void OnMapInit(Entity<RandomHumanoidAppearanceComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<HumanoidProfileComponent>(ent, out var humanoid))
            return;

        var profile = HumanoidCharacterProfile.RandomWithSpecies(humanoid.Species);

        if (ent.Comp.Hair is { } hair)
            profile = WithMarking(profile, HumanoidVisualLayers.Hair, hair);

        if (TryComp<RandomHumanoidAppearanceWhitelistedComponent>(ent, out var whitelist))
            profile = ApplyWhitelist(profile, whitelist);

        if (profile.Sex == Sex.Female)
            profile = WithoutMarkings(profile, HumanoidVisualLayers.FacialHair);

        var appearance = HumanoidCharacterAppearance.EnsureValid(profile.Appearance, profile.Species, profile.Sex);
        profile = profile.WithCharacterAppearance(appearance);

        _visualBody.ApplyProfileTo(ent.Owner, profile);
        _humanoidProfile.ApplyProfileTo(ent.Owner, profile);

        if (ent.Comp.RandomizeName)
            _metaData.SetEntityName(ent, profile.Name);
    }

    private HumanoidCharacterProfile ApplyWhitelist(
        HumanoidCharacterProfile profile,
        RandomHumanoidAppearanceWhitelistedComponent whitelist)
    {
        List<string>? hairOptions = null;
        if (profile.Sex == Sex.Male && whitelist.AllowedMaleHairStyles is { Count: > 0 })
            hairOptions = whitelist.AllowedMaleHairStyles;
        else if (profile.Sex == Sex.Female && whitelist.AllowedFemaleHairStyles is { Count: > 0 })
            hairOptions = whitelist.AllowedFemaleHairStyles;
        else if (whitelist.AllowedHairStyles is { Count: > 0 })
            hairOptions = whitelist.AllowedHairStyles;

        if (hairOptions is { Count: > 0 })
            profile = WithMarking(profile, HumanoidVisualLayers.Hair, _random.Pick(hairOptions));

        if (whitelist.AllowedHairColorsHex is { Count: > 0 })
        {
            var hairColors = whitelist.AllowedHairColorsHex.Select(hex => Color.FromHex(hex)).ToList();
            profile = WithMarkingColor(profile, HumanoidVisualLayers.Hair, _random.Pick(hairColors));
        }

        if (whitelist.AllowedEyeColorsHex is { Count: > 0 })
        {
            var eyeColors = whitelist.AllowedEyeColorsHex.Select(hex => Color.FromHex(hex)).ToList();
            profile = profile.WithCharacterAppearance(
                profile.Appearance.WithEyeColor(_random.Pick(eyeColors)));
        }

        var hairColor = GetMarkingColor(profile, HumanoidVisualLayers.Hair, Color.Black);
        if (whitelist.BeardChance > 0f && _random.NextFloat() < whitelist.BeardChance)
        {
            if (whitelist.AllowedBeardStyles is { Count: > 0 })
            {
                profile = WithMarking(
                    profile,
                    HumanoidVisualLayers.FacialHair,
                    _random.Pick(whitelist.AllowedBeardStyles));
            }

            return WithMarkingColor(profile, HumanoidVisualLayers.FacialHair, hairColor);
        }

        return WithMarking(
            profile,
            HumanoidVisualLayers.FacialHair,
            HairStyles.DefaultFacialHairStyle,
            Color.Black);
    }

    private HumanoidCharacterProfile WithMarking(
        HumanoidCharacterProfile profile,
        HumanoidVisualLayers layer,
        string markingId,
        Color? color = null)
    {
        if (!ProtoMan.TryIndex<MarkingPrototype>(markingId, out var prototype) || prototype.BodyPart != layer)
            return profile;

        var markingDataByOrgan = _markingManager.GetMarkingData(profile.Species);
        var (organ, markingData) = markingDataByOrgan.FirstOrDefault(pair => pair.Value.Layers.Contains(layer));
        if (organ == default ||
            !_markingManager.CanBeApplied(markingData.Group, profile.Sex, prototype))
            return profile;

        var markings = CloneMarkings(profile.Appearance.Markings);
        var organMarkings = markings.GetValueOrDefault(organ) ?? new();
        markings[organ] = organMarkings;

        var markingColor = color ?? GetMarkingColor(profile, layer, Color.White);
        organMarkings[layer] = [prototype.AsMarking().WithColor(markingColor)];

        return profile.WithCharacterAppearance(profile.Appearance.WithMarkings(markings));
    }

    private static HumanoidCharacterProfile WithMarkingColor(
        HumanoidCharacterProfile profile,
        HumanoidVisualLayers layer,
        Color color)
    {
        var markings = CloneMarkings(profile.Appearance.Markings);
        foreach (var organMarkings in markings.Values)
        {
            if (!organMarkings.TryGetValue(layer, out var applied))
                continue;

            organMarkings[layer] = applied.Select(marking => marking.WithColor(color)).ToList();
        }

        return profile.WithCharacterAppearance(profile.Appearance.WithMarkings(markings));
    }

    private static Color GetMarkingColor(
        HumanoidCharacterProfile profile,
        HumanoidVisualLayers layer,
        Color fallback)
    {
        foreach (var organMarkings in profile.Appearance.Markings.Values)
        {
            if (organMarkings.TryGetValue(layer, out var applied) &&
                applied.Count > 0 &&
                applied[0].MarkingColors.Count > 0)
            {
                return applied[0].MarkingColors[0];
            }
        }

        return fallback;
    }

    private static HumanoidCharacterProfile WithoutMarkings(
        HumanoidCharacterProfile profile,
        HumanoidVisualLayers layer)
    {
        var markings = CloneMarkings(profile.Appearance.Markings);
        foreach (var organMarkings in markings.Values)
        {
            organMarkings.Remove(layer);
        }

        return profile.WithCharacterAppearance(profile.Appearance.WithMarkings(markings));
    }

    private static Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>>
        CloneMarkings(Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings)
    {
        return markings.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToDictionary(
                inner => inner.Key,
                inner => inner.Value.ToList()));
    }
}
