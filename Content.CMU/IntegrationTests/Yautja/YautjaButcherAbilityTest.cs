using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round.Antags.SerialKiller;
using Content.Server.CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;

namespace Content.IntegrationTests.CMU14.Yautja;

// the serial killer gets the pred butcher through YautjaButcherAbility, not by being a yautja.
// humans only, and only with a knife or something sharp in hand
public sealed class YautjaButcherAbilityTest : GameTest
{
    [Test]
    public async Task SerialKillerButchersHumansWithABladeOnly()
    {
        var map = await Pair.CreateTestMap();
        EntityUid killer = default;
        EntityUid victim = default;

        await Server.WaitAssertion(() =>
        {
            killer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bystander = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 1)));
            victim = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var xeno = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(-1, 0)));
            var mobState = Server.System<MobStateSystem>();
            mobState.ChangeMobState(victim, MobState.Dead);
            mobState.ChangeMobState(xeno, MobState.Dead);

            var trophies = Server.System<YautjaTrophySystem>();
            var hands = Server.System<SharedHandsSystem>();
            Assert.That(hands.TryPickup(bystander, SEntMan.SpawnEntity("RMCKitchenKnife", map.GridCoords)), Is.True);
            Assert.That(trophies.TryStartButcher(bystander, victim, YautjaButcherProcedure.Skin), Is.False,
                "Without the ability a knife alone doesn't let you butcher.");

            SEntMan.AddComponent<SerialKillerComponent>(killer);
            Assert.That(SEntMan.HasComponent<YautjaButcherAbilityComponent>(killer), Is.True,
                "Becoming a serial killer grants the butcher ability.");
            Assert.That(Server.System<SharedRMCActionsSystem>().GetActionsWithEvent<YautjaButcherActionEvent>(killer).Count(), Is.EqualTo(1),
                "The ability hands out the butcher action.");

            Assert.That(trophies.TryStartButcher(killer, victim, YautjaButcherProcedure.Skin), Is.False,
                "Empty-handed the killer can't butcher.");

            Assert.That(hands.TryPickup(killer, SEntMan.SpawnEntity("RMCKitchenKnife", map.GridCoords)), Is.True);
            Assert.That(trophies.TryStartButcher(killer, xeno, YautjaButcherProcedure.Skin), Is.False,
                "Only preds butcher xenos.");
            Assert.That(trophies.TryStartButcher(killer, victim, YautjaButcherProcedure.Skin), Is.True,
                "With a knife in hand the killer butchers a human corpse.");
        });

        // the four skin stages chain on their own: 7 / 6.5 / 7 / 9 seconds
        await Pair.RunTicksSync(Pair.SecondsToTicks(7f + 6.5f + 7f + 9f + 2f));

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(victim) || SEntMan.IsQueuedForDeletion(victim), Is.True,
                "The full carve-up gets rid of the body.");

            SEntMan.RemoveComponent<YautjaButcherAbilityComponent>(killer);
            Assert.That(Server.System<SharedRMCActionsSystem>().GetActionsWithEvent<YautjaButcherActionEvent>(killer).Count(), Is.Zero,
                "Losing the ability takes the action away.");
        });
    }
}
