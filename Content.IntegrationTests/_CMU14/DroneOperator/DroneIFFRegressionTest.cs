using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class DroneIFFRegressionTest
{
    [TestCase("CMUCombatDrone", "GOVFOR", "OPFOR")]
    [TestCase("CMUFlamerDrone", "OPFOR", "GOVFOR")]
    [TestCase("CMUDroneAndroid", "OPFOR", "GOVFOR")]
    public async Task LinkingDroneUsesOperatorsSide(string prototype, string friendly, string enemy)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = false, Dirty = true });
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.AddComponent<CMUDroneOperatorComponent>(user);
            var iff = entities.System<GunIFFSystem>();
            iff.SetUserFaction(user, friendly);
            var drone = entities.SpawnEntity(prototype, map.GridCoords.Offset(Vector2.UnitX));
            var tablet = entities.SpawnEntity("CMUDroneControlTablet", map.GridCoords);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, tablet), Is.True);
            var link = new InteractUsingEvent(user, tablet, drone, entities.GetComponent<TransformComponent>(drone).Coordinates);
            entities.EventBus.RaiseLocalEvent(drone, link);
            Assert.That(link.Handled, Is.True);
            Assert.That(iff.IsInFaction(drone, friendly), Is.True, "Allied targets must retain friendly-fire protection.");
            Assert.That(iff.IsInFaction(drone, enemy), Is.False, "An opposing faction must not inherit the drone prototype's default IFF.");
        });
        await pair.CleanReturnAsync();
    }
}
