#pragma warning disable RA0002 // Fixtures configure service deadlines/rates; assertions inspect owner state after public interactions.
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Administration.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Stomach;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class VisceralPhysiologyLifecycleTest
{
    [TestCase("liver")]
    [TestCase("kidneys")]
    public async Task LateToxinServiceMatchesRegularServiceAtPublicHealingBoundary(string kind)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid regular = default, delayed = default, regularOrgan = default, delayedOrgan = default;
        TimeSpan started = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (regular, regularOrgan) = CreatePatient(entities, kind);
            (delayed, delayedOrgan) = CreatePatient(entities, kind);
            DamageToStage(entities, regular, regularOrgan, OrganDamageStage.Dead);
            DamageToStage(entities, delayed, delayedOrgan, OrganDamageStage.Dead);
            DelayService(entities, delayedOrgan, kind);
            started = pair.Server.Timing.CurTime;
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(Probe(entities, delayed).Toxin, Is.EqualTo(FixedPoint2.Zero),
                "The fault injection defers only this organ's periodic service.");
            var expected = ToxinRate(kind) * (pair.Server.Timing.CurTime - started).TotalSeconds;
            Heal(entities, regular, regularOrgan);
            Heal(entities, delayed, delayedOrgan);
            Assert.That(Probe(entities, regular).Toxin.Float(), Is.EqualTo(expected).Within(0.011));
            Assert.That(Probe(entities, delayed).Toxin, Is.EqualTo(Probe(entities, regular).Toxin));
            Assert.That(entities.GetComponent<OrganHealthComponent>(delayedOrgan).Stage, Is.EqualTo(OrganDamageStage.Healthy));
            entities.DeleteEntity(regular);
            entities.DeleteEntity(delayed);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("liver", "stasis")]
    [TestCase("liver", "patient")]
    [TestCase("liver", "organ")]
    [TestCase("liver", "both")]
    [TestCase("kidneys", "stasis")]
    [TestCase("kidneys", "patient")]
    [TestCase("kidneys", "organ")]
    [TestCase("kidneys", "both")]
    public async Task ToxinExcludesFrozenTimeAndPreservesSubcentActiveIntervals(string kind, string mode)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, organ = default;
        TimeSpan started = default, entered = default, left = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, organ) = CreatePatient(entities, kind);
            DamageToStage(entities, patient, organ, OrganDamageStage.Dead);
            DelayService(entities, organ, kind);
            started = pair.Server.Timing.CurTime;
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.1f));
        await pair.Server.WaitPost(() =>
        {
            entered = pair.Server.Timing.CurTime;
            SetFrozen(pair.Server.EntMan, patient, organ, mode, true);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2.1f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(Probe(entities, patient).Toxin.Float(),
                Is.EqualTo((entered - started).TotalSeconds * ToxinRate(kind)).Within(0.011));
            left = pair.Server.Timing.CurTime;
            SetFrozen(entities, patient, organ, mode, false);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.1f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var active = pair.Server.Timing.CurTime - started - (left - entered);
            Heal(entities, patient, organ);
            Assert.That(Probe(entities, patient).Toxin.Float(), Is.EqualTo(active.TotalSeconds * ToxinRate(kind)).Within(0.011),
                "Body and organ freezes overlap; a sub-cent remainder survives each boundary without billing frozen time.");
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("liver")]
    [TestCase("kidneys")]
    public async Task NativeHealingSettlesOldStagePressureAndRefreshesClearance(string kind)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, organ = default;
        TimeSpan started = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, organ) = CreatePatient(entities, kind);
            DamageToStage(entities, patient, organ, OrganDamageStage.Bruised);
            DelayService(entities, organ, kind);
            entities.AddComponent<CMUVisceralStageProbeComponent>(organ);
            started = pair.Server.Timing.CurTime;
            entities.GetComponent<OrganHealthComponent>(organ).NextRegenTick = started + TimeSpan.FromSeconds(0.7);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<OrganHealthComponent>(organ).Stage, Is.EqualTo(OrganDamageStage.Healthy));
            var healed = entities.GetComponent<CMUVisceralStageProbeComponent>(organ).HealthyAt;
            Assert.That(healed, Is.GreaterThan(started));
            Assert.That(Probe(entities, patient).Toxin.Float(), Is.EqualTo((healed - started).TotalSeconds * 0.05).Within(0.011));
            var clearance = kind == "liver"
                ? entities.System<SharedLiverSystem>().GetClearanceMultiplier(patient)
                : entities.System<SharedKidneysSystem>().GetClearanceMultiplier(patient);
            Assert.That(clearance, Is.EqualTo(1));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("liver")]
    [TestCase("kidneys")]
    public async Task RemovalStartsMissingPressureAndInsertionSettlesOnlyActiveRecipientTime(string kind)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default, organ = default, part = default;
        string slot = string.Empty;
        TimeSpan removed = default, entered = default, left = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, organ) = CreatePatient(entities, kind, entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords));
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrganPart(organ, out part), Is.True);
            slot = index.GetOrganSlots(part).Single(entry => entry.Organ == organ).SlotId;
            Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(organ), Is.True);
            Probe(entities, patient).Origin = patient;
            removed = pair.Server.Timing.CurTime;
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.2f));
        await pair.Server.WaitPost(() =>
        {
            entered = pair.Server.Timing.CurTime;
            pair.Server.EntMan.EnsureComponent<CMInStasisComponent>(patient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.3f));
        await pair.Server.WaitPost(() =>
        {
            left = pair.Server.Timing.CurTime;
            pair.Server.EntMan.RemoveComponent<CMInStasisComponent>(patient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<SharedBodySystem>().InsertOrgan(part, organ, slot), Is.True);
            var active = pair.Server.Timing.CurTime - removed - (left - entered);
            Assert.That(Probe(entities, patient).Toxin.Float(), Is.EqualTo(active.TotalSeconds * ToxinRate(kind)).Within(0.011));
            Assert.That(entities.HasComponent<MissingLiverComponent>(patient), Is.False);
            Assert.That(entities.HasComponent<MissingKidneysComponent>(patient), Is.False);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("liver", false)]
    [TestCase("kidneys", false)]
    [TestCase("stomach", false)]
    [TestCase("liver", true)]
    [TestCase("kidneys", true)]
    [TestCase("stomach", true)]
    public async Task RejuvenationDiscardsUnservicedPressureBeforeHealing(string kind, bool critical)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, organ = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, organ) = CreatePatient(entities, kind);
            ConfigureVomit(entities, organ, kind);
            DamageToStage(entities, patient, organ, OrganDamageStage.Dead);
            if (critical)
            {
                Assert.That(entities.System<MobThresholdSystem>().TryGetThresholdForState(patient, MobState.Critical, out var threshold), Is.True);
                // Stored respiratory saturation can immediately heal an exactly-threshold
                // asphyxiation injury and revive the patient before this reset is exercised.
                // Cellular damage keeps the intended critical state without regional trauma.
                entities.System<DamageableSystem>().TryChangeDamage(patient,
                    new DamageSpecifier { DamageDict = { ["Cellular"] = threshold!.Value } }, ignoreResistances: true);
                Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Critical));
            }
            DelayService(entities, organ, kind);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState,
                Is.EqualTo(critical ? MobState.Critical : MobState.Alive),
                "The delayed-pressure setup must still have its intended state before rejuvenation.");
            Assert.That(Probe(entities, patient).Toxin, Is.EqualTo(FixedPoint2.Zero),
                "No earlier public state boundary may have serviced the deliberately delayed pressure.");
            Assert.That(Probe(entities, patient).Vomits, Is.Zero);
            entities.System<RejuvenateSystem>().PerformRejuvenate(patient);
            Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
            Assert.That(Probe(entities, patient).Toxin, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(Probe(entities, patient).Vomits, Is.Zero,
                "Rejuvenation must not dispatch the old stomach trial while resetting tissue.");
            Assert.That(entities.GetComponent<DamageableComponent>(patient).Damage.GetTotal(), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(entities.GetComponent<OrganHealthComponent>(organ).Stage, Is.EqualTo(OrganDamageStage.Healthy));
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(Probe(entities, patient).Toxin, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(Probe(entities, patient).Vomits, Is.Zero);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("stasis")]
    [TestCase("patient")]
    [TestCase("organ")]
    [TestCase("both")]
    public async Task StomachRetainsOnlyActiveCooldownAcrossFreeze(string mode)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, organ = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, organ) = CreatePatient(entities, "stomach");
            ConfigureVomit(entities, organ, "stomach");
            DamageToStage(entities, patient, organ, OrganDamageStage.Dead);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.7f));
        await pair.Server.WaitPost(() => SetFrozen(pair.Server.EntMan, patient, organ, mode, true));
        await pair.RunTicksSync(pair.SecondsToTicks(2.1f));
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(Probe(pair.Server.EntMan, patient).Vomits, Is.Zero);
            SetFrozen(pair.Server.EntMan, patient, organ, mode, false);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.8f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Heal(entities, patient, organ);
            Assert.That(Probe(entities, patient).Vomits, Is.EqualTo(1),
                "The public healing boundary settles one trial after 1.5 active seconds; frozen time adds no trials.");
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LateStomachServiceKeepsTheExistingSingleTrialPolicy()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, organ = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, organ) = CreatePatient(entities, "stomach");
            ConfigureVomit(entities, organ, "stomach");
            DamageToStage(entities, patient, organ, OrganDamageStage.Dead);
            DelayService(entities, organ, "stomach");
        });
        await pair.RunTicksSync(pair.SecondsToTicks(3.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(Probe(entities, patient).Vomits, Is.Zero);
            Heal(entities, patient, organ);
            Assert.That(Probe(entities, patient).Vomits, Is.EqualTo(1),
                "A delayed service rolls once, preserving existing expected fluid-loss policy instead of replaying missed trials.");
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("liver")]
    [TestCase("kidneys")]
    [TestCase("stomach")]
    public async Task DeathAndPublicRevivalDoNotChargeTheDeadInterval(string kind)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, organ = default;
        FixedPoint2 pressureAtDeath = default;
        TimeSpan revived = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, organ) = CreatePatient(entities, kind);
            ConfigureVomit(entities, organ, kind);
            DamageToStage(entities, patient, organ, OrganDamageStage.Dead);
            entities.System<DamageableSystem>().TryChangeDamage(patient,
                new DamageSpecifier { DamageDict = { ["Asphyxiation"] = 1000 } });
            Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Dead));
            pressureAtDeath = Probe(entities, patient).Toxin;
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2.1f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(Probe(entities, patient).Toxin, Is.EqualTo(pressureAtDeath));
            Assert.That(Probe(entities, patient).Vomits, Is.Zero);
            entities.System<DamageableSystem>().SetAllDamage(patient, FixedPoint2.Zero);
            entities.System<MobStateSystem>().ChangeMobState(patient, MobState.Alive);
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out var heart), Is.True);
            entities.System<SharedHeartSystem>().TryRestartHeart(heart);
            Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.EqualTo(MobState.Alive));
            revived = pair.Server.Timing.CurTime;
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.4f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Heal(entities, patient, organ);
            Assert.That(Probe(entities, patient).Toxin.Float(),
                Is.EqualTo(pressureAtDeath.Float() + (pair.Server.Timing.CurTime - revived).TotalSeconds * ToxinRate(kind)).Within(0.011));
            Assert.That(Probe(entities, patient).Vomits, Is.Zero,
                "A revived stomach starts with only its pre-death active cooldown, not the two-second dead interval.");
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("liver")]
    [TestCase("kidneys")]
    [TestCase("stomach")]
    public async Task DisablingTheOrganLayerDoesNotBankInactivePhysiology(string kind)
    {
        await using var pair = await PoolManager.GetServerClient();
        var configuration = pair.Server.ResolveDependency<IConfigurationManager>();
        EntityUid patient = default, organ = default;
        TimeSpan started = default, disabled = default, enabled = default;
        var original = true;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                original = configuration.GetCVar(CMUMedicalCCVars.OrganEnabled);
                Assert.That(original, Is.True);
                var entities = pair.Server.EntMan;
                (patient, organ) = CreatePatient(entities, kind);
                ConfigureVomit(entities, organ, kind);
                DamageToStage(entities, patient, organ, OrganDamageStage.Dead);
                started = pair.Server.Timing.CurTime;
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.2f));
            await pair.Server.WaitPost(() =>
            {
                disabled = pair.Server.Timing.CurTime;
                configuration.SetCVar(CMUMedicalCCVars.OrganEnabled, false);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(2.1f));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(Probe(pair.Server.EntMan, patient).Toxin.Float(),
                    Is.EqualTo((disabled - started).TotalSeconds * ToxinRate(kind)).Within(0.011));
                Assert.That(Probe(pair.Server.EntMan, patient).Vomits, Is.Zero);
                enabled = pair.Server.Timing.CurTime;
                configuration.SetCVar(CMUMedicalCCVars.OrganEnabled, true);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.2f));
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                Heal(entities, patient, organ);
                var active = pair.Server.Timing.CurTime - started - (enabled - disabled);
                Assert.That(Probe(entities, patient).Toxin.Float(), Is.EqualTo(active.TotalSeconds * ToxinRate(kind)).Within(0.011));
                Assert.That(Probe(entities, patient).Vomits, Is.Zero);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (pair.Server.EntMan.EntityExists(patient))
                    pair.Server.EntMan.DeleteEntity(patient);
                configuration.SetCVar(CMUMedicalCCVars.OrganEnabled, original);
            });
        }
        await pair.CleanReturnAsync();
    }
    [TestCase("liver")]
    [TestCase("kidneys")]
    [TestCase("stomach")]
    public async Task RejuvenationFromMetabolismPermissionCannotBeFollowedByOldPressure(string kind)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, organ = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, organ) = CreatePatient(entities, kind);
            ConfigureVomit(entities, organ, kind);
            DamageToStage(entities, patient, organ, OrganDamageStage.Dead);
            DelayService(entities, organ, kind);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Probe(entities, patient).RejuvenateOnPermission = true;
            Heal(entities, patient, organ);
            Assert.That(Probe(entities, patient).Rejuvenated, Is.True,
                "The ordinary healing boundary consulted the real public metabolism permission event.");
            Assert.That(Probe(entities, patient).Toxin, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(Probe(entities, patient).Vomits, Is.Zero);
            Assert.That(entities.GetComponent<DamageableComponent>(patient).Damage.GetTotal(), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(entities.GetComponent<OrganHealthComponent>(organ).Stage, Is.EqualTo(OrganDamageStage.Healthy));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("liver")]
    [TestCase("kidneys")]
    [TestCase("stomach")]
    public async Task BodySuspensionStillReachesOtherOrgansWithoutAnAttachedHeart(string kind)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default, organ = default, heart = default;
        TimeSpan started = default, entered = default, left = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, organ) = CreatePatient(entities, kind, entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords));
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out heart), Is.True);
            Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(heart), Is.True);
            ConfigureVomit(entities, organ, kind);
            DamageToStage(entities, patient, organ, OrganDamageStage.Dead);
            started = pair.Server.Timing.CurTime;
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.2f));
        await pair.Server.WaitPost(() =>
        {
            entered = pair.Server.Timing.CurTime;
            pair.Server.EntMan.EnsureComponent<CMInStasisComponent>(patient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2.1f));
        await pair.Server.WaitPost(() =>
        {
            left = pair.Server.Timing.CurTime;
            pair.Server.EntMan.RemoveComponent<CMInStasisComponent>(patient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out _), Is.False);
            var active = pair.Server.Timing.CurTime - started - (left - entered);
            Heal(entities, patient, organ);
            Assert.That(Probe(entities, patient).Toxin.Float(), Is.EqualTo(active.TotalSeconds * ToxinRate(kind)).Within(0.011));
            Assert.That(Probe(entities, patient).Vomits, Is.Zero);
            entities.DeleteEntity(patient);
            entities.DeleteEntity(heart);
        });
        await pair.CleanReturnAsync();
    }
    [TestCase("liver", true)]
    [TestCase("kidneys", false)]
    [TestCase("stomach", false)]
    public async Task APermissionCallbackQueuingPatientOrOrganDeletionCancelsPendingPressure(string kind, bool queuePatient)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default, organ = default, queued = default;
        CMUVisceralPressureProbeComponent observed = default!;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, organ) = CreatePatient(entities, kind);
            observed = Probe(entities, patient);
            ConfigureVomit(entities, organ, kind);
            DamageToStage(entities, patient, organ, OrganDamageStage.Dead);
            DelayService(entities, organ, kind);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            queued = queuePatient ? patient : organ;
            observed.QueueOnPermission = queued;
            Heal(entities, patient, organ);
            Assert.That(observed.Queued, Is.True);
            Assert.That(entities.IsQueuedForDeletion(queued), Is.True,
                "QueueDeleteEntity has run, but synchronous deletion has not hidden stale-owner effects yet.");
            Assert.That(observed.Toxin, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(observed.Vomits, Is.Zero);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.EntityExists(queued), Is.False);
            Assert.That(observed.Toxin, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(observed.Vomits, Is.Zero);
            if (entities.EntityExists(patient))
                entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }
    private static (EntityUid Patient, EntityUid Organ) CreatePatient(IEntityManager entities, string kind,
        MapCoordinates? coordinates = null)
    {
        entities.System<CMUVisceralPressureProbeSystem>();
        var patient = entities.SpawnEntity("CMMobHuman", coordinates ?? MapCoordinates.Nullspace);
        var index = entities.System<CMUMedicalBodyIndexSystem>();
        EntityUid organ;
        var found = kind switch
        {
            "liver" => index.TryGetOrgan<LiverComponent>(patient, out organ),
            "kidneys" => index.TryGetOrgan<KidneysComponent>(patient, out organ),
            _ => index.TryGetOrgan<CMUStomachComponent>(patient, out organ),
        };
        Assert.That(found, Is.True);
        entities.AddComponent<CMUVisceralPressureProbeComponent>(patient).Origin = organ;
        return (patient, organ);
    }

    private static CMUVisceralPressureProbeComponent Probe(IEntityManager entities, EntityUid patient)
        => entities.GetComponent<CMUVisceralPressureProbeComponent>(patient);

    private static double ToxinRate(string kind) => kind switch { "liver" => 1, "kidneys" => 0.75, _ => 0 };

    private static void ConfigureVomit(IEntityManager entities, EntityUid organ, string kind)
    {
        if (kind != "stomach")
            return;
        var stomach = entities.GetComponent<CMUStomachComponent>(organ);
        stomach.VomitCheckInterval = TimeSpan.FromSeconds(1);
        stomach.VomitChance[OrganDamageStage.Dead] = 1;
    }

    private static void DelayService(IEntityManager entities, EntityUid organ, string kind)
    {
        if (kind == "liver")
            entities.GetComponent<LiverComponent>(organ).NextSelfDamageTick = TimeSpan.MaxValue;
        else if (kind == "kidneys")
            entities.GetComponent<KidneysComponent>(organ).NextSelfDamageTick = TimeSpan.MaxValue;
        else
            entities.GetComponent<CMUStomachComponent>(organ).NextVomitCheck = TimeSpan.MaxValue;
    }

    private static void DamageToStage(IEntityManager entities, EntityUid patient, EntityUid organ, OrganDamageStage stage)
    {
        var health = entities.GetComponent<OrganHealthComponent>(organ);
        var injury = new OrganDamagedEvent(patient, organ,
            new DamageSpecifier { DamageDict = { ["Blunt"] = health.Current - health.StageThresholds[stage] } },
            OrganDamageSource.Direct);
        entities.EventBus.RaiseLocalEvent(organ, ref injury, broadcast: true);
        Assert.That(health.Stage, Is.EqualTo(stage));
    }

    private static void Heal(IEntityManager entities, EntityUid patient, EntityUid organ)
        => entities.System<SharedOrganHealthSystem>().HealOrgan(organ, patient, entities.GetComponent<OrganHealthComponent>(organ).Max);

    private static void SetFrozen(IEntityManager entities, EntityUid patient, EntityUid organ, string mode, bool frozen)
    {
        if (mode == "stasis")
        {
            if (frozen)
                entities.EnsureComponent<CMInStasisComponent>(patient);
            else
                entities.RemoveComponent<CMInStasisComponent>(patient);
            return;
        }
        var metadata = entities.System<MetaDataSystem>();
        if (mode is "patient" or "both")
            metadata.SetEntityPaused(patient, frozen);
        if (mode is "organ" or "both")
            metadata.SetEntityPaused(organ, frozen);
    }
}

