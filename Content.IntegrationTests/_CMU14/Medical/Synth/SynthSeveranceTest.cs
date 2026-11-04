#pragma warning disable RA0002 // reads the damage dict straight off the component

using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Part;
using Content.Shared.Damage.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Medical.Synth;

// BUG-1025: a synth that lost a limb got a bleeding stump nothing could close
[TestFixture]
public sealed class SynthSeveranceTest
{
    [Test]
    public async Task SynthLimbLossLeavesNoBleedingStump()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            var synth = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            entities.EnsureComponent<SynthComponent>(synth);

            Assert.That(index.TryGetBodyPart(synth, new(BodyPartType.Torso, BodyPartSymmetry.None), out var torso), Is.True);
            Assert.That(index.TryGetBodyPart(synth, new(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);

            var attempt = new BodyPartSeverAttemptEvent(synth, arm, BodyPartType.Arm, Surgical: false);
            entities.EventBus.RaiseLocalEvent(arm, ref attempt);
            Assert.That(attempt.Succeeded, Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entities.TryGetComponent(torso, out CMUOpenStumpComponent? stumps) && stumps.Stumps.Count > 0,
                    Is.False, "a synth has no flesh to leave an open stump");
                Assert.That(entities.GetComponent<DamageableComponent>(synth).Damage.DamageDict.GetValueOrDefault("Bloodloss").Float(),
                    Is.Zero, "a synth limb coming off shouldn't start a bleed");
            });
        });

        await pair.CleanReturnAsync();
    }
}
