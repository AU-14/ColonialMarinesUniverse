using Content.Server.Administration.Systems;
using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.Recovery;

[TestFixture]
public sealed class CMUMedicalRejuvenateTest
{
    [Test]
    public async Task RejuvenateClosesOpenSurgicalIncisions()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rejuvenate = entMan.System<RejuvenateSystem>();
            var surgery = entMan.System<CMUSurgeryFlowSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var surgeon = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<BypassSkillChecksComponent>(surgeon);
                entMan.EnsureComponent<CMUAutodocContainedPatientComponent>(human);
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                entMan.EnsureComponent<CMIncisionOpenComponent>(torso);
                entMan.EnsureComponent<CMBleedersClampedComponent>(torso);
                entMan.EnsureComponent<CMSkinRetractedComponent>(torso);
                entMan.EnsureComponent<CMRibcageSawedComponent>(torso);
                entMan.EnsureComponent<CMRibcageOpenComponent>(torso);

                surgery.EnsureSurgeryInFlight(
                    human,
                    torso,
                    surgeon,
                    "CMUSurgeryCloseIncision",
                    "Close Incision",
                    BodyPartType.Torso,
                    BodyPartSymmetry.None);
                var armed = surgery.TryArmExactStep(
                    surgeon,
                    human,
                    torso,
                    "CMUSurgeryCloseIncision",
                    0,
                    BodyPartType.Torso,
                    BodyPartSymmetry.None);

                Assert.That(armed, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<CMUSurgeryInProgressComponent>(human), Is.True);
                    Assert.That(entMan.HasComponent<CMUSurgeryInFlightComponent>(torso), Is.True);
                });

                rejuvenate.PerformRejuvenate(human);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUSurgeryArmedStepComponent>(human), Is.False);
                    Assert.That(entMan.HasComponent<CMUSurgeryInProgressComponent>(human), Is.False);
                    Assert.That(entMan.HasComponent<CMUSurgeryInFlightComponent>(torso), Is.False);
                    Assert.That(entMan.HasComponent<CMIncisionOpenComponent>(torso), Is.False);
                    Assert.That(entMan.HasComponent<CMBleedersClampedComponent>(torso), Is.False);
                    Assert.That(entMan.HasComponent<CMSkinRetractedComponent>(torso), Is.False);
                    Assert.That(entMan.HasComponent<CMRibcageSawedComponent>(torso), Is.False);
                    Assert.That(entMan.HasComponent<CMRibcageOpenComponent>(torso), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(surgeon);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid GetBodyPart(IEntityManager entMan, EntityUid body, BodyPartType type)
    {
        foreach (var (part, component) in entMan.System<SharedBodySystem>().GetBodyChildren(body))
        {
            if (component.PartType == type)
                return part;
        }

        Assert.Fail($"Expected CMU human to have a {type}.");
        return EntityUid.Invalid;
    }
}
