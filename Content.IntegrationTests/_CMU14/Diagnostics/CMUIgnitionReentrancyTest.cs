using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Damage.Systems;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUIgnitionReentrancyTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestFireSpawningFuel
          components:
          - type: Transform
            anchored: true
          - type: Physics
            bodyType: Static
          - type: Damageable
          - type: Injurable
          - type: Flammable
            damage:
              types:
                Heat: 1
          - type: Destructible
            thresholds:
            - trigger: !type:DamageTypeTrigger
                damageType: Heat
                damage: 1
              behaviors:
              - !type:SpawnEntitiesBehavior
                offset: 0
                spawn:
                  RMCTileFire:
                    min: 1
                    max: 1
        """;

    [Test]
    public async Task IgnitionThatSpawnsFireDoesNotSkipOtherOriginalSources()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.SpawnEntity("RMCTileFire", map.GridCoords);
            var other = SEntMan.SpawnEntity("RMCTileFire", map.GridCoords.Offset(new Vector2(2, 0)));
            var fuel = SEntMan.SpawnEntity("CMUTestFireSpawningFuel", map.GridCoords);
            var before = SEntMan.EntityQuery<RMCIgniteOnCollideComponent>().Count();
            Server.System<SharedRMCFlammableSystem>().Update(0);
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(fuel).Float(), Is.GreaterThan(0));
            Assert.That(SEntMan.EntityQuery<RMCIgniteOnCollideComponent>().Count(), Is.GreaterThan(before),
                "Ignition damage must exercise the callback that creates another fire source.");
            Assert.That(SEntMan.GetComponent<RMCIgniteOnCollideComponent>(other).InitDamaged, Is.True,
                "Creating a new fire must not abort the remaining original sources.");
        });
    }
}
