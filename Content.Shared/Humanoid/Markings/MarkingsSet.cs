using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid.Markings;

/// <summary>
/// Compatibility marking collection used by legacy CMU/RMC humanoid appearance components.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MarkingSet
{
    [DataField("markings")]
    public Dictionary<MarkingCategories, List<Marking>> Markings = new();

    [DataField("points")]
    public Dictionary<MarkingCategories, MarkingPoints> Points = new();

    public MarkingSet()
    {
    }

    public MarkingSet(
        List<Marking> markings,
        string? pointsPrototype,
        MarkingManager? markingManager = null,
        IPrototypeManager? prototypeManager = null)
        : this(pointsPrototype, markingManager, prototypeManager)
    {
        AddValidated(markings, markingManager);
    }

    public MarkingSet(List<Marking> markings, MarkingManager? markingManager = null)
    {
        AddValidated(markings, markingManager);
    }

    public MarkingSet(
        string? pointsPrototype,
        MarkingManager? markingManager = null,
        IPrototypeManager? prototypeManager = null)
    {
        IoCManager.Resolve(ref markingManager, ref prototypeManager);

        if (!string.IsNullOrEmpty(pointsPrototype) &&
            prototypeManager.TryIndex(pointsPrototype, out MarkingPointsPrototype? points))
        {
            Points = MarkingPoints.CloneMarkingPointDictionary(points.Points);
        }
    }

    public MarkingSet(MarkingSet other)
    {
        foreach (var (category, markings) in other.Markings)
            Markings[category] = markings.Select(marking => new Marking(marking)).ToList();

        Points = MarkingPoints.CloneMarkingPointDictionary(other.Points);
    }

    private void AddValidated(IEnumerable<Marking> markings, MarkingManager? markingManager)
    {
        IoCManager.Resolve(ref markingManager);

        foreach (var marking in markings)
        {
            if (markingManager.TryGetMarking(marking, out var prototype))
                AddBack(prototype.MarkingCategory, new Marking(marking));
        }
    }

    public void EnsureSpecies(
        string species,
        Color? skinColor,
        MarkingManager? markingManager = null,
        IPrototypeManager? prototypeManager = null)
    {
        IoCManager.Resolve(ref markingManager, ref prototypeManager);

        foreach (var (category, markings) in Markings)
        {
            for (var i = markings.Count - 1; i >= 0; i--)
            {
                var marking = markings[i];
                if (!markingManager.TryGetMarking(marking, out var prototype))
                {
                    Remove(category, i);
                    continue;
                }

                if (!MarkingManager.CanBeAppliedToLegacySpecies(species, prototype, prototypeManager))
                {
                    Remove(category, i);
                    continue;
                }

                if (skinColor != null &&
                    markingManager.MustMatchSkin(species, prototype.BodyPart, out var alpha, prototypeManager))
                {
                    markings[i] = marking.WithColor(skinColor.Value.WithAlpha(alpha));
                }
            }
        }
    }

    public void EnsureSexes(Sex sex, MarkingManager? markingManager = null)
    {
        IoCManager.Resolve(ref markingManager);

        foreach (var (category, markings) in Markings)
        {
            for (var i = markings.Count - 1; i >= 0; i--)
            {
                if (!markingManager.TryGetMarking(markings[i], out var prototype) ||
                    prototype.SexRestriction != null && prototype.SexRestriction != sex)
                {
                    Remove(category, i);
                }
            }
        }
    }

    public void EnsureValid(MarkingManager? markingManager = null)
    {
        IoCManager.Resolve(ref markingManager);

        foreach (var (category, markings) in Markings)
        {
            for (var i = markings.Count - 1; i >= 0; i--)
            {
                var current = markings[i];
                if (!markingManager.TryGetMarking(current, out var prototype))
                {
                    Remove(category, i);
                    continue;
                }

                if (prototype.Sprites.Count == current.MarkingColors.Count)
                    continue;

                markings[i] = new Marking(prototype.ID, prototype.Sprites.Count)
                {
                    Forced = current.Forced,
                    Visible = current.Visible,
                };
            }
        }
    }

    public void EnsureDefault(
        Color? skinColor = null,
        Color? eyeColor = null,
        MarkingManager? markingManager = null)
    {
        IoCManager.Resolve(ref markingManager);

        foreach (var (category, points) in Points)
        {
            if (points.Points <= 0 || points.DefaultMarkings.Count == 0)
                continue;

            foreach (var markingId in points.DefaultMarkings)
            {
                if (points.Points <= 0)
                    break;

                if (!markingManager.Markings.TryGetValue(markingId, out var prototype))
                    continue;

                var colors = MarkingColoring.GetMarkingLayerColors(
                    prototype,
                    skinColor,
                    eyeColor,
                    GetForwardEnumerator().ToList());
                AddBack(category, new Marking(markingId, colors));
            }
        }
    }

    public void AddBack(MarkingCategories category, Marking marking)
    {
        if (!marking.Forced && Points.TryGetValue(category, out var points))
        {
            if (points.Points <= 0)
                return;

            points.Points--;
        }

        if (!Markings.TryGetValue(category, out var markings))
        {
            markings = new();
            Markings[category] = markings;
        }

        markings.Add(marking);
    }

    public void Replace(MarkingCategories category, int index, Marking marking)
    {
        if (Markings.TryGetValue(category, out var markings) && index >= 0 && index < markings.Count)
            markings[index] = marking;
    }

    public bool Remove(MarkingCategories category, string id)
    {
        if (!Markings.TryGetValue(category, out var markings))
            return false;

        var index = markings.FindIndex(marking => marking.MarkingId == id);
        if (index < 0)
            return false;

        Remove(category, index);
        return true;
    }

    public void Remove(MarkingCategories category, int index)
    {
        if (!Markings.TryGetValue(category, out var markings) || index < 0 || index >= markings.Count)
            return;

        if (!markings[index].Forced && Points.TryGetValue(category, out var points))
            points.Points++;

        markings.RemoveAt(index);
    }

    public void Clear()
    {
        foreach (var (category, markings) in Markings)
        {
            if (!Points.TryGetValue(category, out var points))
                continue;

            points.Points += markings.Count(marking => !marking.Forced);
        }

        Markings.Clear();
    }

    public bool TryGetCategory(
        MarkingCategories category,
        [NotNullWhen(true)] out IReadOnlyList<Marking>? markings)
    {
        if (Markings.TryGetValue(category, out var list))
        {
            markings = list;
            return true;
        }

        markings = null;
        return false;
    }

    public IEnumerable<Marking> GetForwardEnumerator()
    {
        foreach (var markings in Markings.Values)
        {
            foreach (var marking in markings)
                yield return marking;
        }
    }
}
