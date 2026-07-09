using System.Collections.Generic;
using System.Reflection;
using Content.Server._CMU14.Medical.Treatment.FirstAid;
using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Medical.Core;

[TestFixture]
public sealed class CMUMedicalFeatureDeadlineTest
{
    [Test]
    public async Task CastHealingAndPostOpMalunionUseSparseDeadlines()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid castPart = default;
        EntityUid postOpPart = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var timing = server.ResolveDependency<IGameTiming>();
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var fractures = entMan.System<SharedFractureSystem>();
            var splints = entMan.System<CMUSplintItemSystem>();
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            castPart = GetPart(index, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
            postOpPart = GetPart(index, patient, BodyPartType.Arm, BodyPartSymmetry.Right);

            var fracture = entMan.EnsureComponent<FractureComponent>(castPart);
            fractures.SetSeverity((castPart, fracture), FractureSeverity.Simple);
            var castItem = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var castItemComp = entMan.EnsureComponent<CMUCastItemComponent>(castItem);
            SetField(castItemComp, nameof(CMUCastItemComponent.ConsumedOnApply), false);
            var healMinutes = GetField<Dictionary<FractureSeverity, float>>(
                castItemComp,
                nameof(CMUCastItemComponent.HealMinutesPerSeverity));
            healMinutes[FractureSeverity.Simple] = 0.001f;
            Assert.That(splints.ApplyCastToPart((castItem, castItemComp), castPart), Is.True);

            var postOp = entMan.EnsureComponent<CMUPostOpBoneSetComponent>(postOpPart);
            postOp.MalunionCheckAt = timing.CurTime + TimeSpan.FromSeconds(0.06);
            postOp.MalunionChance = 1f;
            splints.SchedulePostOpMalunion(postOpPart, postOp);
            entMan.DeleteEntity(castItem);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.2f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<CMUCastComponent>(castPart).ReadyToRemove, Is.True);
                Assert.That(entMan.HasComponent<FractureComponent>(castPart), Is.False);
                Assert.That(entMan.HasComponent<CMUPostOpBoneSetComponent>(postOpPart), Is.False);
                Assert.That(entMan.HasComponent<CMUMalunionComponent>(postOpPart), Is.True);
                Assert.That(entMan.GetComponent<FractureComponent>(postOpPart).Severity, Is.EqualTo(FractureSeverity.Simple));
            });
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RefreshingAnArmedSurgeryStepReplacesItsExpiry()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid surgeon = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var fractures = entMan.System<SharedFractureSystem>();
            var flow = entMan.System<CMUSurgeryFlowSystem>();
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(patient);
            var part = GetPart(index, patient, BodyPartType.Arm, BodyPartSymmetry.Right);
            entMan.EnsureComponent<CMIncisionOpenComponent>(part);
            entMan.EnsureComponent<CMBleedersClampedComponent>(part);
            entMan.EnsureComponent<CMSkinRetractedComponent>(part);
            var fracture = entMan.EnsureComponent<FractureComponent>(part);
            fractures.SetSeverity((part, fracture), FractureSeverity.Simple);

            var armed = flow.TryArmStep(
                surgeon,
                patient,
                part,
                "CMUSurgerySetSimpleFracture",
                0,
                BodyPartType.Arm,
                BodyPartSymmetry.Right);
            Assert.That(armed, Is.Not.Null);
            SetField(armed!, nameof(CMUSurgeryArmedStepComponent.ExpireAfter), TimeSpan.FromSeconds(0.06));
            Assert.That(flow.TryArmStep(
                surgeon,
                patient,
                part,
                "CMUSurgerySetSimpleFracture",
                0,
                BodyPartType.Arm,
                BodyPartSymmetry.Right), Is.SameAs(armed));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(0.2f));

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<CMUSurgeryArmedStepComponent>(patient), Is.False);
            server.EntMan.DeleteEntity(patient);
            server.EntMan.DeleteEntity(surgeon);
        });

        await pair.CleanReturnAsync();
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
