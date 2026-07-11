using System.Collections.Generic;
using System.Reflection;
using Content.Server._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Stomach;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Vision;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Medical.Injuries;

[TestFixture]
public sealed class OrganDamageEffectsTest
{
    [Test]
    public async Task FractureMovementCanSeedInternalBleedingAndDamageRegionalOrgans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var index = entMan.System<CMUMedicalBodyIndexSystem>();
                var torso = GetPart(index, human, BodyPartType.Torso, BodyPartSymmetry.None);
                var before = GetChestOrganHealth(entMan, index, torso);
                var fracture = entMan.EnsureComponent<FractureComponent>(torso);
                SetField(fracture, nameof(FractureComponent.SourceZone), (TargetBodyZone?) TargetBodyZone.Chest);
                entMan.System<SharedFractureSystem>()
                    .SetSeverity((torso, fracture), FractureSeverity.Simple);

                entMan.System<CMUFractureMovementSystem>()
                    .ApplyMovementConsequences(human, torso, fracture, true, true);

                var internalBleed = entMan.GetComponent<InternalBleedingComponent>(torso);
                Assert.Multiple(() =>
                {
                    Assert.That(internalBleed.Source, Is.EqualTo("fracture-movement"));
                    Assert.That(internalBleed.BloodlossPerSecond, Is.EqualTo(0.3f).Within(0.001f));
                    Assert.That(GetChestOrganHealth(entMan, index, torso), Is.LessThan(before));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BrainDamageImpairmentAndDisorientationAreApplied()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var brain = GetOrgan<CMUBrainComponent>(index, human);
            var symptoms = entMan.GetComponent<CMUBrainComponent>(brain);
            SetField(symptoms, nameof(CMUBrainComponent.BruisedDisorientationChance), 1f);
            SetField(symptoms, nameof(CMUBrainComponent.DisorientationCheckInterval), TimeSpan.FromMilliseconds(10));
            SetField(symptoms, nameof(CMUBrainComponent.NextDisorientCheck), timing.CurTime);
            DamageOrgan(entMan, human, brain, 15);

            var vision = entMan.GetComponent<CMUBrainVisionImpairmentComponent>(human);
            Assert.That(vision.Magnitude, Is.EqualTo(symptoms.BruisedVisionBlur));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var temporaryBlur = entMan.GetComponent<CMUTemporaryBlurryVisionComponent>(human);
            Assert.That(temporaryBlur.Modifiers, Has.Some.Matches<CMUTemporaryBlurModifier>(
                modifier => modifier.Strength >= 1.25f));
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MaximumBrainDamageDoesNotKillThePatient()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var brain = GetOrgan<CMUBrainComponent>(index, human);
                DamageOrgan(entMan, human, brain, 60);

                Assert.Multiple(() =>
                {
                    Assert.That(
                        entMan.GetComponent<OrganHealthComponent>(brain).Stage,
                        Is.EqualTo(OrganDamageStage.Dead));
                    Assert.That(
                        entMan.GetComponent<MobStateComponent>(human).CurrentState,
                        Is.Not.EqualTo(MobState.Dead));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FailingHeartAppliesOxygenAndToxinPressure()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var heart = GetOrgan<HeartComponent>(index, human);
            var heartComp = entMan.GetComponent<HeartComponent>(heart);
            GetField<Dictionary<OrganDamageStage, FixedPoint2>>(
                heartComp,
                nameof(HeartComponent.AsphyxPerSecond))[OrganDamageStage.Failing] = FixedPoint2.New(5);
            GetField<Dictionary<OrganDamageStage, FixedPoint2>>(
                heartComp,
                nameof(HeartComponent.ToxinPerSecond))[OrganDamageStage.Failing] = FixedPoint2.New(2);
            SetField(heartComp, nameof(HeartComponent.NextOrganDamageTick), timing.CurTime);
            DamageOrgan(entMan, human, heart, 52);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damage = entMan.GetComponent<DamageableComponent>(human).Damage.DamageDict;
            Assert.Multiple(() =>
            {
                Assert.That(damage["Asphyxiation"], Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(damage["Poison"], Is.GreaterThan(FixedPoint2.Zero));
            });
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DamagedLungsCauseBloodCoughing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid human = default;
        var bloodBefore = FixedPoint2.Zero;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var blood = entMan.System<SharedRMCBloodstreamSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            Assert.That(blood.TryGetBloodSolution(human, out var solution), Is.True);
            bloodBefore = solution!.Volume;

            var lungs = GetOrgan<LungsComponent>(index, human);
            var lungsComp = entMan.GetComponent<LungsComponent>(lungs);
            GetField<Dictionary<OrganDamageStage, float>>(
                lungsComp,
                nameof(LungsComponent.BloodCoughChance))[OrganDamageStage.Damaged] = 1f;
            SetField(lungsComp, nameof(LungsComponent.BloodCoughInterval), TimeSpan.FromMilliseconds(10));
            SetField(lungsComp, nameof(LungsComponent.NextAsphyxTick), timing.CurTime);
            SetField(lungsComp, nameof(LungsComponent.NextBloodCoughCheck), timing.CurTime);
            DamageOrgan(entMan, human, lungs, 33);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var blood = entMan.System<SharedRMCBloodstreamSystem>();
            Assert.That(blood.TryGetBloodSolution(human, out var solution), Is.True);
            Assert.That(solution!.Volume, Is.LessThan(bloodBefore));
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BruisedLiverAndKidneysGenerateToxinDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var timing = server.ResolveDependency<IGameTiming>();
        EntityUid human = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var liver = GetOrgan<LiverComponent>(index, human);
            var kidneys = GetOrgan<KidneysComponent>(index, human);

            var liverComp = entMan.GetComponent<LiverComponent>(liver);
            var kidneysComp = entMan.GetComponent<KidneysComponent>(kidneys);
            SetField(liverComp, nameof(LiverComponent.NextSelfDamageTick), timing.CurTime);
            SetField(kidneysComp, nameof(KidneysComponent.NextSelfDamageTick), timing.CurTime);
            DamageOrgan(entMan, human, liver, 16);
            DamageOrgan(entMan, human, kidneys, 16);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var poison = entMan.GetComponent<DamageableComponent>(human).Damage.DamageDict["Poison"];
            Assert.That(poison, Is.GreaterThan(FixedPoint2.Zero));
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MissingLiverAndKidneysDisableClearanceAndGenerateToxins()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;
        EntityUid liver = default;
        EntityUid kidneys = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var body = entMan.System<SharedBodySystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            liver = GetOrgan<LiverComponent>(index, human);
            kidneys = GetOrgan<KidneysComponent>(index, human);

            Assert.That(body.RemoveOrgan(liver), Is.True);
            Assert.That(body.RemoveOrgan(kidneys), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<MissingLiverComponent>(human), Is.True);
                Assert.That(entMan.HasComponent<MissingKidneysComponent>(human), Is.True);
                Assert.That(entMan.System<SharedLiverSystem>().GetClearanceMultiplier(human), Is.Zero);
                Assert.That(entMan.System<SharedKidneysSystem>().GetClearanceMultiplier(human), Is.Zero);
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var poison = entMan.GetComponent<DamageableComponent>(human).Damage.DamageDict["Poison"];
            Assert.That(poison, Is.GreaterThan(FixedPoint2.Zero));
            entMan.DeleteEntity(liver);
            entMan.DeleteEntity(kidneys);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MissingEyesBlindUntilTheEyesAreReinserted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var body = entMan.System<SharedBodySystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var eyes = GetOrgan<EyesComponent>(index, human);
            var head = GetPart(index, human, BodyPartType.Head, BodyPartSymmetry.None);

            try
            {
                Assert.That(body.RemoveOrgan(eyes), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUOrganBlindnessComponent>(human), Is.True);
                    Assert.That(entMan.GetComponent<BlindableComponent>(human).IsBlind, Is.True);
                });

                Assert.That(body.InsertOrgan(head, eyes, "eyes"), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUOrganBlindnessComponent>(human), Is.False);
                    Assert.That(entMan.GetComponent<BlindableComponent>(human).IsBlind, Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                if (entMan.EntityExists(eyes))
                    entMan.DeleteEntity(eyes);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MissingStomachCausesPersistentNauseaUntilReinserted()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var body = entMan.System<SharedBodySystem>();
            var status = entMan.System<SharedStatusEffectsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var stomach = GetOrgan<CMUStomachComponent>(index, human);
            var torso = GetPart(index, human, BodyPartType.Torso, BodyPartSymmetry.None);

            try
            {
                Assert.That(body.RemoveOrgan(stomach), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<MissingStomachComponent>(human), Is.True);
                    Assert.That(status.HasStatusEffect(human, "StatusEffectCMUNausea"), Is.True);
                });

                Assert.That(body.InsertOrgan(torso, stomach, "stomach"), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<MissingStomachComponent>(human), Is.False);
                    Assert.That(status.HasStatusEffect(human, "StatusEffectCMUNausea"), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                if (entMan.EntityExists(stomach))
                    entMan.DeleteEntity(stomach);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StasisPausesMissingOrganDamageUntilMetabolismResumes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;
        EntityUid heart = default;
        EntityUid lungs = default;
        EntityUid liver = default;
        EntityUid kidneys = default;
        var asphyxBefore = FixedPoint2.Zero;
        var poisonBefore = FixedPoint2.Zero;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var body = entMan.System<SharedBodySystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            heart = GetOrgan<HeartComponent>(index, human);
            lungs = GetOrgan<LungsComponent>(index, human);
            liver = GetOrgan<LiverComponent>(index, human);
            kidneys = GetOrgan<KidneysComponent>(index, human);
            var damage = entMan.GetComponent<DamageableComponent>(human).Damage.DamageDict;
            asphyxBefore = damage["Asphyxiation"];
            poisonBefore = damage["Poison"];

            entMan.EnsureComponent<CMInStasisComponent>(human);
            Assert.That(body.RemoveOrgan(heart), Is.True);
            Assert.That(body.RemoveOrgan(lungs), Is.True);
            Assert.That(body.RemoveOrgan(liver), Is.True);
            Assert.That(body.RemoveOrgan(kidneys), Is.True);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(6.5f));

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var damage = entMan.GetComponent<DamageableComponent>(human).Damage.DamageDict;
            var status = entMan.System<SharedStatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(damage["Asphyxiation"], Is.EqualTo(asphyxBefore));
                Assert.That(damage["Poison"], Is.EqualTo(poisonBefore));
                Assert.That(status.HasStatusEffect(human, "StatusEffectCMUUnconscious"), Is.False);
            });
            entMan.RemoveComponent<CMInStasisComponent>(human);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.5f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damage = entMan.GetComponent<DamageableComponent>(human).Damage.DamageDict;
            var status = entMan.System<SharedStatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(damage["Asphyxiation"], Is.GreaterThan(asphyxBefore));
                Assert.That(damage["Poison"], Is.GreaterThan(poisonBefore));
                Assert.That(status.HasStatusEffect(human, "StatusEffectCMUUnconscious"), Is.False);
            });
            entMan.DeleteEntity(human);
            entMan.DeleteEntity(heart);
            entMan.DeleteEntity(lungs);
            entMan.DeleteEntity(liver);
            entMan.DeleteEntity(kidneys);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DefibrillationDamagesAndRestartsARecoverableHeart()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var heart = GetOrgan<HeartComponent>(index, human);
                var heartComp = entMan.GetComponent<HeartComponent>(heart);
                var health = entMan.GetComponent<OrganHealthComponent>(heart);
                var before = health.Current;
                SetField(heartComp, nameof(HeartComponent.Stopped), true);
                SetField(heartComp, nameof(HeartComponent.BeatsPerMinute), 0);

