using System.Numerics;
using Content.Shared._CMU14.Visuals;
using Content.Shared.Containers;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Weapons;

[TestFixture]
public sealed class GenericGunRackPrototypeTest
{
    [Test]
    public async Task GenericRackHasTwoEmptyGunOnlySlotsAndIconVisuals()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var factory = server.EntMan.ComponentFactory;
                var rackId = "CMUGunRackGenericEmpty";

                Assert.That(prototypes.TryIndex<EntityPrototype>(rackId, out var rack), Is.True);
                Assert.That(rack!.TryComp<ItemSlotsComponent>(out var slots, factory), Is.True);
                Assert.That(rack.TryComp<CMUGunRackVisualizerComponent>(out var visuals, factory), Is.True);
                Assert.That(rack.TryComp<ContainerFillComponent>(out _, factory), Is.False);

                Assert.Multiple(() =>
                {
                    Assert.That(slots!.Slots, Has.Count.EqualTo(2));
                    Assert.That(visuals!.Slots, Is.EqualTo(new[] { "item_1", "item_2" }));
                    Assert.That(visuals.Offsets, Is.EqualTo(new[]
                    {
                        new Vector2(-0.125f, 0),
                        new Vector2(0.125f, 0),
                    }));
                });

                foreach (var slotId in new[] { "item_1", "item_2" })
                {
                    var slot = slots!.Slots[slotId];
                    Assert.Multiple(() =>
                    {
                        Assert.That(slot.StartingItem, Is.Null);
                        Assert.That(slot.Whitelist?.Components, Is.EqualTo(new[] { "Gun" }));
                    });
                }
            });
        }
        finally
        {
            server.Dispose();
        }
    }
}
