using System.Linq;
using Content.Shared.CMU14.Specimens;
using Content.Shared.CMU14.Threats;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Specimens;

[TestFixture]
public sealed class CMUWhiteoutShuttleTest
{
    [Test]
    public async Task ShuttleCarriesTheCrateAndTheTeam()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var path = server.ProtoMan.Index<ThirdPartyPrototype>("WYPMCWhiteout").dropshippath;

        await server.WaitAssertion(() =>
        {
            // Loaded the way ThirdPartySystem loads a party's dropship.
            var options = new MapLoadOptions { DeserializationOptions = DeserializationOptions.Default with { InitializeMaps = true, LogOrphanedGrids = false } };
            var loader = entities.System<MapLoaderSystem>();
            Assert.That(loader.TryLoadGeneric(path, out var result, options), $"Failed to load {path}");
            var grid = result!.Grids.First().Owner;

            bool OnShuttle(EntityUid uid) => entities.GetComponent<TransformComponent>(uid).GridUid == grid;

            var crates = entities.EntityQuery<CMUSpecimenCrateComponent>().Where(c => OnShuttle(c.Owner)).ToList();
            Assert.That(crates, Has.Count.EqualTo(1), "The Whiteout shuttle should carry one specimen crate.");

            var markers = entities.EntityQuery<ThreatSpawnMarkerComponent>().Where(m => m.ThirdParty && OnShuttle(m.Owner)).ToList();
            Assert.That(markers.Any(m => m.ThreatMarkerType == ThreatMarkerType.Leader), "No leader spawn on the Whiteout shuttle.");

            // Every Whiteout party fits on the shuttle without stacking two synths on one marker.
            foreach (var partyId in new[] { "WYPMCWhiteout" })
            {
                var party = server.ProtoMan.Index<ThirdPartyPrototype>(partyId);
                var spawn = server.ProtoMan.Index<PartySpawnPrototype>(party.PartySpawn);
                Assert.That(markers.Count(m => m.ThreatMarkerType == ThreatMarkerType.Member), Is.GreaterThanOrEqualTo(spawn.GruntsToSpawn.Values.Sum()),
                    $"Not enough member spawns for {partyId}.");
            }

            loader.Delete(result);
        });

        await pair.CleanReturnAsync();
    }
}
