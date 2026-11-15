using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Clothing;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Xenonids;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaHuntPreyGearTest
{
    [Test]
    public async Task HuntPreyLandWithoutNightVisionOrIrradiatedAmmo()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var before = new HashSet<EntityUid>();
        var preyPrototypes = new List<EntProtoId>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var protoMan = server.ProtoMan;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            entMan.EnsureComponent<YautjaComponent>(hunter);
            var flightConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13", map.GridCoords);
            var huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);

            entMan.EventBus.RaiseLocalEvent(flightConsole, new YautjaHuntingGroundSelectedEvent(entMan.GetNetEntity(hunter), "jungle_moon"));

            var query = entMan.EntityQueryEnumerator<YautjaHuntPreyComponent>();
            while (query.MoveNext(out var uid, out _))
                before.Add(uid);

            // every human kit the console can roll, one call each so none of them hides behind the weighted pick
            var console = entMan.GetComponent<YautjaHuntConsoleComponent>(huntsmasterConsole);
            var factory = entMan.ComponentFactory;
            preyPrototypes.AddRange(console.HuntCallOptions
                .SelectMany(o => o.Spawns)
                .Select(s => s.EntityPrototype)
                .OfType<EntProtoId>()
                .Distinct()
                .Where(id => !protoMan.Index(id).Components.ContainsKey(factory.GetComponentName<XenoComponent>())));

            Assert.That(preyPrototypes, Does.Contain(new EntProtoId("CMUYautjaHuntPMCSmartgunner")));

            console.HuntCooldown = TimeSpan.Zero;
            foreach (var prototype in preyPrototypes)
            {
                console.HuntCallOptions = new List<YautjaHuntCallOption>
                {
                    new()
                    {
                        Id = "test_single",
                        DisplayName = "test",
                        SpawnCount = 1,
                        Spawns = { new YautjaHuntSpawnEntry { EntityPrototype = prototype } },
                    },
                };
                entMan.EventBus.RaiseLocalEvent(huntsmasterConsole, new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), "test_single"));
            }
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new Dictionary<string, EntityUid>();
            var query = entMan.EntityQueryEnumerator<YautjaHuntPreyComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out _, out var meta))
            {
                if (!before.Contains(uid) && meta.EntityPrototype is { } proto)
                    spawned[proto.ID] = uid;
            }

            Assert.That(spawned.Keys, Is.EquivalentTo(preyPrototypes.Select(p => p.Id)));

            var smartgunMags = 0;
            var m42aScopes = 0;
            foreach (var (proto, prey) in spawned)
            {
                Assert.That(entMan.HasComponent<NightVisionComponent>(prey), Is.False, $"{proto} sees in the dark");

                var contained = new List<EntityUid>();
                Collect(entMan, prey, contained);
                foreach (var item in contained)
                {
                    var itemProto = entMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID ?? string.Empty;
                    Assert.That(itemProto, Does.Not.Contain("irradiated").IgnoreCase, $"{proto} carries {itemProto}");
                    Assert.That(entMan.HasComponent<NightVisionItemComponent>(item) ||
                                entMan.HasComponent<NightVisionVisorComponent>(item),
                        Is.False,
                        $"{proto} carries working night vision {itemProto}");

                    if (entMan.HasComponent<CMUHelmetGarbNightVisionComponent>(item))
                        Assert.That(entMan.HasComponent<YautjaPreyJammedOpticsComponent>(item), Is.True, $"{proto} helmet NVGs aren't jammed");

                    if (proto == "CMUYautjaHuntPMCSmartgunner" && itemProto == "RMCMagazineSmartGun")
                        smartgunMags++;

                    // gear stays, the NV is just dead and says so
                    if (proto == "CMUYautjaHuntPMCContractorM42A" && itemProto == "RMCM96SIntegratedScope")
                    {
                        m42aScopes++;
                        Assert.That(entMan.HasComponent<YautjaPreyJammedOpticsComponent>(item), Is.True);
                    }
                }
            }

            Assert.That(m42aScopes, Is.EqualTo(1), "the integrated scope stays on the rifle, it only loses the night vision");

            // the drum pouch rolls irradiated drums, they come back as plain ones instead of vanishing
            Assert.That(smartgunMags, Is.GreaterThan(1), "pmc smartgunner prey kept their drums");
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("/Maps/CMU14/Ships/HunterShip/huntership.yml")]
    [TestCase("/Maps/CMU14/Ships/HunterShip/huntership_lower.yml")]
    public async Task HunterShipStocksRealTrapsAndRopes(string path)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var loader = entMan.System<MapLoaderSystem>();
            Assert.That(loader.TryLoadMap(new ResPath(path), out var map, out _, DeserializationOptions.Default with { InitializeMaps = true }), Is.True);

            var traps = 0;
            var ropes = 0;
            var query = entMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var meta, out var xform))
            {
                if (xform.MapUid != map!.Value.Owner)
                    continue;

                var id = meta.EntityPrototype?.ID;
                Assert.That(id, Does.Not.StartWith("CMUHunterShipPlacedCMUYautjaHuntingTrap"));
                Assert.That(id, Does.Not.StartWith("CMUHunterShipPlacedCMUYautjaMetalChainsCoil"));

                // the mapped ones used to be chains dressed up as rope, they never hung anyone
                if (id == "CMUYautjaRope")
                {
                    ropes++;
                    Assert.That(entMan.HasComponent<YautjaRopeComponent>(uid), Is.True);
                }

                if (id == "CMUYautjaHuntingTrap")
                {
                    traps++;
                    Assert.That(entMan.HasComponent<YautjaTrapComponent>(uid), Is.True);
                }
            }

            Assert.That(traps, Is.GreaterThanOrEqualTo(16));
            Assert.That(ropes, Is.GreaterThanOrEqualTo(6));
        });

        await pair.CleanReturnAsync();
    }

    private static void Collect(IEntityManager entMan, EntityUid uid, List<EntityUid> into)
    {
        if (!entMan.TryGetComponent(uid, out ContainerManagerComponent? manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                into.Add(contained);
                Collect(entMan, contained, into);
            }
        }
    }
}
