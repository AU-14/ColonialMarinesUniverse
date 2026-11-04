#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

// humans got zapped and delimbed just for examining pred gear, the punishment ran on the pickup attempt
// and verb menus/examine ask that question too
[TestFixture]
public sealed class YautjaTechExamineTest : GameTest
{
    [Test]
    public async Task LookingAndGrabbingAreFreeLikeCmss13ItemPredator()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hivebreaker = SEntMan.SpawnEntity("CMUYautjaHivebreaker", map.GridCoords);

            // CM-SS13's flags_item = ITEM_PREDATOR only tracks the item (code/game/objects/items.dm:183), so
            // neither examining nor taking Yautja gear costs the user anything: the shock/delimb punishment
            // lives on misuse (yaut_bracers.dm:112/175/398 and the bracer's random-function roll).
            SEntMan.System<SharedVerbSystem>().GetLocalVerbs(hivebreaker, human, Verb.VerbTypes);
            Assert.That(SEntMan.System<SharedHandsSystem>().CanPickupAnyHand(human, hivebreaker), Is.True,
                "CM-SS13 lets anyone carry Yautja gear it finds");
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(human).Float(), Is.Zero,
                "looking at yautja gear shouldn't hurt");

            SEntMan.System<SharedInteractionSystem>().InteractHand(human, hivebreaker);
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(human).Float(), Is.Zero,
                "taking it off the floor shouldn't hurt either");
        });
    }
}
