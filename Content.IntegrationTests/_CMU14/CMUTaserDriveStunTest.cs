using Content.Shared._RMC14.Stamina;
using Content.Shared._RMC14.Stun;
using Content.Shared.Damage;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Weapons.Melee.Events;

namespace Content.IntegrationTests.CMU14;

[TestFixture]
public sealed class CMUTaserDriveStunTest
{
    [Test]
    public async Task ChargedDriveStunKnocksOutHumanAndEmptyTaserDoesNot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;

        await pair.Server.WaitAssertion(() =>
        {
            var taser = entities.SpawnEntity("RMCWeaponTaser", map.GridCoords);
            var target = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var emptyTarget = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                var battery = entities.System<SharedBatterySystem>();
                var stun = entities.System<RMCSizeStunSystem>();
                var charge = battery.GetCharge(taser);
                entities.EventBus.RaiseLocalEvent(taser,
                    new MeleeHitEvent([target], emptyTarget, taser, new DamageSpecifier(), null));
                Assert.That(battery.GetCharge(taser), Is.EqualTo(charge - 50));
                Assert.That(stun.IsKnockedOut(target), Is.True,
                    "A charged taser drive stun must knock out an ordinary human with one hit.");

                battery.TryUseCharge(taser, battery.GetCharge(taser));
                var stamina = entities.GetComponent<RMCStaminaComponent>(emptyTarget).Current;
                entities.EventBus.RaiseLocalEvent(taser,
                    new MeleeHitEvent([emptyTarget], target, taser, new DamageSpecifier(), null));
                Assert.That(stun.IsKnockedOut(emptyTarget), Is.False);
                Assert.That(entities.GetComponent<RMCStaminaComponent>(emptyTarget).Current, Is.EqualTo(stamina));
            }
            finally
            {
                entities.DeleteEntity(taser);
                entities.DeleteEntity(target);
                entities.DeleteEntity(emptyTarget);
            }
        });

        await pair.CleanReturnAsync();
    }
}
