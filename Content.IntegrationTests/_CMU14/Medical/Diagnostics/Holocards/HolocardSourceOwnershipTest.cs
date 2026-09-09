#pragma warning disable RA0002 // Public commands drive changes; inspect committed sources and anatomy.
using System.Numerics;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Medical.HUD.Systems;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Administration.Systems;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Diagnostics.Holocards;

[TestFixture]
public sealed class HolocardSourceOwnershipTest
{
    [TestPrototypes]
    private const string ParasitePrototype = """
        - type: entity
          parent: CMXenoParasite
          id: CMUTestHolocardParasite
          components:
          - type: XenoParasite
            fallOffDelay: 0
        """;

    [TestCase(false)]
    [TestCase(true)]
    public async Task AutomaticAssessmentDowngradesToRemainingTraumaAndThenClears(bool multipleOrgans)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrgan<KidneysComponent>(patient, out var kidneys), Is.True);
            Assert.That(index.TryGetOrgan<LiverComponent>(patient, out var liver), Is.True);
            var arm = FractureArm(entities, patient);
            DamageToStage(entities, patient, kidneys, OrganDamageStage.Failing);
            if (multipleOrgans)
                DamageToStage(entities, patient, liver, OrganDamageStage.Failing);
            AssertCard(entities, patient, HolocardStatus.OrganFailure, HolocardStatus.None, HolocardStatus.OrganFailure);
            entities.System<SharedOrganHealthSystem>().HealOrgan(kidneys, patient, 100);
            if (multipleOrgans)
            {
                AssertCard(entities, patient, HolocardStatus.OrganFailure, HolocardStatus.None, HolocardStatus.OrganFailure);
                entities.System<SharedOrganHealthSystem>().HealOrgan(liver, patient, 100);
            }
            AssertCard(entities, patient, HolocardStatus.Trauma, HolocardStatus.None, HolocardStatus.Trauma);
            ClearFracture(entities, arm);
            AssertCard(entities, patient, HolocardStatus.None, HolocardStatus.None, HolocardStatus.None);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(HolocardStatus.Trauma)]
    [TestCase(HolocardStatus.OrganFailure)]
    [TestCase(HolocardStatus.Emergency)]
    [TestCase(HolocardStatus.Xeno)]
    [TestCase(HolocardStatus.Permadead)]
    public async Task ManualAnnotationSurvivesHealingEvenWhenItMatchesAnAutomaticLabel(HolocardStatus annotation)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid medic = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            medic = CreateMedic(entities, map.GridCoords);
            var arm = FractureArm(entities, patient);
            SendManual(entities, patient, medic, annotation);
            ClearFracture(entities, arm);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertCard(entities, patient, annotation, annotation, HolocardStatus.None);
            SendManual(entities, patient, medic, HolocardStatus.None);
            AssertCard(entities, patient, HolocardStatus.None, HolocardStatus.None, HolocardStatus.None);
            entities.DeleteEntity(patient);
            entities.DeleteEntity(medic);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClearingManualSourceRevealsCurrentInjuryAndRejuvenationPreservesExplicitAnnotation()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid medic = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            medic = CreateMedic(entities, map.GridCoords);
            FractureArm(entities, patient);
            SendManual(entities, patient, medic, HolocardStatus.Emergency);
            AssertCard(entities, patient, HolocardStatus.Emergency, HolocardStatus.Emergency, HolocardStatus.Trauma);
            SendManual(entities, patient, medic, HolocardStatus.None);
            AssertCard(entities, patient, HolocardStatus.Trauma, HolocardStatus.None, HolocardStatus.Trauma);
            SendManual(entities, patient, medic, HolocardStatus.Urgent);
            entities.System<RejuvenateSystem>().PerformRejuvenate(patient);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertCard(entities, patient, HolocardStatus.Urgent, HolocardStatus.Urgent, HolocardStatus.None);
            entities.DeleteEntity(patient);
            entities.DeleteEntity(medic);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RealInfectionAndPublicChemicalCureReleaseOnlyTheAutomaticXenoSource()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid medic = default;
        EntityUid parasite = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            medic = CreateMedic(entities, map.GridCoords);
            SendManual(entities, patient, medic, HolocardStatus.Emergency);
            parasite = entities.SpawnEntity("CMUTestHolocardParasite", map.GridCoords.Offset(new Vector2(1, 0)));
            Assert.That(entities.System<SharedXenoParasiteSystem>().Infect(
                (parasite, entities.GetComponent<XenoParasiteComponent>(parasite)), patient, force: true), Is.True);
        });
        await pair.RunTicksSync(5);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.HasComponent<VictimInfectedComponent>(patient), Is.True);
            AssertCard(entities, patient, HolocardStatus.Xeno, HolocardStatus.Emergency, HolocardStatus.Xeno);
            Assert.That(entities.System<SharedXenoParasiteSystem>().TryChemicallyExpelInfection(patient), Is.True);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.HasComponent<VictimInfectedComponent>(patient), Is.False);
            AssertCard(entities, patient, HolocardStatus.Emergency, HolocardStatus.Emergency, HolocardStatus.None);
            entities.DeleteEntity(patient);
            entities.DeleteEntity(medic);
            if (entities.EntityExists(parasite)) entities.DeleteEntity(parasite);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("reattach")]
    [TestCase("revive")]
    [TestCase("rejuvenate")]
    public async Task BrainRemovalAssessmentIsReversibleAndNeverConsumesTheManualAnnotation(string resolution)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid medic = default;
        EntityUid brain = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            medic = CreateMedic(entities, map.GridCoords);
            SendManual(entities, patient, medic, HolocardStatus.Urgent);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrgan<CMUBrainComponent>(patient, out brain), Is.True);
            Assert.That(index.TryGetOrganPart(brain, out var head), Is.True);
            var bodies = entities.System<SharedBodySystem>();
            Assert.That(bodies.RemoveOrgan(brain), Is.True);
            Assert.That(entities.GetComponent<HolocardStateComponent>(patient).BrainRemovalAssessment, Is.True);
            Assert.That(entities.GetComponent<HolocardStateComponent>(patient).HolocardStatus, Is.EqualTo(HolocardStatus.Permadead));
            Assert.That(entities.GetComponent<HolocardStateComponent>(patient).ManualStatus, Is.EqualTo(HolocardStatus.Urgent));
            switch (resolution)
            {
                case "reattach": Assert.That(bodies.InsertOrgan(head, brain, "brain"), Is.True); break;
                case "revive":
                    var defibrillator = entities.SpawnEntity("CMDefibrillator", map.GridCoords);
                    Assert.That(entities.System<ItemToggleSystem>().TryActivate(defibrillator, user: medic), Is.True);
                    var defibs = entities.System<SharedDefibrillatorSystem>();
                    Assert.That(defibs.CanZap(defibrillator, patient, medic), Is.True);
                    defibs.Zap(defibrillator, patient, medic);
                    Assert.That(entities.GetComponent<MobStateComponent>(patient).CurrentState, Is.Not.EqualTo(MobState.Dead));
                    entities.DeleteEntity(defibrillator);
                    break;
                case "rejuvenate": entities.System<RejuvenateSystem>().PerformRejuvenate(patient); break;
            }
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<HolocardStateComponent>(patient).BrainRemovalAssessment, Is.False);
            AssertCard(entities, patient, HolocardStatus.Urgent, HolocardStatus.Urgent, HolocardStatus.None);
            entities.DeleteEntity(patient);
            entities.DeleteEntity(medic);
            if (entities.EntityExists(brain)) entities.DeleteEntity(brain);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DiagnosticsToggleReleasesAndReconstructsAutomaticSourceOnPausedPatient()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var medic = CreateMedic(entities, map.GridCoords);
            var original = pair.Server.CfgMan.GetCVar(CMUMedicalCCVars.DiagnosticsEnabled);
            try
            {
                SendManual(entities, patient, medic, HolocardStatus.Urgent);
                FractureArm(entities, patient);
                entities.System<MetaDataSystem>().SetEntityPaused(patient, true);
                pair.Server.CfgMan.SetCVar(CMUMedicalCCVars.DiagnosticsEnabled, false);
                AssertCard(entities, patient, HolocardStatus.Urgent, HolocardStatus.Urgent, HolocardStatus.None);
                pair.Server.CfgMan.SetCVar(CMUMedicalCCVars.DiagnosticsEnabled, true);
                AssertCard(entities, patient, HolocardStatus.Trauma, HolocardStatus.Urgent, HolocardStatus.Trauma);
            }
            finally
            {
                pair.Server.CfgMan.SetCVar(CMUMedicalCCVars.DiagnosticsEnabled, original);
                entities.System<MetaDataSystem>().SetEntityPaused(patient, false);
                entities.DeleteEntity(patient);
                entities.DeleteEntity(medic);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AutomaticDowngradeUpdatesTheActualStasisBagAppearance()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var bag = entities.SpawnEntity("CMStasisBag", map.GridCoords);
            var storage = entities.System<SharedEntityStorageSystem>();
            storage.CloseStorage(bag);
            Assert.That(storage.Insert(patient, bag), Is.True);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrgan<KidneysComponent>(patient, out var kidneys), Is.True);
            var arm = FractureArm(entities, patient);
            DamageToStage(entities, patient, kidneys, OrganDamageStage.Failing);
            AssertBag(entities, bag, HolocardStatus.OrganFailure);
            entities.System<SharedOrganHealthSystem>().HealOrgan(kidneys, patient, 100);
            AssertBag(entities, bag, HolocardStatus.Trauma);
            ClearFracture(entities, arm);
            AssertBag(entities, bag, HolocardStatus.None);
            entities.DeleteEntity(bag);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AuthenticatedHolocardNetworkCommandsRejectForgedMedicAndUndefinedStatus()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var oldActor = player.AttachedEntity;
        var entities = pair.Server.EntMan;
        EntityUid patient = default, actor = default, otherMedic = default;
        NetEntity patientNet = default, actorNet = default, otherNet = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                actor = CreateMedic(entities, map.GridCoords);
                otherMedic = CreateMedic(entities, map.GridCoords);
                pair.Server.PlayerMan.SetAttachedEntity(player, actor);
                var hud = entities.SpawnEntity("RMCGlassesMedicalHUDGlasses", map.GridCoords);
                Assert.That(entities.System<InventorySystem>().TryEquip(actor, hud, "eyes", silent: true, force: true), Is.True);
                var verbs = entities.System<SharedVerbSystem>().GetLocalVerbs(patient, actor, typeof(ExamineVerb));
                var verb = verbs.Single(value => value.Text == Loc.GetString("scannable-holocard-verb-text"));
                Assert.That(verb.Act, Is.Not.Null);
                verb.Act!();
                Assert.That(entities.System<SharedUserInterfaceSystem>().IsUiOpen(patient, HolocardChangeUIKey.Key, actor), Is.True);
                patientNet = entities.GetNetEntity(patient);
                actorNet = entities.GetNetEntity(actor);
                otherNet = entities.GetNetEntity(otherMedic);
                entities.System<SkillsSystem>().SetSkill(actor, HolocardSystem.SkillType, 0);
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                pair.Client.EntMan.GetEntity(patientNet), HolocardChangeUIKey.Key,
                new HolocardChangeEvent(otherNet, HolocardStatus.Permadead)));
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() =>
            {
                AssertCard(entities, patient, HolocardStatus.None, HolocardStatus.None, HolocardStatus.None);
                entities.System<SkillsSystem>().SetSkill(actor, HolocardSystem.SkillType, HolocardSystem.MinimumRequiredSkill);
            });
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                pair.Client.EntMan.GetEntity(patientNet), HolocardChangeUIKey.Key,
                new HolocardChangeEvent(actorNet, (HolocardStatus)255)));
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() => AssertCard(entities, patient, HolocardStatus.None, HolocardStatus.None, HolocardStatus.None));
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                pair.Client.EntMan.GetEntity(patientNet), HolocardChangeUIKey.Key,
                new HolocardChangeEvent(actorNet, HolocardStatus.Emergency)));
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() => AssertCard(entities, patient, HolocardStatus.Emergency, HolocardStatus.Emergency, HolocardStatus.None));
            // The first barrier targets the server tick before the inbound
            // command commits. Now wait for the resulting server state too.
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() => Assert.That(pair.Client.EntMan
                .GetComponent<HolocardStateComponent>(pair.Client.EntMan.GetEntity(patientNet)).HolocardStatus, Is.EqualTo(HolocardStatus.Emergency)));
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                pair.Server.PlayerMan.SetAttachedEntity(player, oldActor);
                if (entities.EntityExists(patient)) entities.DeleteEntity(patient);
                if (entities.EntityExists(actor)) entities.DeleteEntity(actor);
                if (entities.EntityExists(otherMedic)) entities.DeleteEntity(otherMedic);
            });
        }
        await pair.RunUntilSynced();
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ScannerNetworkShortcutRejectsPreviouslyScannedPatient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var oldActor = player.AttachedEntity;
        var entities = pair.Server.EntMan;
        EntityUid first = default, second = default, actor = default, scanner = default;
        NetEntity scannerNet = default, firstNet = default, secondNet = default, actorNet = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                first = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                second = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                actor = CreateMedic(entities, map.GridCoords);
                pair.Server.PlayerMan.SetAttachedEntity(player, actor);
                scanner = entities.SpawnEntity("CMHealthAnalyzer", map.GridCoords);
                Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(actor, scanner), Is.True);
                Scan(entities, scanner, actor, first);
                scannerNet = entities.GetNetEntity(scanner);
                firstNet = entities.GetNetEntity(first);
                secondNet = entities.GetNetEntity(second);
                actorNet = entities.GetNetEntity(actor);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Server.WaitAssertion(() => Scan(entities, scanner, actor, second));
            await pair.RunUntilSynced();
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                pair.Client.EntMan.GetEntity(scannerNet), HealthScannerUIKey.Key, new OpenChangeHolocardUIEvent(actorNet, firstNet)));
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() => Assert.That(entities.System<SharedUserInterfaceSystem>()
                .IsUiOpen(first, HolocardChangeUIKey.Key, actor), Is.False));
            await pair.Client.WaitPost(() => pair.Client.EntMan.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
                pair.Client.EntMan.GetEntity(scannerNet), HealthScannerUIKey.Key, new OpenChangeHolocardUIEvent(actorNet, secondNet)));
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() => Assert.That(entities.System<SharedUserInterfaceSystem>()
                .IsUiOpen(second, HolocardChangeUIKey.Key, actor), Is.True));
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                pair.Server.PlayerMan.SetAttachedEntity(player, oldActor);
                foreach (var uid in new[] { first, second, actor, scanner })
                    if (entities.EntityExists(uid)) entities.DeleteEntity(uid);
            });
        }
        await pair.RunUntilSynced();
        await pair.CleanReturnAsync();
    }

    private static void Scan(IEntityManager entities, EntityUid scanner, EntityUid actor, EntityUid patient)
    {
        var scan = new AfterInteractEvent(actor, scanner, patient, default, true);
        entities.EventBus.RaiseLocalEvent(scanner, scan);
        Assert.That(entities.GetComponent<HealthScannerComponent>(scanner).Target, Is.EqualTo(patient));
        Assert.That(entities.System<SharedUserInterfaceSystem>().IsUiOpen(scanner, HealthScannerUIKey.Key, actor), Is.True);
    }

    private static EntityUid CreateMedic(IEntityManager entities, EntityCoordinates coordinates)
    {
        var medic = entities.SpawnEntity("CMMobHuman", coordinates);
        entities.System<SkillsSystem>().SetSkill(medic, HolocardSystem.SkillType, HolocardSystem.MinimumRequiredSkill);
        return medic;
    }

    private static void SendManual(IEntityManager entities, EntityUid patient, EntityUid medic, HolocardStatus status)
    {
        Assert.That(entities.System<SharedUserInterfaceSystem>().TryOpenUi(patient, HolocardChangeUIKey.Key, medic), Is.True);
        entities.EventBus.RaiseLocalEvent(patient, new HolocardChangeEvent(entities.GetNetEntity(medic), status)
        {
            Actor = medic,
            UiKey = HolocardChangeUIKey.Key,
        });
        Assert.That(entities.GetComponent<HolocardStateComponent>(patient).ManualStatus, Is.EqualTo(status));
    }

    private static EntityUid FractureArm(IEntityManager entities, EntityUid patient)
    {
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
            new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
        var fracture = entities.EnsureComponent<FractureComponent>(arm);
        entities.System<SharedFractureSystem>().SetSeverity((arm, fracture), FractureSeverity.Simple);
        return arm;
    }

    private static void ClearFracture(IEntityManager entities, EntityUid arm)
        => entities.System<SharedFractureSystem>().SetSeverity((arm, entities.GetComponent<FractureComponent>(arm)), FractureSeverity.None);

    private static void DamageToStage(IEntityManager entities, EntityUid patient, EntityUid organ, OrganDamageStage stage)
    {
        var health = entities.GetComponent<OrganHealthComponent>(organ);
        var damage = new OrganDamagedEvent(patient, organ,
            new DamageSpecifier { DamageDict = { ["Blunt"] = health.Current - health.StageThresholds[stage] } }, OrganDamageSource.Direct);
        entities.EventBus.RaiseLocalEvent(organ, ref damage, broadcast: true);
        Assert.That(health.Stage, Is.EqualTo(stage));
    }

    private static void AssertCard(IEntityManager entities, EntityUid patient, HolocardStatus visible, HolocardStatus manual, HolocardStatus automatic)
    {
        var card = entities.GetComponent<HolocardStateComponent>(patient);
        Assert.Multiple(() =>
        {
            Assert.That(card.HolocardStatus, Is.EqualTo(visible));
            Assert.That(card.ManualStatus, Is.EqualTo(manual));
            Assert.That(card.AutomaticStatus, Is.EqualTo(automatic));
        });
    }

    private static void AssertBag(IEntityManager entities, EntityUid bag, HolocardStatus status)
    {
        Assert.That(entities.System<SharedAppearanceSystem>().TryGetData<HolocardStatus>(bag, HolocardContainerVisuals.State, out var actual), Is.True);
        Assert.That(actual, Is.EqualTo(status));
    }
}
