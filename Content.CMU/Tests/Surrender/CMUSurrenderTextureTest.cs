using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Robust.Shared.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Tests.CMU14.Surrender;

[TestFixture]
public sealed class CMUSurrenderTextureTest
{
    [Test]
    public void SurrenderIconHasTransparentPadding()
    {
        using var icon = Image.Load<Rgba32>(Path.Combine(SourceRoot(),
            "Content.CMU/Resources/Textures/CMU14/Interface/Emotes/surrender.png"));
        Assert.That(icon[0, 0].A, Is.Zero, "The wheel must not show a rectangular image background.");
        Assert.That(icon[icon.Width - 1, icon.Height - 1].A, Is.Zero);
        Assert.That(icon[icon.Width / 2, icon.Height / 2].A, Is.GreaterThan(0),
            "The icon must contain a visible central face.");
    }

    [Test]
    public void PoseMapsUseLinearTextureSampling()
    {
        var path = Path.Combine(SourceRoot(),
            "Content.CMU/Resources/Textures/CMU14/Mobs/Surrender/pose.rsi/meta.json");
        using var metadata = JsonDocument.Parse(File.ReadAllText(path));
        var srgb = TextureLoadParameters.Default.Srgb;
        if (metadata.RootElement.TryGetProperty("load", out var load)
            && load.TryGetProperty("srgb", out var setting))
        {
            srgb = setting.GetBoolean();
        }

        Assert.That(srgb, Is.False,
            "Displacement channels are numeric data. sRGB decoding turns neutral pixels into large offsets into neighbouring atlas sprites.");
    }

    [TestCase(0, "m")]
    [TestCase(1, "m")]
    [TestCase(2, "m")]
    [TestCase(3, "m")]
    [TestCase(0, "f")]
    [TestCase(1, "f")]
    [TestCase(2, "f")]
    [TestCase(3, "f")]
    public void RaisedShouldersCoverStandingTorsoSeams(int direction, string body)
    {
        var root = SourceRoot();
        var maps = Path.Combine(root, "Content.CMU/Resources/Textures/CMU14/Mobs/Surrender/pose.rsi");
        var parts = Path.Combine(root, "Resources/Textures/Mobs/Species/Human/parts.rsi");
        using var torso = Image.Load<Rgba32>(Path.Combine(parts, $"torso_{body}.png"));
        using var torsoMap = Image.Load<Rgba32>(Path.Combine(maps, "torso.png"));
        using var armMap = Image.Load<Rgba32>(Path.Combine(maps, "limbs.png"));
        var seamBottom = 0;
        var seams = 0;
        // Only inspect the shoulder region; the torso also has leg attachment seams.
        for (var y = 10; y < 19; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                if (!IsSeam(FramePixel(torso, direction, x, y)))
                    continue;

                seams++;
                seamBottom = System.Math.Max(seamBottom, y);
                Assert.That(IsSeam(Sample(torso, torsoMap, direction, x, y)), Is.False,
                    $"Raising the arm exposes a wound at ({x}, {y}) on the standing torso.");
            }
        }
        Assert.That(seams, Is.GreaterThan(0), "The reference torso must contain arm attachment seams.");

