using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared._RMC14.OrbitalCannon;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.ZLevels.Ordnance;

[TestFixture]
public sealed class CMUOrbitalExplosionZLevelTest
{
    [Test]
    public async Task BlastColumnIncludesEveryConnectedLevelAtTheSamePosition()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var map = entMan.System<SharedMapSystem>();
            var zLevels = entMan.System<CMUZLevelsSystem>();
            var orbital = entMan.System<OrbitalCannonSystem>();
            var lower = map.CreateMap(out var lowerId);
            var middle = map.CreateMap(out var middleId);
            var upper = map.CreateMap(out var upperId);
            var unrelated = map.CreateMap(out var unrelatedId);
            var network = zLevels.CreateZNetwork();

            try
            {
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
                {
                    [lower] = -1,
                    [middle] = 0,
                    [upper] = 1,
                }), Is.True);

                var impact = new MapCoordinates(new Vector2(8.5f, 11.5f), middleId);
                var layers = orbital.GetOrbitalExplosionLayers(impact, true);

                Assert.That(layers.Select(layer => layer.MapId), Is.EquivalentTo(new[] { lowerId, middleId, upperId }));
                Assert.That(layers.All(layer => layer.Position == impact.Position), Is.True);
                Assert.That(orbital.GetOrbitalExplosionLayers(impact, false), Is.EqualTo(new[] { impact }));

                var isolated = new MapCoordinates(impact.Position, unrelatedId);
                Assert.That(orbital.GetOrbitalExplosionLayers(isolated, true), Is.EqualTo(new[] { isolated }));
            }
            finally
            {
                if (!entMan.Deleted(network))
                    entMan.DeleteEntity(network);

                map.DeleteMap(unrelatedId);
                map.DeleteMap(upperId);
                map.DeleteMap(middleId);
                map.DeleteMap(lowerId);
            }
        });

        await pair.CleanReturnAsync();
    }
}
