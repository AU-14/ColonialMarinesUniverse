using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Explosion;

[TestFixture]
[TestOf(typeof(SharedExplosionSystem))]
[TestOf(typeof(HardpointSystem))]
public sealed class ExplosionResistanceHardpointMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: ExplosionResistanceMergeVehicle
          components:
          - type: HardpointSlots
            slots:
            - id: armor-a
              hardpointType: Armor
              required: false
            - id: armor-b
              hardpointType: Armor
              required: false

        - type: entity
          id: ExplosionResistanceMergeArmorHalf
          components:
          - type: HardpointItem
            hardpointType: Armor
          - type: VehicleArmorHardpoint
            explosionCoefficient: 0.5

        - type: entity
          id: ExplosionResistanceMergeArmorQuarter
          components:
          - type: HardpointItem
            hardpointType: Armor
          - type: VehicleArmorHardpoint
            explosionCoefficient: 0.25
        """;

    [Test]
    public async Task ArmorInstallSwapAndRemovalDirtyTheEffectiveResistance()
    {
        var map = await Pair.CreateTestMap();
        EntityUid vehicle = default;
        EntityUid halfArmor = default;
        EntityUid quarterArmor = default;
        NetEntity vehicleNet = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                vehicle = SEntMan.SpawnEntity("ExplosionResistanceMergeVehicle", map.GridCoords);
                halfArmor = SEntMan.SpawnEntity("ExplosionResistanceMergeArmorHalf", map.GridCoords);
                quarterArmor = SEntMan.SpawnEntity("ExplosionResistanceMergeArmorQuarter", map.GridCoords);
                vehicleNet = SEntMan.GetNetEntity(vehicle);

                var itemSlots = Server.System<ItemSlotsSystem>();
                var slots = SEntMan.GetComponent<ItemSlotsComponent>(vehicle);
                Assert.That(itemSlots.TryInsert((vehicle, slots), "armor-a", halfArmor, null, excludeUserAudio: true), Is.True);

                AssertResistance(SEntMan, vehicle, 0.5f);
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                AssertResistance(CEntMan, CEntMan.GetEntity(vehicleNet), 0.5f);
            });

            await Server.WaitAssertion(() =>
            {
                var itemSlots = Server.System<ItemSlotsSystem>();
                var slots = SEntMan.GetComponent<ItemSlotsComponent>(vehicle);
                Assert.That(itemSlots.TryInsert((vehicle, slots), "armor-b", quarterArmor, null, excludeUserAudio: true), Is.True);
                AssertResistance(SEntMan, vehicle, 0.5f,
                    "the first functional mounted armor remains the effective coefficient");

                Assert.That(itemSlots.TryEject((vehicle, slots), "armor-a", null, out var ejected, excludeUserAudio: true), Is.True);
                Assert.That(ejected, Is.EqualTo(halfArmor));
                AssertResistance(SEntMan, vehicle, 0.25f,
                    "removing the active armor must restore the next functional coefficient");
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                AssertResistance(CEntMan, CEntMan.GetEntity(vehicleNet), 0.25f);
            });

            await Server.WaitAssertion(() =>
            {
                var explosion = Server.System<ExplosionSystem>();
                var itemSlots = Server.System<ItemSlotsSystem>();
                var slots = SEntMan.GetComponent<ItemSlotsComponent>(vehicle);

                explosion.SetExplosionResistance(vehicle, 0.9f, worn: false);
                Assert.That(itemSlots.TryEject((vehicle, slots), "armor-b", null, out var ejected, excludeUserAudio: true), Is.True);
                Assert.That(ejected, Is.EqualTo(quarterArmor));
                AssertResistance(SEntMan, vehicle, 0.9f,
                    "hardpoint removal must not delete a resistance value it does not own");
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                AssertResistance(CEntMan, CEntMan.GetEntity(vehicleNet), 0.9f,
                    "a dynamic non-hardpoint replacement must replicate before final removal");
            });

            await Server.WaitAssertion(() =>
            {
                var itemSlots = Server.System<ItemSlotsSystem>();
                var slots = SEntMan.GetComponent<ItemSlotsComponent>(vehicle);
                SEntMan.RemoveComponent<ExplosionResistanceComponent>(vehicle);
                Assert.That(itemSlots.TryInsert((vehicle, slots), "armor-b", quarterArmor, null, excludeUserAudio: true), Is.True);
                AssertResistance(SEntMan, vehicle, 0.25f);
                Assert.That(itemSlots.TryEject((vehicle, slots), "armor-b", null, out _, excludeUserAudio: true), Is.True);
                Assert.That(SEntMan.HasComponent<ExplosionResistanceComponent>(vehicle), Is.False,
                    "the final matching armor coefficient is removed with its hardpoint");
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.HasComponent<ExplosionResistanceComponent>(CEntMan.GetEntity(vehicleNet)), Is.False);
            });
        }
        finally
        {
            await Delete(vehicle, halfArmor, quarterArmor);
        }
    }

    [Test]
    public async Task SharedDefaultAndServerOverridesRemainOnTheUnifiedSystem()
    {
        await Server.WaitAssertion(() =>
        {
            var explosion = Server.System<ExplosionSystem>();
            var queue = typeof(ExplosionSystem)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(method =>
                    method.Name == nameof(SharedExplosionSystem.QueueExplosion) &&
                    method.GetBaseDefinition().DeclaringType == typeof(SharedExplosionSystem));
            var reload = typeof(ExplosionSystem)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Single(method =>
                    method.Name == nameof(SharedExplosionSystem.ReloadMap) &&
                    method.GetBaseDefinition().DeclaringType == typeof(SharedExplosionSystem));

            Assert.Multiple(() =>
            {
                Assert.That(explosion, Is.InstanceOf<SharedExplosionSystem>());
                Assert.That(ExplosionSystem.DefaultExplosionPrototypeId.ToString(), Is.EqualTo("Default"));
                Assert.That(queue.DeclaringType, Is.EqualTo(typeof(ExplosionSystem)));
                Assert.That(queue.GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(SharedExplosionSystem)));
                Assert.That(reload.DeclaringType, Is.EqualTo(typeof(ExplosionSystem)));
                Assert.That(reload.GetBaseDefinition().DeclaringType, Is.EqualTo(typeof(SharedExplosionSystem)));
            });
        });
    }

    private static void AssertResistance(
        IEntityManager entityManager,
        EntityUid uid,
        float coefficient,
        string? message = null)
    {
        var resistance = entityManager.GetComponent<ExplosionResistanceComponent>(uid);
        Assert.Multiple(() =>
        {
            Assert.That(resistance.DamageCoefficient, Is.EqualTo(coefficient).Within(0.0001f), message);
            Assert.That(resistance.Worn, Is.False,
                "vehicle armor must protect the vehicle itself rather than relay as worn equipment");
        });
    }

    private async Task Delete(params EntityUid[] entities)
    {
        await Server.WaitPost(() =>
        {
            foreach (var uid in entities)
            {
                if (SEntMan.EntityExists(uid))
                    SEntMan.DeleteEntity(uid);
            }
        });
    }
}
