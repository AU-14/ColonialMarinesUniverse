#pragma warning disable RA0002 // tests read component data directly
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Robust.Shared.Prototypes;
using Content.Server.NPC.HTN;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Gibbing;
using Content.Shared._RMC14.K9.Components;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-115: the landing party gets its sleepers, waking one binds the master and opens the ghost role, the hound stands
// idle and goes back to sleep unclaimed, and it walks over the clan's own traps.
// ARCH-603: every other hound is free at the kennel, a ghost-role spawner that stocks MinStock at pitch, one more each
// Interval up to its cap, and binds the hound to the Leader. ECS-606: the clan hound is grunt spam (150 / 220, a 1 s
// leap knockdown) and always gibs. FLOW-605: a hound player waits out a respawn delay after their hound dies
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
    public async Task LandingHoundIsBoundIdleAndGoesBackToSleep()
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

                rule.Comp.ThreatBudgetFraction = 1000f;
                rule.Comp.ReserveQueue.Clear();
                rule.Comp.DeadBadBloods.Clear();

                var landing = Sleepers(map);
                Assert.That(landing, Has.Count.EqualTo(1), "a landing party of two lands with one sleeping hound");
                Assert.That(hounds.CountHounds(), Is.EqualTo(1));
                Assert.That(hounds.GetHoundCap(), Is.EqualTo(2), "one hound for every two living Bad Bloods, plus one");

                lodge = entMan.SpawnEntity(Lodge, map.GridCoords);
                kennel = entMan.SpawnEntity(Kennel, map.GridCoords.Offset(new Vector2(1, 1)));
                Assert.That(camp.TryGetPart(BadBloodCampPartKind.Kennel, out var part) && part == kennel, Is.True);
                Assert.That(Sleepers(map), Has.Count.EqualTo(1), "the kennel spawns no sleepers");

                var sleeper = (landing[0], entMan.GetComponent<BadBloodSleepingHoundComponent>(landing[0]));
                Assert.That(hounds.CanWake(sleeper, grunt, out _), Is.True, "any Bad Blood wakes a landing hound");

                // the shared wake path, as the confirm dialog raises it
                entMan.EventBus.RaiseLocalEvent(landing[0], new YautjaSleepingHellhoundConfirmEvent(entMan.GetNetEntity(grunt)));

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

                    // ECS-606, the body: crit 150, dead 220, xeno armor 30, a bite that pierces 20
                    var thresholds = Server.System<MobThresholdSystem>();
                    Assert.That(thresholds.GetThresholdForState(hound, MobState.Critical).Int(), Is.EqualTo(150));
                    Assert.That(thresholds.GetThresholdForState(hound, MobState.Dead).Int(), Is.EqualTo(220));
                    Assert.That(entMan.GetComponent<CMArmorComponent>(hound).XenoArmor, Is.EqualTo(30));
                    Assert.That(entMan.GetComponent<CMArmorPiercingComponent>(hound).Amount, Is.EqualTo(20));

                    // a 1 s leap knockdown, and the rest of the leap is the hellhound's
                    var leap = entMan.GetComponent<XenoLeapComponent>(hound);
                    Assert.That(leap.KnockdownTime, Is.EqualTo(TimeSpan.FromSeconds(1)));
                    Assert.That(leap.Delay, Is.EqualTo(TimeSpan.Zero), "the leap keeps the hellhound's instant pounce");
                    Assert.That(leap.CanBeShieldBlocked, Is.False);
                    Assert.That(leap.Damage.GetTotal().Int(), Is.EqualTo(30), "the leap keeps the hellhound's pounce damage");
                    Assert.That(entMan.GetComponent<RMCGibOnDeathComponent>(hound).GibChance, Is.EqualTo(1f));
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

                // FLOW-601: the clan's fallen get no window of their own and hold nobody back
                rule.Comp.DeadBadBloods[new NetUserId(Guid.NewGuid())] = Server.Timing.CurTime;
                Assert.That(hounds.CanTakeHound(houndEnt, session, out _), Is.True,
                    "the clan's fallen do not go before ordinary ghosts");
                rule.Comp.DeadBadBloods[session.UserId] = Server.Timing.CurTime;
                rule.Comp.ReserveQueue.Add(new NetUserId(Guid.NewGuid()));
                Assert.That(hounds.CanTakeHound(houndEnt, session, out seatReason), Is.False,
                    "a fallen Bad Blood waits out the reserve like anyone else");
                Assert.That(seatReason, Is.EqualTo("cmu-bad-blood-hound-seat-reserve"));
                rule.Comp.ReserveQueue.Clear();
                rule.Comp.DeadBadBloods.Clear();

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
                Assert.That(entMan.Deleted(hound), Is.True, "nobody took the hound: it goes back to sleep");

                var asleep = Sleepers(map);
                Assert.That(asleep, Has.Count.EqualTo(1), "the hound is a sleeper again, nothing is lost");
                var at = entMan.GetComponent<TransformComponent>(asleep[0]).Coordinates;
                var home = entMan.GetComponent<TransformComponent>(kennel).Coordinates;
                Assert.That((at.Position - home.Position).Length(), Is.LessThan(0.1f), "it sleeps at the kennel");
            });
        }
        finally
        {
            await Cleanup(party, trap, kennel, lodge);
        }
    }

    // ARCH-603: the kennel is a free spawner. Six living Bad Bloods cap it at three
    [Test]
    public async Task KennelStocksFreeHoundsForTheLeaderUpToItsCap()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 5);
        var session = Pair.Player!;
        EntityUid lodge = default;
        EntityUid kennel = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var hounds = Server.System<BadBloodHoundSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                rule.Comp.ThreatBudgetFraction = 1000f;
                rule.Comp.ReserveQueue.Clear();
                rule.Comp.DeadBadBloods.Clear();
                Server.PlayerMan.SetAttachedEntity(session, null);

                lodge = entMan.SpawnEntity(Lodge, map.GridCoords);
                kennel = entMan.SpawnEntity(Kennel, map.GridCoords.Offset(new Vector2(1, 1)));
                var stock = entMan.GetComponent<BadBloodKennelSpawnerComponent>(kennel);
                var spawner = entMan.GetComponent<GhostRoleMobSpawnerComponent>(kennel);
                var role = entMan.GetComponent<GhostRoleComponent>(kennel);

                // at pitch: MinStock free hounds, a raffle with the hound's job and the threat role
                Assert.Multiple(() =>
                {
                    Assert.That(stock.MinStock, Is.EqualTo(2));
                    Assert.That(stock.Max, Is.EqualTo(3), "one hound for every two of the six living Bad Bloods");
                    Assert.That(spawner.AvailableTakeovers, Is.EqualTo(stock.MinStock), "the kennel offers MinStock at pitch");
                    Assert.That(spawner.Prototype?.Id, Is.EqualTo(Hound));
                    Assert.That(spawner.DeleteOnSpawn, Is.False);
                    Assert.That(role.RaffleConfig, Is.Not.Null);
                    Assert.That(role.JobProto?.Id, Is.EqualTo("CMUYautjaHellhound"));
                    Assert.That(role.MindRoles, Does.Contain(new EntProtoId("MindRoleThreat")));
                    var open = Server.System<GhostRoleSystem>().GetGhostRolesInfo(null).Select(info => info.Entity);
                    Assert.That(open, Does.Contain(entMan.GetNetEntity(kennel)), "the kennel is an open ghost role");
                });

                // the reserve goes first for the kennel's first minute, through the kennel's own request gate
                var kennelEnt = (kennel, stock);
                rule.Comp.ReserveQueue.Add(new NetUserId(Guid.NewGuid()));
                Assert.That(RequestAllowed(kennel, session), Is.False, "the reserve gets a fresh stock's first minute");
                Assert.That(hounds.CanTakeHound(kennelEnt, session, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-hound-seat-reserve"));
                rule.Comp.ReserveQueue.Add(session.UserId);
                Assert.That(RequestAllowed(kennel, session), Is.True);
                rule.Comp.ReserveQueue.Remove(session.UserId);
                stock.StockedAt = Server.Timing.CurTime - stock.ReservePriority - TimeSpan.FromSeconds(1);
                Assert.That(RequestAllowed(kennel, session), Is.True, "after the window anyone takes a hound");
                rule.Comp.ReserveQueue.Clear();

                // a ghost takes one: it is the kennel's, driven by the player and the Leader's to command
                var take = new TakeGhostRoleEvent(session);
                entMan.EventBus.RaiseLocalEvent(kennel, ref take);
                Assert.That(take.TookRole, Is.True);
                Assert.That(stock.LiveHounds, Has.Count.EqualTo(1));
                var hound = stock.LiveHounds[0];
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(hound).EntityPrototype?.ID, Is.EqualTo(Hound));
                    Assert.That(session.AttachedEntity, Is.EqualTo(hound), "the player drives the hound");
                    Assert.That(entMan.GetComponent<BadBloodHoundComponent>(hound).Claimed, Is.True,
                        "a kennel hound never goes back to sleep");
                    Assert.That(entMan.GetComponent<K9DogComponent>(hound).Master, Is.EqualTo(party.Leader),
                        "a kennel hound answers to the Leader");
                    Assert.That(entMan.HasComponent<K9HandlerComponent>(party.Leader), Is.True);
                    Assert.That(spawner.CurrentTakeovers, Is.EqualTo(1));
                    Assert.That(Sleepers(map), Has.Count.EqualTo(3), "the landing sleepers are untouched");
                });

                // the stock grows on a short timer for the test
                stock.Interval = TimeSpan.FromSeconds(2);
                stock.NextAt = Server.Timing.CurTime + stock.Interval;
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 3);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var stock = entMan.GetComponent<BadBloodKennelSpawnerComponent>(kennel);
                var spawner = entMan.GetComponent<GhostRoleMobSpawnerComponent>(kennel);
                Assert.That(stock.Current, Is.EqualTo(3), "one more hound is stocked each interval");
                Assert.That(spawner.AvailableTakeovers, Is.EqualTo(3));
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 5);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var stock = entMan.GetComponent<BadBloodKennelSpawnerComponent>(kennel);
                var spawner = entMan.GetComponent<GhostRoleMobSpawnerComponent>(kennel);
                Assert.That(stock.Current, Is.EqualTo(stock.Max), "the stock stops at the cap");
                Assert.That(spawner.AvailableTakeovers, Is.EqualTo(3));
            });
        }
        finally
        {
            await Cleanup(party, kennel, lodge);
        }
    }

    // ECS-606 + FLOW-605: a dead clan hound gibs, even from crit, and its player waits out the respawn delay at the kennel
    [Test]
    public async Task KilledHoundGibsAndItsPlayerWaitsToRespawn()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var session = Pair.Player!;
        EntityUid lodge = default;
        EntityUid kennel = default;
        EntityUid hound = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                rule.Comp.ThreatBudgetFraction = 1000f;
                rule.Comp.ReserveQueue.Clear();
                rule.Comp.DeadBadBloods.Clear();
                rule.Comp.HoundRespawnDelay = TimeSpan.FromSeconds(5);
                Server.PlayerMan.SetAttachedEntity(session, null);

                lodge = entMan.SpawnEntity(Lodge, map.GridCoords);
                kennel = entMan.SpawnEntity(Kennel, map.GridCoords.Offset(new Vector2(1, 1)));

                var take = new TakeGhostRoleEvent(session);
                entMan.EventBus.RaiseLocalEvent(kennel, ref take);
                Assert.That(take.TookRole, Is.True);
                hound = entMan.GetComponent<BadBloodKennelSpawnerComponent>(kennel).LiveHounds.Single();

                // into crit, then dead from crit: the way a hound dies in a fight
                Hurt(entMan, hound, 180);
                Assert.That(Server.System<MobStateSystem>().IsCritical(hound), Is.True);
                Hurt(entMan, hound, 100);
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 2);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var hounds = Server.System<BadBloodHoundSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.Deleted(hound), Is.True, "a dead clan hound gibs");
                    Assert.That(entMan.EntityQuery<BadBloodHoundComponent>()
                            .Any(comp => entMan.HasComponent<MobStateComponent>(comp.Owner)),
                        Is.False,
                        "no hound body is left behind");
                    Assert.That(entMan.GetComponent<BadBloodKennelSpawnerComponent>(kennel).LiveHounds, Is.Empty);
                    Assert.That(rule.Comp.HoundDiedAt.ContainsKey(session.UserId), Is.True,
                        "the hound's player starts their respawn delay");
                });

                var kennelEnt = (kennel, entMan.GetComponent<BadBloodKennelSpawnerComponent>(kennel));
                Assert.That(hounds.CanTakeHound(kennelEnt, session, out var reason), Is.False,
                    "a player whose hound just died waits");
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-hound-respawn-wait"));
                Assert.That(hounds.GetHoundRespawnWait(session.UserId), Is.GreaterThan(TimeSpan.Zero));
                Assert.That(RequestAllowed(kennel, session), Is.False, "the kennel's request gate holds the wait");

                // the reserve does not skip the wait
                rule.Comp.ReserveQueue.Add(session.UserId);
                Assert.That(hounds.CanTakeHound(kennelEnt, session, out _), Is.False);
                rule.Comp.ReserveQueue.Clear();
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 4);

            await Server.WaitAssertion(() =>
            {
                var hounds = Server.System<BadBloodHoundSystem>();
                var kennelEnt = (kennel, Server.EntMan.GetComponent<BadBloodKennelSpawnerComponent>(kennel));
                Assert.That(hounds.GetHoundRespawnWait(session.UserId), Is.EqualTo(TimeSpan.Zero));
                Assert.That(hounds.CanTakeHound(kennelEnt, session, out _), Is.True, "the wait is over");
            });
        }
        finally
        {
            await Cleanup(party, kennel, lodge);
        }
    }

    // ARCH-01: one landing hound per two Bad Bloods, rounded up, so the default landing party of five brings three
    [Test]
    public async Task LandingHoundsRoundUp()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 4);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rule.Comp.LandingParty, Is.EqualTo(5));
                Assert.That(Sleepers(map), Has.Count.EqualTo(3), "five Bad Bloods land with three sleeping hounds");
            });
        }
        finally
        {
            await Cleanup(party);
        }
    }

    private bool RequestAllowed(EntityUid role, ICommonSession session)
    {
        var comp = Server.EntMan.GetComponent<GhostRoleComponent>(role);
        var attempt = new GhostRoleRequestAttemptEvent(session, role, comp);
        Server.EntMan.EventBus.RaiseLocalEvent(role, ref attempt);
        return !attempt.Cancelled;
    }

    private static void Hurt(IEntityManager entMan, EntityUid target, int blunt)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = blunt;
        entMan.System<DamageableSystem>().TryChangeDamage(target, damage, ignoreResistances: true);
    }

    private async Task Cleanup(BadBloodTestParty party, params EntityUid[] extra)
    {
        await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        await Server.WaitPost(() =>
        {
            var entMan = Server.EntMan;
            var leftovers = entMan.EntityQuery<BadBloodHoundComponent>().Select(comp => comp.Owner)
                .Concat(entMan.EntityQuery<BadBloodSleepingHoundComponent>().Select(comp => comp.Owner))
                .Concat(party.All)
                .Concat(extra)
                .ToList();

            foreach (var uid in leftovers)
            {
                if (entMan.EntityExists(uid))
                    entMan.DeleteEntity(uid);
            }
        });
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
