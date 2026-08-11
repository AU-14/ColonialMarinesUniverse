#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.Shared._RMC14.Damage;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Damage;

[TestFixture]
[TestOf(typeof(DamageMultipliersComponent))]
public sealed class RMCDamageMultipliersNetworkTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: RMCDamageMultipliersNetworkTestProjectile
          components:
          - type: DamageMultipliers
            multipliers:
              Turf: 5

        - type: entity
          id: RMCDamageMultipliersNetworkTestGun
          components:
          - type: GunDamageMultipliers
            multipliers:
              Breaching: 10.8
        """;

    [Test]
    public async Task MultiplierDictionariesReplicateToClient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid serverProjectile = default;
        EntityUid serverGun = default;

        await server.WaitPost(() =>
        {
            serverProjectile = server.EntMan.SpawnEntity("RMCDamageMultipliersNetworkTestProjectile", map.GridCoords);
            serverGun = server.EntMan.SpawnEntity("RMCDamageMultipliersNetworkTestGun", map.GridCoords);
        });
        await pair.RunTicksSync(5);

        await client.WaitAssertion(() =>
        {
            var projectile = client.EntMan.GetEntity(server.EntMan.GetNetEntity(serverProjectile));
            var projectileMultipliers = client.EntMan.GetComponent<DamageMultipliersComponent>(projectile);
            Assert.That(projectileMultipliers.Multipliers.Single(), Is.EqualTo(
                new KeyValuePair<DamageMultiplierFlag, float>(DamageMultiplierFlag.Turf, 5)));

            var gun = client.EntMan.GetEntity(server.EntMan.GetNetEntity(serverGun));
            var gunMultipliers = client.EntMan.GetComponent<GunDamageMultipliersComponent>(gun);
            Assert.That(gunMultipliers.Multipliers.Single(), Is.EqualTo(
                new KeyValuePair<DamageMultiplierFlag, float>(DamageMultiplierFlag.Breaching, 10.8f)));
        });

        await pair.CleanReturnAsync();
    }
}
