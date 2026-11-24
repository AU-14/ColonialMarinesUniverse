using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaMeatSpikeTest : InteractionTest
{
    // bug 1512: a skinned and beheaded body on the meat spike could be neither butchered nor taken down
    [Test]
    public async Task FlayedHeadlessBodyOnTheSpikeCanBeButcheredAndUnhooked()
    {
        await SpawnTarget("CMMobHuman");
        var body = STarget!.Value;
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<YautjaComponent>(SPlayer);
            SEntMan.System<MobStateSystem>().ChangeMobState(body, MobState.Dead);
        });

        await PlaceInHands("CMUYautjaCeremonialDagger");
        await Interact(awaitDoAfters: false);
        await AwaitFlayed(body);

        EntityUid spike = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(body,
                new CMUMedicalBodyPartKey(BodyPartType.Head, BodyPartSymmetry.None), out var head), Is.True);
            var sever = new BodyPartSeverAttemptEvent(body, head, BodyPartType.Head);
            SEntMan.EventBus.RaiseLocalEvent(head, ref sever, broadcast: true);
            Assert.That(sever.Succeeded, Is.True, "test setup: the head has to come off");

            spike = SEntMan.SpawnEntity("CMUHunterShipPlacedKitchenSpikeSpikeSouth", SEntMan.GetCoordinates(TargetCoords));
            var comp = SEntMan.GetComponent<KitchenSpikeComponent>(spike);
            Assert.That(Server.System<SharedContainerSystem>().Insert(body, comp.BodyContainer), Is.True,
                "the flayed body goes on the spike");
            Assert.That(SEntMan.GetComponent<ButcherableComponent>(body).SpawnedEntities, Is.Not.Empty,
                "there's still meat on it");
        });

        Target = SEntMan.GetNetEntity(spike);
        await Interact();
        await Server.WaitAssertion(() =>
        {
            var spawned = SEntMan.GetComponent<ButcherableComponent>(body).SpawnedEntities.Sum(e => e.Amount);
            Assert.That(spawned, Is.LessThan(6), "the dagger cut meat off the body on the spike");

            var verbs = Server.System<SharedVerbSystem>().GetLocalVerbs(spike, SPlayer, typeof(Verb));
            var unhook = verbs.FirstOrDefault(v => v.Text == Loc.GetString("comp-kitchen-spike-unhook-verb"));
            Assert.That(unhook, Is.Not.Null, "the body can be taken down");
            unhook!.Act!.Invoke();
        });

        await AwaitDoAfters();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<KitchenSpikeComponent>(spike).BodyContainer.ContainedEntity, Is.Null,
                "the unhook finished and the body is off the spike");
        });
    }

    private async Task AwaitFlayed(EntityUid body)
    {
        for (var ticks = 0; ticks < Pair.SecondsToTicks(40); ticks += 10)
        {
            var done = false;
            await Server.WaitPost(() => done =
                SEntMan.TryGetComponent<YautjaFlayedComponent>(body, out var flayed) &&
                flayed.NextStage == YautjaFlayingStage.Complete);
            if (done)
            {
                await RunTicks(5);
                return;
            }

            await RunTicks(10);
        }

        Assert.Fail("the body never finished flaying");
    }
}
