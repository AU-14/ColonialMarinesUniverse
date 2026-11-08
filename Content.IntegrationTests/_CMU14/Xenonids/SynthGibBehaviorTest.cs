using Content.Server.Chat;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.ActionBlocker;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Xenonids;

[TestFixture]
public sealed class SynthGibBehaviorTest
{
    [Test]
    public async Task QueenGibLeavesLiveSynthHeadlessSpeakerWithDestroyedTorso()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            var synth = entities.Spawn("CMMobHuman", MapCoordinates.Nullspace);
            EntityUid? detachedHead = null;

            try
            {
                entities.EnsureComponent<SynthComponent>(synth);
                var gib = new XenoGibTargetAttemptEvent(synth, synth);
                entities.EventBus.RaiseLocalEvent(synth, ref gib);

                var torsoKey = new CMUMedicalBodyPartKey(BodyPartType.Torso, BodyPartSymmetry.None);
                Assert.That(index.TryGetBodyPart(synth, torsoKey, out var torso), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(gib.Handled, Is.True);
                    Assert.That(index.TryGetBodyPart(synth,
                        new(BodyPartType.Leg, BodyPartSymmetry.Left), out _), Is.False);
                    Assert.That(index.TryGetBodyPart(synth,
                        new(BodyPartType.Leg, BodyPartSymmetry.Right), out _), Is.False);
                    Assert.That(entities.GetComponent<BodyPartHealthComponent>(torso).Current, Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(entities.GetComponent<MobStateComponent>(synth).CurrentState, Is.Not.EqualTo(MobState.Dead));
                    Assert.That(entities.HasComponent<EmoteOnDamageComponent>(synth), Is.False);
                });

                Assert.That(index.TryGetBodyPart(synth,
                    new(BodyPartType.Head, BodyPartSymmetry.None), out var head), Is.True);
                detachedHead = entities.System<DetachableOrganSystem>().Detach(head);
                Assert.That(detachedHead, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<MobStateComponent>(synth).CurrentState, Is.Not.EqualTo(MobState.Dead));
                    Assert.That(entities.HasComponent<SpeechComponent>(synth), Is.True);
                    Assert.That(entities.HasComponent<VocalComponent>(synth), Is.True);
                    Assert.That(entities.System<ActionBlockerSystem>().CanSpeak(synth), Is.True);
                });
            }
            finally
            {
                if (entities.EntityExists(synth))
                    entities.DeleteEntity(synth);
                if (detachedHead is { } carrier && entities.EntityExists(carrier))
                    entities.DeleteEntity(carrier);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeadSynthKeepsNormalGibHandling()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var synth = entities.Spawn("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                entities.EnsureComponent<SynthComponent>(synth);
                entities.System<MobStateSystem>().ChangeMobState(synth, MobState.Dead);

                var gib = new XenoGibTargetAttemptEvent(synth, synth);
                entities.EventBus.RaiseLocalEvent(synth, ref gib);
                Assert.That(gib.Handled, Is.False);
            }
            finally
            {
                if (entities.EntityExists(synth))
                    entities.DeleteEntity(synth);
            }
        });

        await pair.CleanReturnAsync();
    }
}
