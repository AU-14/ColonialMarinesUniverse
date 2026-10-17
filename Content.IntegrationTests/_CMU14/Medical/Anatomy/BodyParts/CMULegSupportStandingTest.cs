using System.Collections.Generic;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared.Body.Part;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Standing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;

// BUG-333: reattaching both legs left the patient lying down with item use blocked forever,
// and shooting the feet off didn't put anyone on the floor
[TestFixture]
public sealed class CMULegSupportStandingTest
{
    [Test]
    public async Task ReattachingBothLegsStandsThePatientBackUp()
    {
        await using var pair = await PoolManager.GetServerClient();
        var em = pair.Server.EntMan;
        EntityUid patient = default;
        EntityUid surgeon = default;
        EntityUid torso = default;
        var carriers = new Dictionary<BodyPartSymmetry, EntityUid>();

        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                surgeon = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                // pain from the amputations would knock them down for its own reasons
                em.System<SharedPainShockSystem>().AddPainSuppressionProfile(patient, 1f, 4, 0f, TimeSpan.FromSeconds(60));

                var index = em.System<CMUMedicalBodyIndexSystem>();
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Torso, BodyPartSymmetry.None), out torso), Is.True);
                foreach (var symmetry in new[] { BodyPartSymmetry.Left, BodyPartSymmetry.Right })
                {
                    Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Leg, symmetry), out var leg), Is.True);
                    var attempt = new BodyPartSeverAttemptEvent(patient, leg, BodyPartType.Leg, Surgical: true);
                    em.EventBus.RaiseLocalEvent(leg, ref attempt);
                    Assert.That(attempt.Succeeded, Is.True);
                    carriers[symmetry] = attempt.DetachedBody!.Value;
                }
            });
            await pair.RunTicksSync(5);

            await pair.Server.WaitAssertion(() =>
            {
                var standing = em.System<StandingStateSystem>();
                Assert.That(standing.IsDown(patient), Is.True, "a legless patient must be on the floor");

                Reattach(em, surgeon, patient, torso, carriers[BodyPartSymmetry.Left], BodyPartType.Leg, BodyPartSymmetry.Left);
            });
            await pair.RunTicksSync(5);

            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(em.System<StandingStateSystem>().IsDown(patient), Is.True, "one leg still can't hold them up");
                Reattach(em, surgeon, patient, torso, carriers[BodyPartSymmetry.Right], BodyPartType.Leg, BodyPartSymmetry.Right);
            });
            await pair.RunTicksSync(5);

            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(em.System<StandingStateSystem>().IsDown(patient), Is.False,
                    "with both legs back nothing else holds them down, so they must stand again");
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                em.DeleteEntity(surgeon);
                em.DeleteEntity(patient);
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LosingAFootKeepsYouDownUntilItIsReattached()
    {
        await using var pair = await PoolManager.GetServerClient();
        var em = pair.Server.EntMan;
        EntityUid patient = default;
        EntityUid surgeon = default;
        EntityUid leftLeg = default;
        EntityUid carrier = default;

        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                surgeon = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                em.System<SharedPainShockSystem>().AddPainSuppressionProfile(patient, 1f, 4, 0f, TimeSpan.FromSeconds(60));

                var index = em.System<CMUMedicalBodyIndexSystem>();
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Leg, BodyPartSymmetry.Left), out leftLeg), Is.True);
                Assert.That(index.TryGetBodyPart(patient, new(BodyPartType.Foot, BodyPartSymmetry.Left), out var foot), Is.True);
                var attempt = new BodyPartSeverAttemptEvent(patient, foot, BodyPartType.Foot, Surgical: true);
                em.EventBus.RaiseLocalEvent(foot, ref attempt);
                Assert.That(attempt.Succeeded, Is.True);
                carrier = attempt.DetachedBody!.Value;
            });
            await pair.RunTicksSync(5);

            await pair.Server.WaitAssertion(() =>
            {
                var standing = em.System<StandingStateSystem>();
                Assert.Multiple(() =>
                {
                    Assert.That(em.System<CMULegSupportSystem>().HasLegsToStand(patient), Is.False);
                    Assert.That(standing.IsDown(patient), Is.True, "a missing foot must put them on the floor like a missing leg");
                    Assert.That(standing.Stand(patient), Is.False, "and they can't stand back up on the stump");
                });

                Reattach(em, surgeon, patient, leftLeg, carrier, BodyPartType.Foot, BodyPartSymmetry.Left);
            });
            await pair.RunTicksSync(5);

            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(em.System<CMULegSupportSystem>().HasLegsToStand(patient), Is.True);
                Assert.That(em.System<StandingStateSystem>().IsDown(patient), Is.False);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                em.DeleteEntity(surgeon);
                em.DeleteEntity(patient);
            });
        }

        await pair.CleanReturnAsync();
    }

    private static void Reattach(
        IEntityManager em,
        EntityUid surgeon,
        EntityUid patient,
        EntityUid parentPart,
        EntityUid carrier,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        Assert.That(em.System<SharedHandsSystem>().TryPickupAnyHand(surgeon, carrier), Is.True);
        var step = em.System<SharedCMSurgerySystem>().GetSingleton("CMUSurgeryStepReattachLimb")!.Value;
        var effect = new CMSurgeryStepEvent(surgeon, patient, parentPart, new List<EntityUid>())
        {
            Used = carrier,
            TargetType = type,
            TargetSymmetry = symmetry,
        };
        Assert.That(em.System<SharedCMUSurgerySystem>().TryExecuteStep(step, ref effect),
            Is.EqualTo(CMUSurgeryStepOutcome.Succeeded), $"reattaching the {symmetry} {type}");
    }
}
