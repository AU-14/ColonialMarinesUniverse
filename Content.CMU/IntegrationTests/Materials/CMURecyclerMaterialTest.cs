using Content.Server.Materials;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Materials;

[TestFixture]
public sealed class CMURecyclerMaterialTest
{
    [Test]
    public async Task ReclaimedSteelBecomesConstructionMetalWithoutLosingRemainders()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var material = prototypes.Index<MaterialPrototype>("CMSteel");
            var sheet = prototypes.Index<EntityPrototype>(material.StackEntity!.Value);
            Assert.That(sheet.TryComp<PhysicalCompositionComponent>(out var composition, em.ComponentFactory), Is.True);
            var perSheet = composition!.MaterialComposition["CMSteel"];
            var recycler = em.SpawnEntity("CMURecycler", map.GridCoords);
            var efficiency = em.GetComponent<MaterialReclaimerComponent>(recycler).Efficiency;
            var storage = em.GetComponent<MaterialStorageComponent>(recycler);
            var reclaimer = em.System<MaterialReclaimerSystem>();

            void Recycle(int volume)
            {
                var scrap = em.SpawnEntity(null, map.GridCoords);
                em.AddComponent<PhysicalCompositionComponent>(scrap).MaterialComposition["Steel"] = (int) Math.Ceiling(volume / efficiency);
                reclaimer.Reclaim(recycler, scrap);
            }

            Recycle(perSheet - 1);
            Assert.That(storage.Storage.GetValueOrDefault("CMSteel"), Is.EqualTo(perSheet - 1));
            Recycle(2);
            Assert.That(storage.Storage.GetValueOrDefault("CMSteel"), Is.EqualTo(1),
                "The second batch must complete one sheet and retain the remaining material.");
            Assert.That(storage.Storage.GetValueOrDefault("Steel"), Is.Zero);

            var output = 0;
            var stacks = em.EntityQueryEnumerator<StackComponent, MetaDataComponent, TransformComponent>();
            while (stacks.MoveNext(out _, out var stack, out var metadata, out var transform))
            {
                if (transform.MapUid != map.MapUid)
                    continue;
                Assert.That(metadata.EntityPrototype?.ID, Is.Not.EqualTo("SheetSteel1"));
                if (stack.StackTypeId == "CMSteel")
                    output += stack.Count;
            }
            Assert.That(output, Is.EqualTo(1), "The output must be usable CMU construction material.");
        });
        await pair.CleanReturnAsync();
    }
}
