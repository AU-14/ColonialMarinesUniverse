using System.Linq;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DynamicGunshipMapTest
{
    [Test]
    public async Task DynamicGunshipLoads()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var loader = server.System<MapLoaderSystem>();
            var maps = server.System<SharedMapSystem>();
            var options = DeserializationOptions.Default with { InitializeMaps = false };

            Assert.That(loader.TryLoadMap(
                new ResPath("/Maps/_RMC14/Shuttles/dynamic_gunship.yml"),
                out var map,
                out var grids,
                options), Is.True);

            Assert.That(map, Is.Not.Null);
            Assert.That(grids, Has.Count.EqualTo(1));
            var grid = grids!.Single();
            Assert.That(maps.GetAllTiles(grid.Owner, grid.Comp).Count(), Is.EqualTo(55));

            var descendants = 0;
            CountDescendants(grid.Owner, ref descendants);
            Assert.That(descendants, Is.EqualTo(70));

            maps.DeleteMap(map!.Value.Comp.MapId);
        });

        await pair.CleanReturnAsync();

        void CountDescendants(EntityUid parent, ref int count)
        {
            var enumerator = server.Transform(parent).ChildEnumerator;
            while (enumerator.MoveNext(out var child))
            {
                count++;
                CountDescendants(child, ref count);
            }
        }
    }
}
