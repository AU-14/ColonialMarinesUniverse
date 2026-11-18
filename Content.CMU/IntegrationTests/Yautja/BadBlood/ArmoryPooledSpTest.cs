using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-112 / ARCH-01: the Lodge armory sells for the buyer's own Honour through the vendor's pooled-points hook
[TestFixture]
public sealed class ArmoryPooledSpTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Armory = "CMUBadBloodCampArmory";
    private const string Human = "CMMobHuman";
    private const string Hunter = "CMUMobYautja";
    private const string PlainVendor = "CMUTestArmoryPlainVendor";

    private const string Traps = "CMUBadBloodArmoryTrapsBundle";
    private const string SmartDisc = "CMUYautjaSmartDisc";
    private const string WristBlades = "CMUBadBloodWristBladesAttachment";
    private const string Glaive = "CMUYautjaWarGlaive";
    private const string Hivebreaker = "CMUYautjaHivebreaker";
    private const string Eradicator = "CMUBadBloodEradicatorUnlock";
    private const string Lethal = "CMUBadBloodLethalUnlock";
    private const string Kennel = "CMUBadBloodCampKennel";
    private const string KennelRate = "CMUBadBloodKennelRate";
    private const string KennelPack = "CMUBadBloodKennelPack";

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMUTestArmoryPlainVendor
  components:
  - type: CMAutomatedVendor
    scaling: false
    sections:
    - name: Test
      entries:
      - id: CMUYautjaHealingGel
        name: Ware
        points: 3
