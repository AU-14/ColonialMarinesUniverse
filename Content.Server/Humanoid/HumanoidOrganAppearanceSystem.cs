using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Prototypes;

namespace Content.Server.Humanoid;

/// <summary>
/// Server-side convenience operations for modifying humanoid organ appearance at runtime.
/// </summary>
public sealed partial class HumanoidOrganAppearanceSystem : EntitySystem
{
    private static readonly ProtoId<OrganCategoryPrototype> Head = "Head";
    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";

    [Dependency] private MarkingManager _marking = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedVisualBodySystem _visualBody = default!;

    public bool TryGetMarkings(
        EntityUid body,
        HumanoidVisualLayers layer,
        out ProtoId<OrganCategoryPrototype> organ,
        out OrganMarkingData markingData,
        out List<Marking> markings)
    {
        return TryGetMarkings(body, layer, out organ, out markingData, out _, out markings);
    }

    private bool TryGetMarkings(
        EntityUid body,
        HumanoidVisualLayers layer,
        out ProtoId<OrganCategoryPrototype> organ,
        out OrganMarkingData markingData,
        out OrganProfileData? profile,
        out List<Marking> markings)
    {
        organ = default;
        markingData = default;
        profile = null;
        markings = [];

        if (!_visualBody.TryGatherMarkingsData(
                body,
                [layer],
                out var profiles,
                out var markingDataByOrgan,
                out var appliedByOrgan))
        {
            return false;
        }

        foreach (var (candidateOrgan, candidateData) in markingDataByOrgan
                     .OrderBy(pair => pair.Key == Head ? 0 : pair.Key == Torso ? 1 : 2)
                     .ThenBy(pair => pair.Key.Id, StringComparer.Ordinal))
        {
            if (!candidateData.Layers.Contains(layer))
                continue;

            organ = candidateOrgan;
            markingData = candidateData;
            if (profiles.TryGetValue(candidateOrgan, out var candidateProfile))
                profile = candidateProfile;

            if (appliedByOrgan.TryGetValue(candidateOrgan, out var appliedByLayer) &&
                appliedByLayer.TryGetValue(layer, out var applied))
            {
                markings = applied.Select(CloneMarking).ToList();
            }

            return true;
        }

        return false;
    }

    public void SetMarkings(
        EntityUid body,
        ProtoId<OrganCategoryPrototype> organ,
        HumanoidVisualLayers layer,
        IEnumerable<Marking> markings)
    {
        _visualBody.ApplyMarkings(body, new()
        {
            [organ] = new()
            {
                [layer] = markings.Select(CloneMarking).ToList(),
            },
        });
    }

    public bool TryAddMarking(EntityUid body, string markingId, Color color, bool forced = false)
    {
        if (!_prototype.TryIndex<MarkingPrototype>(markingId, out var prototype) ||
            !TryGetMarkings(
                body,
                prototype.BodyPart,
                out var organ,
                out var markingData,
                out var profile,
                out var markings))
        {
            return false;
        }

        if (!forced &&
            (profile is not { } organProfile ||
             !_marking.CanBeApplied(markingData.Group, organProfile.Sex, prototype)))
        {
            return false;
        }

        markings.Add(new Marking(markingId, Enumerable.Repeat(color, prototype.Sprites.Count))
        {
            Forced = forced,
        });
        SetMarkings(body, organ, prototype.BodyPart, markings);
        return true;
    }

    public bool TrySetSkinColor(EntityUid body, Color color)
    {
        return TrySetColors(body, color, null);
    }

    public bool TrySetEyeColor(EntityUid body, Color color)
    {
        return TrySetColors(body, null, color);
    }

    public bool TrySetColors(EntityUid body, Color? skinColor, Color? eyeColor)
    {
        if (!_visualBody.TryGatherMarkingsData(body, null, out var profiles, out _, out _))
            return false;

        var updated = profiles.ToDictionary(
            pair => pair.Key,
            pair => pair.Value with
            {
                SkinColor = skinColor ?? pair.Value.SkinColor,
                EyeColor = eyeColor ?? pair.Value.EyeColor,
            });

        _visualBody.ApplyProfiles(body, updated);
        return true;
    }

    public bool TryGetSkinColor(EntityUid body, out Color color)
    {
        return TryGetColors(body, out color, out _);
    }

    public bool TryGetColors(EntityUid body, out Color skinColor, out Color eyeColor)
    {
        return TryGetAppearance(body, out skinColor, out eyeColor, out _);
    }

    public bool TryGetAppearance(
        EntityUid body,
        out Color skinColor,
        out Color eyeColor,
        out Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings)
    {
        skinColor = default;
        eyeColor = default;
        markings = new();
        if (!_visualBody.TryGatherMarkingsData(body, null, out var profiles, out _, out var applied) ||
            profiles.Count == 0)
        {
            return false;
        }

        // Humanoid appearance used to be body-global. Prefer the head, then torso, and use a
        // stable category order for species without either so container insertion order can
        // never change the compatibility result.
        var profile = profiles.TryGetValue(Head, out var head)
            ? head
            : profiles.TryGetValue(Torso, out var torso)
                ? torso
                : profiles.OrderBy(pair => pair.Key.Id, StringComparer.Ordinal).First().Value;
        skinColor = profile.SkinColor;
        eyeColor = profile.EyeColor;
        markings = applied.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToDictionary(
                inner => inner.Key,
                inner => inner.Value.Select(CloneMarking).ToList()));
        return true;
    }

    private static Marking CloneMarking(Marking marking)
    {
        return new Marking(marking.MarkingId, marking.MarkingColors.ToList())
        {
            Forced = marking.Forced,
        };
    }
}
