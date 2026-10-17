using Content.IntegrationTests.Fixtures;
using Content.Server.Temperature.Components;
using Content.Shared.Temperature.Components;
using ServerTemperatureSystem = Content.Server.Temperature.Systems.TemperatureSystem;

namespace Content.IntegrationTests.Tests.Atmos;

[TestFixture]
[TestOf(typeof(TemperatureProtectionComponent))]
public sealed class TemperatureProtectionMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TemperatureProtectionMergeSymmetric
  components:
  - type: Temperature
    specificHeat: 1
  - type: TemperatureProtection
    coefficient: 0.25

- type: entity
  id: TemperatureProtectionMergeDirectional
  parent: TemperatureProtectionMergeSymmetric
  components:
  - type: TemperatureProtection
    heatingCoefficient: 0.5
    coolingCoefficient: 0.75
";

    public override PoolSettings PoolSettings => new()
    {
        Connected = false,
    };

    [Test]
    public async Task DirectionalOverridesPreserveSymmetricCoefficientFallback()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var temperature = Server.System<ServerTemperatureSystem>();
            var symmetric = SEntMan.SpawnEntity("TemperatureProtectionMergeSymmetric", map.GridCoords);
            var directional = SEntMan.SpawnEntity("TemperatureProtectionMergeDirectional", map.GridCoords);
            var symmetricTemperature = SEntMan.GetComponent<TemperatureComponent>(symmetric);
            var directionalTemperature = SEntMan.GetComponent<TemperatureComponent>(directional);
            var symmetricProtection = SEntMan.GetComponent<TemperatureProtectionComponent>(symmetric);
            var directionalProtection = SEntMan.GetComponent<TemperatureProtectionComponent>(directional);
            var symmetricStart = symmetricTemperature.Temperature;
            var directionalStart = directionalTemperature.Temperature;

            Assert.Multiple(() =>
            {
                Assert.That(symmetricProtection.Coefficient, Is.EqualTo(0.25f));
                Assert.That(symmetricProtection.HeatingCoefficient, Is.Null);
                Assert.That(symmetricProtection.CoolingCoefficient, Is.Null);
                Assert.That(temperature.ChangeHeat(symmetric, 40f), Is.EqualTo(10f));
                Assert.That(symmetricTemperature.Temperature, Is.EqualTo(symmetricStart + 10f));
                Assert.That(temperature.ChangeHeat(symmetric, -40f), Is.EqualTo(-10f));
                Assert.That(symmetricTemperature.Temperature, Is.EqualTo(symmetricStart));

                Assert.That(directionalProtection.Coefficient, Is.EqualTo(0.25f),
                    "the inherited symmetric coefficient must remain available as the fallback");
                Assert.That(directionalProtection.HeatingCoefficient, Is.EqualTo(0.5f));
                Assert.That(directionalProtection.CoolingCoefficient, Is.EqualTo(0.75f));
                Assert.That(temperature.ChangeHeat(directional, 40f), Is.EqualTo(20f));
                Assert.That(directionalTemperature.Temperature, Is.EqualTo(directionalStart + 20f));
                Assert.That(temperature.ChangeHeat(directional, -40f), Is.EqualTo(-30f));
                Assert.That(directionalTemperature.Temperature, Is.EqualTo(directionalStart - 10f));
            });
        });
    }
}
