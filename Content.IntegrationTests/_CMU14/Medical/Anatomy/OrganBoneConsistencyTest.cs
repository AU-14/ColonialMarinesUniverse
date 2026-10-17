#pragma warning disable RA0002 // Regression tests inspect committed medical state.
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared.CMU14.Medical.Injuries.Pain.Penalties;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Random;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class OrganBoneConsistencyTest
{
    [Test]
    public async Task AnatomyAndOrganChangesRefreshMovementWithoutAnUnrelatedMedication()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid lungs = default;
        float initialSpeed = 0;
        await pair.Server.WaitPost(() => patient = pair.Server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords));
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var speed = entities.System<SharedCMUMedicalSpeedSystem>();
            initialSpeed = entities.GetComponent<MovementSpeedModifierComponent>(patient).CurrentWalkSpeed;
            speed.RefreshAggregatedPenalties(patient);
            Assert.That(entities.GetComponent<MovementSpeedModifierComponent>(patient).CurrentWalkSpeed,
                Is.EqualTo(initialSpeed), "Constructed anatomy retained a temporary missing-lungs penalty.");
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<LungsComponent>(patient, out lungs), Is.True);
            var health = entities.GetComponent<OrganHealthComponent>(lungs);
            DamageOrgan(entities, patient, lungs, health.Max - health.StageThresholds[OrganDamageStage.Failing]);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<MovementSpeedModifierComponent>(patient).CurrentWalkSpeed,
                Is.EqualTo(initialSpeed * 0.85f).Within(0.0001f));
            entities.System<SharedOrganHealthSystem>().HealOrgan(lungs, patient, 100);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<MovementSpeedModifierComponent>(patient).CurrentWalkSpeed,
                Is.EqualTo(initialSpeed).Within(0.0001f));
            Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(lungs), Is.True);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<MovementSpeedModifierComponent>(patient).CurrentWalkSpeed,
                Is.EqualTo(initialSpeed * 0.85f).Within(0.0001f));
            entities.DeleteEntity(patient);
            entities.DeleteEntity(lungs);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HealingDoesNotRegenerateRemovedSurgicalComplications()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrgan<LiverComponent>(patient, out var liver), Is.True);
            Assert.That(index.TryGetOrganOwner(liver, out _, out var torso), Is.True);
            Assert.That(index.TryGetBodyPart(patient,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
            var traits = entities.System<SharedCMUSurgicalTraitSystem>();
            var organs = entities.System<SharedOrganHealthSystem>();
            var bones = entities.System<SharedBoneSystem>();
            var fractures = entities.System<SharedFractureSystem>();
            var health = entities.GetComponent<OrganHealthComponent>(liver);
            pair.Server.ResolveDependency<IRobustRandom>().SetSeed(781);

            // Repeated injury/treatment crosses every complication-producing recovery
            // boundary. The fixed seed also exercises positive rolls in the old code.
            for (var i = 0; i < 16; i++)
            {
                DamageOrgan(entities, patient, liver, health.Max);
                foreach (var trait in CMUSurgicalTraitMetadata.ResolutionOrder)
                    traits.RemoveTrait(torso, trait);
                organs.HealOrgan((liver, health), patient, health.StageThresholds[OrganDamageStage.Failing]);
                organs.HealOrgan((liver, health), patient,
                    health.StageThresholds[OrganDamageStage.Damaged] - health.Current);
                Assert.That(traits.CountTraits(torso), Is.Zero, "Organ recovery recreated a treated complication.");
                organs.HealOrgan((liver, health), patient, health.Max);

                bones.SeedFracture(arm, FractureSeverity.Shattered);
                foreach (var trait in CMUSurgicalTraitMetadata.ResolutionOrder)
                    traits.RemoveTrait(arm, trait);
                fractures.SetSeverity((arm, entities.GetComponent<FractureComponent>(arm)), FractureSeverity.Compound);
                Assert.That(traits.CountTraits(arm), Is.Zero, "Fracture recovery recreated contamination.");
                fractures.SetSeverity((arm, entities.GetComponent<FractureComponent>(arm)), FractureSeverity.None);
            }
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NativeLiverRecoveryReconcilesStageAndClearance()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid liver = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<LiverComponent>(patient, out liver), Is.True);
            var health = entities.GetComponent<OrganHealthComponent>(liver);
            DamageOrgan(entities, patient, liver, health.Current - health.StageThresholds[OrganDamageStage.Bruised]);
            Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Bruised));
            Assert.That(entities.GetComponent<LiverComponent>(liver).ToxinClearMultiplier, Is.EqualTo(0.8f));
        });
        await pair.RunTicksSync(pair.SecondsToTicks(11));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<OrganHealthComponent>(liver).Stage, Is.EqualTo(OrganDamageStage.Healthy));
            Assert.That(entities.GetComponent<LiverComponent>(liver).ToxinClearMultiplier, Is.EqualTo(1));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DetachedOrganPreservationExpiresAfterItsRemainingUnpausedTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid liver = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<LiverComponent>(patient, out liver), Is.True);
            Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(liver), Is.True);
            entities.System<SharedOrganHealthSystem>().SetStasisExpire(liver,
                pair.Server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromSeconds(0.25));
            entities.System<MetaDataSystem>().SetEntityPaused(liver, true);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.5f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<OrganHealthComponent>(liver).Stage, Is.EqualTo(OrganDamageStage.Healthy));
            entities.System<MetaDataSystem>().SetEntityPaused(liver, false);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(0.15f));
        await pair.Server.WaitAssertion(() =>
            Assert.That(pair.Server.EntMan.GetComponent<OrganHealthComponent>(liver).Current, Is.GreaterThan(FixedPoint2.Zero)));
        await pair.RunTicksSync(pair.SecondsToTicks(0.25f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<OrganHealthComponent>(liver).Current, Is.EqualTo(FixedPoint2.Zero));
            Assert.That(entities.GetComponent<OrganHealthComponent>(liver).Stage, Is.EqualTo(OrganDamageStage.Dead));
            entities.DeleteEntity(patient);
            entities.DeleteEntity(liver);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StasisFreezesIntactFailureAndArrestCollapseTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        FixedPoint2 initialAsphyx = default;
        FixedPoint2 initialPoison = default;
        await pair.Server.WaitPost(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetOrgan<HeartComponent>(patient, out var heart), Is.True);
            Assert.That(index.TryGetOrgan<LiverComponent>(patient, out var liver), Is.True);
            Assert.That(index.TryGetOrgan<LungsComponent>(patient, out var lungs), Is.True);
            DamageOrgan(entities, patient, heart, 100);
            DamageOrgan(entities, patient, liver, 35);
            DamageOrgan(entities, patient, lungs, 35);
            entities.EnsureComponent<CMInStasisComponent>(patient);
            var damage = entities.GetComponent<DamageableComponent>(patient).Damage.DamageDict;
            initialAsphyx = damage.GetValueOrDefault("Asphyxiation");
            initialPoison = damage.GetValueOrDefault("Poison");
        });
        await pair.RunTicksSync(pair.SecondsToTicks(7));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var damage = entities.GetComponent<DamageableComponent>(patient).Damage.DamageDict;
            Assert.That(damage.GetValueOrDefault("Asphyxiation"), Is.EqualTo(initialAsphyx));
            Assert.That(damage.GetValueOrDefault("Poison"), Is.EqualTo(initialPoison));
            Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUUnconscious"), Is.False);
            entities.RemoveComponent<CMInStasisComponent>(patient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.5f));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectCMUUnconscious"), Is.False);
            Assert.That(entities.GetComponent<DamageableComponent>(patient).Damage.DamageDict["Asphyxiation"], Is.GreaterThan(initialAsphyx));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FractureObserversSeeCommittedStateAndScenarioIntegrityMatchesSeverity()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<CMUFractureCommitProbeSystem>();
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetBodyPart(patient,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
            var probe = entities.AddComponent<CMUFractureCommitProbeComponent>(arm);
            var bones = entities.System<SharedBoneSystem>();
            Assert.That(bones.SeedFracture(arm, FractureSeverity.Hairline), Is.True);
            var fracture = entities.GetComponent<FractureComponent>(arm);
            Assert.That(fracture.AppearedAt, Is.EqualTo(pair.Server.ResolveDependency<IGameTiming>().CurTime));
            Assert.That(probe.Observed, Is.EqualTo(FractureSeverity.Hairline));
            Assert.That(probe.Notified, Is.EqualTo(FractureSeverity.Hairline));
            Assert.That(bones.SeedFracture(arm, FractureSeverity.Compound), Is.True);
            var bone = entities.GetComponent<BoneComponent>(arm);
            Assert.That(bone.Integrity, Is.EqualTo(bone.FractureThresholds[FractureSeverity.Compound]));
            entities.System<SharedFractureSystem>().SetSeverity((arm, fracture), FractureSeverity.None);
            Assert.That(probe.Observed, Is.EqualTo(FractureSeverity.None));
            Assert.That(probe.Notified, Is.EqualTo(FractureSeverity.None));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    private static void DamageOrgan(IEntityManager entities, EntityUid patient, EntityUid organ, FixedPoint2 amount)
    {
        var damage = new DamageSpecifier { DamageDict = { ["Blunt"] = amount } };
        var ev = new OrganDamagedEvent(patient, organ, damage, OrganDamageSource.Direct);
        entities.EventBus.RaiseLocalEvent(organ, ref ev, broadcast: true);
    }
}

[RegisterComponent]
public sealed partial class CMUFractureCommitProbeComponent : Component
{
    public FractureSeverity Observed;
    public FractureSeverity Notified;
}

public sealed partial class CMUFractureCommitProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUFractureCommitProbeComponent, FractureSeverityChangedEvent>(OnFracture);
    }

    private void OnFracture(Entity<CMUFractureCommitProbeComponent> ent, ref FractureSeverityChangedEvent args)
    {
        ent.Comp.Notified = args.New;
        ent.Comp.Observed = TryComp<FractureComponent>(ent, out var fracture) ? fracture.Severity : FractureSeverity.None;
    }
}
