using System.Linq;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Actions.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaSmokeTest
{
    private static readonly string[] ClanArmorLoadoutIds =
    {
        "CMUYautjaClanArmor",
        "CMUYautjaClanArmorBronze",
        "CMUYautjaClanArmorSilver",
        "CMUYautjaClanArmorCrimson",
        "CMUYautjaClanArmorBone",
    };

    [Test]
    public async Task DirectYautjaSpawnGetsCoreLoadout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hunter = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.HasComponent<YautjaComponent>(hunter), Is.True);
                AssertEquipped(entMan, inventory, hunter, "mask", "CMUYautjaMask");
                AssertEquipped(entMan, inventory, hunter, "gloves", "CMUYautjaBracer");
                AssertEquipped(entMan, inventory, hunter, "back", "CMUYautjaCloakPack");
                AssertEquippedAny(entMan, inventory, hunter, "outerClothing", ClanArmorLoadoutIds);
                AssertEquipped(entMan, inventory, hunter, "jumpsuit", "CMUYautjaBodyMesh");
                AssertEquipped(entMan, inventory, hunter, "shoes", "CMUYautjaClanGreaves");
                AssertEquipped(entMan, inventory, hunter, "pocket1", "CMUYautjaSmartDisc");
                AssertEquipped(entMan, inventory, hunter, "pocket2", "CMUYautjaMedicomp");
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerStoredGearDeploysAndRetractsSameEntity()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            var action = entMan.SpawnEntity("CMUActionYautjaToggleScimitar", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                var gearComp = entMan.GetComponent<YautjaGearContainerComponent>(bracer);
                Assert.That(gearComp.Gear.TryGetValue(YautjaGearKind.Scimitar, out var scimitar), Is.True);
                Assert.That(gearComp.Container, Is.Not.Null);
                Assert.That(gearComp.Container!.Contains(scimitar), Is.True);

                var actionComp = entMan.GetComponent<ActionComponent>(action);
                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                Assert.That(hands.IsHolding(hunter, scimitar), Is.True);
                Assert.That(gearComp.Container.Contains(scimitar), Is.False);

                entMan.EventBus.RaiseLocalEvent(bracer, NewToggleScimitarEvent(hunter, action, actionComp));

                Assert.That(hands.IsHolding(hunter, scimitar), Is.False);
                Assert.That(gearComp.Container.Contains(scimitar), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(action);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicompSpawnsReferenceHealingSet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var medicomp = entMan.SpawnEntity("CMUYautjaMedicomp", MapCoordinates.Nullspace);

            try
            {
                var storage = entMan.GetComponent<StorageComponent>(medicomp);
                var prototypes = storage.Container.ContainedEntities
                    .Select(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID)
                    .ToList();

                Assert.That(prototypes, Does.Contain("CMUYautjaHealingGun"));
                Assert.That(prototypes.Count(id => id == "CMUYautjaWoundClamp"), Is.EqualTo(2));
                Assert.That(prototypes.Count(id => id == "CMUYautjaAutoInjector"), Is.EqualTo(6));
                Assert.That(prototypes.Count(id => id == "CMUYautjaHerbalCase"), Is.EqualTo(2));

                var healingGelTotal = storage.Container.ContainedEntities
                    .Where(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID == "CMUYautjaHealingGel")
                    .Sum(gel => entMan.GetComponent<StackComponent>(gel).Count);
                Assert.That(healingGelTotal, Is.EqualTo(12));

                var stabilizerGelTotal = storage.Container.ContainedEntities
                    .Where(contained => entMan.GetComponent<MetaDataComponent>(contained).EntityPrototype?.ID == "CMUYautjaStabilizerGel")
                    .Sum(gel => entMan.GetComponent<StackComponent>(gel).Count);
                Assert.That(stabilizerGelTotal, Is.EqualTo(3));
            }
            finally
            {
                if (!entMan.Deleted(medicomp))
                    entMan.DeleteEntity(medicomp);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BracerSelfDestructCannotBeArmedWhileCritical()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var mobState = entMan.System<MobStateSystem>();
            var selfDestruct = entMan.System<YautjaSelfDestructSystem>();

            var hunter = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var bracer = entMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<YautjaComponent>(hunter);
                Assert.That(inventory.TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);

                mobState.ChangeMobState(hunter, MobState.Critical);

                var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
                Assert.That(selfDestruct.TryArmSelfDestruct((bracer, bracerComp), hunter), Is.False);
                Assert.That(bracerComp.SelfDestructArmed, Is.False);
            }
            finally
            {
                entMan.DeleteEntity(hunter);

                if (!entMan.Deleted(bracer))
                    entMan.DeleteEntity(bracer);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertEquipped(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid wearer,
        string slot,
        string prototype)
    {
        AssertEquippedAny(entMan, inventory, wearer, slot, prototype);
    }

    private static void AssertEquippedAny(
        IEntityManager entMan,
        InventorySystem inventory,
        EntityUid wearer,
        string slot,
        params string[] prototypes)
    {
        Assert.That(inventory.TryGetSlotEntity(wearer, slot, out var equipped), Is.True, slot);
        Assert.That(equipped, Is.Not.Null, slot);

        var meta = entMan.GetComponent<MetaDataComponent>(equipped.Value);
        Assert.That(prototypes, Does.Contain(meta.EntityPrototype?.ID), slot);
    }

    private static YautjaToggleScimitarActionEvent NewToggleScimitarEvent(
        EntityUid hunter,
        EntityUid action,
        ActionComponent actionComp)
    {
        return new YautjaToggleScimitarActionEvent
        {
            Performer = hunter,
            Action = (action, actionComp),
        };
    }
}