""";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ArmorySpendsTheBuyersHonourAndKeepsItsTiers()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();
        EntityUid armory = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                var vendors = Server.System<SharedCMAutomatedVendorSystem>();
                var unlocks = Server.System<BadBloodEradicatorUnlockSystem>();

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
                armory = entMan.SpawnEntity(Armory, map.GridCoords);
                spawned.Add(armory);
                Assert.That(rules.HasLodge(), Is.True);

                var armoryEnt = new Entity<BadBloodCampArmoryComponent>(armory, entMan.GetComponent<BadBloodCampArmoryComponent>(armory));
                var vendor = entMan.GetComponent<CMAutomatedVendorComponent>(armory);
                var pointsType = armoryEnt.Comp.PointsType;
                Assert.That(vendor.PointsType, Is.EqualTo(pointsType), "the vendor window reads the clan's balance");

                // nothing the armory sells is a plasma caster: the lethal bolt and the eradicator are clan unlocks,
                // and the plasma lance, which gibs the prey, is not for sale at all
                foreach (var section in vendor.Sections)
                {
                    foreach (var entry in section.Entries)
                    {
                        Assert.That(entry.Points, Is.Not.Null, $"{entry.Id.Id} is sold for Superiority Points");
                        Assert.That(Server.ProtoMan.Index<EntityPrototype>(entry.Id.Id).TryComp<YautjaCasterComponent>(out _, entMan.ComponentFactory),
                            Is.False,
                            $"{entry.Id.Id} is a plasma caster");
                    }
                }

                // the grunt has vendor points of their own, and the armory must never touch them
                var buyer = entMan.EnsureComponent<CMVendorUserComponent>(grunt);
                vendors.SetPoints((grunt, buyer), 50);
                camp.PrepareArmoryBuyer(armoryEnt, grunt);
                camp.PrepareArmoryBuyer(armoryEnt, party.Leader);

                // prices come from the vendor's own entries, so a retune does not break this
                FixedPoint4 Price(string id) => FixedPoint4.New(FindEntry(entMan, armory, id).Entry.Points!.Value);
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                // ARCH-01: the armory sells for each buyer's own Honour and never touches the clan's SP, which is
                // kept for squads, hounds and Totems
                Assert.That(rules.AddSp(FixedPoint4.New(5), 0f), Is.True);
                var clanSp = rules.GetSp();

                var g = Price(Traps) + FixedPoint4.New(2);
                Assert.That(g, Is.LessThan(rule.Comp.TierTwoLifetimeSp), "the opening balance must stay in tier I");
                Assert.That(g, Is.LessThan(Price(SmartDisc) + Price(Traps)), "and must not cover the traps and a disc");
                Assert.That(rules.AddHonour(grunt, g), Is.True);
                var l = FixedPoint4.Zero;
                Assert.That(rules.GetTier(), Is.EqualTo(1));
                unlocks.SyncHolders();

                // at the landing: stun and immobilizer only
                Assert.Multiple(() =>
                {
                    foreach (var badBlood in new[] { grunt, party.Leader })
                    {
                        Assert.That(SwitchRefused(entMan, badBlood, BadBloodEradicatorSystem.StunMode), Is.False);
                        Assert.That(SwitchRefused(entMan, badBlood, 1), Is.False, "the immobilizer is there from the landing");
                        Assert.That(SwitchRefused(entMan, badBlood, BadBloodEradicatorSystem.LethalMode), Is.True,
                            "the lethal bolt has to be earned");
                        Assert.That(SwitchRefused(entMan, badBlood, BadBloodEradicatorSystem.EradicatorMode), Is.True);
                    }
                });

                // a locked tier is refused
                Vend(entMan, armory, grunt, WristBlades);
                Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "a tier II ware is refused at tier I");
                Vend(entMan, armory, grunt, Lethal);
                Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "the lethal bolt is a tier II ware");

                // tier I spends the buyer's own Honour
                Vend(entMan, armory, grunt, Traps);
                g -= Price(Traps);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "the traps cost the buyer their price");
                    Assert.That(rules.GetSp(), Is.EqualTo(clanSp), "the clan's SP is never spent at the armory");
                    Assert.That(buyer.Points, Is.EqualTo(50), "the buyer's own vendor points are untouched");
                    Assert.That(buyer.ExtraPoints?.GetValueOrDefault(pointsType), Is.EqualTo(g.Int()),
                        "the vendor window shows the buyer's own Honour");
                });

                // nobody overdraws, and the clan's SP does not cover them
                Vend(entMan, armory, grunt, SmartDisc);
                Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "a ware the buyer cannot pay for is refused");

                // tier III: the grunt earns enough for the weapons, the Leader for the clan unlocks
                var gruntGrant = Price(Hivebreaker) + Price(Glaive);
                var leaderGrant = Price(Lethal) + Price(Eradicator);
                Assert.That(rules.AddHonour(grunt, gruntGrant), Is.True);
                Assert.That(rules.AddHonour(party.Leader, leaderGrant), Is.True);
                g += gruntGrant;
                l += leaderGrant;
                // the clan's cooking opens tier III; its SP still never reaches the armory
                Assert.That(rules.AddSp(rule.Comp.TierThreeLifetimeSp), Is.True);
                clanSp = rules.GetSp();
                Assert.That(rules.GetTier(), Is.EqualTo(3));
                unlocks.SyncHolders();

                // the eradicator needs the lethal bolt first, whoever asks
                Vend(entMan, armory, party.Leader, Eradicator);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetHonour(party.Leader), Is.EqualTo(l), "the eradicator is refused before the lethal bolt");
                    Assert.That(unlocks.IsEradicatorUnlocked(), Is.False);
                    Assert.That(camp.CanBuyFromArmory(armoryEnt, party.Leader, Eradicator, out var why), Is.False);
                    Assert.That(why, Is.EqualTo("cmu-bad-blood-armory-needs-lethal"));
                    Assert.That(unlocks.UnlockEradicator(), Is.False, "nor can it be unlocked behind the armory's back");
                });

                // only the Leader buys the lethal bolt, once, and the whole clan gets it
                Vend(entMan, armory, grunt, Lethal);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "a grunt cannot buy the lethal bolt");
                    Assert.That(unlocks.IsLethalUnlocked(), Is.False);
                    Assert.That(camp.CanBuyFromArmory(armoryEnt, grunt, Lethal, out var whyNot), Is.False);
                    Assert.That(whyNot, Is.EqualTo("cmu-bad-blood-armory-leader-only"));
                });

                Vend(entMan, armory, party.Leader, Lethal);
                l -= Price(Lethal);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetHonour(party.Leader), Is.EqualTo(l), "the lethal bolt costs the Leader its price");
                    Assert.That(unlocks.IsLethalUnlocked(), Is.True);
                    Assert.That(SwitchRefused(entMan, grunt, BadBloodEradicatorSystem.LethalMode), Is.False,
                        "every Bad Blood fires the lethal bolt once the clan has it");
                    Assert.That(SwitchRefused(entMan, party.Leader, BadBloodEradicatorSystem.LethalMode), Is.False);
                    Assert.That(SwitchRefused(entMan, grunt, BadBloodEradicatorSystem.EradicatorMode), Is.True,
                        "nobody fires the eradicator before the clan has bought it");
                    Assert.That(SwitchRefused(entMan, party.Leader, BadBloodEradicatorSystem.EradicatorMode), Is.True);
                });

                Vend(entMan, armory, party.Leader, Lethal);
                Assert.That(rules.GetHonour(party.Leader), Is.EqualTo(l), "the lethal bolt is bought once");

                Vend(entMan, armory, grunt, Eradicator);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "the eradicator is the Leader's to buy");
                    Assert.That(unlocks.IsEradicatorUnlocked(), Is.False);
                });

                Vend(entMan, armory, party.Leader, Eradicator);
                l -= Price(Eradicator);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetHonour(party.Leader), Is.EqualTo(l), "the eradicator costs the Leader its price");
                    Assert.That(unlocks.IsEradicatorUnlocked(), Is.True);
                    Assert.That(SwitchRefused(entMan, party.Leader, BadBloodEradicatorSystem.EradicatorMode), Is.False,
                        "the Leader can switch to the eradicator once it is bought");
                    Assert.That(SwitchRefused(entMan, grunt, BadBloodEradicatorSystem.EradicatorMode), Is.True,
                        "a grunt still cannot");
                });

                Vend(entMan, armory, party.Leader, Eradicator);
                Assert.That(rules.GetHonour(party.Leader), Is.EqualTo(l), "the eradicator is bought once");

                // any Bad Blood buys the Hivebreaker at tier III
                Vend(entMan, armory, grunt, Hivebreaker);
                g -= Price(Hivebreaker);
                Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "the Hivebreaker costs the buyer its price");

                // tier IV: half price, and nothing is Leader only
                var fullGlaive = Price(Glaive);
                rule.Comp.TierFourUnlocked = true;
                camp.ApplyArmoryTierFour(armoryEnt);
                unlocks.SyncHolders();
                Assert.Multiple(() =>
                {
                    Assert.That(Price(Glaive), Is.EqualTo(FixedPoint4.New(armoryEnt.Comp.TierFourPrices[Glaive])));
                    Assert.That(Price(Glaive).Double(), Is.EqualTo(fullGlaive.Double() / 2).Within(0.51),
                        "tier IV is half price, rounded up");
                    Assert.That(armoryEnt.Comp.LeaderOnly, Is.Empty);
                    Assert.That(SwitchRefused(entMan, grunt, BadBloodEradicatorSystem.EradicatorMode), Is.False,
                        "the clan ship puts the eradicator on every bracer");
                });

                Vend(entMan, armory, grunt, Glaive);
                g -= Price(Glaive);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "the glaive is half price at tier IV");
                    Assert.That(rules.GetSp(), Is.EqualTo(clanSp), "and the clan's SP was never touched");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned.Append(party.Leader).Append(grunt))
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }

    // STATE-602: the kennel wares buy the standing kennel's rate and stock, never a hound
    [Test]
    public async Task KennelWaresUpgradeTheStandingKennel()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
                var armory = entMan.SpawnEntity(Armory, map.GridCoords);
                spawned.Add(armory);
                var armoryEnt = new Entity<BadBloodCampArmoryComponent>(armory, entMan.GetComponent<BadBloodCampArmoryComponent>(armory));
                camp.PrepareArmoryBuyer(armoryEnt, grunt);

                FixedPoint4 Price(string id) => FixedPoint4.New(FindEntry(entMan, armory, id).Entry.Points!.Value);
                // Honour also credits armory tiers: keep the clan at tier I until the test lifts it with SP
                rule.Comp.HonourTierCredit = 0f;
                Assert.That(rules.GetTier(), Is.EqualTo(1));
                Assert.That(rules.AddHonour(grunt, Price(KennelRate) + Price(KennelRate) + Price(KennelRate)), Is.True);
                var g = rules.GetHonour(grunt);

                // no kennel stands: nothing to improve, nothing spent
                Assert.That(camp.CanBuyFromArmory(armoryEnt, grunt, KennelRate, out var why), Is.False);
                Assert.That(why, Is.EqualTo("cmu-bad-blood-armory-kennel-none"));
                Vend(entMan, armory, grunt, KennelRate);
                Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "a kennel ware without a kennel is refused");

                var kennel = entMan.SpawnEntity(Kennel, map.GridCoords.Offset(new Vector2(1, 0)));
                spawned.Add(kennel);
                var stock = entMan.GetComponent<BadBloodKennelSpawnerComponent>(kennel);
                var interval = stock.Interval;
                Assert.That(interval, Is.EqualTo(TimeSpan.FromSeconds(120)));

                // tier I, any Bad Blood: each buy halves the interval
                Vend(entMan, armory, grunt, KennelRate);
                g -= Price(KennelRate);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "the rate ware costs the buyer its price");
                    Assert.That(stock.Interval, Is.EqualTo(interval / 2), "the rate ware halves the kennel's interval");
                    Assert.That(stock.NextAt - Server.Timing.CurTime, Is.LessThanOrEqualTo(stock.Interval),
                        "the next hound comes no later than the new interval");
                });

                Vend(entMan, armory, grunt, KennelRate);
                g -= Price(KennelRate);
                Assert.That(stock.Interval, Is.EqualTo(interval / 4), "the second buy halves it again");

                // a third is refused and costs nothing
                Vend(entMan, armory, grunt, KennelRate);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "the rate ware is bought twice at most");
                    Assert.That(stock.Interval, Is.EqualTo(interval / 4));
                    Assert.That(camp.CanBuyFromArmory(armoryEnt, grunt, KennelRate, out var maxed), Is.False);
                    Assert.That(maxed, Is.EqualTo("cmu-bad-blood-armory-kennel-maxed"));
                });

                // the pack ware is tier II: two more hounds always ready, once
                Assert.That(rules.AddHonour(grunt, Price(KennelPack) + Price(KennelPack)), Is.True);
                g = rules.GetHonour(grunt);
                var minStock = stock.MinStock;
                Assert.That(rules.GetTier(), Is.EqualTo(1), "Test setup: the clan is still at tier I");
                Vend(entMan, armory, grunt, KennelPack);
                Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "the pack ware waits for tier II");

                Assert.That(rules.AddSp(rule.Comp.TierTwoLifetimeSp), Is.True);
                Assert.That(rules.GetTier(), Is.EqualTo(2));
                Vend(entMan, armory, grunt, KennelPack);
                g -= Price(KennelPack);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(g));
                    Assert.That(stock.MinStock, Is.EqualTo(minStock + 2), "the pack ware raises the kennel's floor by two");
                    Assert.That(stock.Max, Is.GreaterThanOrEqualTo(minStock + 2));
                });

                Vend(entMan, armory, grunt, KennelPack);
                Assert.Multiple(() =>
                {
                    Assert.That(rules.GetHonour(grunt), Is.EqualTo(g), "the pack ware is bought once");
                    Assert.That(stock.MinStock, Is.EqualTo(minStock + 2));
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

    [Test]
    public async Task OrdinaryVendorsAndHuntersAreUnchanged()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var vendors = Server.System<SharedCMAutomatedVendorSystem>();

                var vendor = entMan.SpawnEntity(PlainVendor, map.GridCoords);
                var marine = entMan.SpawnEntity(Human, map.GridCoords);
                var hunter = entMan.SpawnEntity(Hunter, map.GridCoords);
                spawned.Add(vendor);
                spawned.Add(marine);
                spawned.Add(hunter);

                Assert.That(rules.AddSp(10), Is.True);

                // an ordinary vendor still charges the buyer and never the clan
                var buyer = entMan.EnsureComponent<CMVendorUserComponent>(marine);
                vendors.SetPoints((marine, buyer), 10);
                Vend(entMan, vendor, marine, "CMUYautjaHealingGel");
                Assert.Multiple(() =>
                {
                    Assert.That(buyer.Points, Is.EqualTo(7), "an ordinary vendor charges the buyer's own points");
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(10)), "and leaves the clan's points alone");
                });

                // and refuses a buyer who cannot pay, as before
                vendors.SetPoints((marine, buyer), 2);
                Vend(entMan, vendor, marine, "CMUYautjaHealingGel");
                Assert.That(buyer.Points, Is.EqualTo(2));

                // a Bad Blood at an ordinary vendor pays like anyone else
                var badBloodBuyer = entMan.EnsureComponent<CMVendorUserComponent>(party.Leader);
                vendors.SetPoints((party.Leader, badBloodBuyer), 5);
                Vend(entMan, vendor, party.Leader, "CMUYautjaHealingGel");
                Assert.Multiple(() =>
                {
                    Assert.That(badBloodBuyer.Points, Is.EqualTo(2));
                    Assert.That(rules.GetSp(), Is.EqualTo(FixedPoint4.New(10)));
                });

                // an honourable hunter's lethal bolt and eradicator are nobody's business, rule or no rule
                Assert.That(SwitchRefused(entMan, hunter, BadBloodEradicatorSystem.LethalMode), Is.False);
                Assert.That(SwitchRefused(entMan, hunter, BadBloodEradicatorSystem.EradicatorMode), Is.False);
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

    // the vend message the vendor window sends, so the purchase goes through the vendor's own points logic
    private static void Vend(IEntityManager entMan, EntityUid vendor, EntityUid user, string id)
    {
        var (section, entry, _) = FindEntry(entMan, vendor, id);
        entMan.EventBus.RaiseLocalEvent(vendor, new CMVendorVendBuiMsg(section, entry, new())
        {
            Actor = user,
            UiKey = CMAutomatedVendorUI.Key,
        });
    }

    private static bool SwitchRefused(IEntityManager entMan, EntityUid user, int mode)
    {
        var attempt = new YautjaCasterModeAttemptEvent(EntityUid.Invalid, mode);
        entMan.EventBus.RaiseLocalEvent(user, ref attempt);
        return attempt.Cancelled;
    }
}