                var attempt = new RMCDefibrillatorAttemptEvent(human);
                entMan.EventBus.RaiseLocalEvent(human, attempt);

                Assert.Multiple(() =>
                {
                    Assert.That(attempt.Cancelled, Is.False);
                    Assert.That(heartComp.Stopped, Is.False);
                    Assert.That(health.Current, Is.InRange(before - 5, before - 3));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DefibrillationRejectsADamagedHeart()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var heart = GetOrgan<HeartComponent>(index, human);
                var heartComp = entMan.GetComponent<HeartComponent>(heart);
                DamageOrgan(entMan, human, heart, 36);
                SetField(heartComp, nameof(HeartComponent.Stopped), true);

                var attempt = new RMCDefibrillatorAttemptEvent(human);
                entMan.EventBus.RaiseLocalEvent(human, attempt);

                Assert.Multiple(() =>
                {
                    Assert.That(attempt.Cancelled, Is.True);
                    Assert.That(heartComp.Stopped, Is.True);
                    Assert.That(
                        entMan.GetComponent<OrganHealthComponent>(heart).Stage,
                        Is.EqualTo(OrganDamageStage.Damaged));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void DamageOrgan(
        IEntityManager entMan,
        EntityUid body,
        EntityUid organ,
        FixedPoint2 amount)
    {
        var damage = new DamageSpecifier
        {
            DamageDict = { ["Blunt"] = amount },
        };
        var ev = new OrganDamagedEvent(body, organ, damage, OrganDamageSource.Direct);
        entMan.EventBus.RaiseLocalEvent(organ, ref ev, broadcast: true);
    }

    private static EntityUid GetPart(
        CMUMedicalBodyIndexSystem index,
        EntityUid body,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        Assert.That(index.TryGetBodyPart(body, new CMUMedicalBodyPartKey(type, symmetry), out var part), Is.True);
        return part;
    }

    private static EntityUid GetOrgan<T>(CMUMedicalBodyIndexSystem index, EntityUid body)
        where T : IComponent
    {
        Assert.That(index.TryGetOrgan<T>(body, out var organ), Is.True);
        return organ;
    }

    private static FixedPoint2 GetChestOrganHealth(
        IEntityManager entMan,
        CMUMedicalBodyIndexSystem index,
        EntityUid torso)
    {
        var total = FixedPoint2.Zero;
        foreach (var organ in index.GetPartOrgans(torso))
        {
            if (!entMan.HasComponent<HeartComponent>(organ) &&
                !entMan.HasComponent<LungsComponent>(organ) &&
                !entMan.HasComponent<LiverComponent>(organ))
            {
                continue;
            }

            total += entMan.GetComponent<OrganHealthComponent>(organ).Current;
        }

        return total;
    }

    private static T GetField<T>(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        return (T) field!.GetValue(instance)!;
    }

    private static void SetField<T>(object instance, string name, T value)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field!.SetValue(instance, value);
    }
}
