using Content.Client.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Doors.Components;

namespace Content.IntegrationTests.CMU14.ThreeD;

[TestFixture]
[TestOf(typeof(CMU3DXenoAppearance))]
public sealed class CMU3DXenoAppearanceTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Fresh = true, Dirty = true };

    [Test]
    public async Task SourceFramesOpenPassagesAndHiddenLayersDoNotBecomeGeometry()
    {
        await Client.WaitAssertion(() =>
        {
            using var lease = Client.ResolveDependency<CMU3DModelLibrary>().AcquireWorld();
            var models = CProtoMan.EnumeratePrototypes<CMU3DModelPrototype>().ToArray();
            var catalog = new CMU3DSceneCatalog(models, _ => null);
            var door = catalog.Resolve("DoorXenoResin");
            Assert.That(catalog.WithDoorState(door, DoorState.Opening), Is.EqualTo(door));
            var model = door.Value.Model;
            var rsi = model.ReferenceRsi;
            var closed = new CMU3DButtonLayer(rsi, "resin", 0, true, Color.White, true);
            Assert.That(CMU3DXenoAppearance.TryParts(model, [closed], out var closedParts), Is.True);
            Assert.That(Occupies(closedParts, new Vector3(0, 0, 1.2f)), Is.True);
            var opening = model.XenoStates[rsi]["resinopening"];
            Assert.That(CMU3DXenoAppearance.TryParts(model,
                [closed with { State = "resinopening", Frame = opening.Frames.Count - 1 }], out var openParts), Is.True);
            Assert.That(Occupies(openParts, new Vector3(0, 0, 1.2f)), Is.False,
                "The source's final opening frame must retract the modeled leaf out of the passage.");
            Assert.That(CMU3DXenoAppearance.TryParts(model,
                [closed with { State = "resinclosing", Frame = 0 }], out var closingStart), Is.True);
            Assert.That(Occupies(closingStart, new Vector3(0, 0, 1.2f)), Is.False);

            var egg = catalog.Resolve("XenoEgg").Value.Model;
            var shell = new CMU3DButtonLayer(egg.ReferenceRsi, "egg", 0, true, Color.White, true);
            Assert.That(CMU3DXenoAppearance.TryParts(egg, [shell], out var wholeEgg), Is.True);
            Assert.That(Occupies(wholeEgg, new Vector3(0, 0, .65f)), Is.True);
            Assert.That(CMU3DXenoAppearance.TryParts(egg, [shell with { State = "egg_opened" }], out var hatchedEgg), Is.True);
            Assert.That(Occupies(hatchedEgg, new Vector3(0, 0, .65f)), Is.False,
                "Hatching must expose the egg's mouth instead of keeping the closed shell.");

            var weeds = catalog.Resolve("XenoWeedsSourceHidden").Value.Model;
            var root = new CMU3DButtonLayer(weeds.ReferenceRsi, "weed0", 0, true, Color.White, true);
            var marker = new CMU3DButtonLayer("/Textures/_RMC14/Markers/landmarks.rsi", "weednode", 0, false, Color.White, true);
            Assert.That(CMU3DXenoAppearance.TryParts(weeds, [root], out var roots), Is.True);
            Assert.That(CMU3DXenoAppearance.TryParts(weeds, [root, marker], out var hiddenMarker), Is.True);
            Assert.That(hiddenMarker, Is.EqualTo(roots));
            Assert.That(CMU3DXenoAppearance.TryParts(weeds, [root, marker with { Visible = true }], out var shownMarker), Is.True);
            Assert.That(shownMarker.Count, Is.GreaterThan(roots.Count));
            Assert.That(CMU3DXenoAppearance.TryParts(weeds,
                [root, marker with { Visible = true, State = "unmodeled_state" }], out _), Is.False,
                "Unknown visible layers must retain the source sprite, not disappear.");
        });
    }

    private static bool Occupies(IReadOnlyList<CMU3DModelPart> parts, Vector3 point)
    {
        foreach (var part in parts)
        {
            part.Bounds(out var min, out var max);
            if (point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y &&
                point.Z >= min.Z && point.Z <= max.Z)
                return true;
        }
        return false;
    }
}
