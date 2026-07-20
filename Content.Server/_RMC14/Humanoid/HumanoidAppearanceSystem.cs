using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;

namespace Content.Server.Humanoid;

/// <summary>
/// Compatibility host for RMC systems that still operate on legacy humanoid appearance data.
/// </summary>
public sealed partial class HumanoidAppearanceSystem : SharedHumanoidAppearanceSystem
{
    [Dependency] private MarkingManager _markingManager = default!;

    public void SetMarkingId(
        EntityUid uid,
        MarkingCategories category,
        int index,
        string markingId,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (index < 0 ||
            !_markingManager.MarkingsByCategory(category).TryGetValue(markingId, out var markingPrototype) ||
            !Resolve(uid, ref humanoid) ||
            !humanoid.MarkingSet.TryGetCategory(category, out var markings) ||
            index >= markings.Count)
        {
            return;
        }

        var marking = markingPrototype.AsMarking();
        for (var i = 0; i < marking.MarkingColors.Count && i < markings[index].MarkingColors.Count; i++)
        {
            marking.SetColor(i, markings[index].MarkingColors[i]);
        }

        humanoid.MarkingSet.Replace(category, index, marking);
        Dirty(uid, humanoid);
    }

    public void SetMarkingColor(
        EntityUid uid,
        MarkingCategories category,
        int index,
        List<Color> colors,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (index < 0 ||
            !Resolve(uid, ref humanoid) ||
            !humanoid.MarkingSet.TryGetCategory(category, out var markings) ||
            index >= markings.Count)
        {
            return;
        }

        for (var i = 0; i < markings[index].MarkingColors.Count && i < colors.Count; i++)
        {
            markings[index].SetColor(i, colors[i]);
        }

        Dirty(uid, humanoid);
    }
}
