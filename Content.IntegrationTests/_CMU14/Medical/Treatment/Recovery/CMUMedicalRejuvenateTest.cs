using System.Linq;
using Content.Shared.Administration.Systems;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.Recovery;

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

    [Test]
    public async Task RejuvenateRestoresExactMissingPartAndCanonicalRelationship()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var rejuvenate = entMan.System<RejuvenateSystem>();
            var medical = entMan.System<CMUMedicalBodyIndexSystem>();
            var detachable = entMan.System<DetachableOrganSystem>();
            var relations = entMan.System<OrganRelationSystem>();
            var containers = entMan.System<SharedContainerSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            EntityUid? carrier = null;

            try
            {
                var armKey = new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left);
                var handKey = new CMUMedicalBodyPartKey(BodyPartType.Hand, BodyPartSymmetry.Left);
                var torsoKey = new CMUMedicalBodyPartKey(BodyPartType.Torso, BodyPartSymmetry.None);
                Assert.That(medical.TryGetBodyPart(human, armKey, out var oldArm), Is.True);
                Assert.That(medical.TryGetBodyPart(human, handKey, out var retainedHand), Is.True);
                Assert.That(medical.TryGetBodyPart(human, torsoKey, out var torso), Is.True);

                carrier = detachable.Detach(oldArm);
                Assert.That(carrier, Is.Not.Null);
                relations.Orphan(retainedHand);
                Assert.That(containers.TryGetContainer(human, BodyComponent.ContainerID, out var bodyOrgans), Is.True);
                Assert.That(containers.Insert(retainedHand, bodyOrgans!, force: true), Is.True);
                relations.Relate(torso, retainedHand);
                entMan.DeleteEntity(carrier!.Value);
                carrier = null;

                Assert.Multiple(() =>
                {
                    Assert.That(medical.TryGetBodyPart(human, armKey, out _), Is.False);
                    Assert.That(entMan.GetComponent<ChildOrganComponent>(retainedHand).Parent, Is.EqualTo(torso),
                        "fixture must exercise wrong-parent repair, not only missing-parent repair");
                    Assert.That(entMan.EntityExists(retainedHand), Is.True);
                });

                rejuvenate.PerformRejuvenate(human);

                Assert.That(medical.TryGetBodyPart(human, armKey, out var restoredArm), Is.True);
                Assert.That(medical.TryGetBodyPart(human, handKey, out var indexedHand), Is.True);
                var body = entMan.GetComponent<BodyComponent>(human);
                var hands = entMan.GetComponent<HandsComponent>(human);
                Assert.Multiple(() =>
                {
                    Assert.That(restoredArm, Is.Not.EqualTo(oldArm));
                    Assert.That(entMan.GetComponent<MetaDataComponent>(restoredArm).EntityPrototype?.ID,
                        Is.EqualTo("CMUPartHumanLeftArm"));
                    Assert.That(indexedHand, Is.EqualTo(retainedHand));
                    Assert.That(entMan.GetComponent<ChildOrganComponent>(retainedHand).Parent,
                        Is.EqualTo(restoredArm));
                    Assert.That(entMan.GetComponent<ChildOrganComponent>(restoredArm).Parent,
                        Is.EqualTo(torso));
                    Assert.That(body.Organs!.ContainedEntities, Has.Count.EqualTo(17));
                    Assert.That(body.Organs.ContainedEntities.Distinct().Count(), Is.EqualTo(17));
                    Assert.That(hands.Hands.Keys, Is.EquivalentTo(new[] { "left", "right" }));
                    Assert.That(hands.SortedHands, Is.EqualTo(new[] { "right", "left" }));
                    Assert.That(hands.ActiveHandId, Is.Not.Null);
                });
            }
            finally
            {
                if (carrier is { } remainingCarrier && entMan.EntityExists(remainingCarrier))
                    entMan.DeleteEntity(remainingCarrier);
                entMan.DeleteEntity(human);
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
