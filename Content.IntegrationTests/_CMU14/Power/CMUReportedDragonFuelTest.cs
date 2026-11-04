using Content.Server.Power.Generator;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Stacks;

namespace Content.IntegrationTests.CMU14.Power;

[TestFixture]
public sealed class CMUReportedDragonFuelTest
{
    [TestCase(0)]
    [TestCase(1)]
    public async Task DragonAcceptsOnlyThePhoronSheetsThatFit(int initialSheets)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var generator = entities.SpawnEntity("CMUGeneratorPortableDragon", map.GridCoords);
            var materials = entities.System<SharedMaterialStorageSystem>();
            var generators = entities.System<GeneratorSystem>();
            if (initialSheets != 0)
            {
                var single = entities.SpawnEntity("CMSheetPhoron1", map.GridCoords);
                var singleUse = new InteractUsingEvent(user, single, generator, map.GridCoords);
                entities.EventBus.RaiseLocalEvent(generator, singleUse);
                Assert.That(singleUse.Handled, Is.True);
                Assert.That(generators.GetFuel(generator), Is.EqualTo(1).Within(0.001));
            }

            var stack = entities.SpawnEntity("CMSheetPhoron", map.GridCoords);
            var hands = entities.System<SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(user, stack, checkActionBlocker: false), Is.True);
            var fill = new InteractUsingEvent(user, stack, generator, map.GridCoords);
            entities.EventBus.RaiseLocalEvent(generator, fill);
            Assert.That(fill.Handled, Is.True);
            Assert.That(materials.GetMaterialAmount(generator, "CMPhoron"), Is.EqualTo(60000));
            Assert.That(generators.GetFuel(generator), Is.EqualTo(30).Within(0.001));
            Assert.That(entities.GetComponent<StackComponent>(stack).Count, Is.EqualTo(20 + initialSheets));
            Assert.That(hands.IsHolding(user, stack), Is.True, "Unused fuel remains in the user's hand.");

            var full = new InteractUsingEvent(user, stack, generator, map.GridCoords);
            entities.EventBus.RaiseLocalEvent(generator, full);
            Assert.That(full.Handled, Is.False);
            Assert.That(entities.GetComponent<StackComponent>(stack).Count, Is.EqualTo(20 + initialSheets));
        });
        await pair.CleanReturnAsync();
    }
}
