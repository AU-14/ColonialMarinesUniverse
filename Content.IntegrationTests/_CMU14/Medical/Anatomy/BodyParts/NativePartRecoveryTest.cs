#pragma warning disable RA0002 // Configure the opt-in native recovery feature and inspect its committed state.
using Content.Server.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;

[TestFixture]
public sealed class NativePartRecoveryTest
{
    [TestCase(1f)]
    [TestCase(20f)]
    public async Task NativeRecoverySpendsOnlyAcceptedHealthOnSeverance(float quantum)
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        var cfg = pair.Server.ResolveDependency<IConfigurationManager>();
        var originalWounds = cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled);
        EntityUid patient = default, arm = default, control = default;
        BodyPartHealthComponent health = default!;
        FixedPoint2 initialHealth = default, initialSeverance = default, controlHealth = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                // Isolate native recovery from the independent dressing recovery service.
                cfg.SetCVar(CMUMedicalCCVars.WoundsEnabled, false);
                patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                arm = InjureArm(entities, patient, BodyPartSymmetry.Left);
                control = InjureArm(entities, patient, BodyPartSymmetry.Right);
                health = entities.GetComponent<BodyPartHealthComponent>(arm);
                initialHealth = health.Current;
                initialSeverance = health.SeveranceDamage;
                controlHealth = entities.GetComponent<BodyPartHealthComponent>(control).Current;
                Assert.That(initialSeverance, Is.GreaterThan(FixedPoint2.Zero));
                EnableRecovery(pair.Server.ResolveDependency<IGameTiming>(), health, quantum);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Server.WaitAssertion(() =>
            {
                var recovered = health.Current - initialHealth;
                Assert.That(recovered, Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(health.Current, Is.LessThanOrEqualTo(health.Max));
                Assert.That(health.SeveranceDamage, Is.EqualTo(FixedPoint2.Max(FixedPoint2.Zero, initialSeverance - recovered)));
                if (quantum == 20f)
                    Assert.That(health.Current, Is.EqualTo(health.Max), "The final quantum must be capped by the actual missing HP.");
                Assert.That(entities.GetComponent<BodyPartHealthComponent>(control).Current, Is.EqualTo(controlHealth));
                Assert.That(entities.System<DamageableSystem>().GetAllDamage(patient).GetTotal(), Is.EqualTo(FixedPoint2.New(16)));
                Assert.That(entities.System<SharedBodyPartHealthSystem>().GetOutstandingBodyDamage(arm), Is.EqualTo(FixedPoint2.New(8)));
                Assert.That(entities.System<SharedBodyPartHealthSystem>().GetOutstandingBodyDamage(control), Is.EqualTo(FixedPoint2.New(8)),
                    "Native structural recovery must not silently spend either region's separate aggregate attribution.");
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (entities.EntityExists(patient)) entities.DeleteEntity(patient);
                cfg.SetCVar(CMUMedicalCCVars.WoundsEnabled, originalWounds);
            });
        }
        await pair.CleanReturnAsync();
    }

    [TestCase("stasis")]
    [TestCase("patient")]
    [TestCase("part")]
    [TestCase("both")]
    public async Task NativeRecoveryDoesNotRunWhileItsPatientOrPartIsSuspended(string suspension)
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        var cfg = pair.Server.ResolveDependency<IConfigurationManager>();
        var originalWounds = cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled);
        EntityUid patient = default, arm = default;
        BodyPartHealthComponent health = default!;
        FixedPoint2 initialHealth = default, initialSeverance = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                cfg.SetCVar(CMUMedicalCCVars.WoundsEnabled, false);
                patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                arm = InjureArm(entities, patient, BodyPartSymmetry.Left);
                health = entities.GetComponent<BodyPartHealthComponent>(arm);
                initialHealth = health.Current;
                initialSeverance = health.SeveranceDamage;
                EnableRecovery(pair.Server.ResolveDependency<IGameTiming>(), health, 1f);
                if (suspension == "stasis") entities.EnsureComponent<CMInStasisComponent>(patient);
                if (suspension is "patient" or "both") entities.System<MetaDataSystem>().SetEntityPaused(patient, true);
                if (suspension is "part" or "both") entities.System<MetaDataSystem>().SetEntityPaused(arm, true);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(4));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(health.Current, Is.EqualTo(initialHealth));
                Assert.That(health.SeveranceDamage, Is.EqualTo(initialSeverance));
                entities.System<MetaDataSystem>().SetEntityPaused(patient, false);
                entities.System<MetaDataSystem>().SetEntityPaused(arm, false);
                if (suspension == "stasis") entities.RemoveComponent<CMInStasisComponent>(patient);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Server.WaitAssertion(() =>
            {
                var recovered = health.Current - initialHealth;
                Assert.That(recovered, Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(health.SeveranceDamage, Is.EqualTo(FixedPoint2.Max(FixedPoint2.Zero, initialSeverance - recovered)));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (entities.EntityExists(patient)) entities.DeleteEntity(patient);
                cfg.SetCVar(CMUMedicalCCVars.WoundsEnabled, originalWounds);
            });
        }
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task QueuedDeletionCannotReceiveOneLastNativeRecoveryQuantum(bool deletePart)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var cfg = pair.Server.ResolveDependency<IConfigurationManager>();
            var originalWounds = cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled);
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                cfg.SetCVar(CMUMedicalCCVars.WoundsEnabled, false);
                var arm = InjureArm(entities, patient, BodyPartSymmetry.Left);
                var health = entities.GetComponent<BodyPartHealthComponent>(arm);
                var initialHealth = health.Current;
                var initialSeverance = health.SeveranceDamage;
                EnableRecovery(pair.Server.ResolveDependency<IGameTiming>(), health, 1f);
                health.NextHealTick = TimeSpan.Zero;
                entities.QueueDeleteEntity(deletePart ? arm : patient);
                // Exercise the service before the engine flushes the public deletion request.
                entities.System<BodyPartHealthSystem>().Update(1f);
                Assert.That(health.Current, Is.EqualTo(initialHealth));
                Assert.That(health.SeveranceDamage, Is.EqualTo(initialSeverance));
            }
            finally
            {
                entities.DeleteEntity(patient);
                cfg.SetCVar(CMUMedicalCCVars.WoundsEnabled, originalWounds);
            }
        });
        await pair.CleanReturnAsync();
    }

    private static EntityUid InjureArm(IEntityManager entities, EntityUid patient, BodyPartSymmetry side)
    {
        entities.System<RegionalDamageProbeSystem>();
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
            new(BodyPartType.Arm, side), out var arm), Is.True);
        entities.EnsureComponent<RegionalDamageProbeComponent>(patient).Target = arm;
        var injury = new DamageSpecifier();
        injury.DamageDict["Slash"] = 8;
        var impact = new DamageImpact(DamageImpactDelivery.Melee, DamageImpactContact.Slash,
            DamageImpactPenetration.None, DamageImpactEnergy.Medium);
        var applied = entities.System<DamageableSystem>().TryChangeDamage(patient, injury, ignoreResistances: true, impact: impact);
        Assert.That(applied?.GetTotal(), Is.EqualTo(FixedPoint2.New(8)));
        Assert.That(entities.HasComponent<BodyPartWoundComponent>(arm), Is.False);
        return arm;
    }

    private static void EnableRecovery(IGameTiming timing, BodyPartHealthComponent health, float quantum)
    {
        health.PassiveHealMultiplier = quantum;
        health.HealInterval = TimeSpan.FromSeconds(1);
        health.NextHealTick = timing.CurTime + TimeSpan.FromSeconds(0.25);
    }
}
