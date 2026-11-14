#pragma warning disable RA0002 // the test puts the caster in a mode directly to check where it is dropped back to
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-301: a Bad Blood lands with stun and immobilizer. The lethal bolt is a clan-wide armory unlock, the eradicator
// is the Leader's and needs the lethal bolt first. Honourable hunters are not asked anything.
[TestFixture]
public sealed class BadBloodCasterUnlockTest : GameTest
{
    private const string Hunter = "CMUMobYautja";
    private const string BracerSlot = "gloves";
    private const int ImmobilizerMode = 1;

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task CasterModesAreEarnedAndALockedModeDropsBackToStun()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var grunt = party.Grunts[0];
        var late = party.Grunts[1];
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var unlocks = Server.System<BadBloodEradicatorUnlockSystem>();
                var gate = Server.System<BadBloodEradicatorSystem>();
                Assert.That(rules.IsRuleActive(), Is.True);
                Assert.That(rules.GetLeader(), Is.EqualTo(party.Leader));

                var hunter = entMan.SpawnEntity(Hunter, map.GridCoords);
                spawned.Add(hunter);

                unlocks.SyncHolders();

                // the landing: stun and immobilizer, nothing else, for the Leader and the grunts alike
                Assert.Multiple(() =>
                {
                    foreach (var badBlood in party.All)
                    {
                        Assert.That(SwitchRefused(entMan, badBlood, BadBloodEradicatorSystem.StunMode), Is.False);
                        Assert.That(SwitchRefused(entMan, badBlood, ImmobilizerMode), Is.False);
                        Assert.That(SwitchRefused(entMan, badBlood, BadBloodEradicatorSystem.LethalMode), Is.True,
                            "the lethal bolt is not there at the landing");
                        Assert.That(SwitchRefused(entMan, badBlood, BadBloodEradicatorSystem.EradicatorMode), Is.True);
                        Assert.That(gate.CanUseLethal(badBlood), Is.False);
                        Assert.That(entMan.HasComponent<BadBloodLethalBoltComponent>(badBlood), Is.False);
                    }

                    Assert.That(unlocks.IsLethalUnlocked(), Is.False);
                });

