#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Jobs;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Marines.LastStand;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared._RMC14.Weapons.Ranged;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Marines;

[TestFixture]
public sealed class LastStandTest : GameTest
{
    private const string LastStandComponentName = "CMULastStand";

    public override PoolSettings PoolSettings => new() { Connected = false };

    private static readonly string[] RoundJobsWithLastStand =
    {
        "AU14JobGOVFORSquadAutomaticRifleman",
        "AU14JobOPFORSquadAutomaticRifleman",
        "AU14JobGOVFORSquadAutomaticRiflemanRMC",
        "AU14JobGOVFORSquadAutomaticRiflemanUPP",
        "AU14JobGOVFORSquadAutomaticRiflemanWYPMC",
        "AU14JobCLFMachineGunner",
        "AU14JobCLFMachineGunnerReinforcement",
        "AU14VAIMachinegunner",
        "AU14VAISOMachinegunner",
        "AU14JobWYPMCPartySmartgunner",
        "CMUJobPMCWhiteoutGunner",
        "AU14JobProdigyPartySmartgunner",
        "CMUJobProdigySFSmartgunner",
        "AU14FORECONSmartGunOperator",
        "AU14FORECONFirstReconSmartGunOperator",
        "AU14RMCRRTSmartgunner",
        "AU14JobUNISCAutomaticRifleman",
        "AU14UPPGROMMachinegunner",
        "AU14UPPGROMRRUMachinegunner",
        "AU14USArmyHeavyGunner",
        "AU14JobUSArmyHeavyGunnerArmored",
        "RuMCUPPBEARMachinegunner",
        "RuMCUPPBEARMachinegunnerGROZA",
    };

    [Test]
    public async Task SmartgunnerJobsCarryLastStand()
    {
        await Server.WaitAssertion(() =>
        {
            foreach (var jobId in RoundJobsWithLastStand)
            {
                var job = SProtoMan.Index<JobPrototype>(jobId);
                Assert.That(job.RoundComponents.ContainsKey(LastStandComponentName), Is.True,
                    $"{jobId} must grant Last Stand via roundComponents.");
            }

            // The upstream squad smart gun operator uses AddComponentSpecial instead.
            var sgo = SProtoMan.Index<JobPrototype>("CMSmartGunOperator");
            Assert.That(
                sgo.Special.OfType<AddComponentSpecial>().Any(s => s.Components.ContainsKey(LastStandComponentName)),
                Is.True,
                "CMSmartGunOperator must grant Last Stand via AddComponentSpecial.");
        });
    }

