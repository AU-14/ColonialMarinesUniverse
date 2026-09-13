using Content.Client._RMC14.Weapons.Melee;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Rage;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoRevivedTargetTest : GameTest
{
    [Test]
    public async Task ReplicatedRevivalAllowsXenoTargetingAndMeleeDamage()
    {
        var map = await Pair.CreateTestMap();
        var originalAttached = ServerSession!.AttachedEntity;
        EntityUid patient = default;
        EntityUid berserker = default;
        NetEntity patientNet = default;
        NetEntity berserkerNet = default;
        FixedPoint2 lethalDamage = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                berserker = SEntMan.SpawnEntity("RMCXenoRavagerBerserker", map.GridCoords);
                patientNet = SEntMan.GetNetEntity(patient);
                berserkerNet = SEntMan.GetNetEntity(berserker);
                Server.PlayerMan.SetAttachedEntity(ServerSession, berserker);
            });
            await Pair.RunTicksSync(3);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<MobThresholdSystem>().TryGetDeadThreshold(patient, out var dead), Is.True);
                lethalDamage = dead.Value;
                Server.System<DamageableSystem>().TryChangeDamage(patient,
                    new DamageSpecifier { DamageDict = { ["Poison"] = lethalDamage } }, true);
                Assert.That(Server.System<MobStateSystem>().IsDead(patient), Is.True);
            });
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.System<MobStateSystem>().IsDead(CEntMan.GetEntity(patientNet)), Is.True);
            });
            await Server.WaitAssertion(() =>
            {
                var defibrillator = Server.System<RMCDefibrillatorSystem>();
                var attempt = defibrillator.PrepareRevival(patient);
                Assert.That(defibrillator.TryRevive(patient, attempt,
                    new DamageSpecifier { DamageDict = { ["Poison"] = -lethalDamage } }), Is.True);
                Assert.That(Server.System<MobStateSystem>().IsAlive(patient), Is.True);
            });
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
            {
                var clientPatient = CEntMan.GetEntity(patientNet);
                var clientBerserker = CEntMan.GetEntity(berserkerNet);
                Assert.That(Client.System<MobStateSystem>().IsAlive(clientPatient), Is.True,
                    "the xeno client's dead icon must clear after a replicated revival");
                var coordinates = Client.System<SharedTransformSystem>().GetMapCoordinates(clientPatient);
                Assert.That(Client.System<RMCMeleeWeaponSystem>().TryGetAlternativeXenoAttackTarget(
                    clientBerserker, coordinates, [clientPatient], out var selected), Is.True);
                Assert.That(selected, Is.EqualTo(clientPatient));

                var melee = Client.System<SharedMeleeWeaponSystem>();
                Assert.That(melee.TryGetWeapon(clientBerserker, out var weapon, out var comp), Is.True);
                Assert.That(melee.AttemptLightAttack(clientBerserker, weapon, comp, clientPatient, requireCombatMode: false), Is.True);
                Assert.That(Client.System<XenoRageSystem>().GetRage(clientBerserker), Is.EqualTo(1));
                Assert.That(Client.System<DamageableSystem>().GetTotalDamage(clientPatient).Float(), Is.GreaterThan(0));
                Assert.That(CEntMan.HasComponent<ColorFlashEffectComponent>(clientPatient), Is.True,
                    "the attacking xeno must see the hit flash on a revived patient");
            });
            await Server.WaitAssertion(() =>
            {
                var melee = Server.System<SharedMeleeWeaponSystem>();
                Assert.That(melee.TryGetWeapon(berserker, out var weapon, out var comp), Is.True);
                Assert.That(melee.AttemptLightAttack(berserker, weapon, comp, patient, requireCombatMode: false), Is.True);
                Assert.That(Server.System<XenoRageSystem>().GetRage(berserker), Is.EqualTo(1));
                Assert.That(Server.System<DamageableSystem>().GetTotalDamage(patient).Float(), Is.GreaterThan(0));
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, originalAttached));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RevivedPatientRemainsAliveAndGrantsBerserkerRage(bool reviveInCritical)
    {
        var map = await Pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid berserker = default;
        await Server.WaitAssertion(() =>
        {
            patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            berserker = SEntMan.SpawnEntity("RMCXenoRavagerBerserker", map.GridCoords);
            var thresholds = Server.System<MobThresholdSystem>();
            Assert.That(thresholds.TryGetDeadThreshold(patient, out var dead), Is.True);
            Assert.That(thresholds.TryGetThresholdForState(patient, MobState.Critical, out var critical), Is.True);
            var damage = Server.System<DamageableSystem>();
            damage.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Poison"] = dead.Value } }, true);
            Assert.That(Server.System<MobStateSystem>().IsDead(patient), Is.True);
            Assert.That(Server.System<XenoSystem>().CanGainRewardsFromTarget(berserker, patient), Is.False,
                "a corpse must still be ineligible for berserker rewards before revival");

            var remaining = reviveInCritical ? critical.Value + 10 : 0;
            var defibrillator = Server.System<RMCDefibrillatorSystem>();
            var attempt = defibrillator.PrepareRevival(patient);
            Assert.That(attempt.Cancelled, Is.False);
            Assert.That(defibrillator.TryRevive(patient, attempt,
                new DamageSpecifier { DamageDict = { ["Poison"] = remaining - dead.Value } }), Is.True);

            Server.System<MobStateSystem>().UpdateMobState(patient);
            Assert.That(Server.System<MobStateSystem>().IsDead(patient), Is.False,
                "a successful revival must clear the cached death threshold before the next state refresh");

            // Recover enough to walk, then exercise the same state refresh used by other systems.
            damage.TryChangeDamage(patient, new DamageSpecifier { DamageDict = { ["Poison"] = -remaining } }, true);
            Server.System<MobStateSystem>().UpdateMobState(patient);
            Assert.That(Server.System<MobStateSystem>().IsAlive(patient), Is.True,
                "a revived, healed patient must not retain the death state used by the xeno HUD");
        });

        await Pair.RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<MobStateSystem>().IsAlive(patient), Is.True);
            Assert.That(Server.System<XenoSystem>().CanGainRewardsFromTarget(berserker, patient), Is.True);
            var hit = new MeleeHitEvent([patient], berserker, berserker, new DamageSpecifier(), null);
            SEntMan.EventBus.RaiseLocalEvent(berserker, hit);
            Assert.That(Server.System<XenoRageSystem>().GetRage(berserker), Is.EqualTo(1),
                "hitting a revived patient must grant a berserker stack");
        });
    }
}
