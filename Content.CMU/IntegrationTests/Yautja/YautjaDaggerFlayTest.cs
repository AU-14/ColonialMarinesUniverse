using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Examine;
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
            while (stacks.MoveNext(out var uid, out var stack, out var meta))
            {
                if (meta.EntityPrototype?.ID != "CMUYautjaHumanHide")
                    continue;

                skin += stack.Count;
                Assert.That(SEntMan.GetComponent<YautjaFlayedHideComponent>(uid).Victim, Is.Not.Empty,
                    "flayed skin remembers whose it was so it won't stack with anyone else's");
            }

            Assert.That(skin, Is.EqualTo(6), "cmss13 gives 1 + 3 + 2 pieces of human skin");

            var text = Server.System<ExamineSystemShared>().GetExamineText(target, SPlayer).ToMarkup();
            Assert.That(text, Does.Contain("SKIN HAS BEEN PEELED OFF"), "cmss13 MOB_FLAYED examine");
            Assert.That(text, Does.Contain("have a face"));
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

    // cmss13 stack_id is "[victim]-hide", so Alice's skin and Bob's skin stay separate stacks
    [Test]
    public async Task FlayedSkinOnlyStacksWithTheSameVictim()
    {
        await Server.WaitAssertion(() =>
        {
            var stacks = Server.System<SharedStackSystem>();
            var coords = SEntMan.GetCoordinates(TargetCoords);
            EntityUid Hide(string? victim)
            {
                var hide = SEntMan.SpawnEntity("CMUYautjaHumanHide", coords);
                if (victim != null)
                    SEntMan.EnsureComponent<YautjaFlayedHideComponent>(hide).Victim = victim;
                return hide;
            }

            var alice = Hide("Alice");
            Assert.That(stacks.TryMergeStacks(Hide("Bob"), alice, out _), Is.False, "different victims");
            Assert.That(stacks.TryMergeStacks(Hide(null), alice, out _), Is.False, "plain human skin isn't anyone's");
            Assert.That(stacks.TryMergeStacks(alice, Hide(null), out _), Is.False, "blocked from either side");
            Assert.That(stacks.TryMergeStacks(Hide("Alice"), alice, out _), Is.True, "same victim still stacks");

            var split = stacks.Split(alice, 1, coords);
            Assert.That(split, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<YautjaFlayedHideComponent>(split!.Value).Victim, Is.EqualTo("Alice"),
                "a split piece is still Alice's");
            Assert.That(stacks.TryMergeStacks(split.Value, Hide("Bob"), out _), Is.False);
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
