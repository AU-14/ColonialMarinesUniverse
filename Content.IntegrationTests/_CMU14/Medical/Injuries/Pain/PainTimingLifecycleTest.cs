#pragma warning disable RA0002 // Regression tests inspect committed state and control the service deadline.
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.Effects;
using System.Linq;
using Content.Shared.Body.Systems;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.Medical.Injuries.Pain;

[TestFixture]
public sealed class PainTimingLifecycleTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task IdenticalInjuryIntegratesEqualTimeAcrossWakeupsAndDelayedService(bool medication)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid quiet = default;
        EntityUid noisy = default;
        EntityUid delayed = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            quiet = InjuredPatient(entities);
            noisy = InjuredPatient(entities);
            delayed = InjuredPatient(entities);
            if (medication)
            {
                foreach (var patient in new[] { quiet, noisy, delayed })
                    ApplyParacetamol(entities, pair.Server.ResolveDependency<IPrototypeManager>(), patient);
            }
        });
        // Equal anatomy, equal elapsed time; only the invalidation/service cadence differs.
        for (var i = 0; i < 48; i++)
        {
            await pair.Server.WaitPost(() =>
            {
                var entities = pair.Server.EntMan;
                entities.System<SharedPainShockSystem>().OnRecomputeTrigger(noisy);
                if (medication)
                    ApplyParacetamol(entities, pair.Server.ResolveDependency<IPrototypeManager>(), noisy);
                entities.GetComponent<PainShockComponent>(delayed).NextUpdate = TimeSpan.MaxValue;
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.25f));
        }
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var system = entities.System<SharedPainShockSystem>();
            foreach (var patient in new[] { quiet, noisy, delayed })
                system.TickOne(patient);
            var expected = entities.GetComponent<PainShockComponent>(quiet).Pain.Float();
            Assert.That(expected, Is.GreaterThan(1));
            Assert.That(expected, Is.LessThan(entities.GetComponent<PainShockComponent>(quiet).PainTarget.Float()),
                "The comparison must not be hidden by reaching the pain target.");
            Assert.That(entities.GetComponent<PainShockComponent>(noisy).Pain.Float(), Is.EqualTo(expected).Within(0.02f));
            Assert.That(entities.GetComponent<PainShockComponent>(delayed).Pain.Float(), Is.EqualTo(expected).Within(0.02f));
            entities.DeleteEntity(quiet);
            entities.DeleteEntity(noisy);
            entities.DeleteEntity(delayed);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DelayedPainServiceCrossesStrongAndWeakProfileExpiriesAtTheirOwnTimes()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid quiet = default;
        EntityUid delayed = default;
        TimeSpan start = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            quiet = InjuredPatient(entities);
            delayed = InjuredPatient(entities);
            start = pair.Server.ResolveDependency<IGameTiming>().CurTime;
            foreach (var patient in new[] { quiet, delayed })
            {
                var system = entities.System<SharedPainShockSystem>();
                system.AddPainSuppressionProfile(patient, 0.75f, 3, 1, TimeSpan.FromSeconds(1.1), 0.25f);
                system.AddPainSuppressionProfile(patient, 0.25f, 1, 0, TimeSpan.FromSeconds(2.2), 0.25f);
            }
        });
        for (var i = 0; i < 15; i++)
        {
            await pair.Server.WaitPost(() =>
                pair.Server.EntMan.GetComponent<PainShockComponent>(delayed).NextUpdate = TimeSpan.MaxValue);
            await pair.RunTicksSync(pair.SecondsToTicks(0.2f));
        }
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var system = entities.System<SharedPainShockSystem>();
            system.TickOne(quiet);
            system.TickOne(delayed);
            var seconds = (pair.Server.ResolveDependency<IGameTiming>().CurTime - start).TotalSeconds;
            var rate = entities.GetComponent<PainShockComponent>(quiet).CachedRiseRate.Float();
            // dP/dt = rate * (1-a) + rate*a*r*P/100 for each drug; then no suppression.
            var expected = AdvanceLinear(0, rate * 0.25, rate * 0.75 * 0.25 / 100, 1.1);
            expected = AdvanceLinear(expected, rate * 0.75, rate * 0.25 * 0.25 / 100, 1.1);
            expected += rate * (seconds - 2.2);
            Assert.That(entities.GetComponent<PainShockComponent>(quiet).Pain.Float(), Is.EqualTo(expected).Within(0.02));
            Assert.That(entities.GetComponent<PainShockComponent>(delayed).Pain.Float(), Is.EqualTo(expected).Within(0.02));
            entities.DeleteEntity(quiet);
            entities.DeleteEntity(delayed);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ADrugApplicationSettlesThePreDoseIntervalWithTheOldRate()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        TimeSpan start = default;
        await pair.Server.WaitPost(() =>
        {
            patient = InjuredPatient(pair.Server.EntMan);
            start = pair.Server.ResolveDependency<IGameTiming>().CurTime;
        });
        for (var i = 0; i < 12; i++)
        {
            await pair.Server.WaitPost(() =>
                pair.Server.EntMan.GetComponent<PainShockComponent>(patient).NextUpdate = TimeSpan.MaxValue);
            await pair.RunTicksSync(pair.SecondsToTicks(0.25f));
        }
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            ApplyParacetamol(entities, pair.Server.ResolveDependency<IPrototypeManager>(), patient);
            var pain = entities.GetComponent<PainShockComponent>(patient);
            var elapsed = (pair.Server.ResolveDependency<IGameTiming>().CurTime - start).TotalSeconds;
            Assert.That(pain.Pain.Float(), Is.EqualTo(pain.CachedRiseRate.Float() * elapsed).Within(0.02));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(5, 95)]
    [TestCase(85, 0)]
    public void ProfileWinnerAndExpiryBoundariesAreIndependentOfIntegrationPartitions(double initial, double target)
    {
        PainSuppressionEntry[] profiles =
        [
            new() { AccumulationSuppression = 0.75f, TierSuppression = 3, DecayBonus = 1,
                ReductionDecreaseRate = 1.2f, ExpiresAt = TimeSpan.FromSeconds(7) },
            new() { AccumulationSuppression = 0.35f, TierSuppression = 2, DecayBonus = 0.2f,
                ReductionDecreaseRate = 0.2f, ExpiresAt = TimeSpan.FromSeconds(20) },
            new() { AccumulationSuppression = 0.15f, TierSuppression = 0, DecayBonus = 0.3f,
                ReductionDecreaseRate = 0.8f, ExpiresAt = TimeSpan.FromSeconds(12), Additive = true },
        ];
        var once = CMUPainIntegrator.Integrate(initial, 100, target, 4, 0.5, 1,
            profiles, TimeSpan.Zero, TimeSpan.FromSeconds(18));
        var split = initial;
        for (var i = 0; i < 1800; i++)
            split = CMUPainIntegrator.Integrate(split, 100, target, 4, 0.5, 1,
                profiles, TimeSpan.FromSeconds(i * 0.01), TimeSpan.FromSeconds((i + 1) * 0.01));
        Assert.That(once, Is.EqualTo(split).Within(0.00001));
        Assert.That(once, Is.Not.EqualTo(initial));
        Assert.That(once, Is.Not.EqualTo(target), "The test must cross boundaries without hiding differences at the cap.");
    }

    [Test, Timeout(10000)]
    public void DeterministicProfileCombinationsPreserveBoundsAndFractionalPartitionInvariance()
    {
        var random = new Random(0xC0FFEE);
        for (var sample = 0; sample < 300; sample++)
        {
            var initial = 2 + random.NextDouble() * 96;
            var target = sample % 2 == 0 ? 100 : 0;
            var duration = 0.05 + random.NextDouble() * 60;
            var rise = random.NextDouble() * 4;
            var decay = 0.05 + random.NextDouble() * 2;
            var sensitivity = 1 + random.NextDouble() * 3;
            var profiles = new PainSuppressionEntry[random.Next(1, 9)];
            for (var i = 0; i < profiles.Length; i++)
            {
                profiles[i] = new PainSuppressionEntry
                {
                    AccumulationSuppression = (float)random.NextDouble(),
                    DecayBonus = (float)random.NextDouble() * 3,
                    TierSuppression = random.Next(0, 7),
                    ReductionDecreaseRate = (float)random.NextDouble() * 2,
                    Additive = random.Next(4) == 0,
                    ExpiresAt = TimeSpan.FromSeconds(random.NextDouble() * duration * 1.5),
                };
            }

            var until = TimeSpan.FromSeconds(duration);
            var once = CMUPainIntegrator.Integrate(initial, 100, target, rise, decay, sensitivity,
                profiles, TimeSpan.Zero, until);
            var split = initial;
            var from = TimeSpan.Zero;
            while (from < until)
            {
                var next = from + TimeSpan.FromSeconds(0.01 + random.NextDouble() * 0.8);
                if (next > until)
                    next = until;
                split = CMUPainIntegrator.Integrate(split, 100, target, rise, decay, sensitivity, profiles, from, next);
                Assert.That(double.IsFinite(split), Is.True, $"partition result for sample {sample}");
                Assert.That(split, Is.InRange(Math.Min(initial, target), Math.Max(initial, target)), $"sample {sample}");
                from = next;
            }

            Assert.That(double.IsFinite(once), Is.True, $"long result for sample {sample}");
            Assert.That(once, Is.InRange(Math.Min(initial, target), Math.Max(initial, target)), $"sample {sample}");
            Assert.That(once, Is.EqualTo(split).Within(0.0005), $"sample {sample}");
        }
    }

    [Test]
    public async Task ANewInjuryDoesNotAccumulatePainForThePrecedingIdleTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        TimeSpan injuredAt = default;
        float riseRate = default;
        await pair.Server.WaitPost(() => patient = pair.Server.EntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace));
        await pair.RunTicksSync(pair.SecondsToTicks(8));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            InjureEye(entities, patient);
            entities.System<SharedPainShockSystem>().TickOne(patient);
            injuredAt = pair.Server.ResolveDependency<IGameTiming>().CurTime;
            riseRate = entities.GetComponent<PainShockComponent>(patient).CachedRiseRate.Float();
            Assert.That(entities.GetComponent<PainShockComponent>(patient).Pain, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(entities.GetComponent<PainShockComponent>(patient).PainTarget, Is.GreaterThan(FixedPoint2.Zero));
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedPainShockSystem>().TickOne(patient);
            var pain = entities.GetComponent<PainShockComponent>(patient);
            var injuredSeconds = (pair.Server.ResolveDependency<IGameTiming>().CurTime - injuredAt).TotalSeconds;
            Assert.That(pain.Pain.Float(), Is.GreaterThan(0));
            Assert.That(pain.Pain.Float(), Is.EqualTo(injuredSeconds * riseRate).Within(0.02),
                "Only the time after the injury contributes, including its derived internal bleeding.");
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InjuryThenTreatmentBetweenPainServicesPreservesItsActualPainInterval()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid serviced = default;
        EntityUid delayed = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            serviced = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            delayed = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            InjureEye(entities, serviced);
            InjureEye(entities, delayed);
            entities.System<SharedPainShockSystem>().TickOne(serviced);
            entities.GetComponent<PainShockComponent>(serviced).NextUpdate = TimeSpan.MaxValue;
            entities.GetComponent<PainShockComponent>(delayed).NextUpdate = TimeSpan.MaxValue;
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedPainShockSystem>().TickOne(serviced);
            foreach (var patient in new[] { serviced, delayed })
            {
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<EyesComponent>(patient, out var eye), Is.True);
                entities.System<SharedOrganHealthSystem>().HealOrgan(eye, patient, 100);
            }
            var expected = entities.GetComponent<PainShockComponent>(serviced).Pain.Float();
            Assert.That(expected, Is.GreaterThan(0.05));
            Assert.That(entities.GetComponent<PainShockComponent>(delayed).Pain.Float(), Is.EqualTo(expected).Within(0.01));
            Assert.That(entities.GetComponent<PainShockComponent>(delayed).PainTarget, Is.EqualTo(FixedPoint2.Zero));
            entities.DeleteEntity(serviced);
            entities.DeleteEntity(delayed);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StasisEnteredAndLeftBetweenScansOnlyFreezesItsOwnInterval()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        TimeSpan started = default;
        TimeSpan entered = default;
        TimeSpan left = default;
        await pair.Server.WaitPost(() =>
        {
            patient = InjuredPatient(pair.Server.EntMan);
            started = pair.Server.ResolveDependency<IGameTiming>().CurTime;
            pair.Server.EntMan.GetComponent<PainShockComponent>(patient).NextUpdate = TimeSpan.MaxValue;
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.1f));
        await pair.Server.WaitPost(() =>
        {
            entered = pair.Server.ResolveDependency<IGameTiming>().CurTime;
            pair.Server.EntMan.EnsureComponent<CMInStasisComponent>(patient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.2f));
        await pair.Server.WaitPost(() =>
        {
            left = pair.Server.ResolveDependency<IGameTiming>().CurTime;
            pair.Server.EntMan.RemoveComponent<CMInStasisComponent>(patient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.1f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedPainShockSystem>().TickOne(patient);
            var pain = entities.GetComponent<PainShockComponent>(patient);
            var active = (entered - started + pair.Server.ResolveDependency<IGameTiming>().CurTime - left).TotalSeconds;
            Assert.That(pain.Pain.Float(), Is.EqualTo(active * pain.CachedRiseRate.Float()).Within(0.02));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SuppressionExpiryCannotAdvancePainWhileTheLayerIsDisabled()
    {
        await using var pair = await PoolManager.GetServerClient();
        var cfg = pair.Server.ResolveDependency<IConfigurationManager>();
        var enabled = cfg.GetCVar(CMUMedicalCCVars.PainEnabled);
        EntityUid patient = default;
        FixedPoint2 initial = default;
        try
        {
            await pair.Server.WaitPost(() =>
            {
                var entities = pair.Server.EntMan;
                patient = InjuredPatient(entities);
                var pain = entities.System<SharedPainShockSystem>();
                pain.AddPainPulse(patient, 10);
                pain.AddPainSuppressionProfile(patient, 0.5f, 2, 0, TimeSpan.FromSeconds(0.25));
                initial = entities.GetComponent<PainShockComponent>(patient).Pain;
                cfg.SetCVar(CMUMedicalCCVars.PainEnabled, false);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.6f));
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUPainSuppression"), Is.False);
                Assert.That(entities.GetComponent<PainShockComponent>(patient).Pain, Is.EqualTo(initial));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                pair.Server.EntMan.DeleteEntity(patient);
                cfg.SetCVar(CMUMedicalCCVars.PainEnabled, enabled);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingTheOnlyInjuredOrganInvalidatesTheCachedSource()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid eye = default;
        EntityUid part = default;
        string slot = default!;
        FixedPoint2 injuredTarget = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            InjureEye(entities, patient);
            entities.System<SharedPainShockSystem>().TickOne(patient);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrgan<EyesComponent>(patient, out eye), Is.True);
            Assert.That(index.TryGetOrganPart(eye, out part), Is.True);
            slot = index.GetOrganSlots(part).Single(candidate => candidate.Organ == eye).SlotId;
            injuredTarget = entities.GetComponent<PainShockComponent>(patient).PainTarget;
            Assert.That(injuredTarget, Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(entities.HasComponent<InternalBleedingComponent>(part), Is.True);
            Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(eye), Is.True);
            Assert.That(entities.HasComponent<InternalBleedingComponent>(part), Is.False,
                "Removing the only organ bleed source must reconcile its old site immediately.");
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.75f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var pain = entities.GetComponent<PainShockComponent>(patient);
            Assert.That(pain.PainTarget, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(pain.CachedRiseRate, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(entities.System<SharedBodySystem>().InsertOrgan(part, eye, slot), Is.True);
            Assert.That(entities.HasComponent<InternalBleedingComponent>(part), Is.True,
                "Inserting an already injured donor must restore its derived bleeding.");
            Assert.That(pain.PainTarget, Is.EqualTo(injuredTarget));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HealingWhileDeadDoesNotRestoreTheOldSourceOnRevival()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = InjuredPatient(entities);
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<EyesComponent>(patient, out var eye), Is.True);
            entities.System<MobStateSystem>().ChangeMobState(patient, MobState.Dead);
            entities.System<SharedOrganHealthSystem>().HealOrgan(eye, patient, 100);
            entities.System<MobStateSystem>().ChangeMobState(patient, MobState.Alive);
            entities.System<SharedPainShockSystem>().TickOne(patient);
            var pain = entities.GetComponent<PainShockComponent>(patient);
            Assert.That(pain.PainTarget, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(pain.CachedRiseRate, Is.EqualTo(FixedPoint2.Zero));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PausedProfilesKeepRemainingTimeIncludingAnApplicationDuringPause()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid effect = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entities.System<SharedPainShockSystem>().AddPainSuppressionProfile(patient, 0.75f, 3, 1, TimeSpan.FromSeconds(1));
            Assert.That(entities.System<StatusEffectsSystem>().TryGetStatusEffect(patient,
                "StatusEffectCMUPainSuppression", out var status), Is.True);
            effect = status!.Value;
            entities.System<MetaDataSystem>().SetEntityPaused(patient, true);
            entities.System<MetaDataSystem>().SetEntityPaused(effect, true);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var pain = entities.System<SharedPainShockSystem>();
            Assert.That(pain.GetTierSuppression(patient), Is.EqualTo(3));
            pain.AddPainSuppressionProfile(patient, 0.25f, 1, 0, TimeSpan.FromSeconds(2));
            entities.System<MetaDataSystem>().SetEntityPaused(patient, false);
            entities.System<MetaDataSystem>().SetEntityPaused(effect, false);
            Assert.That(pain.GetTierSuppression(patient), Is.EqualTo(3));
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
        await pair.Server.WaitAssertion(() => Assert.That(
            pair.Server.EntMan.System<SharedPainShockSystem>().GetTierSuppression(patient), Is.EqualTo(1)));
        await pair.RunTicksSync(pair.SecondsToTicks(1.1f));
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(pair.Server.EntMan.System<SharedPainShockSystem>().GetTierSuppression(patient), Is.Zero);
            pair.Server.EntMan.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task IdenticalDrugProfilesRefreshWhileDistinctAndAdditiveProfilesRemainIndependent()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var pain = entities.System<SharedPainShockSystem>();
            for (var i = 0; i < 100; i++)
                pain.AddPainSuppressionProfile(patient, 0.5f, 2, 1, TimeSpan.FromSeconds(2));
            pain.AddPainSuppressionProfile(patient, 0.25f, 1, 0, TimeSpan.FromSeconds(10));
            pain.AddAdditivePainSuppressionProfile(patient, 0.1f, 1, 0, TimeSpan.FromSeconds(3));
            pain.AddAdditivePainSuppressionProfile(patient, 0.1f, 1, 0, TimeSpan.FromSeconds(3));
            Assert.That(entities.System<StatusEffectsSystem>().TryGetStatusEffect(patient,
                "StatusEffectCMUPainSuppression", out var effect), Is.True);
            var profiles = entities.GetComponent<PainSuppressionComponent>(effect!.Value).ActiveProfiles;
            Assert.That(profiles, Has.Count.EqualTo(4));
            Assert.That(profiles[0].ExpiresAt, Is.LessThan(profiles[1].ExpiresAt),
                "The long weak profile cannot extend the strong profile.");
            Assert.That(pain.GetTierSuppression(patient), Is.EqualTo(4));
            Assert.That(pain.GetAccumulationSuppression(patient), Is.EqualTo(0.7f).Within(0.001f));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    private static EntityUid InjuredPatient(IEntityManager entities)
    {
        var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        InjureEye(entities, patient);
        entities.System<SharedPainShockSystem>().TickOne(patient);
        return patient;
    }

    private static double AdvanceLinear(double pain, double constant, double slope, double seconds)
        => pain + (constant + slope * pain) * (Math.Exp(slope * seconds) - 1) / slope;

    private static void ApplyParacetamol(IEntityManager entities, IPrototypeManager prototypes, EntityUid patient)
    {
        ProtoId<ReagentPrototype> id = "CMUParacetamol";
        var reagent = prototypes.Index(id);
        var effects = reagent.Metabolisms!.Metabolisms["Bloodstream"].Effects;
        var suppression = effects.OfType<CMUApplyPainSuppressionEffect>().Single();
        var context = new ReagentEffectContext(reagent, new Solution(id, 1), null, null,
            new ReagentQuantity(id, 1), "Bloodstream", null, ReagentEffectOrigin.Metabolism);
        Assert.That(entities.System<SharedEntityEffectsSystem>().TryApplyEffect(patient, suppression,
            reagentContext: context), Is.True);
    }

    private static void InjureEye(IEntityManager entities, EntityUid patient)
    {
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<EyesComponent>(patient, out var eye), Is.True);
        var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = 100 } };
        var injury = new OrganDamagedEvent(patient, eye, damage, OrganDamageSource.Direct);
        entities.EventBus.RaiseLocalEvent(eye, ref injury, broadcast: true);
    }
}
