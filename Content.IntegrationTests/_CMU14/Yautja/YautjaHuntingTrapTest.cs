using System.Linq;
using Content.Server.Cargo.Components;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHuntingTrapTest
{
    [Test]
    public async Task HuntingTrapStaticPrototypeMatchesCmss13SourceFacts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var client = pair.Client;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trap = entMan.SpawnEntity("CMUYautjaHuntingTrap", MapCoordinates.Nullspace);

            try
            {
                var meta = entMan.GetComponent<MetaDataComponent>(trap);
                var item = entMan.GetComponent<ItemComponent>(trap);
                var tech = entMan.GetComponent<YautjaTechItemComponent>(trap);
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                var price = entMan.GetComponent<StaticPriceComponent>(trap);

                Assert.Multiple(() =>
                {
                    Assert.That(meta.EntityName, Is.EqualTo("hunting trap"),
                        "CMSS13 /obj/item/hunting_trap source name.");
                    Assert.That(meta.EntityDescription, Is.EqualTo("A bizarre Yautja device used for trapping and killing prey."),
                        "CMSS13 /obj/item/hunting_trap source description.");
                    Assert.That(item.Size.Id, Is.EqualTo("Small"),
                        "CMSS13 /obj/item/hunting_trap local item size mapping.");
                    Assert.That(price.Price, Is.EqualTo(50),
                        "CMSS13 /obj/item/hunting_trap black_market_value = 50.");
                    Assert.That(trapComp.TetherRange, Is.EqualTo(2f),
                        "A hunting trap holds prey within two tiles.");
                    Assert.That(trapComp.CanConfigureRange, Is.False,
                        "The hunting trap does not expose the obsolete configurable range menu.");
                    Assert.That(trapComp.TrapDuration, Is.EqualTo(TimeSpan.FromSeconds(30)),
                        "CMSS13 /obj/item/hunting_trap var/duration = 30 SECONDS.");
                    Assert.That(trapComp.Armed, Is.False,
                        "CMSS13 /obj/item/hunting_trap starts with var/armed = 0.");
                    Assert.That(tech.BlockPickup, Is.False,
                        "Local rack/vend handling keeps source ITEM_PREDATOR without blocking Yautja recovery pickup.");
                    Assert.That(tech.BlockUse, Is.False,
                        "Local trap arming handles source ITEM_PREDATOR without the generic use blocker.");
                });
            }
            finally
            {
                if (!entMan.Deleted(trap))
                    entMan.DeleteEntity(trap);
            }
        });

        await client.WaitAssertion(() =>
        {
            var prototypes = client.ResolveDependency<IPrototypeManager>();
            var factory = client.ResolveDependency<IComponentFactory>();
            var prototype = prototypes.Index<EntityPrototype>("CMUYautjaHuntingTrap");

            Assert.That(prototype.TryGetComponent<SpriteComponent>(out var sprite, factory), Is.True);
            Assert.That(sprite!.BaseRSI?.Path, Is.EqualTo(new ResPath("/Textures/_CMU14/Yautja/yautja_items.rsi")));
            Assert.That(sprite.AllLayers.First().RsiState.Name, Is.EqualTo("yauttrap0"),
                "CMSS13 /obj/item/hunting_trap icon_state = \"yauttrap0\".");
        });

        await pair.CleanReturnAsync();
    }
}
