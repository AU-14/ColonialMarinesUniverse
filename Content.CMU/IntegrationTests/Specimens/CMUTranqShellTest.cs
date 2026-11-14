using System.Linq;
using Content.Server.CMU14.Specimens;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Specimens;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Specimens;

[TestFixture]
[TestOf(typeof(CMUTranqShellSystem))]
public sealed class CMUTranqShellTest
{
    private static FixedPoint2 Units(IEntityManager entities, SharedSolutionContainerSystem solutions, EntityUid uid, string solution = "tranq")
    {
        Assert.That(solutions.TryGetSolution(uid, solution, out _, out var soln), Is.True, $"{uid} has no {solution} solution.");
        return soln!.Volume;
    }

    [Test]
    public async Task DoseFollowsTheShells()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var solutions = server.System<SharedSolutionContainerSystem>();
            var stacks = server.System<SharedStackSystem>();

            // Five Somnazine tranquilizer slugs hold 5u of Somnazine each.
            var handful = entities.SpawnEntity("CMUShellShotgunTranquilizerSomnazine", map.GridCoords);
            Assert.That(Units(entities, solutions, handful), Is.EqualTo(FixedPoint2.New(25)));

            // Taking one off (as loading a gun does) takes its 5u with it.
            var one = stacks.GetOne(handful);
            Assert.That(one, Is.Not.EqualTo(handful));
            Assert.That(Units(entities, solutions, one), Is.EqualTo(FixedPoint2.New(5)));
            Assert.That(Units(entities, solutions, handful), Is.EqualTo(FixedPoint2.New(20)));

            // An empty tranquilizer shell merged in brings nothing, and the dose isn't duplicated.
            var empty = entities.SpawnEntity("CMUShellShotgunTranquilizer", map.GridCoords);
            stacks.SetCount(empty, 1);
            Assert.That(Units(entities, solutions, empty), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(stacks.TryMergeStacks(empty, handful, out _), Is.True);
            Assert.That(Units(entities, solutions, handful), Is.EqualTo(FixedPoint2.New(20)));
            Assert.That(entities.GetComponent<StackComponent>(handful).Count, Is.EqualTo(5));

            // The loaded shell's dose goes into its slug when fired.
            var slug = entities.SpawnEntity("CMUPelletShotgunTranquilizer", map.GridCoords);
            entities.EventBus.RaiseLocalEvent(one, new AmmoShotEvent { FiredProjectiles = new List<EntityUid> { slug } });
            Assert.That(Units(entities, solutions, one), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(Units(entities, solutions, slug, "ammo"), Is.EqualTo(FixedPoint2.New(5)));

            entities.DeleteEntity(handful);
            entities.DeleteEntity(one);
            entities.DeleteEntity(slug);
            if (entities.EntityExists(empty))
                entities.DeleteEntity(empty);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SomnazineSedatesXenosAndKnocksOutHumans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        EntityUid human = default;
        await server.WaitAssertion(() =>
        {
            var solutions = server.System<SharedSolutionContainerSystem>();
            var capture = server.System<CMUSpecimenCaptureSystem>();

            EntityUid LoadedSlug()
            {
                var slug = entities.SpawnEntity("CMUPelletShotgunTranquilizer", map.GridCoords);
                Assert.That(solutions.TryGetSolution(slug, "ammo", out var soln, out _), Is.True);
                solutions.TryAddReagent(soln!.Value, CMUTranqShellSystem.Somnazine.Id, FixedPoint2.New(5), out _);
                return slug;
            }

            // A xenonid has no bloodstream: each full slug is one capture hit.
            var xeno = entities.SpawnEntity("CMXenoDrone", map.GridCoords);
            var threshold = (int) capture.GetSedationThreshold(xeno);
            for (var i = 0; i < threshold; i++)
            {
                var slug = LoadedSlug();
                var hit = new ProjectileHitEvent(new DamageSpecifier(), xeno);
                entities.EventBus.RaiseLocalEvent(slug, ref hit);
                entities.DeleteEntity(slug);
            }

            Assert.That(capture.IsSedated(xeno), Is.True, "Somnazine slugs didn't sedate the xenonid.");
            entities.DeleteEntity(xeno);

            // A person gets the dose in their bloodstream.
            human = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var humanSlug = LoadedSlug();
            var humanHit = new ProjectileHitEvent(new DamageSpecifier(), human);
            entities.EventBus.RaiseLocalEvent(humanSlug, ref humanHit);
            entities.DeleteEntity(humanSlug);
        });

        await server.WaitRunTicks((int) (3 / server.Timing.TickPeriod.TotalSeconds));
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.HasComponent<Content.Shared._RMC14.Stun.RMCUnconsciousComponent>(human), Is.True,
                "Somnazine didn't knock the human out.");
            entities.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }
}
