using System.Linq;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Core;

[TestFixture]
public sealed class CMUMedicalBodyIndexTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMUMedicalBodyIndexFallbackDummy
  components:
  - type: Body
    prototype: CMUHumanBody
  - type: StandingState
""";

    [Test]
    public async Task SpawnedBodyExposesCanonicalLookupsAndStableSnapshot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var medical = entMan.System<CMUMedicalBodyIndexSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var leftArmKey = new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left);
                var foundPart = medical.TryGetBodyPart(patient, leftArmKey, out var leftArm);
                var foundOrgan = medical.TryGetOrgan<HeartComponent>(patient, out var heart);
                var foundSnapshot = medical.TryGetSnapshot(patient, out var snapshot);
                var foundCachedSnapshot = medical.TryGetSnapshot(patient, out var cachedSnapshot);

                Assert.Multiple(() =>
                {
                    Assert.That(foundPart, Is.True);
                    Assert.That(foundOrgan, Is.True);
                    Assert.That(foundSnapshot, Is.True);
                    Assert.That(foundCachedSnapshot, Is.True);
                    Assert.That(snapshot, Is.Not.Null);
                    Assert.That(cachedSnapshot, Is.SameAs(snapshot));
                    Assert.That(snapshot!.Revision, Is.GreaterThan(0));
                    Assert.That(snapshot.BodyParts[leftArmKey], Is.EqualTo(leftArm));
                    Assert.That(snapshot.Organs, Does.Contain(heart));
                });
            }
            finally
            {
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PartReplacementAdvancesRevisionAndInvalidatesSnapshot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var medical = entMan.System<CMUMedicalBodyIndexSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            EntityUid? detachedArm = null;
            EntityUid? replacementArm = null;

            try
            {
                var leftArmKey = new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left);
                Assert.That(medical.TryGetBodyPart(patient, leftArmKey, out var leftArm), Is.True);
                Assert.That(medical.TryGetSnapshot(patient, out var initialSnapshot), Is.True);

                var parentSlot = body.GetParentPartAndSlotOrNull(leftArm);
                Assert.That(parentSlot, Is.Not.Null);
                detachedArm = leftArm;
                transform.DetachEntity(leftArm, entMan.GetComponent<TransformComponent>(leftArm));

                var foundRemovedPart = medical.TryGetBodyPart(patient, leftArmKey, out _);
                var foundRemovedSnapshot = medical.TryGetSnapshot(patient, out var removedSnapshot);
                var foundCachedRemovedSnapshot = medical.TryGetSnapshot(patient, out var cachedRemovedSnapshot);

                Assert.Multiple(() =>
                {
                    Assert.That(foundRemovedPart, Is.False);
                    Assert.That(
                        medical.GetBodyPartSlots(parentSlot!.Value.Parent)
                            .Single(slot => slot.SlotId == parentSlot.Value.Slot)
                            .Part,
                        Is.Null);
                    Assert.That(foundRemovedSnapshot, Is.True);
                    Assert.That(foundCachedRemovedSnapshot, Is.True);
                    Assert.That(removedSnapshot, Is.Not.SameAs(initialSnapshot));
                    Assert.That(removedSnapshot!.Revision, Is.GreaterThan(initialSnapshot!.Revision));
                    Assert.That(cachedRemovedSnapshot, Is.SameAs(removedSnapshot));
                    Assert.That(removedSnapshot.BodyParts.ContainsKey(leftArmKey), Is.False);
                });

                replacementArm = entMan.SpawnEntity("CMUPartHumanLeftArm", MapCoordinates.Nullspace);
                Assert.That(body.AttachPart(parentSlot!.Value.Parent, parentSlot.Value.Slot, replacementArm.Value), Is.True);

                var foundReplacement = medical.TryGetBodyPart(patient, leftArmKey, out var indexedReplacement);
                var foundReplacementSnapshot = medical.TryGetSnapshot(patient, out var replacementSnapshot);
                var foundCachedReplacementSnapshot = medical.TryGetSnapshot(patient, out var cachedReplacementSnapshot);

                Assert.Multiple(() =>
                {
                    Assert.That(foundReplacement, Is.True);
                    Assert.That(indexedReplacement, Is.EqualTo(replacementArm));
                    Assert.That(
                        medical.GetBodyPartSlots(parentSlot.Value.Parent)
                            .Single(slot => slot.SlotId == parentSlot.Value.Slot)
                            .Part,
                        Is.EqualTo(replacementArm));
                    Assert.That(foundReplacementSnapshot, Is.True);
                    Assert.That(foundCachedReplacementSnapshot, Is.True);
                    Assert.That(replacementSnapshot, Is.Not.SameAs(removedSnapshot));
                    Assert.That(replacementSnapshot!.Revision, Is.GreaterThan(removedSnapshot.Revision));
                    Assert.That(replacementSnapshot.BodyParts[leftArmKey], Is.EqualTo(replacementArm));
                    Assert.That(cachedReplacementSnapshot, Is.SameAs(replacementSnapshot));
                });
            }
            finally
            {
                if (detachedArm is { } detached && entMan.EntityExists(detached))
                    entMan.DeleteEntity(detached);
                if (replacementArm is { } replacement && entMan.EntityExists(replacement))
                    entMan.DeleteEntity(replacement);
                if (entMan.EntityExists(patient))
                    entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BodyEnumerationPreservesTraversalOrderWithAndWithoutIndex()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var medical = entMan.System<CMUMedicalBodyIndexSystem>();
            var indexed = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var fallback = entMan.SpawnEntity("CMUMedicalBodyIndexFallbackDummy", MapCoordinates.Nullspace);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(
                        medical.GetBodyParts(indexed).Select(part => part.Owner),
                        Is.EqualTo(body.GetBodyChildren(indexed).Select(part => part.Id)));
                    Assert.That(
                        medical.GetOrgans(indexed).Select(organ => organ.Owner),
                        Is.EqualTo(body.GetBodyOrgans(indexed).Select(organ => organ.Id)));
                    Assert.That(
                        medical.GetBodyParts(fallback).Select(part => part.Owner),
                        Is.EqualTo(body.GetBodyChildren(fallback).Select(part => part.Id)));
                    Assert.That(
                        medical.GetOrgans(fallback).Select(organ => organ.Owner),
                        Is.EqualTo(body.GetBodyOrgans(fallback).Select(organ => organ.Id)));
                    Assert.That(medical.TryGetRootPart(indexed, out var indexedRoot), Is.True);
                    Assert.That(medical.TryGetRootPart(fallback, out var fallbackRoot), Is.True);
                    Assert.That(indexedRoot.Owner, Is.EqualTo(body.GetRootPartOrNull(indexed)!.Value.Entity));
                    Assert.That(fallbackRoot.Owner, Is.EqualTo(body.GetRootPartOrNull(fallback)!.Value.Entity));
                });

                foreach (var part in medical.GetBodyParts(indexed))
                {
                    Assert.That(
                        medical.GetBodyPartSlots(part.Owner).Select(slot => slot.SlotId),
                        Is.EqualTo(part.Comp.Children.Keys));
                    Assert.That(
                        medical.GetPartOrgans(part.Owner).Select(organ => organ.Owner),
                        Is.EqualTo(body.GetPartOrgans(part.Owner, part.Comp).Select(organ => organ.Id)));
                    Assert.That(
                        medical.GetOrganSlots(part.Owner).Select(slot => slot.SlotId),
                        Is.EqualTo(part.Comp.Organs.Keys));
                }

                foreach (var part in medical.GetBodyParts(fallback))
                {
                    Assert.That(
                        medical.GetBodyPartSlots(part.Owner).Select(slot => slot.SlotId),
                        Is.EqualTo(part.Comp.Children.Keys));
                    Assert.That(
                        medical.GetPartOrgans(part.Owner).Select(organ => organ.Owner),
                        Is.EqualTo(body.GetPartOrgans(part.Owner, part.Comp).Select(organ => organ.Id)));
                    Assert.That(
                        medical.GetOrganSlots(part.Owner).Select(slot => slot.SlotId),
                        Is.EqualTo(part.Comp.Organs.Keys));
                }
            }
            finally
            {
                entMan.DeleteEntity(indexed);
                entMan.DeleteEntity(fallback);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganRemovalInvalidatesBothRelationshipDirectionsAndSnapshot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var medical = entMan.System<CMUMedicalBodyIndexSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            EntityUid? removedHeart = null;

            try
            {
                Assert.That(medical.TryGetOrgan<HeartComponent>(patient, out var heart), Is.True);
                removedHeart = heart;
                Assert.That(medical.TryGetOrganOwner(heart, out var owner, out var part), Is.True);
                Assert.That(medical.TryGetSnapshot(patient, out var initialSnapshot), Is.True);
                var heartSlot = medical.GetOrganSlots(part).Single(slot => slot.Organ == heart).SlotId;

                Assert.Multiple(() =>
                {
                    Assert.That(owner, Is.EqualTo(patient));
                    Assert.That(medical.GetPartOrgans(part).Select(organ => organ.Owner), Does.Contain(heart));
                });

                Assert.That(body.RemoveOrgan(heart), Is.True);
                Assert.That(medical.TryGetSnapshot(patient, out var removedSnapshot), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(medical.TryGetOrganOwner(heart, out _, out _), Is.False);
                    Assert.That(medical.GetPartOrgans(part).Select(organ => organ.Owner), Does.Not.Contain(heart));
                    Assert.That(medical.GetOrganSlots(part).Single(slot => slot.SlotId == heartSlot).Organ, Is.Null);
                    Assert.That(medical.GetOrgans(patient).Select(organ => organ.Owner), Does.Not.Contain(heart));
                    Assert.That(removedSnapshot, Is.Not.SameAs(initialSnapshot));
                    Assert.That(removedSnapshot!.Revision, Is.GreaterThan(initialSnapshot!.Revision));
                    Assert.That(removedSnapshot.Organs, Does.Not.Contain(heart));
                });
            }
            finally
            {
                if (removedHeart is { } heart && entMan.EntityExists(heart))
                    entMan.DeleteEntity(heart);
                if (entMan.EntityExists(patient))
                    entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }
}
