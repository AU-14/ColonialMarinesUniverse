using System.Numerics;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaRecallTest
{
    private static readonly string[] RecallablePrototypes =
    {
        "CMUYautjaHarpoon",
        "CMUYautjaChainwhip",
        "CMUYautjaClanSword",
        "CMUYautjaRendingSword",
        "CMUYautjaPiercingSword",
        "CMUYautjaSeveringSword",
        "CMUYautjaDualWarScythe",
        "CMUYautjaDoubleWarScythe",
        "CMUYautjaCruelStaff",
        "CMUYautjaCombistick",
        "CMUYautjaWarAxe",
        "CMUYautjaCeremonialDagger",
        "CMUYautjaClanShield",
        "CMUYautjaAncientShield",
        "CMUYautjaHunterSpear",
        "CMUYautjaWarGlaive",
        "CMUYautjaCleavingGlaive",
        "CMUYautjaAncientWarGlaive",
        "CMUYautjaLongaxe",
        "CMUYautjaDuellingBlade",
        "CMUYautjaDuellingClub",
        "CMUYautjaDuellingHatchet",
        "CMUYautjaDuellingKnife",
        "CMUYautjaSpikeLauncher",
        "CMUYautjaPlasmaRifle",
        "CMUYautjaPlasmaPistol",
        "CMUYautjaSmartDisc",
        "CMUYautjaHealingGun",
    };

    [Test]
    public async Task LooseWeaponsAndHealingGunAreRecallable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            foreach (var prototype in RecallablePrototypes)
            {
                var item = server.EntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
                Assert.That(server.EntMan.HasComponent<YautjaRecallableComponent>(item), Is.True, prototype);
                server.EntMan.DeleteEntity(item);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FirstUseBindsRecallableToYautja()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hunter = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entities.EnsureComponent<YautjaComponent>(hunter);

            foreach (var prototype in new[] { "CMUYautjaHealingGun", "CMUYautjaSmartDisc", "CMUYautjaClanSword" })
            {
                var item = entities.SpawnEntity(prototype, MapCoordinates.Nullspace);
                var use = new UseInHandEvent(hunter);
                entities.EventBus.RaiseLocalEvent(item, use);

                Assert.That(use.Handled, Is.True, prototype);
                Assert.That(entities.GetComponent<YautjaRecallableComponent>(item).YautjaOwner,
                    Is.EqualTo(hunter), prototype);
                entities.DeleteEntity(item);
            }

            entities.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RecallWorksAtMapRangeAndSkipsAcidCoveredItems()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hands = entities.System<SharedHandsSystem>();
            var inventory = entities.System<InventorySystem>();
            var hunter = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.EnsureComponent<YautjaComponent>(hunter);
            var bracer = entities.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

            var acidItem = entities.SpawnEntity("CMUYautjaSmartDisc", map.GridCoords.Offset(new Vector2(1, 0)));
            entities.GetComponent<YautjaRecallableComponent>(acidItem).YautjaOwner = hunter;
            entities.EnsureComponent<DamageableCorrodingComponent>(acidItem);

            var distantItem = entities.SpawnEntity("CMUYautjaClanSword", map.GridCoords.Offset(new Vector2(30, 0)));
            entities.GetComponent<YautjaRecallableComponent>(distantItem).YautjaOwner = hunter;

            RaiseRecall(entities, hunter, bracer);

            Assert.That(hands.IsHolding(hunter, distantItem), Is.True);
            Assert.That(hands.IsHolding(hunter, acidItem), Is.False);
            var charge = entities.GetComponent<YautjaBracerComponent>(bracer).Charge;
            Assert.That(charge.Float(), Is.EqualTo(2930));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RecallDoesNotRetrieveStoredItemsOrSpendPower()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var containers = entities.System<SharedContainerSystem>();
            var inventory = entities.System<InventorySystem>();
            var hunter = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.EnsureComponent<YautjaComponent>(hunter);
            var bracer = entities.SpawnEntity("CMUYautjaBracer", map.GridCoords);
            Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

            var crate = entities.SpawnEntity("RMCCrateSupplyInternals", map.GridCoords.Offset(new Vector2(2, 0)));
            var item = entities.SpawnEntity("CMUYautjaHealingGun", map.GridCoords);
            entities.GetComponent<YautjaRecallableComponent>(item).YautjaOwner = hunter;
            var storage = entities.GetComponent<EntityStorageComponent>(crate);
            Assert.That(containers.Insert(item, storage.Contents, force: true), Is.True);

            RaiseRecall(entities, hunter, bracer);

            Assert.That(storage.Contents.Contains(item), Is.True);
            var charge = entities.GetComponent<YautjaBracerComponent>(bracer).Charge;
            Assert.That(charge.Float(), Is.EqualTo(3000));
        });

        await pair.CleanReturnAsync();
    }

    private static void RaiseRecall(IEntityManager entities, EntityUid hunter, EntityUid bracer)
    {
        var action = entities.SpawnEntity("CMUActionYautjaRecall", MapCoordinates.Nullspace);
        var actionComp = entities.GetComponent<ActionComponent>(action);
        var recall = new YautjaRecallActionEvent
        {
            Performer = hunter,
            Action = (action, actionComp),
        };
        entities.EventBus.RaiseLocalEvent(bracer, recall);
        Assert.That(recall.Handled, Is.True);
        entities.DeleteEntity(action);
    }
}
