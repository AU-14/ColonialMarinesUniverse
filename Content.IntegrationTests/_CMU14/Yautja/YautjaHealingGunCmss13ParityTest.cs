using Content.Shared._CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaHealingGunCmss13ParityTest
{
    [Test]
    public async Task HealingGunDoesNotDirectlyHealOutsideMedicompSurgery()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var gun = entMan.SpawnEntity("CMUYautjaHealingGun", MapCoordinates.Nullspace);
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var damageable = entMan.System<DamageableSystem>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            try
            {
                damageable.TryChangeDamage(user, new DamageSpecifier(prototypes.Index<DamageTypePrototype>("Blunt"), 30));
                var beforeHeal = entMan.GetComponent<DamageableComponent>(user).TotalDamage;

                var firstUse = new UseInHandEvent(user);
                entMan.EventBus.RaiseLocalEvent(gun, firstUse);

                var gunComp = entMan.GetComponent<YautjaHealingGunComponent>(gun);
                Assert.Multiple(() =>
                {
                    Assert.That(firstUse.Handled, Is.False,
                        "CMSS13 healing_gun is a Medicomp surgery tool, not a direct-use injector.");
                    Assert.That(entMan.GetComponent<DamageableComponent>(user).TotalDamage, Is.EqualTo(beforeHeal),
                        "Using the loaded gun in hand must not apply treatment outside the surgery flow.");
                    Assert.That(gunComp.Loaded, Is.True,
                        "The loaded capsule is consumed only when the Medicomp tend-wounds step completes.");
                });

                var emptyUse = new UseInHandEvent(user);
                entMan.EventBus.RaiseLocalEvent(gun, emptyUse);
                Assert.Multiple(() =>
                {
                    Assert.That(emptyUse.Handled, Is.False,
                        "The gun must remain non-interactive as a direct treatment tool when empty.");
                    Assert.That(entMan.GetComponent<YautjaHealingGunComponent>(gun).Loaded, Is.True,
                        "A failed direct-use attempt must not change the loaded state.");
                });

                entMan.GetComponent<YautjaHealingGunComponent>(gun).Loaded = false;
                var capsule = entMan.SpawnEntity("CMUYautjaHealingCapsule", MapCoordinates.Nullspace);
                try
                {
                    Assert.That(entMan.HasComponent<YautjaHealingCapsuleComponent>(capsule), Is.True,
                        "The spawned healing capsule must carry the discrete reload marker.");
                    var reload = new AfterInteractUsingEvent(user, capsule, gun, default, true);
                    entMan.EventBus.RaiseLocalEvent(gun, reload, broadcast: true);
                    Assert.That(reload.Handled, Is.True);
                    Assert.That(entMan.Deleted(capsule), Is.True);
                }
                finally
                {
                    if (!entMan.Deleted(capsule))
                        entMan.DeleteEntity(capsule);
                }

                var reloadedUse = new UseInHandEvent(user);
                entMan.EventBus.RaiseLocalEvent(gun, reloadedUse);
                Assert.Multiple(() =>
                {
                    Assert.That(reloadedUse.Handled, Is.False,
                        "Reloading with a CMSS13 healing_gel restores a surgery charge, not a direct-use action.");
                    Assert.That(entMan.GetComponent<YautjaHealingGunComponent>(gun).Loaded, Is.True);
                });
            }
            finally
            {
                if (!entMan.Deleted(gun))
                    entMan.DeleteEntity(gun);
                if (!entMan.Deleted(user))
                    entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HealingGunLoadsOneDiscreteCapsuleAndRefusesSecondLoad()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var gun = entMan.SpawnEntity("CMUYautjaHealingGun", MapCoordinates.Nullspace);
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var firstCapsule = entMan.SpawnEntity("CMUYautjaHealingCapsule", MapCoordinates.Nullspace);
            var secondCapsule = entMan.SpawnEntity("CMUYautjaHealingCapsule", MapCoordinates.Nullspace);

            try
            {
                entMan.GetComponent<YautjaHealingGunComponent>(gun).Loaded = false;

                var firstLoad = new AfterInteractUsingEvent(user, firstCapsule, gun, default, true);
                entMan.EventBus.RaiseLocalEvent(gun, firstLoad, broadcast: true);

                Assert.Multiple(() =>
                {
                    Assert.That(firstLoad.Handled, Is.True,
                        "CMSS13 healing_gun accepts a discrete healing_gel capsule when empty.");
                    Assert.That(entMan.Deleted(firstCapsule), Is.True,
                        "Loading the CMSS13 healing_gel consumes the discrete capsule.");
                });

                var secondLoad = new AfterInteractUsingEvent(user, secondCapsule, gun, default, true);
                entMan.EventBus.RaiseLocalEvent(gun, secondLoad, broadcast: true);

                Assert.Multiple(() =>
                {
                    Assert.That(secondLoad.Handled, Is.False,
                        "CMSS13 healing_gun refuses a second capsule while already loaded.");
                    Assert.That(entMan.Deleted(secondCapsule), Is.False,
                        "A capsule must not be consumed by a failed reload attempt.");
                });
            }
            finally
            {
                if (!entMan.Deleted(gun))
                    entMan.DeleteEntity(gun);
                if (!entMan.Deleted(user))
                    entMan.DeleteEntity(user);
                if (!entMan.Deleted(secondCapsule))
                    entMan.DeleteEntity(secondCapsule);
            }
        });

        await pair.CleanReturnAsync();
    }
}
