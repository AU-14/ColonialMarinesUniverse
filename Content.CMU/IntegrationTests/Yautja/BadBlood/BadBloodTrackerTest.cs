#pragma warning disable RA0002 // reads armour, cloak and tracking data, and sets the bracer's charge for the test
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Stealth;
using Content.Shared.CMU14.TacticalMap;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Inventory;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 ARCH-606: the Tracker caste. Its armour counts for 85%, its cloak cooldown is 75% of the bracer's, and its
// "Mark for the clan" verb marks the prey and puts it on every Yautja bracer map (the shared Yautja map bucket) for
// 60 s. A prey mark on its own shows on no map. Other Bad Bloods are unchanged.
[TestFixture]
public sealed class BadBloodTrackerTest : GameTest
{
    private static readonly ProtoId<BadBloodCastePrototype> Tracker = "Tracker";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task TrackerMarksForTheClanWithLighterArmourAndAQuickerCloak()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var verbs = entMan.System<SharedVerbSystem>();
                var marks = entMan.System<YautjaMarkSystem>();
                var (tracker, plain) = (party.Grunts[0], party.Grunts[1]);
                Assert.That(entMan.System<BadBloodCasteSystem>().TryTakeCaste(tracker, Tracker), Is.True);

                // armour counts for 85%
                Assert.That(Armor(entMan, tracker), Is.EqualTo(Armor(entMan, plain) * 0.85).Within(0.0001),
                    "a Tracker's armour counts for 85%");

                // the cloak comes back after 75% of the bracer's cooldown; another Bad Blood waits it all
                var trackerBracer = Bracer(tracker);
                var plainBracer = Bracer(plain);
                var cooldown = plainBracer.Comp.CloakCooldown;
                Assert.That(cooldown, Is.GreaterThan(TimeSpan.Zero), "Test setup: the Bad Blood bracer has a cooldown");
                foreach (var (user, bracer) in new[] { (tracker, trackerBracer), (plain, plainBracer) })
                {
                    bracer.Comp.Charge = 1500;
                    PressCloak(entMan, user, bracer);
                    Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(user), Is.True, "Test setup: the cloak goes up");
                    PressCloak(entMan, user, bracer);
                    // the active-invisible marker leaves deferred, the cloak itself is off at once
                    Assert.That(entMan.TryGetComponent(user, out EntityTurnInvisibleComponent? invisible) && invisible.Enabled,
                        Is.False, "Test setup: and down");
                }

                var now = Server.Timing.CurTime;
                Assert.Multiple(() =>
                {
                    Assert.That(trackerBracer.Comp.CloakCooldownUntil, Is.EqualTo(now + cooldown * 0.75),
                        "a Tracker's cloak cooldown is 75%");
                    Assert.That(plainBracer.Comp.CloakCooldownUntil, Is.EqualTo(now + cooldown),
                        "another Bad Blood's cloak cooldown is unchanged");
                });

                // only the Tracker is offered the mark
                var prey = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(0, 2));
                spawned.Add(prey);
                var markText = Loc.GetString("cmu-bad-blood-tracker-mark-verb");
                Assert.That(verbs.GetLocalVerbs(prey, plain, typeof(Verb)).Any(v => v.Text == markText), Is.False,
                    "another Bad Blood has no clan mark");
                Assert.That(verbs.GetLocalVerbs(plain, tracker, typeof(Verb)).Any(v => v.Text == markText), Is.False,
                    "nor can a Bad Blood be marked");
                var mark = verbs.GetLocalVerbs(prey, tracker, typeof(Verb)).FirstOrDefault(v => v.Text == markText);
                Assert.That(mark, Is.Not.Null, "the Tracker can mark the prey for the clan");
                Assert.That(entMan.HasComponent<YautjaTrackedPreyComponent>(prey), Is.False,
                    "Test setup: the prey is on no map yet");

                verbs.ExecuteVerb(mark!, tracker, prey);
                Assert.Multiple(() =>
                {
                    Assert.That(marks.IsMarkedBy(prey, YautjaMarkKind.Prey, tracker), Is.True, "the prey carries the Tracker's mark");
                    Assert.That(entMan.TryGetComponent(prey, out YautjaTrackedPreyComponent? tracked), Is.True,
                        "the prey is on the clan's bracer maps");
                    Assert.That(tracked!.ExpiresAt, Is.EqualTo(now + TimeSpan.FromSeconds(60)), "for 60 s");
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

    private static double Armor(IEntityManager entMan, EntityUid uid)
    {
        var ev = new CMGetArmorEvent(SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING);
        entMan.EventBus.RaiseLocalEvent(uid, ref ev);
        return ev.ArmorModifier;
    }

    private Entity<YautjaBracerComponent> Bracer(EntityUid user)
    {
        Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(user, "gloves", out var worn), Is.True,
            "a Bad Blood lands wearing a bracer");
        return (worn!.Value, Server.EntMan.GetComponent<YautjaBracerComponent>(worn.Value));
    }

    // the bracer's own toggle, the one the cloak action runs, without the action's use checks
    private static void PressCloak(IEntityManager entMan, EntityUid user, Entity<YautjaBracerComponent> bracer)
    {
        Assert.That(entMan.System<YautjaCloakSystem>().TryToggleCloakForced(bracer, user, 25), Is.True,
            "Test setup: the cloak toggles");
    }
}
