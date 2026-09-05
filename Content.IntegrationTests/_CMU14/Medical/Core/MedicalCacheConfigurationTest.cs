#pragma warning disable RA0002 // Observe committed and replicated medical/gun projections.
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain.Penalties;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical.Core;

[TestFixture]
public sealed class MedicalCacheConfigurationTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task TogglingMedicalLayerRefreshesHeldGunAndPatientCachesWithoutRemovingMedication(bool masterSwitch)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var originalPlayer = player.AttachedEntity;
        var configuration = pair.Server.ResolveDependency<IConfigurationManager>();
        var toggle = masterSwitch ? CMUMedicalCCVars.Enabled : CMUMedicalCCVars.StatusEffectsEnabled;
        var originalMaster = configuration.GetCVar(CMUMedicalCCVars.Enabled);
        var originalStatus = configuration.GetCVar(CMUMedicalCCVars.StatusEffectsEnabled);
        EntityUid patient = default;
        EntityUid weapon = default;
        NetEntity networkPatient = default;
        NetEntity networkWeapon = default;
        Projection healthy = default;
        Projection medicated = default;
        Projection injured = default;
        try
        {
            await pair.Server.WaitPost(() =>
            {
                configuration.SetCVar(CMUMedicalCCVars.Enabled, true);
                configuration.SetCVar(CMUMedicalCCVars.StatusEffectsEnabled, true);
                var entities = pair.Server.EntMan;
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                weapon = entities.SpawnEntity("CMWeaponPistolM1984", map.GridCoords);
                Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(patient, weapon), Is.True);
                pair.Server.PlayerMan.SetAttachedEntity(player, patient);
                networkPatient = entities.GetNetEntity(patient);
                networkWeapon = entities.GetNetEntity(weapon);
            });
            await pair.RunTicksSync(2);
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                healthy = ReadProjection(entities, patient, weapon);
                var chemicals = entities.System<ChemicalPropertyStatusSystem>();
                chemicals.ApplyNerveStimulation(patient, 1);
                chemicals.ApplyMuscleStimulation(patient, 2);
                medicated = ReadProjection(entities, patient, weapon);
                Assert.That(medicated.Walk, Is.GreaterThan(healthy.Walk));
                Assert.That(medicated.Aim, Is.LessThan(healthy.Aim));
                var index = entities.System<CMUMedicalBodyIndexSystem>();
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Leg, BodyPartSymmetry.Right), out var leg), Is.True);
                Assert.That(entities.System<SharedBoneSystem>().SeedFracture(leg, FractureSeverity.Simple), Is.True);
                Assert.That(index.TryGetOrgan<EyesComponent>(patient, out var eyes), Is.True);
                Assert.That(index.TryGetOrgan<CMUBrainComponent>(patient, out var brain), Is.True);
                DamageOrgan(entities, patient, eyes, OrganDamageStage.Failing);
                DamageOrgan(entities, patient, brain, OrganDamageStage.Damaged);
            });
            await pair.RunTicksSync(2);
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() =>
            {
                injured = ReadProjection(pair.Server.EntMan, patient, weapon);
                Assert.That(injured.Walk, Is.LessThan(medicated.Walk));
                Assert.That(injured.Aim, Is.GreaterThan(healthy.Aim));
                Assert.That(injured.Action, Is.GreaterThan(medicated.Action));
                Assert.That(injured.GunSpread, Is.GreaterThan(medicated.GunSpread));
            });
            await AssertClientProjection(injured);

            await pair.Server.WaitPost(() => configuration.SetCVar(toggle, false));
            await pair.RunTicksSync(2);
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() =>
            {
                AssertProjection(ReadProjection(pair.Server.EntMan, patient, weapon), medicated);
                AssertMedicationPresent(pair.Server.EntMan, patient);
            });
            await AssertClientProjection(medicated);

            await pair.Server.WaitPost(() => configuration.SetCVar(toggle, true));
            await pair.RunTicksSync(2);
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() =>
            {
                AssertProjection(ReadProjection(pair.Server.EntMan, patient, weapon), injured);
                AssertMedicationPresent(pair.Server.EntMan, patient);
            });
            await AssertClientProjection(injured);
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                configuration.SetCVar(CMUMedicalCCVars.Enabled, originalMaster);
                configuration.SetCVar(CMUMedicalCCVars.StatusEffectsEnabled, originalStatus);
                pair.Server.PlayerMan.SetAttachedEntity(player, originalPlayer);
                var entities = pair.Server.EntMan;
                if (entities.EntityExists(patient))
                    entities.DeleteEntity(patient);
                if (entities.EntityExists(weapon))
                    entities.DeleteEntity(weapon);
            });
        }
        await pair.CleanReturnAsync();

        async Task AssertClientProjection(Projection expected)
        {
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var clientPatient = entities.GetEntity(networkPatient);
                AssertProjection(ReadProjection(entities, clientPatient, entities.GetEntity(networkWeapon)), expected);
                AssertMedicationPresent(entities, clientPatient);
            });
        }
    }

    private static void DamageOrgan(IEntityManager entities, EntityUid patient, EntityUid organ, OrganDamageStage stage)
    {
        var health = entities.GetComponent<OrganHealthComponent>(organ);
        var injury = new OrganDamagedEvent(patient, organ,
            new DamageSpecifier { DamageDict = { ["Blunt"] = health.Current - health.StageThresholds[stage] } },
            OrganDamageSource.Direct);
        entities.EventBus.RaiseLocalEvent(organ, ref injury, broadcast: true);
    }

    private static Projection ReadProjection(IEntityManager entities, EntityUid patient, EntityUid weapon)
    {
        var aim = entities.GetComponent<CMUAimAccuracyComponent>(patient);
        Assert.That(aim.SpreadMultiplier, Is.EqualTo(aim.SwayMultiplier));
        return new Projection(entities.GetComponent<MovementSpeedModifierComponent>(patient).CurrentWalkSpeed,
            aim.SwayMultiplier,
            entities.System<SharedCMUMedicalSpeedSystem>().ComputeActionSpeedMultiplier(patient),
            entities.GetComponent<GunComponent>(weapon).MaxAngleModified.Theta);
    }

    private static void AssertProjection(Projection actual, Projection expected)
    {
        Assert.That(actual.Walk, Is.EqualTo(expected.Walk).Within(0.0001f));
        Assert.That(actual.Aim, Is.EqualTo(expected.Aim).Within(0.0001f));
        Assert.That(actual.Action, Is.EqualTo(expected.Action).Within(0.0001f));
        Assert.That(actual.GunSpread, Is.EqualTo(expected.GunSpread).Within(0.0001));
    }

    private static void AssertMedicationPresent(IEntityManager entities, EntityUid patient)
    {
        Assert.That(entities.GetComponent<ChemicalNerveStimulationComponent>(patient).Strength, Is.EqualTo(1));
        Assert.That(entities.GetComponent<ChemicalMuscleStimulationComponent>(patient).Strength, Is.EqualTo(2));
    }

    private readonly record struct Projection(float Walk, float Aim, float Action, double GunSpread);
}
