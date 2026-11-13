#pragma warning disable RA0002 // tests read component data directly
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// The Leader is somewhat stronger than the grunts: better armor, a fuller kit, and the armory one tier early
[TestFixture]
public sealed class BadBloodLeaderEdgeTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Armory = "CMUBadBloodCampArmory";
    private const string Combistick = "CMUYautjaCombistick";
    private const string Glaive = "CMUYautjaWarGlaive";
    private const string SmartDisc = "CMUYautjaSmartDisc";
    private const string HealingGel = "CMUYautjaHealingGel";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task EveryLeaderLoadoutOutArmorsAndOutEquipsEveryGruntLoadout()
    {
        await Server.WaitAssertion(() =>
        {
            var protos = Server.ProtoMan;
            var factory = Server.EntMan.ComponentFactory;

            var gear = protos.EnumeratePrototypes<StartingGearPrototype>().ToList();
            var leaders = gear.Where(g => g.ID.StartsWith("CMUYautjaBadBloodLeaderGear") && g.ID != "CMUYautjaBadBloodLeaderGearBase").ToList();
            var grunts = gear.Where(g => g.ID.StartsWith("CMUYautjaBadBloodGruntGear")).ToList();
            Assert.That(leaders, Is.Not.Empty);
            Assert.That(grunts, Is.Not.Empty);

            CMArmorComponent ArmorOf(StartingGearPrototype loadout)
            {
                Assert.That(loadout.Equipment.TryGetValue("outerClothing", out var id), Is.True, $"{loadout.ID} has no armor");
                Assert.That(protos.Index<EntityPrototype>(id.Id).TryComp<CMArmorComponent>(out var armor, factory), Is.True,
                    $"{id.Id} carries no armor values");
                return armor!;
            }

            int Count(StartingGearPrototype loadout, string item)
            {
                return loadout.Storage.Values.Sum(list => list.Count(entry => entry.Id == item));
            }

            var bestGruntMelee = grunts.Max(g => ArmorOf(g).Melee);
            var bestGruntBullet = grunts.Max(g => ArmorOf(g).Bullet);
            var mostGruntGel = grunts.Max(g => Count(g, HealingGel));

            // grunts land in patchwork only: one armor grade across every grunt loadout, the weakest the clan has
            var worstGruntBullet = grunts.Min(g => ArmorOf(g).Bullet);
            Assert.That(bestGruntBullet, Is.EqualTo(worstGruntBullet), "every grunt loadout wears the same armor grade");

            // full clan armor is earned at the armory: better than patchwork, short of the Leader's plate, tier II
            Assert.That(protos.Index<EntityPrototype>("CMUBadBloodClanArmor").TryComp<CMArmorComponent>(out var full, factory), Is.True);
            Assert.That(protos.Index<EntityPrototype>(Armory).TryComp<BadBloodCampArmoryComponent>(out var armoryComp, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(full!.Bullet, Is.GreaterThan(bestGruntBullet));
                Assert.That(full.Bullet, Is.LessThan(leaders.Min(l => ArmorOf(l).Bullet)));
                Assert.That(armoryComp!.Tiers["CMUBadBloodClanArmor"], Is.EqualTo(2));
            });

            // STATE-201: nobody lands with a smart disc, the Leader included. discs come from the ship and the armory
            foreach (var loadout in gear.Where(g => g.ID.StartsWith("CMUYautjaBadBlood")))
            {
                var carried = loadout.Equipment.Values
                    .Concat(loadout.Inhand)
                    .Concat(loadout.Storage.Values.SelectMany(list => list));
                Assert.That(carried.Any(entry => entry.Id == SmartDisc), Is.False, $"{loadout.ID} carries a smart disc");
            }

            foreach (var leader in leaders)
            {
                var armor = ArmorOf(leader);
                Assert.Multiple(() =>
                {
                    Assert.That(armor.Melee, Is.GreaterThan(bestGruntMelee), $"{leader.ID}: melee armor");
                    Assert.That(armor.Bullet, Is.GreaterThan(bestGruntBullet), $"{leader.ID}: bullet armor");
                    Assert.That(Count(leader, HealingGel), Is.GreaterThan(mostGruntGel), $"{leader.ID}: healing capsule");
                });
            }
        });
    }

    [Test]
    public async Task LeaderBuysFromTheArmoryOneTierAheadOfTheClan()
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

                spawned.Add(entMan.SpawnEntity(Lodge, map.GridCoords));
                var armory = entMan.SpawnEntity(Armory, map.GridCoords);
                spawned.Add(armory);

                var armoryEnt = new Entity<BadBloodCampArmoryComponent>(armory, entMan.GetComponent<BadBloodCampArmoryComponent>(armory));
                Assert.That(rules.GetTier(), Is.EqualTo(1));

                Assert.Multiple(() =>
                {
                    // tier II opens to the Leader at tier I, and to nobody else
                    Assert.That(camp.CanBuyFromArmory(armoryEnt, party.Leader, Combistick, out _), Is.True);
                    Assert.That(camp.CanBuyFromArmory(armoryEnt, grunt, Combistick, out _), Is.False);

                    // one tier ahead, not two
                    Assert.That(camp.CanBuyFromArmory(armoryEnt, party.Leader, Glaive, out _), Is.False);
                });

                // 12 lifetime SP: the clan reaches tier II, the Leader reaches the tier III wares
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rules.AddSp(rule.Comp.TierTwoLifetimeSp), Is.True);
                Assert.That(rules.GetTier(), Is.EqualTo(2));

                Assert.Multiple(() =>
                {
                    Assert.That(camp.CanBuyFromArmory(armoryEnt, grunt, Combistick, out _), Is.True);
                    Assert.That(camp.CanBuyFromArmory(armoryEnt, party.Leader, Glaive, out _), Is.True);
                    Assert.That(camp.CanBuyFromArmory(armoryEnt, grunt, Glaive, out _), Is.False);
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
            await Server.WaitPost(() =>
            {
                foreach (var uid in spawned)
                {
                    if (Server.EntMan.EntityExists(uid))
                        Server.EntMan.DeleteEntity(uid);
                }
            });
        }
    }
}
