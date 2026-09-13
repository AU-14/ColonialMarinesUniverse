using Content.Shared._RMC14.Damage;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._RMC14.Networking;

[TestFixture]
public sealed class DamageMultipliersStateTest
{
    private static readonly EntProtoId Gun = "RMCWeaponShotgunXM51";
    private static readonly EntProtoId Pellet = "RMCPelletShotgunBreaching";

    [Test]
    public async Task BreachingPelletMultipliersReplicateWithoutStateErrors()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var pellets = new List<NetEntity>();
        Dictionary<DamageMultiplierFlag, float> expected = null;

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var gun = entities.SpawnEntity(Gun, MapCoordinates.Nullspace);
            expected = new(entities.GetComponent<GunDamageMultipliersComponent>(gun).Multipliers);
            var fired = new List<EntityUid>();

            for (var i = 0; i < 4; i++)
            {
                var pellet = entities.SpawnEntity(Pellet, map.GridCoords);
                Assert.That(entities.HasComponent<DamageMultipliersComponent>(pellet), Is.False,
                    "The firing event must add the component at runtime, as in the reported failure.");
                fired.Add(pellet);
                pellets.Add(entities.GetNetEntity(pellet));
            }

            entities.EventBus.RaiseLocalEvent(gun, new AmmoShotEvent { FiredProjectiles = fired });

            foreach (var pellet in fired)
                Assert.That(entities.GetComponent<DamageMultipliersComponent>(pellet).Multipliers, Is.EquivalentTo(expected));
        });

        // The pool fails on client state-application errors, including errors that request a full resync.
        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            foreach (var pellet in pellets)
            {
                var component = entities.GetComponent<DamageMultipliersComponent>(entities.GetEntity(pellet));
                Assert.That(component.Multipliers, Is.EquivalentTo(expected));
            }
        });

        await pair.CleanReturnAsync();
    }
}
