using System;
using System.Collections.Generic;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using NUnit.Framework;

namespace Content.Tests.Shared.Humanoid;

[TestFixture]
[TestOf(typeof(MarkingCategoriesConversion))]
public sealed class MarkingCategoriesConversionTest
{
    private static readonly IReadOnlyDictionary<HumanoidVisualLayers, MarkingCategories> ExpectedLegacyCategories =
        new Dictionary<HumanoidVisualLayers, MarkingCategories>
        {
            [HumanoidVisualLayers.Special] = MarkingCategories.Special,
            [HumanoidVisualLayers.Tail] = MarkingCategories.Tail,
            [HumanoidVisualLayers.TailOverlay] = MarkingCategories.Tail,
            [HumanoidVisualLayers.Hair] = MarkingCategories.Hair,
            [HumanoidVisualLayers.FacialHair] = MarkingCategories.FacialHair,
            [HumanoidVisualLayers.UndergarmentTop] = MarkingCategories.UndergarmentTop,
            [HumanoidVisualLayers.UndergarmentBottom] = MarkingCategories.UndergarmentBottom,
            [HumanoidVisualLayers.Chest] = MarkingCategories.Chest,
            [HumanoidVisualLayers.Head] = MarkingCategories.Head,
            [HumanoidVisualLayers.Snout] = MarkingCategories.Snout,
            [HumanoidVisualLayers.SnoutCover] = MarkingCategories.Snout,
            [HumanoidVisualLayers.HeadSide] = MarkingCategories.HeadSide,
            [HumanoidVisualLayers.HeadTop] = MarkingCategories.HeadTop,
            [HumanoidVisualLayers.Eyes] = MarkingCategories.Eyes,
            [HumanoidVisualLayers.RArm] = MarkingCategories.Arms,
            [HumanoidVisualLayers.LArm] = MarkingCategories.Arms,
            [HumanoidVisualLayers.RHand] = MarkingCategories.Arms,
            [HumanoidVisualLayers.LHand] = MarkingCategories.Arms,
            [HumanoidVisualLayers.RLeg] = MarkingCategories.Legs,
            [HumanoidVisualLayers.LLeg] = MarkingCategories.Legs,
            [HumanoidVisualLayers.RFoot] = MarkingCategories.Legs,
            [HumanoidVisualLayers.LFoot] = MarkingCategories.Legs,
            [HumanoidVisualLayers.Overlay] = MarkingCategories.Overlay,
            [HumanoidVisualLayers.Handcuffs] = MarkingCategories.Overlay,
            [HumanoidVisualLayers.StencilMask] = MarkingCategories.Overlay,
            [HumanoidVisualLayers.Ensnare] = MarkingCategories.Overlay,
            [HumanoidVisualLayers.Fire] = MarkingCategories.Overlay,
        };

    private static readonly IReadOnlyDictionary<MarkingCategories, HumanoidVisualLayers[]> ExpectedVisualLayers =
        new Dictionary<MarkingCategories, HumanoidVisualLayers[]>
        {
            [MarkingCategories.Special] = [HumanoidVisualLayers.Special],
            [MarkingCategories.Hair] = [HumanoidVisualLayers.Hair],
            [MarkingCategories.FacialHair] = [HumanoidVisualLayers.FacialHair],
            [MarkingCategories.Head] = [HumanoidVisualLayers.Head],
            [MarkingCategories.HeadTop] = [HumanoidVisualLayers.HeadTop],
            [MarkingCategories.HeadSide] = [HumanoidVisualLayers.HeadSide],
            [MarkingCategories.Eyes] = [HumanoidVisualLayers.Eyes],
            [MarkingCategories.Snout] = [HumanoidVisualLayers.Snout, HumanoidVisualLayers.SnoutCover],
            [MarkingCategories.Chest] = [HumanoidVisualLayers.Chest],
            [MarkingCategories.UndergarmentTop] = [HumanoidVisualLayers.UndergarmentTop],
            [MarkingCategories.UndergarmentBottom] = [HumanoidVisualLayers.UndergarmentBottom],
            [MarkingCategories.Arms] =
            [
                HumanoidVisualLayers.RArm,
                HumanoidVisualLayers.LArm,
                HumanoidVisualLayers.RHand,
                HumanoidVisualLayers.LHand,
            ],
            [MarkingCategories.Legs] =
            [
                HumanoidVisualLayers.RLeg,
                HumanoidVisualLayers.LLeg,
                HumanoidVisualLayers.RFoot,
                HumanoidVisualLayers.LFoot,
            ],
            [MarkingCategories.Tail] = [HumanoidVisualLayers.Tail, HumanoidVisualLayers.TailOverlay],
            [MarkingCategories.Overlay] = [HumanoidVisualLayers.Overlay],
        };

    [Test]
    public void EveryVisualLayerMapsToExpectedLegacyCategory()
    {
        var layers = Enum.GetValues<HumanoidVisualLayers>();

        Assert.That(ExpectedLegacyCategories.Count, Is.EqualTo(layers.Length),
            $"The expected mapping must explicitly cover every {nameof(HumanoidVisualLayers)} value.");

        Assert.Multiple(() =>
        {
            foreach (var layer in layers)
            {
                Assert.That(ExpectedLegacyCategories, Does.ContainKey(layer));
                Assert.That(MarkingCategoriesConversion.FromHumanoidVisualLayers(layer),
                    Is.EqualTo(ExpectedLegacyCategories[layer]),
                    $"Unexpected legacy marking category for {layer}.");
            }
        });
    }

    [Test]
    public void EveryLegacyCategoryMapsToExpectedVisualLayers()
    {
        var categories = Enum.GetValues<MarkingCategories>();

        Assert.That(ExpectedVisualLayers.Count, Is.EqualTo(categories.Length),
            $"The expected mapping must explicitly cover every {nameof(MarkingCategories)} value.");

        Assert.Multiple(() =>
        {
            foreach (var category in categories)
            {
                Assert.That(ExpectedVisualLayers, Does.ContainKey(category));

                var layers = MarkingCategoriesConversion.ToHumanoidVisualLayers(category);
                Assert.That(layers, Is.EqualTo(ExpectedVisualLayers[category]),
                    $"Unexpected visual layers for legacy category {category}.");

                foreach (var layer in layers)
                {
                    Assert.That(MarkingCategoriesConversion.FromHumanoidVisualLayers(layer), Is.EqualTo(category),
                        $"Visual layer {layer} did not collapse back to {category}.");
                }
            }
        });
    }

    [Test]
    public void InvalidLegacyCategoryMapsToNoVisualLayers()
    {
        var invalidCategory = (MarkingCategories) byte.MaxValue;

        Assert.That(MarkingCategoriesConversion.ToHumanoidVisualLayers(invalidCategory), Is.Empty);
    }
}
