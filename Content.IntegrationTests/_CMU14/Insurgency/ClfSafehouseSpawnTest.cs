using Content.Server.CMU14.Threats.Mobs.CLF;
using Content.Server.Spawners.Components;
using Content.Server.Station.Systems;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.IntegrationTests.CMU14.Insurgency;

[TestFixture]
public sealed class ClfSafehouseSpawnTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task SappersShareLeaderSafehouseRegardlessOfSpawnOrder(bool sapperFirst)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var transform = entities.System<SharedTransformSystem>();
            var spawning = entities.System<ClfSpawnSystem>();
            var random = pair.Server.ResolveDependency<IRobustRandom>();
            entities.SpawnEntity("AU14SpawnPointCLFCellLeader", map.GridCoords);
            entities.SpawnEntity("AU14SpawnPointCLFCellLeader", map.GridCoords.Offset(new Vector2(2, 0)));
            AddJobMarker(entities, map.GridCoords.Offset(new Vector2(4, 0)), "AU14JobCivilianColonist");

            var jobs = sapperFirst
                ? new[] { "AU14JobCLFSapper", "AU14JobCLFCellLeader", "AU14JobCLFSapper" }
                : new[] { "AU14JobCLFCellLeader", "AU14JobCLFSapper", "AU14JobCLFSapper" };
            EntityUid leader = default;
            var sappers = new List<EntityUid>();
            foreach (var job in jobs)
            {
                // Force a civilian roll so randomly choosing the safehouse cannot hide a routing regression.
                random.SetSeed(1);
                var mob = SpawnPlayer(entities, job);
                var chosen = spawning.GetChosenSafehouse();
                Assert.That(chosen, Is.Not.Null);
                Assert.That(transform.ToMapCoordinates(entities.GetComponent<TransformComponent>(mob).Coordinates),
                    Is.EqualTo(transform.ToMapCoordinates(chosen!.Value)), job);

                if (job == "AU14JobCLFCellLeader")
                    leader = mob;
                else
                {
                    sappers.Add(mob);
                    Assert.That(entities.HasComponent<SapperComponent>(mob), Is.True);
                    Assert.That(entities.HasComponent<CLFMemberComponent>(mob), Is.True);
                    var inventory = entities.System<InventorySystem>();
                    Assert.That(inventory.TryGetSlotEntity(mob, "belt", out var belt), Is.True);
                    Assert.That(entities.GetComponent<MetaDataComponent>(belt!.Value).EntityPrototype!.ID,
                        Is.EqualTo("CMBeltUtilityFilled"));
                    var held = entities.System<SharedHandsSystem>().EnumerateHeld(mob)
                        .Select(uid => entities.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID);
                    Assert.That(held, Is.EquivalentTo(new[] { "AU14CLFHeadset", "AU14SapperTrapToolbox" }));
                }
            }

            Assert.That(sappers, Has.Count.EqualTo(2));
            var safehouse = spawning.GetChosenSafehouse()!.Value;
            transform.SetCoordinates(leader, map.GridCoords.Offset(new Vector2(6, 0)));
            random.SetSeed(1);
            var lateSapper = SpawnPlayer(entities, "AU14JobCLFSapper");
            Assert.That(transform.ToMapCoordinates(entities.GetComponent<TransformComponent>(lateSapper).Coordinates),
                Is.EqualTo(transform.ToMapCoordinates(safehouse)),
                "A later sapper must use the safehouse even after the leader moves.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("AU14JobCLFCellLeader", 1, true)]
    [TestCase("AU14JobCLFRadioOperator", 1, true)]
    [TestCase("AU14JobCLFPhysician", 1, true)]
    [TestCase("AU14JobCLFSurgeon", 1, true)]
    [TestCase("AU14JobCLFGuerilla", 1, false)]
    [TestCase("AU14JobCLFGuerilla", 0, true)]
    public async Task ExistingClfRolesKeepTheirSpawnRoutes(string job, int seed, bool atSafehouse)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var civilian = map.GridCoords.Offset(new Vector2(4, 0));
            entities.SpawnEntity("AU14SpawnPointCLFCellLeader", map.GridCoords);
            AddJobMarker(entities, civilian, "AU14JobCivilianColonist");
            SpawnPlayer(entities, "AU14JobCLFCellLeader");
            pair.Server.ResolveDependency<IRobustRandom>().SetSeed(seed);
            var mob = SpawnPlayer(entities, job);
            var transform = entities.System<SharedTransformSystem>();
            var safehouse = entities.System<ClfSpawnSystem>().GetChosenSafehouse()!.Value;
            Assert.That(transform.ToMapCoordinates(entities.GetComponent<TransformComponent>(mob).Coordinates),
                Is.EqualTo(transform.ToMapCoordinates(atSafehouse ? safehouse : civilian)));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HandledSpawnDoesNotChooseSafehouse()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.SpawnEntity("AU14SpawnPointCLFCellLeader", map.GridCoords);
            var existing = entities.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(4, 0)));
            var ev = new PlayerSpawningEvent("AU14JobCLFSapper", null, null) { SpawnResult = existing };
            entities.EventBus.RaiseEvent(EventSource.Local, ev);
            Assert.That(ev.SpawnResult, Is.EqualTo(existing));
            Assert.That(entities.System<ClfSpawnSystem>().GetChosenSafehouse(), Is.Null);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MissingSafehouseFallsBackToSapperJobMarker()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var coordinates = map.GridCoords.Offset(new Vector2(4, 0));
            var marker = entities.SpawnEntity("AU14SpawnPointCLFSapper", coordinates);
            AddJobMarker(entities, map.GridCoords, "AU14JobCivilianColonist");
            var mob = SpawnPlayer(entities, "AU14JobCLFSapper");
            var transform = entities.System<SharedTransformSystem>();
            Assert.That(transform.ToMapCoordinates(entities.GetComponent<TransformComponent>(mob).Coordinates),
                Is.EqualTo(transform.ToMapCoordinates(entities.GetComponent<TransformComponent>(marker).Coordinates)));
            Assert.That(entities.System<ClfSpawnSystem>().GetChosenSafehouse(), Is.Null);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RoundCleanupClearsChosenSafehouse()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var marker = entities.SpawnEntity("AU14SpawnPointCLFCellLeader", map.GridCoords);
            SpawnPlayer(entities, "AU14JobCLFCellLeader");
            var spawning = entities.System<ClfSpawnSystem>();
            Assert.That(spawning.GetChosenSafehouse(), Is.Not.Null);
            entities.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.That(spawning.GetChosenSafehouse(), Is.Null);

            entities.DeleteEntity(marker);
            var nextSafehouse = map.GridCoords.Offset(new Vector2(4, 0));
            var nextMarker = entities.SpawnEntity("AU14SpawnPointCLFCellLeader", nextSafehouse);
            var mob = SpawnPlayer(entities, "AU14JobCLFSapper");
            var transform = entities.System<SharedTransformSystem>();
            var coordinates = entities.GetComponent<TransformComponent>(nextMarker).Coordinates;
            Assert.That(transform.ToMapCoordinates(entities.GetComponent<TransformComponent>(mob).Coordinates),
                Is.EqualTo(transform.ToMapCoordinates(coordinates)));
        });
        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnPlayer(IEntityManager entities, string job)
    {
        var ev = new PlayerSpawningEvent(new ProtoId<JobPrototype>(job), null, null);
        entities.EventBus.RaiseEvent(EventSource.Local, ev);
        Assert.That(ev.SpawnResult, Is.Not.Null, job);
        return ev.SpawnResult!.Value;
    }

    private static void AddJobMarker(IEntityManager entities, EntityCoordinates coordinates, string job)
    {
        var marker = entities.SpawnEntity(null, coordinates);
        var spawn = entities.AddComponent<SpawnPointComponent>(marker);
        spawn.SpawnType = SpawnPointType.Job;
        spawn.Job = new ProtoId<JobPrototype>(job);
    }
}