    [Test]
    public async Task LastStandBuffsGunsWhileHurtAndExpires()
    {
        // Keep the medical aim penalty from skewing the accuracy baseline once the marine is hurt.
        await OverrideCVar(Side.Server, CMUMedicalCCVars.StatusEffectsEnabled, false, sync: false);

        var map = await Pair.CreateTestMap();
        EntityUid marine = default;
        EntityUid gun = default;
        FixedPoint2 baseDamageMult = default;
        FixedPoint2 baseAccuracyMult = default;

        await Server.WaitAssertion(() =>
        {
            marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            gun = SEntMan.SpawnEntity("RMCSmartGun", map.GridCoords);

            var lastStand = SEntMan.EnsureComponent<CMULastStandComponent>(marine);
            var settings = SProtoMan.Index(lastStand.Settings);
            Assert.That(lastStand.Action, Is.Not.Null, "Granting the component must grant the action.");
            var action = SEntMan.GetComponent<ActionComponent>(lastStand.Action!.Value);
            Assert.That(action.UseDelay, Is.EqualTo(settings.Cooldown), "The action cooldown must match the settings prototype.");

            Assert.That(SEntMan.System<SharedHandsSystem>().TryForcePickupAnyHand(marine, gun), Is.True);

            baseDamageMult = SEntMan.GetComponent<GunDamageModifierComponent>(gun).ModifiedMultiplier;
            baseAccuracyMult = SEntMan.GetComponent<RMCWeaponAccuracyComponent>(gun).ModifiedAccuracyMultiplier;

            // Healthy marines cannot make a last stand.
            var healthyAttempt = new CMULastStandActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(marine, healthyAttempt);
            Assert.That(healthyAttempt.Handled, Is.False, "Last Stand must not activate above half health.");
            Assert.That(SEntMan.HasComponent<CMULastStandActiveComponent>(marine), Is.False);

            // Hurt them past half health and try again.
            var thresholds = SEntMan.System<MobThresholdSystem>();
            Assert.That(thresholds.TryGetThresholdForState(marine, MobState.Critical, out var critThreshold), Is.True);
            var damageable = SEntMan.System<DamageableSystem>();
            var blunt = new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Blunt"), critThreshold!.Value / 2 + 1);
            Assert.That(damageable.TryChangeDamage(marine, blunt, ignoreResistances: true), Is.Not.Null);

            var hurtAttempt = new CMULastStandActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(marine, hurtAttempt);
            Assert.That(hurtAttempt.Handled, Is.True, "Last Stand must activate below half health.");
            Assert.That(SEntMan.HasComponent<CMULastStandActiveComponent>(marine), Is.True);
            Assert.That(SEntMan.HasComponent<CMULastStandGunComponent>(gun), Is.True);

            Assert.That(SEntMan.GetComponent<GunDamageModifierComponent>(gun).ModifiedMultiplier,
                Is.EqualTo(baseDamageMult * settings.DamageMultiplier),
                "Gun damage must be doubled while Last Stand is active.");
            Assert.That(SEntMan.GetComponent<RMCWeaponAccuracyComponent>(gun).ModifiedAccuracyMultiplier,
                Is.EqualTo(baseAccuracyMult + settings.AccuracyBonus),
                "Accuracy must gain the flat bonus while Last Stand is active.");
        });

        // Let the surge expire.
        await Pair.RunSeconds(31);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMULastStandActiveComponent>(marine), Is.False,
                "Last Stand must expire after its duration.");
            Assert.That(SEntMan.HasComponent<CMULastStandGunComponent>(gun), Is.False);
            Assert.That(SEntMan.GetComponent<GunDamageModifierComponent>(gun).ModifiedMultiplier,
                Is.EqualTo(baseDamageMult), "Gun damage must return to normal after the surge.");
            Assert.That(SEntMan.GetComponent<RMCWeaponAccuracyComponent>(gun).ModifiedAccuracyMultiplier,
                Is.EqualTo(baseAccuracyMult), "Accuracy must return to normal after the surge.");

            SEntMan.DeleteEntity(gun);
            SEntMan.DeleteEntity(marine);
        });
    }

    [Test]
    public async Task DyingDuringLastStandShortensTheCooldown()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var marine = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var lastStand = SEntMan.EnsureComponent<CMULastStandComponent>(marine);
            var settings = SProtoMan.Index(lastStand.Settings);

            var thresholds = SEntMan.System<MobThresholdSystem>();
            Assert.That(thresholds.TryGetThresholdForState(marine, MobState.Critical, out var critThreshold), Is.True);
            var damageable = SEntMan.System<DamageableSystem>();
            var blunt = new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Blunt"), critThreshold!.Value / 2 + 1);
            Assert.That(damageable.TryChangeDamage(marine, blunt, ignoreResistances: true), Is.Not.Null);

            var attempt = new CMULastStandActionEvent();
            SEntMan.EventBus.RaiseLocalEvent(marine, attempt);
            Assert.That(attempt.Handled, Is.True);
            var activatedAt = SEntMan.GetComponent<CMULastStandActiveComponent>(marine).ActivatedAt;

            // Kill them mid-surge.
            var overkill = new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Blunt"), 1000);
            damageable.TryChangeDamage(marine, overkill, ignoreResistances: true);
            Assert.That(SEntMan.System<MobStateSystem>().IsDead(marine), Is.True);

            Assert.That(SEntMan.HasComponent<CMULastStandActiveComponent>(marine), Is.False,
                "Last Stand must end on death.");
            var action = SEntMan.GetComponent<ActionComponent>(lastStand.Action!.Value);
            Assert.That(action.Cooldown, Is.Not.Null);
            Assert.That(action.Cooldown!.Value.End, Is.EqualTo(activatedAt + settings.DeathCooldown),
                "Dying mid-surge must shorten the cooldown to the death cooldown from activation.");

            SEntMan.DeleteEntity(marine);
        });
    }
}