                // a caster left in a locked mode goes back to the stun bolt
                var caster = CasterOf(entMan, grunt);
                var casterComp = entMan.GetComponent<YautjaCasterComponent>(caster);
                casterComp.CurrentMode = BadBloodEradicatorSystem.LethalMode;
                unlocks.SyncHolders();
                Assert.That(casterComp.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.StunMode),
                    "a caster in the lethal mode drops back to stun while the clan has not earned it");

                casterComp.CurrentMode = ImmobilizerMode;
                unlocks.SyncHolders();
                Assert.That(casterComp.CurrentMode, Is.EqualTo(ImmobilizerMode), "the immobilizer is left alone");

                // the eradicator cannot be unlocked before the lethal bolt
                Assert.That(unlocks.UnlockEradicator(), Is.False);
                Assert.That(unlocks.IsEradicatorUnlocked(), Is.False);

                // the lethal unlock is clan-wide and happens once
                Assert.That(unlocks.UnlockLethal(), Is.True);
                Assert.That(unlocks.UnlockLethal(), Is.False, "the lethal bolt is unlocked once");
                Assert.Multiple(() =>
                {
                    foreach (var badBlood in party.All)
                    {
                        Assert.That(SwitchRefused(entMan, badBlood, BadBloodEradicatorSystem.LethalMode), Is.False,
                            "every Bad Blood fires the lethal bolt once the clan has it");
                        Assert.That(SwitchRefused(entMan, badBlood, BadBloodEradicatorSystem.EradicatorMode), Is.True);
                        Assert.That(gate.CanUseLethal(badBlood), Is.True);
                    }
                });

                casterComp.CurrentMode = BadBloodEradicatorSystem.LethalMode;
                unlocks.SyncHolders();
                Assert.That(casterComp.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.LethalMode),
                    "an earned lethal bolt stays selected");

                // a Bad Blood who arrives after the unlock holds it too
                var arrival = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
                spawned.Add(arrival);
                unlocks.SyncHolders();
                Assert.That(SwitchRefused(entMan, arrival, BadBloodEradicatorSystem.LethalMode), Is.False,
                    "a reinforcement lands able to fire the lethal bolt");

                // the eradicator: the Leader's alone, after its own unlock
                Assert.That(unlocks.UnlockEradicator(), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(SwitchRefused(entMan, party.Leader, BadBloodEradicatorSystem.EradicatorMode), Is.False,
                        "the Leader fires the eradicator once it is unlocked");
                    Assert.That(SwitchRefused(entMan, grunt, BadBloodEradicatorSystem.EradicatorMode), Is.True,
                        "a grunt is still refused the eradicator");
                    Assert.That(SwitchRefused(entMan, late, BadBloodEradicatorSystem.EradicatorMode), Is.True);
                });

                casterComp.CurrentMode = BadBloodEradicatorSystem.EradicatorMode;
                unlocks.SyncHolders();
                Assert.That(casterComp.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.StunMode),
                    "a grunt's caster left in the eradicator drops back to stun");

                var leaderCaster = entMan.GetComponent<YautjaCasterComponent>(CasterOf(entMan, party.Leader));
                leaderCaster.CurrentMode = BadBloodEradicatorSystem.EradicatorMode;
                unlocks.SyncHolders();
                Assert.That(leaderCaster.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.EradicatorMode),
                    "the Leader's stays where it is");

                // an honourable hunter is never asked, and holds neither marker
                Assert.Multiple(() =>
                {
                    for (var mode = 0; mode <= BadBloodEradicatorSystem.EradicatorMode; mode++)
                    {
                        Assert.That(SwitchRefused(entMan, hunter, mode), Is.False, $"a hunter's caster mode {mode}");
                    }

                    Assert.That(gate.CanUseLethal(hunter), Is.True);
                    Assert.That(gate.CanUseEradicator(hunter), Is.True);
                    Assert.That(entMan.HasComponent<BadBloodLethalBoltComponent>(hunter), Is.False);
                    Assert.That(entMan.HasComponent<BadBloodEradicatorComponent>(hunter), Is.False);
                });
            });

            // with no rule running nothing is locked, as for an admin-spawned Bad Blood
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var unlocks = Server.System<BadBloodEradicatorUnlockSystem>();
                Assert.That(rules.IsRuleActive(), Is.False);

                var stray = entMan.SpawnEntity(BadBloodTestHelpers.GruntPrototype, map.GridCoords);
                spawned.Add(stray);
                unlocks.SyncHolders();

                var strayCaster = entMan.GetComponent<YautjaCasterComponent>(CasterOf(entMan, stray));
                strayCaster.CurrentMode = BadBloodEradicatorSystem.EradicatorMode;
                unlocks.SyncHolders();

                Assert.Multiple(() =>
                {
                    Assert.That(SwitchRefused(entMan, stray, BadBloodEradicatorSystem.LethalMode), Is.False,
                        "no rule, no lock on the lethal bolt");
                    Assert.That(SwitchRefused(entMan, stray, BadBloodEradicatorSystem.EradicatorMode), Is.False,
                        "no rule, no lock on the eradicator");
                    Assert.That(strayCaster.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.EradicatorMode),
                        "and no caster is dropped back");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned.Concat(party.All))
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }

    // the caster the Bad Blood's own bracer carries
    private static EntityUid CasterOf(IEntityManager entMan, EntityUid badBlood)
    {
        Assert.That(entMan.System<InventorySystem>().TryGetSlotEntity(badBlood, BracerSlot, out var bracer), Is.True,
            "a Bad Blood lands wearing a bracer");
        var gear = entMan.GetComponent<YautjaGearContainerComponent>(bracer!.Value);
        Assert.That(gear.Gear.TryGetValue(YautjaGearKind.Caster, out var caster), Is.True,
            "the bracer carries a plasma caster");
        return caster;
    }

    private static bool SwitchRefused(IEntityManager entMan, EntityUid user, int mode)
    {
        var attempt = new YautjaCasterModeAttemptEvent(EntityUid.Invalid, mode);
        entMan.EventBus.RaiseLocalEvent(user, ref attempt);
        return attempt.Cancelled;
    }
}
