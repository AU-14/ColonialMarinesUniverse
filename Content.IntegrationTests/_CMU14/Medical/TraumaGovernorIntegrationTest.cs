using System.Linq;
using Content.Shared._CMU14.Medical.Stabilizers;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Actions.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class TraumaGovernorIntegrationTest
{
    [Test]
    public async Task TraumaGovernorFindsEveryOfferedHumanOrgan()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var governor = entMan.System<SharedCMUTraumaGovernorSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                foreach (var target in Enum.GetValues<CMUOrganStabilizerTarget>())
                {
                    Assert.That(governor.TryFindOrgan(human, target, out _, out _), Is.True, $"Missing {target}");
                }
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TraumaGovernorReplicatesFromServerWithoutClientContainerMutation()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var (server, client) = pair;
        var map = await pair.CreateTestMap();
        EntityUid armor = default;

        await server.WaitPost(() =>
        {
            armor = server.EntMan.SpawnAtPosition("AU14ArmorCBRN", map.GridCoords);
            Assert.That(server.EntMan.HasComponent<CMUTraumaGovernorComponent>(armor), Is.True);
        });

        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var clientArmor = pair.ToClientUid(armor);
            Assert.That(client.EntMan.HasComponent<CMUTraumaGovernorComponent>(clientArmor), Is.True);
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(map.MapUid));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MarineArmorStartsWithTraumaGovernorReadoutWhenWorn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var governor = entMan.System<SharedCMUTraumaGovernorSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var armor = entMan.SpawnEntity("CMArmorM3Medium", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.HasComponent<CMUTraumaGovernorComponent>(armor), Is.True);

                var missing = governor.GetReadout(human);
                Assert.That(missing.Installed, Is.False);

                Assert.That(inventory.TryEquip(human, armor, "outerClothing", force: true, predicted: false), Is.True);

                var readout = governor.GetReadout(human);
                Assert.Multiple(() =>
                {
                    Assert.That(readout.Installed, Is.True);
                    Assert.That(readout.State, Is.EqualTo(CMUTraumaGovernorState.Ready));
                    Assert.That(readout.ActiveTarget, Is.Null);
                });
            }
            finally
            {
                if (entMan.EntityExists(human))
                    inventory.TryUnequip(human, "outerClothing", force: true, predicted: false);
                if (entMan.EntityExists(armor))
                    entMan.DeleteEntity(armor);
                if (entMan.EntityExists(human))
                    entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TraumaGovernorAttachmentGrantsActionWhenInsertedIntoWornArmor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var containers = entMan.System<SharedContainerSystem>();
            var inventory = entMan.System<InventorySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var armor = entMan.SpawnEntity("CMArmorM3Medium", MapCoordinates.Nullspace);
            var attachment = entMan.SpawnEntity("CMUTraumaGovernorAttachment", MapCoordinates.Nullspace);

            try
            {
                Assert.That(entMan.TryGetComponent<UniformAccessoryHolderComponent>(armor, out var holder), Is.True);
                Assert.That(containers.TryGetContainer(armor, holder!.ContainerId, out var container), Is.True);

                foreach (var contained in container!.ContainedEntities.ToArray())
                {
                    if (entMan.HasComponent<CMUTraumaGovernorAttachmentComponent>(contained))
                        entMan.DeleteEntity(contained);
                }

                entMan.RemoveComponent<CMUTraumaGovernorComponent>(armor);
                Assert.That(inventory.TryEquip(human, armor, "outerClothing", force: true, predicted: false), Is.True);
                Assert.That(HasTraumaGovernorAction(entMan, human), Is.False);

                Assert.That(containers.Insert(attachment, container, force: true), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUTraumaGovernorComponent>(armor), Is.True);
                    Assert.That(HasTraumaGovernorAction(entMan, human), Is.True);
                });
            }
            finally
            {
                if (entMan.EntityExists(human))
                    inventory.TryUnequip(human, "outerClothing", force: true, predicted: false);
                if (entMan.EntityExists(attachment))
                    entMan.DeleteEntity(attachment);
                if (entMan.EntityExists(armor))
                    entMan.DeleteEntity(armor);
                if (entMan.EntityExists(human))
                    entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TraumaGovernorBypassVialLoadsArmor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var inventory = entMan.System<InventorySystem>();
            var governor = entMan.System<SharedCMUTraumaGovernorSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var armor = entMan.SpawnEntity("CMArmorM3Medium", MapCoordinates.Nullspace);
            var vial = entMan.SpawnEntity("CMUTraumaGovernorVial", MapCoordinates.Nullspace);

            try
            {
                var interact = new InteractUsingEvent(human, vial, armor, entMan.GetComponent<TransformComponent>(armor).Coordinates);
                entMan.EventBus.RaiseLocalEvent(armor, interact);

                Assert.That(interact.Handled, Is.True);
                Assert.That(entMan.GetComponent<CMUTraumaGovernorComponent>(armor).VialLoaded, Is.True);

                Assert.That(inventory.TryEquip(human, armor, "outerClothing", force: true, predicted: false), Is.True);

                var readout = governor.GetReadout(human);
                Assert.Multiple(() =>
                {
                    Assert.That(readout.Installed, Is.True);
                    Assert.That(readout.VialLoaded, Is.True);
                    Assert.That(readout.State, Is.EqualTo(CMUTraumaGovernorState.Ready));
                });
            }
            finally
            {
                if (entMan.EntityExists(human))
                    inventory.TryUnequip(human, "outerClothing", force: true, predicted: false);
                if (entMan.EntityExists(vial))
                    entMan.DeleteEntity(vial);
                if (entMan.EntityExists(armor))
                    entMan.DeleteEntity(armor);
                if (entMan.EntityExists(human))
                    entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static bool HasTraumaGovernorAction(IEntityManager entMan, EntityUid user)
    {
        if (!entMan.TryGetComponent<ActionsComponent>(user, out var actions))
            return false;

        foreach (var action in actions.Actions)
        {
            if (entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == "CMUActionTraumaGovernor")
                return true;
        }

        return false;
    }
}
