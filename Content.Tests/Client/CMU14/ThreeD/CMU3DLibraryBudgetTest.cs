using System.Collections.Generic;
using System;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Numerics;
using Content.Client.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Shared.CMU14.ThreeD;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using YamlDotNet.RepresentationModel;

namespace Content.Tests.Client.CMU14.ThreeD;

/// <summary>Checks authored geometry against the selected preview renderer's budget without a server dependency.</summary>
[TestFixture]
public sealed class CMU3DLibraryBudgetTest
{
    [Test]
    public void EveryAuthoredModelFitsThePreviewPartitionBudget()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        const string relative = "Content.CMU/Resources/Prototypes/CMU14/ThreeD";
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, relative)))
            directory = directory.Parent;
        Assert.That(directory, Is.Not.Null, "Run from a checkout containing the model source resources.");

        var count = 0;
        foreach (var path in Directory.GetFiles(Path.Combine(directory!.FullName, relative), "*.yml"))
        {
            using var reader = File.OpenText(path);
            var yaml = new YamlStream();
            yaml.Load(reader);
            foreach (YamlMappingNode definition in (YamlSequenceNode) yaml.Documents[0].RootNode)
            {
                if (Scalar(definition, "type") != "cmu3DModel")
                    continue;
                var id = Scalar(definition, "id");
                foreach (var field in new[] { "barricadeDamageStates", "barricadeWiredStates", "barricadeAcidStates", "spriteStates" })
                {
                    if (!definition.Children.TryGetValue(new YamlScalarNode(field), out var frames))
                        continue;
                    foreach (var (key, value) in (YamlMappingNode) frames)
                    {
                        var acid = field == "barricadeAcidStates";
                        var poses = acid
                            ? ((YamlMappingNode) value).Children.Values.Cast<YamlSequenceNode>().SelectMany(p => p.Cast<YamlMappingNode>())
                            : field == "spriteStates"
                                ? ((YamlSequenceNode) ((YamlMappingNode) value).Children[new YamlScalarNode("frames")]).Cast<YamlMappingNode>()
                            : new[] { (YamlMappingNode) value };
                        foreach (var frame in poses)
                        {
                            var pose = new List<CMU3DSceneBox>();
                            foreach (YamlMappingNode part in (YamlSequenceNode) frame.Children[new YamlScalarNode("parts")])
                            {
                                var min = Vector(Scalar(part, "min"));
                                var max = Vector(Scalar(part, "max"));
                                var yaw = part.Children.ContainsKey(new YamlScalarNode("yaw")) ? float.Parse(Scalar(part, "yaw"), CultureInfo.InvariantCulture) * MathF.PI / 180 : 0;
                                var pitch = part.Children.ContainsKey(new YamlScalarNode("pitch")) ? float.Parse(Scalar(part, "pitch"), CultureInfo.InvariantCulture) * MathF.PI / 180 : 0;
                                var shape = part.Children.ContainsKey(new YamlScalarNode("shape")) ? Enum.Parse<CMU3DPartShape>(Scalar(part, "shape")) : CMU3DPartShape.Box;
                                pose.Add(new CMU3DSceneBox((min + max) / 2, (max - min) / 2, yaw, Color.White, new EntityUid(1), shape) { Pitch = pitch });
                            }
                            var stateEncoding = new CMU3DSceneEncoding();
                            stateEncoding.Build(pose);
                            Assert.That(pose.Count, Is.InRange(1, acid ? CMU3DBarricadeAppearance.AcidPartLimit : 128), $"{id} {field} {key}");
                            Assert.That(stateEncoding.AcceptedBoxes, Is.EqualTo(pose.Count), $"{id} {field} {key}");
                        }
                    }
                }
                var model = new CMU3DModelPrototype();
                model.EquipmentOnly = definition.Children.TryGetValue(new YamlScalarNode("equipmentOnly"), out var equipmentOnly) &&
                    ((YamlScalarNode) equipmentOnly).Value == "true";
                if (definition.Children.ContainsKey(new YamlScalarNode("supportSurface")))
                    model.SupportSurface = Scalar(definition, "supportSurface");
                if (definition.Children.TryGetValue(new YamlScalarNode("supportSurfaces"), out var supportLabels))
                    model.SupportSurfaces = ((YamlSequenceNode) supportLabels).Cast<YamlScalarNode>().Select(label => label.Value!).ToArray();
                foreach (YamlMappingNode part in (YamlSequenceNode) definition.Children[new YamlScalarNode("parts")])
                    model.Parts.Add(new CMU3DModelPart { Min = Vector(Scalar(part, "min")), Max = Vector(Scalar(part, "max")),
                        Label = part.Children.ContainsKey(new YamlScalarNode("label")) ? Scalar(part, "label") : string.Empty,
                        Pitch = part.Children.ContainsKey(new YamlScalarNode("pitch")) ?
                            float.Parse(Scalar(part, "pitch"), CultureInfo.InvariantCulture) : 0,
                        Yaw = part.Children.ContainsKey(new YamlScalarNode("yaw")) ?
                            float.Parse(Scalar(part, "yaw"), CultureInfo.InvariantCulture) : 0,
                        OmitWhenConnected = part.Children.ContainsKey(new YamlScalarNode("omitWhenConnected")) ?
                            int.Parse(Scalar(part, "omitWhenConnected"), CultureInfo.InvariantCulture) : 0,
                        Shape = part.Children.TryGetValue(new YamlScalarNode("shape"), out var shape) ?
                            Enum.Parse<CMU3DPartShape>(((YamlScalarNode) shape).Value!) : CMU3DPartShape.Box });
                var supports = new List<CMU3DSceneSurface>();
                CMU3DScenePlacement.CollectSurfaces(model, new EntityUid(1), Vector2.Zero, 0, supports);
                Assert.That(supports.Count, Is.EqualTo(model.SupportSurfaces.Length != 0 ? model.SupportSurfaces.Length :
                    string.IsNullOrEmpty(model.SupportSurface) ? 0 : 1), id);
                var textured = ((YamlSequenceNode) definition.Children[new YamlScalarNode("parts")])
                    .Cast<YamlMappingNode>().Any(part => part.Children.ContainsKey(new YamlScalarNode("surface")));
                if (model.EquipmentOnly || model.Parts.Any(part => part.Shape != CMU3DPartShape.Box || part.Yaw != 0 || part.Pitch != 0) || textured)
                {
                    var bounds = new CMU3DModelRenderer();
                    bounds.SetModel(model);
                    var extent = bounds.Max - bounds.Min;
                    var scale = 14 / Math.Max(model.EquipmentOnly ? .01f : 14, Math.Max(extent.X, Math.Max(extent.Y, extent.Z)));
                    var origin = new Vector3((bounds.Min.X + bounds.Max.X) / 2, (bounds.Min.Y + bounds.Max.Y) / 2, bounds.Min.Z);
                    var encoding = new CMU3DSceneEncoding();
                    encoding.Build(model.Parts.Select(part => new CMU3DSceneBox(((part.Min + part.Max) / 2 - origin) * scale,
                        (part.Max - part.Min) / 2 * scale, part.YawRadians, Color.White, new EntityUid(1), part.Shape) { Pitch = part.PitchRadians }).ToArray());
                    Assert.That(encoding.AcceptedBoxes, Is.EqualTo(model.Parts.Count), id);
                    Assert.That(model.Parts.Count, Is.LessThanOrEqualTo(model.EquipmentOnly ? 512 : 128), id);
                    count++;
                    continue;
                }
                var renderer = new CMU3DModelRenderer();
                renderer.SetModel(model);
                Assert.That(renderer.PartCount, Is.EqualTo(model.Parts.Count), id);
                Assert.That(renderer.WithinBudget, Is.True, $"{id}: authored model cannot be rendered within the partition budget.");
                var faces = new List<CMU3DModelFace>();
                foreach (var eye in new[] { new Vector3(10, -10, 10), new Vector3(-10, 10, -10) })
                {
                    renderer.CollectVisibleFaces(eye, faces);
                    Assert.That(faces.Count, Is.InRange(1, CMU3DModelBsp.MaxFragments), id);
                }
                if (definition.Children.TryGetValue(new YamlScalarNode("connectToNeighbours"), out var connected) &&
                    ((YamlScalarNode) connected).Value == "true")
                {
                    for (var mask = 0; mask < 16; mask++)
                    {
                        var boxes = new List<CMU3DSceneBox>();
                        foreach (var part in CMU3DSceneLayout.ConnectedParts(model.Parts, mask, model.SupportSurface))
                            boxes.Add(new CMU3DSceneBox((part.Min + part.Max) / 2 + new Vector3(.5f, .5f, 0),
                                (part.Max - part.Min) / 2, 0, Color.White, new EntityUid(1)));
                        var encoding = new CMU3DSceneEncoding();
                        encoding.Build(boxes);
                        Assert.That(encoding.AcceptedBoxes, Is.EqualTo(boxes.Count), $"{id}, connection mask {mask}");
                    }
                }
                count++;
            }
        }
        Assert.That(count, Is.GreaterThan(0));
    }

    private static string Scalar(YamlMappingNode node, string key) =>
        ((YamlScalarNode) node.Children[new YamlScalarNode(key)]).Value ?? throw new InvalidDataException($"Missing scalar value: {key}");

    private static Vector3 Vector(string value)
    {
        var coordinates = value.Split(',');
        return new Vector3(float.Parse(coordinates[0], CultureInfo.InvariantCulture),
            float.Parse(coordinates[1], CultureInfo.InvariantCulture), float.Parse(coordinates[2], CultureInfo.InvariantCulture));
    }
}
