using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Preferences;

/// <summary>
/// Keeps the human torso overlay in step with the Build field shown in character records.
/// </summary>
public static class CMUHumanBodyType
{
    private static readonly ProtoId<SpeciesPrototype> Human = "Human";
    private static readonly ProtoId<OrganCategoryPrototype> Torso = "Torso";
    private static readonly ProtoId<MarkingPrototype> BuiltFemale = "CMUHumanBuiltFemale";
    private static readonly ProtoId<MarkingPrototype> BuiltMale = "CMUHumanBuiltMale";

    public static HumanoidCharacterAppearance Apply(
        HumanoidCharacterAppearance appearance,
        ProtoId<SpeciesPrototype> species,
        Sex sex,
        BuildType build)
    {
        ProtoId<MarkingPrototype>? desired = null;
        if (species == Human && build is BuildType.Athletic or BuildType.Muscular)
        {
            desired = sex switch
            {
                Sex.Female => BuiltFemale,
                Sex.Male => BuiltMale,
                _ => (ProtoId<MarkingPrototype>?) null,
            };
        }

        List<Marking>? current = null;
        if (appearance.Markings.TryGetValue(Torso, out var organ))
            organ.TryGetValue(HumanoidVisualLayers.Special, out current);
        if (desired is { } selected && current is { Count: 1 } && current[0].MarkingId == selected)
            return appearance;
        if (desired == null && (current == null || !current.Any(IsBuiltMarking)))
            return appearance;

        var updated = new HumanoidCharacterAppearance(appearance);
        if (!updated.Markings.TryGetValue(Torso, out var layers))
        {
            layers = new Dictionary<HumanoidVisualLayers, List<Marking>>();
            updated.Markings[Torso] = layers;
        }

        if (!layers.TryGetValue(HumanoidVisualLayers.Special, out var markings))
        {
            markings = new List<Marking>();
            layers[HumanoidVisualLayers.Special] = markings;
        }

        markings.RemoveAll(IsBuiltMarking);
        if (desired is { } markingId)
        {
            // Human Special has a single slot, so the build overlay takes that slot.
            markings.Clear();
            markings.Add(new Marking(markingId, new[] { appearance.SkinColor }));
        }
        else if (markings.Count == 0)
        {
            layers.Remove(HumanoidVisualLayers.Special);
        }

        return updated;
    }

    private static bool IsBuiltMarking(Marking marking)
        => marking.MarkingId == BuiltFemale || marking.MarkingId == BuiltMale;
}
