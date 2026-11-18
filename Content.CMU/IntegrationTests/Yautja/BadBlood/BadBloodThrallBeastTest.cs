#pragma warning disable RA0002 // tests read component data directly
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Xenonids.JoinXeno;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared._RMC14.Vendors;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.NPC.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 Playtest 5 (docs/GraphAudit-BadBlood-Playtest5Plan.md). ARCH-707: only a Beast-Master buys a thrall cage at the
// kennel; a ghost takes the thrall from it and it is hivebroken into the Bad Blood hive with the clan's faction, bound
// to the buyer, and counted against the buyer's cap and the clan's threat budget. ECS-708: it cannot evolve, devolve
// or join the larva queue, and gibs when it dies. FLOW-709: the cage is taken in the hound's order (the reserve first),
// and an abandoned thrall is never offered to the larva queue
[TestFixture]
public sealed class BadBloodThrallBeastTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Kennel = "CMUBadBloodCampKennel";
    private const string RunnerCage = "CMUBadBloodThrallCageRunner";
    private const string DefenderCage = "CMUBadBloodThrallCageDefender";
    private const string BadBloodFaction = "CMUYautjaBadBlood";
    private static readonly ProtoId<BadBloodCastePrototype> BeastMaster = "BeastMaster";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ABeastMastersCageGivesTheClanAThrall()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 3);
        var beastMaster = party.Grunts[0];
        var grunt = party.Grunts[1];
        var session = Pair.Player!;
        var spawned = new List<EntityUid>();
        EntityUid cage = default;
        EntityUid thrall = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                var thralls = Server.System<BadBloodThrallBeastSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                rule.Comp.ThreatBudgetFraction = 1000f;
                rule.Comp.ReserveQueue.Clear();
                Server.PlayerMan.SetAttachedEntity(session, null);

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
                var kennel = entMan.SpawnEntity(Kennel, map.GridCoords.Offset(new Vector2(1, 1)));
                spawned.Add(kennel);
                var vendor = new Entity<BadBloodCampArmoryComponent>(kennel, entMan.GetComponent<BadBloodCampArmoryComponent>(kennel));

                Assert.That(Server.System<BadBloodCasteSystem>().TryTakeCaste(beastMaster, BeastMaster), Is.True);
                Assert.That(rules.AddSp(rule.Comp.TierTwoLifetimeSp), Is.True);
                Assert.That(rules.AddHonour(beastMaster, FixedPoint4.New(30)), Is.True);
                camp.PrepareArmoryBuyer(vendor, beastMaster);

                // the kennel's vendor is the Beast-Master's
                Assert.That(camp.CanBuyFromArmory(vendor, grunt, RunnerCage, out var notYours), Is.False);
                Assert.That(notYours, Is.EqualTo("cmu-bad-blood-caste-armory-locked"));

                var before = rules.GetHonour(beastMaster);
                Vend(entMan, kennel, beastMaster, RunnerCage);
                Assert.That(rules.GetHonour(beastMaster), Is.EqualTo(before - FixedPoint4.New(Price(entMan, kennel, RunnerCage))));
                cage = entMan.EntityQuery<BadBloodThrallCageComponent>().Single().Owner;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BadBloodThrallCageComponent>(cage).Master, Is.EqualTo(beastMaster));
                    Assert.That(thralls.CountThralls(beastMaster), Is.EqualTo(1), "an untaken cage counts as a thrall");
                    Assert.That(thralls.CountClanThralls(), Is.EqualTo(1));
                    var open = Server.System<GhostRoleSystem>().GetGhostRolesInfo(null).Select(info => info.Entity);
                    Assert.That(open, Does.Contain(entMan.GetNetEntity(cage)), "the cage is an open ghost role");
                });

                // FLOW-709: the reserve goes first through the cage's own gate
                rule.Comp.ReserveQueue.Add(new NetUserId(Guid.NewGuid()));
                Assert.That(RequestAllowed(cage, session), Is.False, "the reserve gets a fresh cage's first minute");
                rule.Comp.ReserveQueue.Clear();
                Assert.That(RequestAllowed(cage, session), Is.True);

                // a ghost takes it
                var take = new TakeGhostRoleEvent(session);
                entMan.EventBus.RaiseLocalEvent(cage, ref take);
                Assert.That(take.TookRole, Is.True);
                thrall = session.AttachedEntity!.Value;
                spawned.Add(thrall);

                var factions = entMan.GetComponent<NpcFactionMemberComponent>(thrall).Factions;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<BadBloodThrallBeastComponent>(thrall), Is.True);
                    Assert.That(entMan.GetComponent<BadBloodBeastComponent>(thrall).Master, Is.EqualTo(beastMaster),
                        "the thrall answers to the Beast-Master who bought the cage");
                    Assert.That(entMan.GetComponent<BadBloodBeastMasterComponent>(beastMaster).Beasts, Does.Contain(thrall));
                    Assert.That(entMan.GetComponent<YautjaThrallComponent>(thrall).Master, Is.EqualTo(beastMaster));
                    Assert.That(entMan.HasComponent<YautjaHivebrokenXenoComponent>(thrall), Is.True, "hivebroken");
                    Assert.That(Server.System<SharedXenoHiveSystem>().GetHive(thrall), Is.Not.Null, "in the Bad Blood hive");
                    Assert.That(factions.Any(f => f.Id == BadBloodFaction), Is.True, "the clan's faction");
                    Assert.That(entMan.HasComponent<XenoTargetFilterComponent>(thrall), Is.True, "its abilities spare the clan");

                    // ECS-708: no growth, no larva queue
                    Assert.That(entMan.HasComponent<XenoEvolutionComponent>(thrall), Is.False, "cannot evolve");
                    Assert.That(entMan.HasComponent<XenoDevolveComponent>(thrall), Is.False, "cannot devolve");
                    Assert.That(entMan.HasComponent<LarvaQueueableComponent>(thrall), Is.False, "never in the larva queue");
                    Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(thrall), Is.False,
                        "not listed again while its player drives it");
                    Assert.That(entMan.GetComponent<XenoComponent>(thrall).ContributesToVictory, Is.False);
                    Assert.That(thralls.CountThralls(beastMaster), Is.EqualTo(1), "the taken cage became the thrall");
                });

                // D11: thralls count against the clan's threat budget
                rule.Comp.ThreatBudgetFraction = 0f;
                Assert.That(camp.CanBuyFromArmory(vendor, beastMaster, DefenderCage, out var full), Is.False);
                Assert.That(full, Is.EqualTo("cmu-bad-blood-thrall-budget"));
                rule.Comp.ThreatBudgetFraction = 1000f;

                // ARCH-707: two thralls per Beast-Master
                Vend(entMan, kennel, beastMaster, DefenderCage);
                Assert.That(thralls.CountThralls(beastMaster), Is.EqualTo(2));
                Assert.That(camp.CanBuyFromArmory(vendor, beastMaster, RunnerCage, out var capped), Is.False);
                Assert.That(capped, Is.EqualTo("cmu-bad-blood-thrall-cap"));
                spawned.AddRange(entMan.EntityQuery<BadBloodThrallCageComponent>().Select(c => c.Owner));

                // FLOW-709: an abandoned clan beast is never offered to the larva queue
                entMan.AddComponent<AbandonedXenoQueueableComponent>(thrall);
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 1);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<AbandonedXenoQueueableComponent>(thrall), Is.False,
                        "the larva queue never gets a thrall");
                    Assert.That(entMan.Deleted(cage), Is.True, "the cage is used up");
                });
            });

            // a dead thrall gibs and its player waits out the beast respawn delay
            await BadBloodTestHelpers.Kill(Pair, thrall);
            await BadBloodTestHelpers.AdvanceClock(Pair, 2);

            await Server.WaitAssertion(() =>
            {
                var rules = Server.System<BadBloodCampRuleSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(Server.EntMan.Deleted(thrall), Is.True, "a dead thrall gibs");
                Assert.That(rule.Comp.HoundDiedAt.ContainsKey(session.UserId), Is.True);
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned.Concat(party.All))
                {
                    if (uid.IsValid() && Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }

    private bool RequestAllowed(EntityUid role, ICommonSession session)
    {
        var comp = Server.EntMan.GetComponent<GhostRoleComponent>(role);
        var attempt = new GhostRoleRequestAttemptEvent(session, role, comp);
        Server.EntMan.EventBus.RaiseLocalEvent(role, ref attempt);
        return !attempt.Cancelled;
    }

    private static int Price(IEntityManager entMan, EntityUid vendor, string id)
    {
        return FindEntry(entMan, vendor, id).Entry.Points!.Value;
    }

    private static (int Section, int Index, CMVendorEntry Entry) FindEntry(IEntityManager entMan, EntityUid vendor, string id)
    {
        var sections = entMan.GetComponent<CMAutomatedVendorComponent>(vendor).Sections;
        for (var section = 0; section < sections.Count; section++)
        {
            for (var entry = 0; entry < sections[section].Entries.Count; entry++)
            {
                if (sections[section].Entries[entry].Id.Id == id)
                    return (section, entry, sections[section].Entries[entry]);
            }
        }

        Assert.Fail($"The vendor does not sell {id}.");
        return default;
    }

    private static void Vend(IEntityManager entMan, EntityUid vendor, EntityUid user, string id)
    {
        var (section, entry, _) = FindEntry(entMan, vendor, id);
        entMan.EventBus.RaiseLocalEvent(vendor, new CMVendorVendBuiMsg(section, entry, new())
        {
            Actor = user,
            UiKey = CMAutomatedVendorUI.Key,
        });
    }
}
