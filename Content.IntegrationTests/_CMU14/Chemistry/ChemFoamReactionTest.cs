using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Chemistry;

// playtest: mfhs grenades popped and nothing came out. reactions don't pass a reagent
// context, so AreaReactionEffect bailed before spawning anything. this checks the foam
// actually shows up, from the real grenade and from a plain beaker mix
[TestFixture]
public sealed class ChemFoamReactionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    // the effect proto RMCAluminiumMetalFoam spawns, which later hardens into RMCFoamedAluminiumMetal
    private const string FoamEffect = "RMCAluminiumMetalFoamEffect";

    [Test]
    public async Task MfhsGrenadeSpawnsMetalFoam()
    {
        var map = await Pair.CreateTestMap();
        EntityUid grenade = default;

        await Server.WaitAssertion(() =>
        {
            grenade = SEntMan.SpawnEntity("RMCGrenadeMFHS", map.GridCoords);
            Assert.That(CountFoam(), Is.Zero);
            // straight to the trigger, skipping the fuse timer
            Server.System<TriggerSystem>().Trigger(grenade);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(CountFoam(), Is.GreaterThan(0), "mfhs went off but no metal foam spawned");
        });
    }

    // same reaction without the payload plumbing, so a grenade-side break doesn't hide a reaction-side one
    [Test]
    public async Task BeakerMixSpawnsMetalFoam()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var solutions = Server.System<SharedSolutionContainerSystem>();
            var beaker = SEntMan.SpawnEntity("CMBeaker", map.GridCoords);
            var solutionName = SEntMan.GetComponent<FitsInDispenserComponent>(beaker).Solution;
            Assert.That(solutions.TryGetSolution(beaker, solutionName, out var soln, out _), Is.True);

            // aluminium first, then the agent + acid, so the reaction fires on the second add
            solutions.TryAddReagent(soln!.Value, "RMCAluminum", 6, out _);
            Assert.That(CountFoam(), Is.Zero);
            solutions.TryAddReagent(soln.Value, "FoamingAgent", 2, out _);
            solutions.TryAddReagent(soln.Value, "PolytrinicAcid", 2, out _);
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(CountFoam(), Is.GreaterThan(0), "foam reaction in a beaker spawned nothing");
        });
    }

    private int CountFoam()
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var meta))
        {
            if (meta.EntityPrototype?.ID == FoamEffect)
                count++;
        }

        return count;
    }
}