[RegisterComponent]
public sealed partial class CMUVisceralPressureProbeComponent : Component
{
    public EntityUid Origin;
    public FixedPoint2 Toxin;
    public int Vomits;
    public bool RejuvenateOnPermission;
    public bool Rejuvenated;
    public EntityUid? QueueOnPermission;
    public bool Queued;
}

[RegisterComponent]
public sealed partial class CMUVisceralStageProbeComponent : Component
{
    public TimeSpan HealthyAt;
}

public sealed partial class CMUVisceralPressureProbeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUVisceralPressureProbeComponent, DamageChangedEvent>(OnDamage);
        SubscribeLocalEvent<CMUVisceralPressureProbeComponent, TryVomitEvent>(OnVomit);
        SubscribeLocalEvent<CMUVisceralPressureProbeComponent, CMMetabolizeAttemptEvent>(OnMetabolismPermission);
        SubscribeLocalEvent<CMUVisceralStageProbeComponent, OrganStageChangedEvent>(OnStage);
    }

    private void OnDamage(Entity<CMUVisceralPressureProbeComponent> ent, ref DamageChangedEvent args)
    {
        if (args.Origin == ent.Comp.Origin && args.DamageDelta is { } delta)
            ent.Comp.Toxin += FixedPoint2.Max(FixedPoint2.Zero, delta.DamageDict.GetValueOrDefault("Poison"));
    }

    private void OnMetabolismPermission(Entity<CMUVisceralPressureProbeComponent> ent, ref CMMetabolizeAttemptEvent args)
    {
        if (ent.Comp.QueueOnPermission is { } queued)
        {
            ent.Comp.QueueOnPermission = null;
            EntityManager.QueueDeleteEntity(queued);
            ent.Comp.Queued = true;
            return;
        }
        if (!ent.Comp.RejuvenateOnPermission)
            return;
        ent.Comp.RejuvenateOnPermission = false;
        EntityManager.System<RejuvenateSystem>().PerformRejuvenate(ent.Owner);
        ent.Comp.Rejuvenated = true;
    }
    private void OnVomit(Entity<CMUVisceralPressureProbeComponent> ent, ref TryVomitEvent args)
        => ent.Comp.Vomits++;

    private void OnStage(Entity<CMUVisceralStageProbeComponent> ent, ref OrganStageChangedEvent args)
    {
        if (args.New == OrganDamageStage.Healthy)
            ent.Comp.HealthyAt = _timing.CurTime;
    }
}
