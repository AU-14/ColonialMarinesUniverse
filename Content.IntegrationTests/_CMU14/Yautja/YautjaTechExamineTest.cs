#nullable enable
using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

// humans got zapped and delimbed just for examining pred gear, the punishment ran on the pickup attempt
// and verb menus/examine ask that question too. The pickup block is gone - CM-SS13's ITEM_PREDATOR only
// tracks yautja tech and never refuses the grab - so looking and grabbing are both free now.
[TestFixture]
public sealed class YautjaTechExamineTest : GameTest
{
    [Test]
    public async Task LookingAndGrabbingAreBothFree()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var hivebreaker = SEntMan.SpawnEntity("CMUYautjaHivebreaker", map.GridCoords);

            // everything examine and the right-click menu do behind the scenes
            SEntMan.System<SharedVerbSystem>().GetLocalVerbs(hivebreaker, human, Verb.VerbTypes);
            Assert.That(SEntMan.System<SharedHandsSystem>().CanPickupAnyHand(human, hivebreaker), Is.True,
                "a human can pick yautja tech up now: ITEM_PREDATOR tracks it, it does not block the grab");
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(human).Float(), Is.Zero,
                "looking at yautja gear shouldn't hurt");

            SEntMan.System<SharedInteractionSystem>().InteractHand(human, hivebreaker);
            Assert.That(SEntMan.System<DamageableSystem>().GetTotalDamage(human).Float(), Is.Zero,
                "grabbing it takes no damage now: the pickup block that dealt the shock is gone");
        });
    }
}
