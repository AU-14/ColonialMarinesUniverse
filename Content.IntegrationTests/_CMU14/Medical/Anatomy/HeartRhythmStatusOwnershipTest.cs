#pragma warning disable RA0002 // Public interactions drive changes; assertions inspect committed source identities.
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Administration.Systems;
using Content.Shared.Alert;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class HeartRhythmStatusOwnershipTest
{
    private const string TestReagent = "CMUTestRhythmOverdose";
    private const string Methamphetamine = "CMUMethamphetamine";
    private const string TissueTachycardia = "StatusEffectCMUHeartTachycardia";
    private const string TissueArrhythmia = "StatusEffectCMUHeartArrhythmia";
    private const string DrugTachycardia = "StatusEffectCMUTachycardia";
    private const string DrugArrhythmia = "StatusEffectCMUArrhythmia";
    private static readonly ProtoId<AlertPrototype> TachycardiaAlert = "CMUTachycardia";
    private static readonly ProtoId<AlertPrototype> ArrhythmiaAlert = "CMUArrhythmia";

    [TestCase("Cardiopeutic")]
    [TestCase("Musclestimulating")]
    [TestCase("Defibrillating")]
    public async Task ActualOverdoseRefreshAndExpiryCannotShortenPersistentTissueRhythm(string property)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        await pair.LoadPrototypes([OverdosePrototype(property)]);
        EntityUid patient = default;
        EntityUid? tissue = null;
        EntityUid? drug = null;
        TimeSpan? firstExpiry = null;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var spawned = SpawnPatient(entities);
            patient = spawned.Patient;
            DamageToStage(entities, patient, spawned.Heart, OrganDamageStage.Failing);
            tissue = Effect(entities, patient, TissueArrhythmia);
            AddChemical(entities, patient, TestReagent, 20);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            drug = Effect(entities, patient, DrugArrhythmia);
            firstExpiry = entities.GetComponent<StatusEffectComponent>(drug.Value).EndEffectTime;
            Assert.That(firstExpiry, Is.Not.Null);
            Assert.That(Effect(entities, patient, TissueArrhythmia), Is.EqualTo(tissue));
            Assert.That(entities.GetComponent<StatusEffectComponent>(tissue!.Value).EndEffectTime, Is.Null);
            Assert.That(BloodSolution(entities, patient).Comp.Solution.GetTotalPrototypeQuantity(TestReagent), Is.LessThan((FixedPoint2)20),
                "The assertion must be driven by real bloodstream metabolism.");
        });
        await pair.RunTicksSync(pair.SecondsToTicks(2.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(Effect(entities, patient, DrugArrhythmia), Is.EqualTo(drug));
            Assert.That(entities.GetComponent<StatusEffectComponent>(drug!.Value).EndEffectTime, Is.GreaterThan(firstExpiry));
            Assert.That(Effect(entities, patient, TissueArrhythmia), Is.EqualTo(tissue));
            ClearChemical(entities, patient, TestReagent);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(3.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, DrugArrhythmia), Is.False);
            Assert.That(Effect(entities, patient, TissueArrhythmia), Is.EqualTo(tissue));
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, ArrhythmiaAlert), Is.True,
                "Expiry of one source must not clear the surviving shared alert.");
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ActualMethamphetamineTimerSurvivesTissueHealingAndThenExpires()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid heart = default;
        EntityUid? drug = null;
        TimeSpan? expiry = null;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, heart) = SpawnPatient(entities);
            DamageToStage(entities, patient, heart, OrganDamageStage.Bruised);
            AddChemical(entities, patient, Methamphetamine, 30);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            drug = Effect(entities, patient, DrugTachycardia);
            expiry = entities.GetComponent<StatusEffectComponent>(drug.Value).EndEffectTime;
            Assert.That(expiry, Is.Not.Null, "Tissue cannot promote the timed drug source to an infinite duration.");
            ClearChemical(entities, patient, Methamphetamine);
            entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, 100);
            Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, TissueTachycardia), Is.False);
            Assert.That(Effect(entities, patient, DrugTachycardia), Is.EqualTo(drug));
            Assert.That(entities.GetComponent<StatusEffectComponent>(drug.Value).EndEffectTime, Is.EqualTo(expiry));
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, TachycardiaAlert), Is.True);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(6.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, DrugTachycardia), Is.False);
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, TachycardiaAlert), Is.False);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(OrganDamageStage.Bruised, TissueTachycardia)]
    [TestCase(OrganDamageStage.Damaged, TissueArrhythmia)]
    public async Task SameTickHealingAndReinjuryKeepsTheReplacementSource(OrganDamageStage stage, string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid? replacement = null;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (body, heart) = SpawnPatient(entities);
            patient = body;
            DamageToStage(entities, patient, heart, stage);
            var original = Effect(entities, patient, prototype);
            entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, 100);
            Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, prototype), Is.False);
            DamageToStage(entities, patient, heart, stage);
            replacement = Effect(entities, patient, prototype);
            Assert.That(replacement, Is.Not.EqualTo(original));
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(Effect(entities, patient, prototype), Is.EqualTo(replacement));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MultipleHeartsRetainIndependentSymptomsAcrossTransplantAndDeletion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid recipient = default;
        EntityUid recipientHeart = default;
        EntityUid donor = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var coords = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            var first = SpawnPatient(entities, coords);
            patient = first.Patient;
            (recipient, recipientHeart) = SpawnPatient(entities, coords);
            donor = AddSecondHeart(entities, patient);
            DamageToStage(entities, patient, first.Heart, OrganDamageStage.Bruised);
            DamageToStage(entities, patient, donor, OrganDamageStage.Damaged);
            AssertTissue(entities, patient, true, true);
            entities.System<SharedOrganHealthSystem>().HealOrgan(first.Heart, patient, 100);
            AssertTissue(entities, patient, false, true);
            var bodies = entities.System<SharedBodySystem>();
            Assert.That(bodies.RemoveOrgan(donor), Is.True);
            AssertTissue(entities, patient, false, false);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrganPart(recipientHeart, out var recipientPart), Is.True);
            Assert.That(bodies.RemoveOrgan(recipientHeart), Is.True);
            Assert.That(bodies.InsertOrgan(recipientPart, donor, "heart"), Is.True);
            AssertTissue(entities, recipient, false, true);
            entities.DeleteEntity(donor);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertTissue(entities, recipient, false, false);
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(recipient, ArrhythmiaAlert), Is.False);
            entities.DeleteEntity(patient);
            entities.DeleteEntity(recipient);
            entities.DeleteEntity(recipientHeart);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task LayerToggleReprojectsPausedTissueWithoutRemovingIndependentMedication(bool wholeMedicalLayer)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (patient, heart) = SpawnPatient(entities);
            var variable = wholeMedicalLayer ? CMUMedicalCCVars.Enabled : CMUMedicalCCVars.OrganEnabled;
            var original = pair.Server.CfgMan.GetCVar(variable);
            try
            {
                DamageToStage(entities, patient, heart, OrganDamageStage.Damaged);
                Assert.That(entities.System<StatusEffectsSystem>().TrySetStatusEffectDuration(patient, DrugArrhythmia,
                    TimeSpan.FromSeconds(30)), Is.True);
                var drug = Effect(entities, patient, DrugArrhythmia);
                entities.System<MetaDataSystem>().SetEntityPaused(patient, true);
                pair.Server.CfgMan.SetCVar(variable, false);
                AssertTissue(entities, patient, false, false);
                Assert.That(Effect(entities, patient, DrugArrhythmia), Is.EqualTo(drug));
                pair.Server.CfgMan.SetCVar(variable, true);
                AssertTissue(entities, patient, false, true);
                Assert.That(Effect(entities, patient, DrugArrhythmia), Is.EqualTo(drug));
            }
            finally
            {
                pair.Server.CfgMan.SetCVar(variable, original);
                entities.System<MetaDataSystem>().SetEntityPaused(patient, false);
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("pause")]
    [TestCase("stasis")]
    public async Task SuspensionRetainsTissueAndFullRejuvenationClearsEveryRhythmSource(string suspension)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var spawned = SpawnPatient(entities);
            patient = spawned.Patient;
            DamageToStage(entities, patient, spawned.Heart, OrganDamageStage.Damaged);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(patient, DrugTachycardia, TimeSpan.FromSeconds(30)), Is.True);
            Assert.That(status.TrySetStatusEffectDuration(patient, DrugArrhythmia, TimeSpan.FromSeconds(30)), Is.True);
            if (suspension == "pause") entities.System<MetaDataSystem>().SetEntityPaused(patient, true);
            else entities.EnsureComponent<CMInStasisComponent>(patient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(3.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertTissue(entities, patient, false, true);
            if (suspension == "pause") entities.System<MetaDataSystem>().SetEntityPaused(patient, false);
            else entities.RemoveComponent<CMInStasisComponent>(patient);
            entities.System<RejuvenateSystem>().PerformRejuvenate(patient);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertTissue(entities, patient, false, false);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.HasStatusEffect(patient, DrugArrhythmia), Is.False);
            Assert.That(status.HasStatusEffect(patient, DrugTachycardia), Is.False);
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, TachycardiaAlert), Is.False);
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, ArrhythmiaAlert), Is.False);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StatusPermissionCallbackHealingCannotLeaveAStaleTissueSource()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<CMUHeartRhythmMutationProbeSystem>();
            var (patient, heart) = SpawnPatient(entities);
            var probe = entities.AddComponent<CMUHeartRhythmMutationProbeComponent>(patient);
            probe.Heart = heart;
            probe.HealOnPermission = true;
            DamageToStage(entities, patient, heart, OrganDamageStage.Damaged, verifyStage: false);
            Assert.That(probe.Ran, Is.True);
            Assert.That(entities.GetComponent<OrganHealthComponent>(heart).Stage, Is.EqualTo(OrganDamageStage.Healthy));
            AssertTissue(entities, patient, false, false);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DamagedDonorStatusCallbackCannotClearTheNewerRecipientArrest(bool replaceHeart)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid donor = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<CMUHeartRhythmMutationProbeSystem>();
            var coords = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            (patient, donor) = SpawnPatient(entities, coords);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrganPart(donor, out var part), Is.True);
            DamageToStage(entities, patient, donor, OrganDamageStage.Damaged);
            var bodies = entities.System<SharedBodySystem>();
            Assert.That(bodies.RemoveOrgan(donor), Is.True);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.HasStatusEffect(patient, "StatusEffectCMUCardiacArrest"), Is.True);
            var originalHeart = entities.GetComponent<HeartComponent>(donor);
            var probe = entities.AddComponent<CMUHeartRhythmMutationProbeComponent>(patient);
            probe.Heart = donor;
            probe.RemoveOnPermission = !replaceHeart;
            probe.ReplaceHeartOnPermission = replaceHeart;

            // Real insertion creates the tissue source. Its permission callback
            // starts a newer extraction or replaces the cardiac component.
            Assert.That(bodies.InsertOrgan(part, donor, "heart"), Is.True);
            Assert.That(probe.Ran, Is.True);
            Assert.That(status.HasStatusEffect(patient, "StatusEffectCMUCardiacArrest"), Is.True,
                "The original insertion must not clear the newer circulation failure.");
            if (replaceHeart)
            {
                var replacement = entities.GetComponent<HeartComponent>(donor);
                Assert.That(replacement, Is.Not.SameAs(originalHeart));
                Assert.That(replacement.Stopped, Is.True);
                Assert.That(entities.HasComponent<MissingHeartComponent>(patient), Is.False);
            }
            else
            {
                Assert.That(entities.GetComponent<OrganComponent>(donor).Body, Is.Null);
                Assert.That(entities.HasComponent<MissingHeartComponent>(patient), Is.True);
                AssertTissue(entities, patient, false, false);
            }
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUCardiacArrest"), Is.True);
            entities.DeleteEntity(patient);
            if (entities.EntityExists(donor)) entities.DeleteEntity(donor);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DirectHeartOrHealthReplacementReprojectsOnlyTheCurrentTissue(bool replaceHealth)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (patient, heart) = SpawnPatient(entities);
            DamageToStage(entities, patient, heart, OrganDamageStage.Damaged);
            var original = Effect(entities, patient, TissueArrhythmia);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(patient, DrugArrhythmia, TimeSpan.FromSeconds(30)), Is.True);
            var drug = Effect(entities, patient, DrugArrhythmia);
            if (replaceHealth) entities.RemoveComponent<OrganHealthComponent>(heart);
            else entities.RemoveComponent<HeartComponent>(heart);
            AssertTissue(entities, patient, false, false);
            Assert.That(Effect(entities, patient, DrugArrhythmia), Is.EqualTo(drug));
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, ArrhythmiaAlert), Is.True);

            // OrganHealth initializes its stage before publishing Startup. A
            // replacement with configured damage must appear immediately.
            if (replaceHealth)
                entities.AddComponent(heart, new OrganHealthComponent { Current = 20 });
            else
                entities.AddComponent<HeartComponent>(heart);
            Assert.That(entities.GetComponent<OrganHealthComponent>(heart).Stage, Is.EqualTo(OrganDamageStage.Damaged));
            Assert.That(Effect(entities, patient, TissueArrhythmia), Is.Not.EqualTo(original));
            Assert.That(Effect(entities, patient, DrugArrhythmia), Is.EqualTo(drug));
            entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, 100);
            AssertTissue(entities, patient, false, false);
            Assert.That(Effect(entities, patient, DrugArrhythmia), Is.EqualTo(drug));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PermissionCallbackRemovingHeartOrHealthCannotLeaveAStaleTissueSource(bool removeHealth)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<CMUHeartRhythmMutationProbeSystem>();
            var (patient, heart) = SpawnPatient(entities);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(patient, DrugArrhythmia, TimeSpan.FromSeconds(30)), Is.True);
            var drug = Effect(entities, patient, DrugArrhythmia);
            var probe = entities.AddComponent<CMUHeartRhythmMutationProbeComponent>(patient);
            probe.Heart = heart;
            probe.RemoveHeartOnPermission = !removeHealth;
            probe.RemoveHealthOnPermission = removeHealth;
            DamageToStage(entities, patient, heart, OrganDamageStage.Damaged, verifyStage: false);
            Assert.That(probe.Ran, Is.True);
            Assert.That(removeHealth ? entities.HasComponent<OrganHealthComponent>(heart) : entities.HasComponent<HeartComponent>(heart), Is.False);
            AssertTissue(entities, patient, false, false);
            Assert.That(Effect(entities, patient, DrugArrhythmia), Is.EqualTo(drug));
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, ArrhythmiaAlert), Is.True);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(OrganDamageStage.Bruised, TissueTachycardia)]
    [TestCase(OrganDamageStage.Damaged, TissueArrhythmia)]
    public async Task NewOrganInjuryAfterFullResetRetainsItsNewRhythmSource(OrganDamageStage stage, string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid replacement = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var spawned = SpawnPatient(entities);
            patient = spawned.Patient;
            DamageToStage(entities, patient, spawned.Heart, stage);
            var original = Effect(entities, patient, prototype);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(patient, DrugArrhythmia, TimeSpan.FromSeconds(30)), Is.True);
            entities.System<RejuvenateSystem>().PerformRejuvenate(patient);
            AssertTissue(entities, patient, false, false);
            DamageToStage(entities, patient, spawned.Heart, stage);
            replacement = Effect(entities, patient, prototype);
            Assert.That(replacement, Is.Not.EqualTo(original));
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(Effect(entities, patient, prototype), Is.EqualTo(replacement));
            Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, DrugArrhythmia), Is.False);
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient,
                stage == OrganDamageStage.Bruised ? TachycardiaAlert : ArrhythmiaAlert), Is.True);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RhythmRemovalCallbackQueuingPatientDeletionCannotCreateMissingHeartState()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid heart = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<CMUHeartRhythmMutationProbeSystem>();
            var coords = entities.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            (patient, heart) = SpawnPatient(entities, coords);
            DamageToStage(entities, patient, heart, OrganDamageStage.Damaged);
            var effect = Effect(entities, patient, TissueArrhythmia);
            var probe = entities.AddComponent<CMUHeartRhythmMutationProbeComponent>(effect);
            probe.QueuePatientDeletionOnRemoval = true;
            Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(heart), Is.True);
            Assert.That(probe.Ran, Is.True);
            Assert.That(entities.IsQueuedForDeletion(patient), Is.True);
            Assert.That(entities.HasComponent<MissingHeartComponent>(patient), Is.False,
                "The old extraction has no authority to add physiology to a retiring patient.");
            Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUCardiacArrest"), Is.False);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.EntityExists(patient), Is.False);
            if (entities.EntityExists(heart)) entities.DeleteEntity(heart);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConnectedAlertsRetainTheSurvivingSourceAfterHealingAndExpiry()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var originalPlayer = player.AttachedEntity;
        var entities = pair.Server.EntMan;
        EntityUid patient = default;
        EntityUid heart = default;
        NetEntity patientNet = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                pair.Server.PlayerMan.SetAttachedEntity(player, patient);
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out heart), Is.True);
                DamageToStage(entities, patient, heart, OrganDamageStage.Damaged);
                Assert.That(entities.System<StatusEffectsSystem>().TrySetStatusEffectDuration(patient,
                    DrugArrhythmia, TimeSpan.FromSeconds(30)), Is.True);
                patientNet = entities.GetNetEntity(patient);
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() => AssertClientRhythm(pair.Client.EntMan, patientNet, true, true));
            await pair.Server.WaitAssertion(() => entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, 100));
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() => AssertClientRhythm(pair.Client.EntMan, patientNet, false, true));
            await pair.Server.WaitAssertion(() =>
            {
                DamageToStage(entities, patient, heart, OrganDamageStage.Damaged);
                Assert.That(entities.System<StatusEffectsSystem>().TrySetStatusEffectDuration(patient,
                    DrugArrhythmia, TimeSpan.FromSeconds(0.1)), Is.True);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(0.3f));
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() => AssertClientRhythm(pair.Client.EntMan, patientNet, true, false));
            await pair.Server.WaitAssertion(() => entities.DeleteEntity(heart));
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() => AssertClientRhythm(pair.Client.EntMan, patientNet, false, false));
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                pair.Server.PlayerMan.SetAttachedEntity(player, originalPlayer);
                if (entities.EntityExists(patient)) entities.DeleteEntity(patient);
            });
        }
        await pair.RunUntilSynced();
        await pair.CleanReturnAsync();
    }

    private static void AssertClientRhythm(IEntityManager entities, NetEntity patientNet, bool tissue, bool drug)
    {
        var patient = entities.GetEntity(patientNet);
        var status = entities.System<StatusEffectsSystem>();
        Assert.That(status.HasStatusEffect(patient, TissueArrhythmia), Is.EqualTo(tissue));
        Assert.That(status.HasStatusEffect(patient, DrugArrhythmia), Is.EqualTo(drug));
        Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, ArrhythmiaAlert), Is.EqualTo(tissue || drug));
    }

    private static (EntityUid Patient, EntityUid Heart) SpawnPatient(IEntityManager entities, MapCoordinates? coordinates = null)
    {
        var patient = entities.SpawnEntity("CMMobHuman", coordinates ?? MapCoordinates.Nullspace);
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out var heart), Is.True);
        return (patient, heart);
    }

    private static EntityUid AddSecondHeart(IEntityManager entities, EntityUid patient)
    {
        var index = entities.System<CMUMedicalBodyIndexSystem>();
        Assert.That(index.TryGetBodyPart(patient, new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
        var bodies = entities.System<SharedBodySystem>();
        Assert.That(bodies.TryCreateOrganSlot(arm, "heart", out _), Is.True);
        var donor = entities.SpawnEntity("CMUOrganHumanHeart", MapCoordinates.Nullspace);
        Assert.That(bodies.InsertOrgan(arm, donor, "heart"), Is.True);
        return donor;
    }

    private static void DamageToStage(IEntityManager entities, EntityUid patient, EntityUid heart, OrganDamageStage stage,
        bool verifyStage = true)
    {
        var health = entities.GetComponent<OrganHealthComponent>(heart);
        var ev = new OrganDamagedEvent(patient, heart,
            new DamageSpecifier { DamageDict = { ["Blunt"] = health.Current - health.StageThresholds[stage] } },
            OrganDamageSource.Direct);
        entities.EventBus.RaiseLocalEvent(heart, ref ev, broadcast: true);
        if (verifyStage) Assert.That(health.Stage, Is.EqualTo(stage));
    }

    private static EntityUid Effect(IEntityManager entities, EntityUid patient, EntProtoId prototype)
    {
        Assert.That(entities.System<StatusEffectsSystem>().TryGetStatusEffect(patient, prototype, out var effect), Is.True, prototype.Id);
        return effect!.Value;
    }

    private static void AssertTissue(IEntityManager entities, EntityUid patient, bool tachycardia, bool arrhythmia)
    {
        var status = entities.System<StatusEffectsSystem>();
        Assert.That(status.HasStatusEffect(patient, TissueTachycardia), Is.EqualTo(tachycardia));
        Assert.That(status.HasStatusEffect(patient, TissueArrhythmia), Is.EqualTo(arrhythmia));
    }

    private static Entity<SolutionComponent> BloodSolution(IEntityManager entities, EntityUid patient)
    {
        Assert.That(entities.System<SharedSolutionContainerSystem>().TryGetSolution(patient,
            BloodstreamComponent.DefaultBloodSolutionName, out var solution), Is.True);
        return solution!.Value;
    }

    private static void AddChemical(IEntityManager entities, EntityUid patient, string reagent, FixedPoint2 quantity)
        => Assert.That(entities.System<SharedSolutionContainerSystem>().TryAddReagent(BloodSolution(entities, patient), reagent, quantity), Is.True);

    private static void ClearChemical(IEntityManager entities, EntityUid patient, string reagent)
    {
        var solution = BloodSolution(entities, patient);
        var quantity = solution.Comp.Solution.GetTotalPrototypeQuantity(reagent);
        Assert.That(quantity, Is.GreaterThan(FixedPoint2.Zero));
        Assert.That(entities.System<SharedSolutionContainerSystem>().RemoveReagent(solution, reagent, quantity), Is.EqualTo(quantity));
    }

    private static string OverdosePrototype(string property) => $$"""
        - type: reagent
          id: {{TestReagent}}
          name: rhythm ownership test reagent
          desc: one real medication property for overlap coverage
          physicalDesc: reagent-physical-desc-translucent
          color: "#ffffff"
          overdose: 10
          criticalOverdose: 100
          metabolisms:
            Bloodstream:
              metabolismRate: 0.1
              effects:
              - !type:{{property}}
                potency: 1
        """;
}

[RegisterComponent]
public sealed partial class CMUHeartRhythmMutationProbeComponent : Component
{
    public EntityUid Heart;
    public bool HealOnPermission;
    public bool RemoveOnPermission;
    public bool ReplaceHeartOnPermission;
    public bool RemoveHeartOnPermission;
    public bool RemoveHealthOnPermission;
    public bool QueuePatientDeletionOnRemoval;
    public bool Ran;
}

public sealed partial class CMUHeartRhythmMutationProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHeartRhythmMutationProbeComponent, BeforeStatusEffectAddedEvent>(OnPermission);
        SubscribeLocalEvent<CMUHeartRhythmMutationProbeComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnPermission(Entity<CMUHeartRhythmMutationProbeComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (args.Effect.Id != "StatusEffectCMUHeartArrhythmia")
            return;
        if (ent.Comp.RemoveHeartOnPermission || ent.Comp.RemoveHealthOnPermission)
        {
            var removeHealth = ent.Comp.RemoveHealthOnPermission;
            ent.Comp.RemoveHeartOnPermission = false;
            ent.Comp.RemoveHealthOnPermission = false;
            ent.Comp.Ran = true;
            if (removeHealth) RemComp<OrganHealthComponent>(ent.Comp.Heart);
            else RemComp<HeartComponent>(ent.Comp.Heart);
            return;
        }
        if (ent.Comp.RemoveOnPermission)
        {
            ent.Comp.RemoveOnPermission = false;
            ent.Comp.Ran = true;
            Assert.That(EntityManager.System<SharedBodySystem>().RemoveOrgan(ent.Comp.Heart), Is.True);
            return;
        }
        if (ent.Comp.ReplaceHeartOnPermission)
        {
            ent.Comp.ReplaceHeartOnPermission = false;
            ent.Comp.Ran = true;
            RemComp<HeartComponent>(ent.Comp.Heart);
            // The replacement's stopped state is its own fixture configuration.
            // The old insertion must not use its preceding beating component.
            AddComp<HeartComponent>(ent.Comp.Heart).Stopped = true;
            return;
        }
        if (!ent.Comp.HealOnPermission)
            return;
        ent.Comp.HealOnPermission = false;
        ent.Comp.Ran = true;
        EntityManager.System<SharedOrganHealthSystem>().HealOrgan(ent.Comp.Heart, ent.Owner, 100);
    }

    private void OnRemoved(Entity<CMUHeartRhythmMutationProbeComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!ent.Comp.QueuePatientDeletionOnRemoval)
            return;
        ent.Comp.QueuePatientDeletionOnRemoval = false;
        ent.Comp.Ran = true;
        QueueDel(args.Target);
    }
}
