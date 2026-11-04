using System.Linq;
using Content.Server.Humanoid;
using Content.Server.Humanoid.Systems;
using Content.Shared._RMC14.Humanoid.Markings;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Humanoid.Markings;

public sealed partial class RMCRandomMarkingsSystem : EntitySystem
{
    [Dependency] private HumanoidOrganAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private MarkingManager _markings = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCRandomMarkingsComponent, MapInitEvent>(OnMapInit,
            after: new[] { typeof(RandomHumanoidAppearanceSystem), typeof(RandomHumanoidSystem) }
        );
    }

    private void OnMapInit(Entity<RMCRandomMarkingsComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<HumanoidProfileComponent>(ent.Owner, out var humanoid) ||
            !_humanoidAppearance.TryGetSkinColor(ent, out var skinColor))
        {
            return;
        }

        if (!ent.Comp.Markings.TryGetValue(humanoid.Species, out var speciesCategory))
            return;

        foreach (var type in speciesCategory)
        {
            if (!_random.Prob(type.Value))
                continue;

            foreach (var layer in MarkingCategoriesConversion.ToHumanoidVisualLayers(type.Key))
            {
                RandomizeLayer(ent, humanoid, skinColor, layer);
            }
        }
    }

    private void RandomizeLayer(
        EntityUid ent,
        HumanoidProfileComponent humanoid,
        Color skinColor,
        HumanoidVisualLayers layer)
    {
        if (!_humanoidAppearance.TryGetMarkings(
                ent,
                layer,
                out var organ,
                out var markingData,
                out var markings))
        {
            return;
        }

        var possibleMarkings = _markings.MarkingsByLayerAndGroupAndSex(layer, markingData.Group, humanoid.Sex);
        if (possibleMarkings.Count == 0)
            return;

        var pickedMarking = _random.Pick(possibleMarkings);

        if (markings.Count == 0)
        {
            var added = pickedMarking.Value.AsMarking().WithColor(skinColor) with { Forced = true };
            _humanoidAppearance.SetMarkings(ent, organ, layer, [added]);
            return;
        }

        var updated = markings.ToList();
        for (var idx = 0; idx < updated.Count; idx++) // Replace existing markings
        {
            var current = updated[idx];
            var replacement = pickedMarking.Value.AsMarking() with { Forced = current.Forced };

            for (var color = 0; color < replacement.MarkingColors.Count && color < current.MarkingColors.Count; color++)
            {
                var replacementColor = color < replacement.MarkingColors.Count - 1
                    ? skinColor
                    : current.MarkingColors[color];
                replacement = replacement.WithColorAt(color, replacementColor);
            }

            updated[idx] = replacement;
        }

        _humanoidAppearance.SetMarkings(ent, organ, layer, updated);
    }
}
