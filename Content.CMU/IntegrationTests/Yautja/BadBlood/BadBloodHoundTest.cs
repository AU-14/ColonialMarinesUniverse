using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Prototypes;
using Content.Server.NPC.HTN;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.K9.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-115: hounds come from sleeping hounds only. the landing party gets its sleepers, the kennel sells one under
// the caps and reserves it for the buyer, waking binds the master and opens the ghost role, the hound stands idle
// and goes back to sleep unclaimed, and it walks over the clan's own traps
[TestFixture]
public sealed class BadBloodHoundTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Kennel = "CMUBadBloodCampKennel";
    private const string Hound = "CMUMobYautjaBadBloodHound";
    private const string Trap = "CMUYautjaHuntingTrap";
    private const string BadBloodFaction = "CMUYautjaBadBlood";
    private const string HunterFaction = "CMUYautja";
    private const string IdleCompound = "CMUBadBloodHoundIdleCompound";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task KennelHoundIsReservedBoundIdleAndGoesBackToSleep()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var session = Pair.Player!;
        EntityUid lodge = default;
        EntityUid kennel = default;
        EntityUid trap = default;
        EntityUid hound = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                var hounds = Server.System<BadBloodHoundSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                // the test server has next to no players: lift the population budget so the hound cap is the thing tested
                rule.Comp.ThreatBudgetFraction = 1000f;
                rule.Comp.ReserveQueue.Clear();
                rule.Comp.DeadBadBloods.Clear();

                var landing = Sleepers(map);
                Assert.That(landing, Has.Count.EqualTo(1), "a landing party of two lands with one sleeping hound");
                Assert.That(hounds.CountHounds(), Is.EqualTo(1));
                Assert.That(hounds.GetHoundCap(), Is.EqualTo(1), "one hound for every two living Bad Bloods");

                Assert.That(rules.AddSp(FixedPoint4.New(6)), Is.True);
                Assert.That(camp.TryBuyHound(grunt), Is.False, "there is no kennel yet");

                lodge = entMan.SpawnEntity(Lodge, map.GridCoords);
                kennel = entMan.SpawnEntity(Kennel, map.GridCoords.Offset(new Vector2(1, 1)));
                Assert.That(camp.TryGetPart(BadBloodCampPartKind.Kennel, out var part) && part == kennel, Is.True);

                Assert.That(camp.TryBuyHound(grunt), Is.False, "the landing hound already fills the cap");
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(6)), "a refused purchase costs nothing");

                entMan.DeleteEntity(landing[0]);
                Assert.That(camp.TryBuyHound(grunt), Is.True, "any Bad Blood buys a hound");
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.Zero), "a hound costs 6 SP");

                var bought = Sleepers(map);
                Assert.That(bought, Has.Count.EqualTo(1));
                var sleeper = (bought[0], entMan.GetComponent<BadBloodSleepingHoundComponent>(bought[0]));

                // the buyer reservation
                Assert.That(hounds.CanWake(sleeper, party.Leader, out var reason), Is.False,
                    "only the buyer wakes a hound they just paid for");
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-hound-sleeper-reserved"));
                Assert.That(hounds.CanWake(sleeper, grunt, out _), Is.True);

                // the shared wake path, as the confirm dialog raises it
                entMan.EventBus.RaiseLocalEvent(bought[0], new YautjaSleepingHellhoundConfirmEvent(entMan.GetNetEntity(grunt)));

                var woken = entMan.EntityQuery<BadBloodHoundComponent>().Select(comp => comp.Owner).ToList();
                Assert.That(woken, Has.Count.EqualTo(1), "waking the sleeper spawns the Bad Blood hound");
                hound = woken[0];

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(hound).EntityPrototype?.ID, Is.EqualTo(Hound));

                    // waking binds the master, and the handler actions are granted to a component added at runtime
                    Assert.That(entMan.GetComponent<K9DogComponent>(hound).Master, Is.EqualTo(grunt));
                    Assert.That(entMan.TryGetComponent(grunt, out K9HandlerComponent? handler), Is.True);
                    Assert.That(handler?.TameAction, Is.Not.Null, "the master gains the handler actions");

                    // the ghost raffle opens
                    var houndRole = entMan.GetComponent<GhostRoleComponent>(hound);
                    Assert.That(houndRole.RaffleConfig, Is.Not.Null);
                    Assert.That(houndRole.JobProto?.Id, Is.EqualTo("CMUYautjaHellhound"));
                    Assert.That(houndRole.MindRoles, Does.Contain(new EntProtoId("MindRoleThreat")),
                        "a hound taken by a ghost is part of the clan's threat, like a squad seat");
                    var open = Server.System<GhostRoleSystem>().GetGhostRolesInfo(null).Select(info => info.Entity);
                    Assert.That(open, Does.Contain(entMan.GetNetEntity(hound)));

                    // its own faction, and nothing to do until a ghost takes it
                    var factions = entMan.GetComponent<NpcFactionMemberComponent>(hound).Factions;
                    Assert.That(factions.Any(faction => faction.Id == BadBloodFaction), Is.True);
                    Assert.That(factions.Any(faction => faction.Id == HunterFaction), Is.False);
                    Assert.That(entMan.GetComponent<HTNComponent>(hound).RootTask.Task.Id, Is.EqualTo(IdleCompound));

                    // the body: 330 health, xeno armor 30, a bite that pierces 20
                    Assert.That(Server.System<MobThresholdSystem>().GetThresholdForState(hound, MobState.Dead).Int(),
                        Is.EqualTo(330));
                    Assert.That(entMan.GetComponent<CMArmorComponent>(hound).XenoArmor, Is.EqualTo(30));
                    Assert.That(entMan.GetComponent<CMArmorPiercingComponent>(hound).Amount, Is.EqualTo(20));
                });

                // seat priority: the reserve goes first
                var houndEnt = (hound, entMan.GetComponent<BadBloodHoundComponent>(hound));
                Assert.That(hounds.CanTakeHound(houndEnt, session, out _), Is.True);
                rule.Comp.ReserveQueue.Add(new NetUserId(Guid.NewGuid()));
                Assert.That(hounds.CanTakeHound(houndEnt, session, out var seatReason), Is.False,
                    "the reserve gets the hound's first minute");
                Assert.That(seatReason, Is.EqualTo("cmu-bad-blood-hound-seat-reserve"));
                rule.Comp.ReserveQueue.Add(session.UserId);
                Assert.That(hounds.CanTakeHound(houndEnt, session, out _), Is.True);
                rule.Comp.ReserveQueue.Clear();

                // the hound walks over a Bad Blood trap, a hunter's trap still takes it
                trap = entMan.SpawnEntity(Trap, map.GridCoords.Offset(new Vector2(3, 3)));
                var trapComp = entMan.GetComponent<YautjaTrapComponent>(trap);
                trapComp.Armed = true;

                trapComp.ArmedFaction = BadBloodFaction;
                var own = new StepTriggerAttemptEvent { Source = trap, Tripper = hound };
                entMan.EventBus.RaiseLocalEvent(trap, ref own);
                Assert.That(own.Cancelled, Is.True, "the hound does not trip Bad Blood traps");

                trapComp.ArmedFaction = HunterFaction;
                var theirs = new StepTriggerAttemptEvent { Source = trap, Tripper = hound };
                entMan.EventBus.RaiseLocalEvent(trap, ref theirs);
                Assert.That(theirs.Cancelled, Is.False, "an honourable hunter's trap is no friend of the hound");
                trapComp.Armed = false;
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 3);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.That(entMan.EntityExists(hound), Is.True, "an unclaimed hound waits for its three minutes");
                Assert.That(Sleepers(map), Is.Empty);

                entMan.GetComponent<BadBloodHoundComponent>(hound).UnclaimedSleepAfter = TimeSpan.FromSeconds(1);
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 3);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(entMan.Deleted(hound), Is.True, "nobody took the hound: it goes back to sleep");

                var asleep = Sleepers(map);
                Assert.That(asleep, Has.Count.EqualTo(1), "the hound is a sleeper again, nothing is lost");
                var at = entMan.GetComponent<TransformComponent>(asleep[0]).Coordinates;
                var home = entMan.GetComponent<TransformComponent>(kennel).Coordinates;
                Assert.That((at.Position - home.Position).Length(), Is.LessThan(0.1f), "it sleeps at the kennel");
                Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.Zero));
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                var entMan = Server.EntMan;
                var leftovers = entMan.EntityQuery<BadBloodHoundComponent>().Select(comp => comp.Owner)
                    .Concat(entMan.EntityQuery<BadBloodSleepingHoundComponent>().Select(comp => comp.Owner))
                    .Concat(party.All)
                    .Append(trap)
                    .Append(kennel)
                    .Append(lodge)
                    .ToList();

                foreach (var uid in leftovers)
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            });
        }
    }

    private List<EntityUid> Sleepers(TestMapData map)
    {
        var entMan = Server.EntMan;
        return entMan.EntityQuery<BadBloodSleepingHoundComponent>()
            .Select(comp => comp.Owner)
            .Where(uid => entMan.GetComponent<TransformComponent>(uid).MapID == map.MapId)
            .ToList();
    }
}
