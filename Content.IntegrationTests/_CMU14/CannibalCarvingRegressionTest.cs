using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CMU14.Round.Antags.Cannibal;
using Content.Shared.Construction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Tools.Components;
using Content.Shared.Traits.Assorted;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.IntegrationTests._CMU14;

public sealed class CannibalCarvingRegressionTest : InteractionTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task StartingKnifeCarvesOnlyThroughExplicitCorpseVerb(bool rotten)
    {
        var target = ToServer(await SpawnTarget("CMMobHuman"));
        await PlaceInHands("KitchenKnife");

        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<CannibalComponent>(SPlayer);
            Assert.That(SliceVerb(target).Disabled, Is.True, "Living people cannot be carved.");
            Server.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);
            if (rotten)
                SEntMan.EnsureComponent<UnrevivableComponent>(target);
        });

        await Interact(awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Any(after => after.Args.Event is ToolRefineDoAfterEvent), Is.False,
            "Ordinary knife clicks must remain surgery interactions.");

        await Server.WaitAssertion(() =>
        {
            var verb = SliceVerb(target);
            Assert.That(verb.Disabled, Is.False, "The starting knife must offer cannibal corpse carving.");
            Server.System<SharedVerbSystem>().ExecuteVerb(verb, SPlayer, target);
        });
        await AwaitDoAfters();

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(target), Is.True, "Carving must also pass completion-time checks.");
            Assert.That(SEntMan.EntityQuery<MetaDataComponent>().Count(meta =>
                !meta.Deleted && meta.EntityPrototype?.ID == "FoodMeatHuman"), Is.EqualTo(6));
        });
    }

    private Verb SliceVerb(EntityUid target)
    {
        var label = Loc.GetString(SEntMan.GetComponent<ToolRefinableComponent>(target).VerbText!);
        return Server.System<SharedVerbSystem>().GetLocalVerbs(target, SPlayer, typeof(InteractionVerb))
            .Single(verb => verb.Text == label);
    }
}
