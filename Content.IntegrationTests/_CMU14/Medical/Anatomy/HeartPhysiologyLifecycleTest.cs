#pragma warning disable RA0002 // Fixtures configure service deadlines; assertions inspect committed physiology.
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Administration.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class HeartPhysiologyLifecycleTest
{
    [Test]
    public async Task RestartThenArrestInTheSameTickKeepsTheNewArrestStatus()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid heart = default;
        EntityUid? renewed = null;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, heart) = CreatePatient(entities);
            var status = entities.System<StatusEffectsSystem>();
            DamageToStage(entities, patient, heart, OrganDamageStage.Dead);
            Assert.That(status.TryGetStatusEffect(patient, "StatusEffectCMUCardiacArrest", out var original), Is.True);
            entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, 100);
            entities.System<SharedHeartSystem>().TryRestartHeart(heart);
            Assert.That(entities.GetComponent<HeartComponent>(heart).Stopped, Is.False);
            DamageToStage(entities, patient, heart, OrganDamageStage.Dead);
            Assert.That(status.TryGetStatusEffect(patient, "StatusEffectCMUCardiacArrest", out renewed), Is.True);
            Assert.That(renewed, Is.Not.EqualTo(original), "The new arrest must not reuse a status awaiting deletion.");
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<HeartComponent>(heart).Stopped, Is.True);
            Assert.That(entities.System<StatusEffectsSystem>()
                .TryGetStatusEffect(patient, "StatusEffectCMUCardiacArrest", out var remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(renewed), "Deletion flush must not erase the committed arrest.");
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemoveInsertRemoveInTheSameTickKeepsMissingHeartAndArrest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid heart = default;
        EntityUid? renewed = null;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, heart) = CreatePatient(entities,
                entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords));
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrganPart(heart, out var part), Is.True);
            var slot = index.GetOrganSlots(part).Single(entry => entry.Organ == heart).SlotId;
            var bodies = entities.System<SharedBodySystem>();
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(bodies.RemoveOrgan(heart), Is.True);
            Assert.That(status.TryGetStatusEffect(patient, "StatusEffectCMUCardiacArrest", out var original), Is.True);
            Assert.That(bodies.InsertOrgan(part, heart, slot), Is.True);
            Assert.That(entities.HasComponent<MissingHeartComponent>(patient), Is.False);
            Assert.That(status.HasStatusEffect(patient, "StatusEffectCMUCardiacArrest"), Is.False);
            Assert.That(bodies.RemoveOrgan(heart), Is.True);
            Assert.That(status.TryGetStatusEffect(patient, "StatusEffectCMUCardiacArrest", out renewed), Is.True);
            Assert.That(renewed, Is.Not.EqualTo(original));
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.HasComponent<MissingHeartComponent>(patient), Is.True);
            Assert.That(entities.System<StatusEffectsSystem>()
                .TryGetStatusEffect(patient, "StatusEffectCMUCardiacArrest", out var remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(renewed));
            entities.DeleteEntity(patient);
            entities.DeleteEntity(heart);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(80, OrganDamageStage.Healthy)]
    [TestCase(80, OrganDamageStage.Damaged)]
    [TestCase(10, OrganDamageStage.Failing)]
    public async Task PhysiologyPreservesTheConfiguredPulseFloor(int configured, OrganDamageStage stage)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (patient, heart) = CreatePatient(entities);
            var heartbeat = entities.GetComponent<HeartComponent>(heart);
            heartbeat.MinBpmBeforeStop = configured;
            if (stage != OrganDamageStage.Healthy)
                DamageToStage(entities, patient, heart, stage);
            entities.System<SharedHeartSystem>().TickPulse(heart);
            Assert.That(heartbeat.MinBpmBeforeStop, Is.EqualTo(configured));
            Assert.That(heartbeat.BelowThresholdSince, Is.EqualTo(pair.Server.Timing.CurTime),
                "Use the configured floor; a failing heart also retains its intrinsic 60 BPM safety floor.");
            entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, 100);
            entities.System<SharedHeartSystem>().TickPulse(heart);
            Assert.That(heartbeat.MinBpmBeforeStop, Is.EqualTo(configured));
            if (configured > 70)
                Assert.That(heartbeat.BelowThresholdSince, Is.Not.Null);
            else
                Assert.That(heartbeat.BelowThresholdSince, Is.Null);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreinjuredDonorStartsAndInsertsWithItsActualTissueStage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var (patient, originalHeart) = CreatePatient(entities, coordinates);
            var donor = entities.CreateEntityUninitialized("CMUOrganHumanHeart", coordinates);
            var health = entities.GetComponent<OrganHealthComponent>(donor);
            health.Current = health.StageThresholds[OrganDamageStage.Failing];
            Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Healthy), "Fixture starts with serialized health and a stale default stage.");
            entities.InitializeAndStartEntity(donor);
            var heartbeat = entities.GetComponent<HeartComponent>(donor);
            Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Failing));
            Assert.That(heartbeat.PhysiologyStage, Is.EqualTo(health.Stage),
                "All component startup consumers must see the initialized tissue stage.");
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrganPart(originalHeart, out var part), Is.True);
            var slot = index.GetOrganSlots(part).Single(entry => entry.Organ == originalHeart).SlotId;
            var bodies = entities.System<SharedBodySystem>();
            Assert.That(bodies.RemoveOrgan(originalHeart), Is.True);
            Assert.That(bodies.InsertOrgan(part, donor, slot), Is.True);
            Assert.That(heartbeat.PhysiologyStage, Is.EqualTo(OrganDamageStage.Failing));
            Assert.That(heartbeat.BelowThresholdSince, Is.EqualTo(pair.Server.Timing.CurTime));
            Assert.That(heartbeat.BeatsPerMinute, Is.InRange(17, 23));
            entities.DeleteEntity(patient);
            entities.DeleteEntity(originalHeart);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(6)]
    public async Task OtherOrganInjuriesCannotCancelFailingHeartArrest(int otherInjuries)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid heart = default;
        TimeSpan started = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, heart) = CreatePatient(entities);
            var heartbeat = entities.GetComponent<HeartComponent>(heart);
            heartbeat.StopGracePeriod = TimeSpan.FromSeconds(2);
            started = pair.Server.Timing.CurTime;
            DamageToStage(entities, patient, heart, OrganDamageStage.Failing);
            var otherOrgans = entities.System<CMUMedicalBodyIndexSystem>().GetOrgans(patient)
                .Where(organ => organ.Owner != heart && entities.HasComponent<OrganHealthComponent>(organ.Owner))
                .Take(otherInjuries).ToArray();
            Assert.That(otherOrgans, Has.Length.EqualTo(otherInjuries));
            foreach (var organ in otherOrgans)
                DamageToStage(entities, patient, organ.Owner, OrganDamageStage.Damaged);
            entities.System<SharedHeartSystem>().TickPulse(heart);
            Assert.That(heartbeat.BelowThresholdSince, Is.EqualTo(started));
            if (otherInjuries >= 3)
                Assert.That(heartbeat.BeatsPerMinute, Is.GreaterThanOrEqualTo(60),
                    "The high compensatory display pulse must not restore cardiac function.");
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.8f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            foreach (var organ in entities.System<CMUMedicalBodyIndexSystem>().GetOrgans(patient))
            {
                if (otherInjuries == 3 && organ.Owner != heart)
                    entities.System<SharedOrganHealthSystem>().HealOrgan(organ.Owner, patient, 100);
            }
            entities.System<SharedHeartSystem>().TickPulse(heart);
            Assert.That(entities.GetComponent<HeartComponent>(heart).BelowThresholdSince, Is.EqualTo(started),
                "Treating other organs must neither start nor reset the failing heart's grace.");
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.4f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedHeartSystem>().TickPulse(heart);
            var heartbeat = entities.GetComponent<HeartComponent>(heart);
            Assert.That(heartbeat.Stopped, Is.True);
            Assert.That(heartbeat.BeatsPerMinute, Is.Zero);
            Assert.That(heartbeat.NoPulseSince, Is.EqualTo(started + heartbeat.StopGracePeriod));
            Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUCardiacArrest"), Is.True);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PressureIsPartitionInvariantAcrossDelayedServiceAndTissueRecovery()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid regular = default;
        EntityUid delayed = default;
        EntityUid regularHeart = default;
        EntityUid delayedHeart = default;
        TimeSpan injuredAt = default;
        TimeSpan treatedAt = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            (regular, regularHeart) = CreatePatient(entities);
            (delayed, delayedHeart) = CreatePatient(entities);
            injuredAt = pair.Server.Timing.CurTime;
            DamageToStage(entities, regular, regularHeart, OrganDamageStage.Damaged);
            DamageToStage(entities, delayed, delayedHeart, OrganDamageStage.Damaged);
            DelayService(entities, delayedHeart);
        });
        for (var i = 0; i < 8; i++)
        {
            await pair.RunTicksSync(pair.SecondsToTicks(0.13f));
            await pair.Server.WaitPost(() => pair.Server.EntMan.System<SharedHeartSystem>().TickPulse(regularHeart));
        }
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            treatedAt = pair.Server.Timing.CurTime;
            foreach (var (patient, heart) in new[] { (regular, regularHeart), (delayed, delayedHeart) })
            {
                var health = entities.GetComponent<OrganHealthComponent>(heart);
                entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient,
                    health.StageThresholds[OrganDamageStage.Bruised] - health.Current);
                Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Bruised));
            }
            DelayService(entities, delayedHeart);
        });
        for (var i = 0; i < 6; i++)
        {
            await pair.RunTicksSync(pair.SecondsToTicks(0.13f));
            await pair.Server.WaitPost(() => pair.Server.EntMan.System<SharedHeartSystem>().TickPulse(regularHeart));
        }
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var finished = pair.Server.Timing.CurTime;
            foreach (var (patient, heart) in new[] { (regular, regularHeart), (delayed, delayedHeart) })
                entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, 100);
            var pressure = entities.GetComponent<CMUHeartPressureProbeComponent>(regular);
            var delayedPressure = entities.GetComponent<CMUHeartPressureProbeComponent>(delayed);
            var rates = entities.GetComponent<HeartComponent>(regularHeart);
            var expectedAsphyx = rates.AsphyxPerSecond[OrganDamageStage.Damaged].Float() * (treatedAt - injuredAt).TotalSeconds +
                rates.AsphyxPerSecond[OrganDamageStage.Bruised].Float() * (finished - treatedAt).TotalSeconds;
            var expectedToxin = rates.ToxinPerSecond[OrganDamageStage.Damaged].Float() * (treatedAt - injuredAt).TotalSeconds;
            Assert.That(pressure.Asphyx.Float(), Is.EqualTo(expectedAsphyx).Within(0.011));
            Assert.That(pressure.Toxin.Float(), Is.EqualTo(expectedToxin).Within(0.011));
            Assert.That(delayedPressure.Asphyx, Is.EqualTo(pressure.Asphyx));
            Assert.That(delayedPressure.Toxin, Is.EqualTo(pressure.Toxin));
            entities.DeleteEntity(regular);
            entities.DeleteEntity(delayed);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LateHealingSettlesArrestThenViableHeartRestartImmediatelyRestoresPulse()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid heart = default;
        TimeSpan started = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, heart) = CreatePatient(entities);
            entities.GetComponent<HeartComponent>(heart).StopGracePeriod = TimeSpan.FromSeconds(1.25);
            started = pair.Server.Timing.CurTime;
            DamageToStage(entities, patient, heart, OrganDamageStage.Failing);
            DelayService(entities, heart);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.6f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var heartbeat = entities.GetComponent<HeartComponent>(heart);
            entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, 100);
            Assert.That(heartbeat.Stopped, Is.True, "Healing cannot erase an arrest whose grace already elapsed.");
            Assert.That(heartbeat.NoPulseSince, Is.EqualTo(started + heartbeat.StopGracePeriod));
            var elapsed = (pair.Server.Timing.CurTime - started).TotalSeconds;
            var expected = heartbeat.AsphyxPerSecond[OrganDamageStage.Failing].Float() * 1.25 +
                heartbeat.CardiacArrestAsphyxPerSecond.Float() * (elapsed - 1.25);
            Assert.That(entities.GetComponent<CMUHeartPressureProbeComponent>(patient).Asphyx.Float(),
                Is.EqualTo(expected).Within(0.011));
            entities.System<SharedHeartSystem>().TryRestartHeart(heart);
            Assert.That(heartbeat.Stopped, Is.False);
            Assert.That(heartbeat.BeatsPerMinute, Is.EqualTo(70));
            Assert.That(heartbeat.NoPulseSince, Is.Null);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("patient", false)]
    [TestCase("heart", false)]
    [TestCase("both", false)]
    [TestCase("stasis", false)]
    [TestCase("patient", true)]
    [TestCase("heart", true)]
    [TestCase("both", true)]
    [TestCase("stasis", true)]
    public async Task FreezeBoundariesPreserveOnlyActivePressureAndGrace(string mode, bool stopped)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid heart = default;
        TimeSpan started = default;
        TimeSpan entered = default;
        TimeSpan left = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, heart) = CreatePatient(entities);
            entities.GetComponent<HeartComponent>(heart).StopGracePeriod = TimeSpan.FromSeconds(2);
            started = pair.Server.Timing.CurTime;
            DamageToStage(entities, patient, heart, stopped ? OrganDamageStage.Dead : OrganDamageStage.Failing);
            if (stopped)
                entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, 100);
            DelayService(entities, heart);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.3f));
        await pair.Server.WaitPost(() =>
        {
            entered = pair.Server.Timing.CurTime;
            SetFrozen(pair.Server.EntMan, patient, heart, mode, true);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(3));
        await pair.Server.WaitPost(() =>
        {
            left = pair.Server.Timing.CurTime;
            SetFrozen(pair.Server.EntMan, patient, heart, mode, false);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedHeartSystem>().TickPulse(heart);
            var heartbeat = entities.GetComponent<HeartComponent>(heart);
            var active = (entered - started + pair.Server.Timing.CurTime - left).TotalSeconds;
            Assert.That(heartbeat.Stopped, Is.EqualTo(stopped));
            Assert.That(stopped ? heartbeat.NoPulseSince : heartbeat.BelowThresholdSince,
                Is.EqualTo(started + left - entered));
            var rate = stopped ? heartbeat.CardiacArrestAsphyxPerSecond : heartbeat.AsphyxPerSecond[OrganDamageStage.Failing];
            Assert.That(entities.GetComponent<CMUHeartPressureProbeComponent>(patient).Asphyx.Float(),
                Is.EqualTo(active * rate.Float()).Within(0.011));
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.5f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedHeartSystem>().TickPulse(heart);
            var heartbeat = entities.GetComponent<HeartComponent>(heart);
            Assert.That(heartbeat.Stopped, Is.True);
            Assert.That(heartbeat.NoPulseSince,
                Is.EqualTo(started + left - entered + (stopped ? TimeSpan.Zero : heartbeat.StopGracePeriod)));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PacingUsesItsActualExpiryAndCannotRetroactivelyRescueAnArrest()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid heart = default;
        TimeSpan expiry = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, heart) = CreatePatient(entities);
            entities.GetComponent<HeartComponent>(heart).StopGracePeriod = TimeSpan.FromSeconds(0.75);
            entities.System<ChemicalPropertyStatusSystem>().ApplyCardiacPacing(patient, 1, "heart-regression");
            expiry = entities.GetComponent<ChemicalCardiacPacingComponent>(patient).ExpiresAt;
            DamageToStage(entities, patient, heart, OrganDamageStage.Failing);
            Assert.That(entities.GetComponent<HeartComponent>(heart).BelowThresholdSince, Is.Null);
            DelayService(entities, heart);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2.9f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            // The new dose settles the interval using the old cached expiry before
            // making the replacement deadline authoritative.
            entities.System<ChemicalPropertyStatusSystem>().ApplyCardiacPacing(patient, 1, "heart-regression");
            var heartbeat = entities.GetComponent<HeartComponent>(heart);
            Assert.That(heartbeat.Stopped, Is.True);
            Assert.That(heartbeat.NoPulseSince, Is.EqualTo(expiry + heartbeat.StopGracePeriod));
            entities.System<SharedHeartSystem>().TryRestartHeart(heart);
            Assert.That(heartbeat.Stopped, Is.False);
            Assert.That(heartbeat.BeatsPerMinute, Is.GreaterThanOrEqualTo(60));
            Assert.That(heartbeat.BelowThresholdSince, Is.Null);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LowBloodCannotUseCompensatoryDisplayPulseToRestoreDamagedHeartFunction()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (patient, heart) = CreatePatient(entities);
            try
            {
                DamageToStage(entities, patient, heart, OrganDamageStage.Damaged);
                var heartbeat = entities.GetComponent<HeartComponent>(heart);
                Assert.That(heartbeat.BelowThresholdSince, Is.Null);
                Assert.That(entities.System<BloodstreamSystem>().TryRegulateBloodLevel(patient, 10000, 0.35f), Is.True);
                entities.System<SharedHeartSystem>().TickPulse(heart);
                Assert.That(heartbeat.CriticalBloodVolume, Is.True);
                Assert.That(heartbeat.BeatsPerMinute, Is.GreaterThan(heartbeat.MinBpmBeforeStop));
                Assert.That(heartbeat.BelowThresholdSince, Is.EqualTo(pair.Server.Timing.CurTime));
                Assert.That(entities.System<BloodstreamSystem>().TryRegulateBloodLevel(patient, 10000, 1), Is.True);
                Assert.That(heartbeat.BelowThresholdSince, Is.Null, "Restoring actual blood volume restores perfusion.");
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RejuvenationDiscardsUnservicedHeartPressureBeforeHealingTissue()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid heart = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, heart) = CreatePatient(entities);
            DamageToStage(entities, patient, heart, OrganDamageStage.Failing);
            DelayService(entities, heart);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(3.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<RejuvenateSystem>().PerformRejuvenate(patient);
            var heartbeat = entities.GetComponent<HeartComponent>(heart);
            Assert.That(entities.GetComponent<DamageableComponent>(patient).Damage.GetTotal(), Is.EqualTo(FixedPoint2.Zero));
            Assert.That(heartbeat.Stopped, Is.False);
            Assert.That(heartbeat.BeatsPerMinute, Is.EqualTo(70));
            Assert.That(heartbeat.BelowThresholdSince, Is.Null);
            Assert.That(heartbeat.NoPulseSince, Is.Null);
            Assert.That(entities.GetComponent<CMUHeartPressureProbeComponent>(patient).Asphyx, Is.EqualTo(FixedPoint2.Zero));
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<SharedHeartSystem>().TickPulse(heart);
            Assert.That(entities.GetComponent<CMUHeartPressureProbeComponent>(patient).Asphyx, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(entities.GetComponent<CMUHeartPressureProbeComponent>(patient).Toxin, Is.EqualTo(FixedPoint2.Zero));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StoppedDonorUsesRecipientArrestHistoryAcrossMissingHeartAndStasis()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid donor = default;
        EntityUid donorHeart = default;
        EntityUid recipient = default;
        EntityUid originalHeart = default;
        EntityUid part = default;
        string slot = default!;
        TimeSpan removedAt = default;
        TimeSpan stasisEntered = default;
        TimeSpan stasisLeft = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            var coordinates = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            (donor, donorHeart) = CreatePatient(entities, coordinates);
            (recipient, originalHeart) = CreatePatient(entities, coordinates);
            DamageToStage(entities, donor, donorHeart, OrganDamageStage.Dead);
            entities.System<SharedOrganHealthSystem>().HealOrgan(donorHeart, donor, 100);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrganPart(originalHeart, out part), Is.True);
            slot = index.GetOrganSlots(part).Single(entry => entry.Organ == originalHeart).SlotId;
            removedAt = pair.Server.Timing.CurTime;
            Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(originalHeart), Is.True);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.2f));
        await pair.Server.WaitPost(() =>
        {
            stasisEntered = pair.Server.Timing.CurTime;
            pair.Server.EntMan.EnsureComponent<CMInStasisComponent>(recipient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.1f));
        await pair.Server.WaitPost(() =>
        {
            stasisLeft = pair.Server.Timing.CurTime;
            pair.Server.EntMan.RemoveComponent<CMInStasisComponent>(recipient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var bodies = entities.System<SharedBodySystem>();
            Assert.That(bodies.RemoveOrgan(donorHeart), Is.True);
            Assert.That(bodies.InsertOrgan(part, donorHeart, slot), Is.True);
            var heartbeat = entities.GetComponent<HeartComponent>(donorHeart);
            Assert.That(heartbeat.Stopped, Is.True);
            Assert.That(heartbeat.NoPulseSince, Is.EqualTo(removedAt + stasisLeft - stasisEntered),
                "Donor age must not replace the recipient's active arrest history.");
            Assert.That(entities.HasComponent<MissingHeartComponent>(recipient), Is.False);
            Assert.That(bodies.RemoveOrgan(donorHeart), Is.True);
            var missing = entities.GetComponent<MissingHeartComponent>(recipient);
            Assert.That(missing.NoPulseElapsed,
                Is.EqualTo(pair.Server.Timing.CurTime - removedAt - (stasisLeft - stasisEntered)),
                "Extraction must not reset an existing recipient arrest interval.");
            entities.DeleteEntity(donor);
            entities.DeleteEntity(recipient);
            entities.DeleteEntity(donorHeart);
            entities.DeleteEntity(originalHeart);
        });
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Patient, EntityUid Heart) CreatePatient(IEntityManager entities,
        MapCoordinates? coordinates = null)
    {
        entities.System<CMUHeartPressureProbeSystem>();
        var patient = entities.SpawnEntity("CMMobHuman", coordinates ?? MapCoordinates.Nullspace);
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out var heart), Is.True);
        entities.AddComponent<CMUHeartPressureProbeComponent>(patient).Origin = heart;
        return (patient, heart);
    }

    private static void DelayService(IEntityManager entities, EntityUid heart)
    {
        var heartbeat = entities.GetComponent<HeartComponent>(heart);
        heartbeat.NextOrganDamageTick = TimeSpan.MaxValue;
        heartbeat.NextCardiacArrestTick = TimeSpan.MaxValue;
        heartbeat.NextPulseUpdate = TimeSpan.MaxValue;
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

    private static void SetFrozen(IEntityManager entities, EntityUid patient, EntityUid heart, string mode, bool frozen)
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
        if (mode is "heart" or "both")
            metadata.SetEntityPaused(heart, frozen);
    }
}

[RegisterComponent]
public sealed partial class CMUHeartPressureProbeComponent : Component
{
    public EntityUid Origin;
    public FixedPoint2 Asphyx;
    public FixedPoint2 Toxin;
}

public sealed partial class CMUHeartPressureProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHeartPressureProbeComponent, DamageChangedEvent>(OnDamage);
    }

    private void OnDamage(Entity<CMUHeartPressureProbeComponent> ent, ref DamageChangedEvent args)
    {
        if (args.Origin != ent.Comp.Origin || args.DamageDelta is not { } delta)
            return;
        ent.Comp.Asphyx += FixedPoint2.Max(FixedPoint2.Zero, delta.DamageDict.GetValueOrDefault("Asphyxiation"));
        ent.Comp.Toxin += FixedPoint2.Max(FixedPoint2.Zero, delta.DamageDict.GetValueOrDefault("Poison"));
    }
}
