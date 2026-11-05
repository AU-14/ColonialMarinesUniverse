using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stacks;
using Content.Shared.Traits.Assorted;
using Robust.Client.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaDaggerFlayTest : InteractionTest
{
    private const string LayerKey = "cmu-yautja-flay";

    // the dagger used to stop after the scalp, so nobody ever got stripped or fully flayed and the body never changed.
    // it starts from a plain click on the corpse now, not a melee hit
    [Test]
    public async Task DaggerFlaysAHumanAllTheWayAndTheBodyShowsIt()
    {
        await SpawnTarget("CMMobHuman");
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<YautjaComponent>(SPlayer);
            var uniform = SEntMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", SEntMan.GetCoordinates(TargetCoords));
            Assert.That(Server.System<InventorySystem>().TryEquip(STarget!.Value, uniform, "jumpsuit", silent: true, force: true), Is.True);
            SEntMan.System<MobStateSystem>().ChangeMobState(STarget.Value, MobState.Dead);
        });
        await PlaceInHands("CMUYautjaCeremonialDagger");
        // a plain click, no combat mode, same as flaying a severed limb
        await Interact(awaitDoAfters: false);

        await AwaitStage(1);
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CTarget!.Value);
            var sprites = CEntMan.System<SpriteSystem>();
            Assert.That(sprites.LayerMapTryGet((CTarget.Value, sprite), LayerKey, out var flay, false), Is.True,
                "observers need the flay overlay on the body");
            Assert.That(sprites.LayerMapTryGet((CTarget.Value, sprite), "jumpsuit", out var jumpsuit, false), Is.True);
            Assert.That(flay, Is.LessThan(jumpsuit), "the first cuts sit under the uniform like cmss13");
        });

        // the stages chain on their own after the first cut, same as cmss13
        await AwaitStage(3);
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            var target = STarget!.Value;
            Assert.That(SEntMan.GetComponent<YautjaFlayedComponent>(target).NextStage, Is.EqualTo(YautjaFlayingStage.Complete));
            Assert.That(SEntMan.HasComponent<UnrevivableComponent>(target), Is.True, "a fully flayed body can't come back");
            Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(target, "jumpsuit", out _), Is.False,
                "the uniform comes off with the skin");

            var skin = 0;
            var stacks = SEntMan.EntityQueryEnumerator<StackComponent, MetaDataComponent>();
            while (stacks.MoveNext(out var stack, out var meta))
            {
                if (meta.EntityPrototype?.ID == "CMUYautjaHumanHide")
                    skin += stack.Count;
            }

            Assert.That(skin, Is.EqualTo(6), "cmss13 gives 1 + 3 + 2 pieces of human skin");
        });
        await Client.WaitAssertion(() =>
        {
            var sprite = CEntMan.GetComponent<SpriteComponent>(CTarget!.Value);
            var sprites = CEntMan.System<SpriteSystem>();
            Assert.That(sprites.LayerMapTryGet((CTarget.Value, sprite), LayerKey, out var flay, false), Is.True);
            var layer = sprite[flay];
            Assert.That(layer.Visible, Is.True);
            Assert.That(layer.RsiState.Name, Is.EqualTo("human_3"));
            var copies = 0;
            foreach (var other in sprite.AllLayers)
            {
                if (other.ActualRsi?.Path.ToString().EndsWith("flayed_human.rsi") == true)
                    copies++;
            }

            Assert.That(copies, Is.EqualTo(1), "re-adding the layer per stage mustn't stack copies");
        });
    }

    private async Task AwaitStage(int stage)
    {
        for (var ticks = 0; ticks < Pair.SecondsToTicks(30); ticks += 10)
        {
            var reached = false;
            await Server.WaitPost(() => reached =
                SEntMan.TryGetComponent<YautjaFlayedComponent>(STarget!.Value, out var flayed) && flayed.Stage >= stage);
            if (reached)
            {
                await RunTicks(5);
                return;
            }

            await RunTicks(10);
        }

        Assert.Fail($"the body never reached flay stage {stage}");
    }
}
