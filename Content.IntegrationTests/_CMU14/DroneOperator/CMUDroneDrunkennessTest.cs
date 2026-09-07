using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Drunk;
using Content.Shared.StatusEffectNew;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class CMUDroneDrunkennessTest
{
    [TestCase("CMUDroneAndroid")]
    [TestCase("CMUCombatDrone")]
    [TestCase("CMUFlamerDrone")]
    public async Task DronesRejectDrunkennessWhileOperatorsRemainSusceptible(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var drone = entities.SpawnEntity(prototype, map.GridCoords);
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.AddComponent<CMUDroneOperatorComponent>(user);
            var drunk = entities.System<SharedDrunkSystem>();
            var status = entities.System<StatusEffectsSystem>();
            var duration = TimeSpan.FromSeconds(30);

            drunk.TryApplyDrunkenness(user, duration);
            Assert.That(status.HasStatusEffect(user, SharedDrunkSystem.Drunk), Is.True,
                "The human operator must remain susceptible to drunkenness.");

            drunk.TryApplyDrunkenness(drone, duration);
            Assert.That(status.HasStatusEffect(drone, SharedDrunkSystem.Drunk), Is.False,
                "Operator drones must not become drunk through the alcohol effect.");
            Assert.That(status.TryAddStatusEffectDuration(drone, SharedDrunkSystem.Drunk, duration), Is.False,
                "Applying the drunk status directly must also respect drone immunity.");
            Assert.That(status.TryAddStatusEffectDuration(drone, "StatusEffectMuted", duration), Is.True,
                "Drunkenness immunity must not block unrelated status effects.");
        });

        await pair.CleanReturnAsync();
    }
}
