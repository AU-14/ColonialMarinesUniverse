using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.VendorMarker;
using Content.Shared._RMC14.Dropship;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class BushDropshipPadTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task FallujahTilesAlignWithBothBushHangarPads()
    {
        await Server.WaitAssertion(() =>
        {
            var loader = Server.System<MapLoaderSystem>();
            var options = DeserializationOptions.Default with { InitializeMaps = false };
            Assert.That(loader.TryLoadMap(new ResPath("/Maps/CMU14/USSBushMultiZ/USSBushMultiZ0.yml"),
                out var bush, out _, options), Is.True);
            Assert.That(loader.TryLoadMap(new ResPath("/Maps/CMU14/Shuttles/alamo.yml"),
                out _, out var ships, options), Is.True);
            var hull = ships!.Single().Comp.LocalAABB;
            var pads = new List<(EntityUid Uid, MetaDataComponent Metadata, TransformComponent Transform)>();
            var query = SEntMan.AllEntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var metadata, out var transform))
            {
                if (metadata.EntityPrototype?.ID != "dropshipdestshipmarker" || transform.MapUid != bush!.Value.Owner)
                    continue;

                pads.Add((uid, metadata, transform));
            }
            Assert.That(pads, Has.Count.EqualTo(2));
            foreach (var (uid, metadata, transform) in pads)
            {
                var marker = new Entity<VendorMarkerComponent>(uid, SEntMan.GetComponent<VendorMarkerComponent>(uid));
                var destination = (EntityUid) typeof(PlatoonSpawnRuleSystem)
                    .GetMethod("SpawnDropshipDestination", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(Server.System<PlatoonSpawnRuleSystem>(), new object[] { marker, transform, "govfor" })!;
                var origin = SEntMan.GetComponent<TransformComponent>(destination).LocalPosition +
                    SEntMan.GetComponent<DropshipDestinationComponent>(destination).LandingOffset;
                Assert.That(origin, Is.EqualTo(new Vector2(MathF.Floor(origin.X), MathF.Floor(origin.Y))),
                    "A tile-aligned ship needs an integer grid origin, not a tile-centered landing marker.");
                var bounds = hull.Translated(origin);
                // The authored pad interiors span x [-14,-3] / [2,13], y [5,26].
                var pad = metadata.EntityName.EndsWith("1")
                    ? new Box2(-14, 5, -3, 26)
                    : new Box2(2, 5, 13, 26);
                Assert.That(bounds.Center.X, Is.EqualTo(pad.Center.X), "Fallujah must be horizontally centered.");
                Assert.That(pad.Contains(bounds), Is.True, "The full tile footprint must remain inside the marked pad.");
            }
        });
    }
}