        foreach (var arm in new[] { "l_arm", "r_arm" })
        {
            using var source = Image.Load<Rgba32>(Path.Combine(parts, arm + ".png"));
            for (var y = seamBottom; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    Assert.That(Sample(source, armMap, direction, x, y).A, Is.Zero,
                        "A raised arm must not leave a second, downward-facing shoulder below its joint.");
                }
            }
        }
    }

    private static bool IsSeam(Rgba32 pixel) => pixel.A != 0 && pixel.R > pixel.G * 1.5f;

    [TestCase(2)]
    [TestCase(3)]
    public void SideViewDoesNotDrawAnotherArmUnderTheCutout(int direction)
    {
        using var limbs = LoadMap("limbs");
        using var clothing = LoadMap("clothing");
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                Assert.That(FramePixel(limbs, direction, x, y).A, Is.Zero,
                    "The base limb layer must not duplicate the foreground arm.");
                var armColumn = direction == 2 ? x is >= 11 and < 16 : x is >= 16 and < 21;
                if (armColumn && y <= 12)
                    Assert.That(FramePixel(clothing, direction, x, y).A, Is.Zero,
                        "A second raised sleeve must not remain behind the head.");
            }
        }
    }

    [TestCase(2, "r_arm", "l_arm")]
    [TestCase(3, "l_arm", "r_arm")]
    [TestCase(2, "r_hand", "l_hand")]
    [TestCase(3, "l_hand", "r_hand")]
    public void SideCutoutPreservesTheNearLimbWithoutStretching(int direction, string near, string far)
    {
        var parts = Path.Combine(SourceRoot(), "Resources/Textures/Mobs/Species/Human/parts.rsi");
        using var source = Image.Load<Rgba32>(Path.Combine(parts, near + ".png"));
        using var hidden = Image.Load<Rgba32>(Path.Combine(parts, far + ".png"));
        using var map = LoadMap("foreground_limbs");
        var original = VisiblePixels(source, null, direction);
        var cutout = VisiblePixels(source, map, direction);
        Assert.That(cutout, Has.Count.EqualTo(original.Count), "Each source limb pixel must appear exactly once.");
        Assert.That(cutout.GroupBy(p => p.Colour).ToDictionary(g => g.Key, g => g.Count()),
            Is.EquivalentTo(original.GroupBy(p => p.Colour).ToDictionary(g => g.Key, g => g.Count())),
            "The cutout must retain the source texture's colours without interpolation.");
        Assert.That(DistanceSignature(cutout), Is.EqualTo(DistanceSignature(original)),
            "A rigid cutout preserves distances between pixels; stretching and shearing do not.");
        Assert.That(VisiblePixels(hidden, map, direction), Is.Empty,
            "The far hand must not float alongside the single visible side arm.");
    }

    [TestCase(2, "r_arm")]
    [TestCase(3, "l_arm")]
    public void SleeveCutoutDoesNotSampleClothingBelowTheArm(int direction, string nearArm)
    {
        using var arm = Image.Load<Rgba32>(Path.Combine(SourceRoot(),
            "Resources/Textures/Mobs/Species/Human/parts.rsi", nearArm + ".png"));
        var bottom = VisiblePixels(arm, null, direction).Max(p => p.Y);
        using var map = LoadMap("foreground_clothing");
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var pixel = FramePixel(map, direction, x, y);
                if (pixel.A != 0)
                    Assert.That(y + pixel.G - 128, Is.LessThanOrEqualTo(bottom),
                        "Waist/coat pixels below the arm must not be lifted above the head.");
            }
        }
    }

    [Test]
    public void PoseMapsUseNearestNeighbourSampling()
    {
        Assert.That(TextureSampleParameters.Default.Filter, Is.False,
            "The native RSI loader uses default sampling for displacement data; pixel cutouts require nearest-neighbour sampling.");
    }

    [TestCase(2, "Content.CMU/Resources/Textures/CMU14/Mobs/Human_Customization/pve_undershirt.rsi/undershirt_uscm_short.png")]
    [TestCase(3, "Content.CMU/Resources/Textures/CMU14/Mobs/Human_Customization/pve_undershirt.rsi/undershirt_uscm_short.png")]
    [TestCase(2, "Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Marine/rto.rsi/equipped-INNERCLOTHING.png")]
    [TestCase(3, "Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Marine/rto.rsi/equipped-INNERCLOTHING.png")]
    [TestCase(2, "Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Civilian/khaki_workwear.rsi/equipped-INNERCLOTHING.png")]
    [TestCase(3, "Resources/Textures/_RMC14/Objects/Clothing/Uniforms/Civilian/khaki_workwear.rsi/equipped-INNERCLOTHING.png")]
    public void SidePosePreservesClothingBelowTheArm(int direction, string clothing)
    {
        using var arm = Image.Load<Rgba32>(Path.Combine(SourceRoot(),
            "Resources/Textures/Mobs/Species/Human/parts.rsi", direction == 2 ? "r_arm.png" : "l_arm.png"));
        var bottom = VisiblePixels(arm, null, direction).Max(pixel => pixel.Y);
        using var source = Image.Load<Rgba32>(Path.Combine(SourceRoot(), clothing));
        using var map = LoadMap("clothing");
        using var foreground = LoadMap("foreground_clothing");
        var visible = 0;
        for (var y = bottom + 1; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var original = FramePixel(source, direction, x, y);
                if (original.A != 0)
                    visible++;
                Assert.That(Sample(source, map, direction, x, y), Is.EqualTo(original),
                    "Raising a sleeve must preserve the original waist, hem and trouser pixels.");
                Assert.That(Sample(source, foreground, direction, x, y).A, Is.Zero,
                    "A copied sleeve must not cover waist-level equipment.");
            }
        }
        Assert.That(visible, Is.GreaterThan(0), "The reference garment must contain waist or lower-body detail.");
    }

    [TestCase(2, "Satchels/Marines/welder_chestrig.rsi", "m")]
    [TestCase(3, "Satchels/Marines/welder_chestrig.rsi", "m")]
    [TestCase(2, "Chestrigs/intel/jungle-classic.rsi", "m")]
    [TestCase(3, "Chestrigs/intel/jungle-classic.rsi", "m")]
    [TestCase(3, "Satchels/Civilians/bag_phone.rsi", "m")]
    [TestCase(2, "Satchels/Marines/welder_chestrig.rsi", "f")]
    [TestCase(3, "Satchels/Marines/welder_chestrig.rsi", "f")]
    [TestCase(2, "Chestrigs/intel/jungle-classic.rsi", "f")]
    [TestCase(3, "Chestrigs/intel/jungle-classic.rsi", "f")]
    [TestCase(3, "Satchels/Civilians/bag_phone.rsi", "f")]
    public void SideGearDoesNotBecomeDetachedFromTheBody(int direction, string backpack, string body)
    {
        var root = SourceRoot();
        var parts = Path.Combine(root, "Resources/Textures/Mobs/Species/Human/parts.rsi");
        var standing = new HashSet<(int X, int Y)>();
        var raised = new HashSet<(int X, int Y)>();
        using var torsoMap = LoadMap("torso");
        using var limbsMap = LoadMap("limbs");
        using var foregroundMap = LoadMap("foreground_limbs");
        foreach (var part in new[] { $"torso_{body}", $"head_{body}", "l_arm", "r_arm", "l_hand", "r_hand",
                     "l_leg", "r_leg", "l_foot", "r_foot" })
        {
            using var image = Image.Load<Rgba32>(Path.Combine(parts, part + ".png"));
            standing.UnionWith(VisiblePixels(image, null, direction).Select(pixel => (pixel.X, pixel.Y)));
            var limb = part is "l_arm" or "r_arm" or "l_hand" or "r_hand";
            var map = part.StartsWith("torso_") ? torsoMap : limb ? limbsMap : null;
            raised.UnionWith(VisiblePixels(image, map, direction).Select(pixel => (pixel.X, pixel.Y)));
            if (limb)
                raised.UnionWith(VisiblePixels(image, foregroundMap, direction).Select(pixel => (pixel.X, pixel.Y)));
        }

        using var shirt = Image.Load<Rgba32>(Path.Combine(root,
            "Content.CMU/Resources/Textures/CMU14/Mobs/Human_Customization/pve_undershirt.rsi/undershirt_uscm_short.png"));
        using var clothingMap = LoadMap("clothing");
        using var sleeveMap = LoadMap("foreground_clothing");
        standing.UnionWith(VisiblePixels(shirt, null, direction).Select(pixel => (pixel.X, pixel.Y)));
        raised.UnionWith(VisiblePixels(shirt, clothingMap, direction).Select(pixel => (pixel.X, pixel.Y)));
        raised.UnionWith(VisiblePixels(shirt, sleeveMap, direction).Select(pixel => (pixel.X, pixel.Y)));

        using var gear = Image.Load<Rgba32>(Path.Combine(root,
            "Resources/Textures/_RMC14/Objects/Clothing/Back", backpack, "equipped-BACKPACK.png"));
        var gearPixels = VisiblePixels(gear, null, direction).Select(pixel => (pixel.X, pixel.Y)).ToArray();
        Assert.That(gearPixels, Is.Not.Empty);
        standing.UnionWith(gearPixels);
        raised.UnionWith(gearPixels);
        var before = ConnectedPixels(standing, (16, 16));
        var after = ConnectedPixels(raised, (16, 16));
        Assert.That(gearPixels.Where(pixel => before.Contains(pixel) && !after.Contains(pixel)), Is.Empty,
            "Raising hands must not detach a backpack strap or pouch by cutting away its torso contact pixels.");
    }

    private static HashSet<(int X, int Y)> ConnectedPixels(HashSet<(int X, int Y)> pixels, (int X, int Y) seed)
    {
        Assert.That(pixels, Does.Contain(seed), "The reference torso must contain the contact test's starting pixel.");
        var connected = new HashSet<(int X, int Y)> { seed };
        var pending = new Queue<(int X, int Y)>();
        pending.Enqueue(seed);
        while (pending.TryDequeue(out var pixel))
        {
            foreach (var next in new[] { (pixel.X - 1, pixel.Y), (pixel.X + 1, pixel.Y),
                         (pixel.X, pixel.Y - 1), (pixel.X, pixel.Y + 1) })
            {
                if (pixels.Contains(next) && connected.Add(next))
                    pending.Enqueue(next);
            }
        }
        return connected;
    }

    private static Image<Rgba32> LoadMap(string state) => Image.Load<Rgba32>(Path.Combine(SourceRoot(),
        "Content.CMU/Resources/Textures/CMU14/Mobs/Surrender/pose.rsi", state + ".png"));

    private static List<(int X, int Y, uint Colour)> VisiblePixels(Image<Rgba32> source, Image<Rgba32>? map, int direction)
    {
        var pixels = new List<(int X, int Y, uint Colour)>();
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var pixel = map == null ? FramePixel(source, direction, x, y) : Sample(source, map, direction, x, y);
                if (pixel.A != 0)
                    pixels.Add((x, y, pixel.PackedValue));
            }
        }
        return pixels;
    }

    private static int[] DistanceSignature(List<(int X, int Y, uint Colour)> pixels)
    {
        var distances = new List<int>();
        for (var i = 0; i < pixels.Count; i++)
        {
            for (var j = i + 1; j < pixels.Count; j++)
            {
                var dx = pixels[i].X - pixels[j].X;
                var dy = pixels[i].Y - pixels[j].Y;
                distances.Add(dx * dx + dy * dy);
            }
        }
        distances.Sort();
        return distances.ToArray();
    }

    [TestCase("foreground_limbs")]
    [TestCase("foreground_clothing")]
    [TestCase("waist_gear")]
    public void SecondaryPoseLayersOnlyDrawInSideViews(string state)
    {
        using var image = Image.Load<Rgba32>(Path.Combine(SourceRoot(),
            "Content.CMU/Resources/Textures/CMU14/Mobs/Surrender/pose.rsi", state + ".png"));
        var visibleSidePixels = 0;
        for (var direction = 0; direction < 4; direction++)
        {
            for (var y = 0; y < 32; y++)
            {
                for (var x = 0; x < 32; x++)
                {
                    var pixel = FramePixel(image, direction, x, y);
                    if (direction < 2)
                        Assert.That(pixel.A, Is.Zero, "Front/back views must keep the ordinary equipment layers.");
                    else if (pixel.A != 0)
                    {
                        visibleSidePixels++;
                        Assert.That(x + pixel.R - 128, Is.InRange(0, 31));
                        Assert.That(y + pixel.G - 128, Is.InRange(0, 31));
                    }
                }
            }
        }
        Assert.That(visibleSidePixels, Is.GreaterThan(0), "The side-view pose layer must have visible pixels.");
    }

    [TestCase(2, "Back/Satchels/Civilians/bag_phone.rsi/equipped-BACKPACK.png")]
    [TestCase(2, "Belt/marine/jungle-classic.rsi/equipped-BELT.png")]
    [TestCase(3, "Belt/marine/jungle-classic.rsi/equipped-BELT.png")]
    [TestCase(2, "Belt/utility.rsi/equipped-BELT.png")]
    [TestCase(3, "Belt/utility.rsi/equipped-BELT.png")]
    public void GearUnderlayClosesTheNativeHandNotch(int direction, string gear)
    {
        using var source = Image.Load<Rgba32>(Path.Combine(SourceRoot(),
            "Resources/Textures/_RMC14/Objects/Clothing", gear));
        using var map = LoadMap("waist_gear");
        var filled = 0;
        for (var y = 0; y < 32; y++)
        {
            var material = Enumerable.Range(0, 32)
                .Where(x => FramePixel(source, direction, x, y).A == 255).ToArray();
            for (var x = 0; x < 32; x++)
            {
                var original = FramePixel(source, direction, x, y);
                var underlay = Sample(source, map, direction, x, y);
                if (original.A == 0 && underlay.A != 0)
                {
                    filled++;
                    Assert.That(y, Is.InRange(18, 21), "The fill must stay around the lowered hand's notch.");
                }
                var handColumn = direction == 2 ? x is >= 12 and < 16 : x is >= 16 and < 20;
                if (y is >= 18 and <= 21 && handColumn && original.A == 0
                    && material.Length > 1 && x > material[0] && x < material[^1])
                {
                    Assert.That(underlay.A, Is.EqualTo(255),
                        "The gap between the native pouch or belt edges must close when the hand is raised.");
                }
            }
        }
        Assert.That(filled, Is.GreaterThan(0), "The native gear's missing hand area must be filled.");
    }

    [Test]
    public void GearUnderlayDoesNotInventTheHiddenSideOfThePhoneBag()
    {
        using var source = Image.Load<Rgba32>(Path.Combine(SourceRoot(),
            "Resources/Textures/_RMC14/Objects/Clothing/Back/Satchels/Civilians/bag_phone.rsi/equipped-BACKPACK.png"));
        using var map = LoadMap("waist_gear");
        Assert.That(VisiblePixels(source, map, 3), Is.Empty,
            "The native west-facing phone bag has no pouch in this area; it must not acquire one from the fill.");
    }

    [TestCase(2, "pve_undershirt.rsi/undershirt_uscm_short.png", 13, 19)]
    [TestCase(3, "pve_undershirt.rsi/undershirt_uscm_short.png", 18, 19)]
    [TestCase(2, "pve_undershirt.rsi/undershirt_black.png", 13, 19)]
    [TestCase(3, "pve_undershirt.rsi/undershirt_black.png", 18, 19)]
    [TestCase(2, "pve_underwear.rsi/army_boxers.png", 13, 21)]
    [TestCase(3, "pve_underwear.rsi/army_boxers.png", 18, 21)]
    [TestCase(2, "pve_underwear.rsi/black_briefs.png", 13, 21)]
    [TestCase(3, "pve_underwear.rsi/black_briefs.png", 18, 21)]
    public void UndergarmentHandNotchesHaveMatchingFill(int direction, string garment, int x, int y)
    {
        using var source = Image.Load<Rgba32>(Path.Combine(SourceRoot(),
            "Content.CMU/Resources/Textures/CMU14/Mobs/Human_Customization", garment));
        using var map = LoadMap("waist_gear");
        Assert.That(FramePixel(source, direction, x, y).A, Is.Zero,
            "The reference must contain the native hand-shaped hole.");
        var fill = Sample(source, map, direction, x, y);
        Assert.That(fill.A, Is.EqualTo(255),
            "The raised hand must leave clothing across the waist instead of an empty hand outline.");
        Assert.That(Enumerable.Range(0, 32).Select(column => FramePixel(source, direction, column, y)),
            Does.Contain(fill), "The fill must retain a colour from the same row of the original garment.");
    }

    private static IEnumerable<TestCaseData> GarmentArmCases()
    {
        foreach (var kind in new[] { "clothing", "armor" })
        {
            foreach (var suffix in new[] { "none", "left", "right" })
            {
                for (var direction = 0; direction < 4; direction++)
                    yield return new TestCaseData(kind, suffix, direction);
            }
        }
    }

    [TestCaseSource(nameof(GarmentArmCases))]
    public void MissingArmsDoNotAcquireRaisedSleeves(string kind, string suffix, int direction)
    {
        var path = kind == "armor"
            ? "OuterClothing/Armor/m3/standard/padded/jungle.rsi/equipped-OUTERCLOTHING.png"
            : "Uniforms/Marine/rto.rsi/equipped-INNERCLOTHING.png";
        using var source = Image.Load<Rgba32>(Path.Combine(SourceRoot(),
            "Resources/Textures/_RMC14/Objects/Clothing", path));
        using var full = LoadMap(direction < 2 ? kind : "foreground_" + kind);
        using var partial = LoadMap((direction < 2 ? kind : "foreground_" + kind) + "_" + suffix);
        var checkedPixels = 0;
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                if (direction < 2 && (y >= 12 || x is >= 11 and < 20))
                    continue;
                var expected = Sample(source, full, direction, x, y);
                if (expected.A == 0)
                    continue;
                checkedPixels++;
                var left = direction == 3 || (direction == 0 ? x >= 16 : direction == 1 && x < 16);
                var present = suffix == (left ? "left" : "right");
                Assert.That(Sample(source, partial, direction, x, y), Is.EqualTo(present ? expected : default(Rgba32)),
                    "Missing arms must not gain sleeves; retained arms must keep their original raised artwork.");
            }
        }
        Assert.That(checkedPixels, Is.GreaterThan(0), "The reference garment must have a visible raised sleeve.");
    }

    [TestCase(2, 10, 13)]
    [TestCase(3, 22, 14)]
    public void ArmorCutoutRetainsTheWideShoulderPad(int direction, int sourceX, int sourceY)
    {
        using var armor = Image.Load<Rgba32>(Path.Combine(SourceRoot(),
            "Resources/Textures/_RMC14/Objects/Clothing/OuterClothing/Armor/m3/standard/padded/jungle.rsi/equipped-OUTERCLOTHING.png"));
        using var map = LoadMap("foreground_armor");
        var original = FramePixel(armor, direction, sourceX, sourceY);
        Assert.That(original.A, Is.EqualTo(255));
        var pivot = direction == 2 ? 14 : 17;
        Assert.That(Sample(armor, map, direction, 2 * pivot - sourceX, 22 - sourceY), Is.EqualTo(original),
            "A shoulder pad wider than the uniform sleeve must move with the armor, using its own artwork.");
    }

    [Test]
    public void EveryPoseMapSamplesWithinItsOwnSpriteFrame()
    {
        var directory = Path.Combine(SourceRoot(), "Content.CMU/Resources/Textures/CMU14/Mobs/Surrender/pose.rsi");
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "meta.json")));
        foreach (var state in metadata.RootElement.GetProperty("states").EnumerateArray())
        {
            using var map = LoadMap(state.GetProperty("name").GetString()!);
            for (var direction = 0; direction < 4; direction++)
            {
                for (var y = 0; y < 32; y++)
                {
                    for (var x = 0; x < 32; x++)
                    {
                        var pixel = FramePixel(map, direction, x, y);
                        if (pixel.A == 0)
                            continue;
                        Assert.That(x + pixel.R - 128, Is.InRange(0, 31), "A pose must not read another atlas sprite.");
                        Assert.That(y + pixel.G - 128, Is.InRange(0, 31), "A pose must not read another atlas sprite.");
                    }
                }
            }
        }
    }

    [TestCase("torso")]
    [TestCase("clothing")]
    [TestCase("armor")]
    public void SideFillShadingStepsIntoTheNativeChest(string state)
    {
        using var map = LoadMap(state);
        foreach (var direction in new[] { 2, 3 })
        {
            for (var y = 14; y <= 18; y++)
            {
                var shades = Enumerable.Range(0, 6)
                    .Select(i => FramePixel(map, direction, direction == 2 ? 11 + i : 20 - i, y).B)
                    .ToArray();
                Assert.That(shades[0], Is.GreaterThan(shades[1]), "Keep the native-style dark outside contour.");
                Assert.That(shades.Distinct().Count(), Is.GreaterThanOrEqualTo(4),
                    "A one-column outline around a flat fill must not replace the stepped side shading.");
                Assert.That(shades, Is.Ordered.Descending, "The shadow must fade inward in both facing directions.");
                Assert.That(shades[^1], Is.Zero, "The original chest pixels must retain their native shading.");
            }
        }
    }

    [TestCase("clothing")]
    [TestCase("armor")]
    public void UnderarmRimDoesNotDrawAFullDarkHorizontalBar(string state)
    {
        using var map = LoadMap(state);
        foreach (var direction in new[] { 2, 3 })
        {
            var shades = Enumerable.Range(0, 5)
                .Select(i => FramePixel(map, direction, direction == 2 ? 11 + i : 20 - i, 13).B)
                .ToArray();
            Assert.That(shades, Is.Ordered.Descending);
            Assert.That(shades.Distinct().Count(), Is.GreaterThanOrEqualTo(3),
                "The underarm shadow must taper instead of stamping a solid dark bar across the garment.");
            Assert.That(shades[^1], Is.LessThan(shades[0]));
        }
    }

    [Test]
    public void FillShadingOnlyTouchesGeneratedSidePixels()
    {
        var directory = Path.Combine(SourceRoot(), "Content.CMU/Resources/Textures/CMU14/Mobs/Surrender/pose.rsi");
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "meta.json")));
        var marked = 0;
        foreach (var state in metadata.RootElement.GetProperty("states").EnumerateArray())
        {
            var name = state.GetProperty("name").GetString()!;
            using var map = LoadMap(name);
            for (var direction = 0; direction < 4; direction++)
            {
                for (var y = 0; y < 32; y++)
                {
                    for (var x = 0; x < 32; x++)
                    {
                        var pixel = FramePixel(map, direction, x, y);
                        if (pixel.B == 0)
                            continue;
                        marked++;
                        Assert.That(direction, Is.GreaterThanOrEqualTo(2), "Front and back views must keep their native shading.");
                        Assert.That(name.StartsWith("foreground_") || name == "waist_gear" || name.StartsWith("limbs"), Is.False,
                            "Raised cutout artwork and waist-level details already have their native outlines.");
                        Assert.That(pixel.A, Is.EqualTo(255));
                        Assert.That(pixel.R != 128 || pixel.G != 128, Is.True,
                            "Only generated fill pixels may be darkened, leaving original texture detail intact.");
                    }
                }
            }
        }
        Assert.That(marked, Is.GreaterThan(0));
    }

    private static Rgba32 Sample(Image<Rgba32> source, Image<Rgba32> map, int direction, int x, int y)
    {
        var offset = FramePixel(map, direction, x, y);
        if (offset.A == 0)
            return default;

        var sx = x + offset.R - 128;
        var sy = y + offset.G - 128;
        Assert.That(sx, Is.InRange(0, 31), "A pose must sample its own frame, not neighbouring atlas sprites.");
        Assert.That(sy, Is.InRange(0, 31), "A pose must sample its own frame, not neighbouring atlas sprites.");
        return FramePixel(source, direction, sx, sy);
    }

    private static Rgba32 FramePixel(Image<Rgba32> image, int direction, int x, int y)
    {
        var columns = image.Width / 32;
        return image[direction % columns * 32 + x, direction / columns * 32 + y];
    }

    private static string SourceRoot()
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "SpaceStation14.slnx")))
            root = root.Parent;

        Assert.That(root, Is.Not.Null, "The pose asset test needs the source checkout.");
        return root!.FullName;
    }
}
