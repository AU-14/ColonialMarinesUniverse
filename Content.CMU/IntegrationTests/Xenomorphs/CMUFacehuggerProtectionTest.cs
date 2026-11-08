using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Inventory;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUFacehuggerProtectionTest
{
    [TestPrototypes]
    private const string Prototypes = """
    - type: entity
      parent: RMCMarineHelmetBase
      id: CMUTestInfiniteFacehuggerProtection
      components:
      - type: CMUFacehuggerProtection
        infinite: true

    - type: entity
      parent: RMCMarineHelmetBase
      id: CMUTestLimitedFacehuggerProtection
      components:
      - type: CMUFacehuggerProtection
        maxUses: 2
    """;

    [TestCase("CMUTestInfiniteFacehuggerProtection", 1, false)]
    [TestCase("CMUTestLimitedFacehuggerProtection", 3, true)]
    public async Task ProtectionBlocksWhileWorn(string helmetPrototype, int attempts, bool shouldAttach)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;

        EntityUid host = default;
        EntityUid parasite = default;
        EntityUid helmet = default;

        await pair.Server.WaitAssertion(() =>
        {
            host = entities.SpawnAtPosition("CMMobHuman", map.GridCoords);
            parasite = entities.SpawnAtPosition("CMXenoParasite", map.GridCoords);
            helmet = entities.SpawnAtPosition(helmetPrototype, map.GridCoords);

            Assert.That(entities.System<InventorySystem>().TryEquip(host, helmet, "head", silent: true, force: true), Is.True);
        });

        await pair.RunSeconds(1.1f);
        await pair.Server.WaitAssertion(() =>
        {
            var parasiteComponent = entities.GetComponent<XenoParasiteComponent>(parasite);
            for (var i = 0; i < attempts; i++)
            {
                var attached = entities.System<SharedXenoParasiteSystem>().Infect((parasite, parasiteComponent), host, force: true);
                Assert.That(attached, Is.EqualTo(shouldAttach && i == attempts - 1));
            }

            var protection = entities.GetComponent<CMUFacehuggerProtectionComponent>(helmet);
            Assert.That(protection.Uses, Is.EqualTo(shouldAttach ? 2 : 0));
            Assert.That(entities.System<InventorySystem>().TryGetSlotEntity(host, "head", out var wornHelmet), Is.EqualTo(!shouldAttach));
            if (!shouldAttach)
                Assert.That(wornHelmet, Is.EqualTo(helmet));
        });

        await pair.CleanReturnAsync();
    }
}
