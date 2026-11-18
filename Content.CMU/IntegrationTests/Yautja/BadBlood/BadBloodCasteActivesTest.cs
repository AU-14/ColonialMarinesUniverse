#pragma warning disable RA0002 // reads caste, rage and war-cry state and thresholds for the test
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stacks;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// CMU14 Playtest 5, the caste lane:
// ARCH-701 a caste can be dropped and everything it gave goes back; ARCH-704 the Hunt Leader holds no caste and a
// caste-holder who takes the lead sheds it; ECS-703 Blood Rage; ECS-705 War Cry and FLOW-701 command sight follow
// leadership; ECS-701 the Engineer's trade; ECS-704 the Tracker's early warning.
[TestFixture]
public sealed class BadBloodCasteActivesTest : GameTest
{
    private static readonly ProtoId<BadBloodCastePrototype> Berserker = "Berserker";
    private static readonly ProtoId<BadBloodCastePrototype> Engineer = "Engineer";
    private static readonly ProtoId<BadBloodCastePrototype> Tracker = "Tracker";
    private static readonly EntProtoId<SkillDefinitionComponent> EngineerSkill = "RMCSkillEngineer";
    private const string Scimitar = "CMUYautjaBadBloodScimitar";
    private const string Bricks = "CMUSheetBadBloodBrick10";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task DroppingACasteGivesEverythingBackAndTheLeaderHoldsNone()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var castes = entMan.System<BadBloodCasteSystem>();
                var thresholds = entMan.System<MobThresholdSystem>();
                var skills = entMan.System<SkillsSystem>();
                var rule = entMan.System<BadBloodCampRuleSystem>();
                var (berserker, engineer) = (party.Grunts[0], party.Grunts[1]);
                Assert.That(rule.SetLeader(party.Leader), Is.True, "Test setup: the party's Leader leads");

                // ARCH-701
                Assert.That(thresholds.TryGetThresholdForState(berserker, MobState.Critical, out var critBefore), Is.True);
                // a grunt body already has engineering 2; lower it so the caste has something to raise and restore
                skills.SetSkill(engineer, EngineerSkill, 0);
                var skillBefore = skills.GetSkill(engineer, EngineerSkill);
                Assert.That(castes.TryTakeCaste(berserker, Berserker), Is.True);
                Assert.That(castes.TryTakeCaste(engineer, Engineer), Is.True);
                Assert.That(skills.GetSkill(engineer, EngineerSkill), Is.GreaterThan(skillBefore), "Test setup: the Engineer's skill rose");
                Assert.That(castes.CountCaste(Berserker), Is.EqualTo(1));

                Assert.That(castes.TryDropCaste(berserker), Is.True);
                Assert.That(castes.TryDropCaste(engineer), Is.True);
                Assert.That(thresholds.TryGetThresholdForState(berserker, MobState.Critical, out var critAfter), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<BadBloodBerserkerComponent>(berserker), Is.False, "the caste's component goes");
                    Assert.That(entMan.HasComponent<BadBloodCasteComponent>(berserker), Is.False, "the caste goes");
                    Assert.That(critAfter, Is.EqualTo(critBefore), "the crit threshold goes back");
                    Assert.That(castes.CountCaste(Berserker), Is.EqualTo(0), "the slot frees");
                    Assert.That(entMan.HasComponent<BadBloodEngineerComponent>(engineer), Is.False);
                    Assert.That(entMan.HasComponent<BadBloodHunterVisorComponent>(engineer), Is.False);
                    Assert.That(skills.GetSkill(engineer, EngineerSkill), Is.EqualTo(skillBefore), "the skill goes back");
                    Assert.That(castes.TryDropCaste(berserker), Is.False, "nothing to drop twice");
                });

                // ARCH-704
                Assert.That(castes.CanTakeCaste(party.Leader, Berserker, out var reason), Is.False, "the Leader holds no caste");
                Assert.That(reason, Is.EqualTo("cmu-bad-blood-caste-leader"));

                Assert.That(castes.TryTakeCaste(berserker, Berserker), Is.True);
                Assert.That(rule.SetLeader(berserker), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<BadBloodBerserkerComponent>(berserker), Is.False,
                        "a Berserker who takes the lead loses the Berserker's body");
                    Assert.That(entMan.HasComponent<BadBloodCasteComponent>(berserker), Is.False);
                    Assert.That(castes.CanTakeCaste(party.Leader, Berserker, out _), Is.True,
                        "the deposed Leader may take a caste again");
                });
            });
        }
        finally
        {
            await Cleanup(party, new List<EntityUid>());
        }
    }

    [Test]
    public async Task BloodRageNeedsWoundsRoarsHitsHarderAndEnds()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        var berserker = party.Grunts[0];

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rage = entMan.System<BadBloodBloodRageSystem>();
                Assert.That(entMan.System<BadBloodCasteSystem>().TryTakeCaste(berserker, Berserker), Is.True);
                var comp = entMan.GetComponent<BadBloodBerserkerComponent>(berserker);
                Assert.That(comp.RageActionEntity, Is.Not.Null, "the caste grants the Blood Rage action");

                var roars = rage.RoarsPlayed;
                Assert.That(rage.TryStartRage((berserker, comp)), Is.False, "refused at full health");
                Assert.That(rage.RoarsPlayed, Is.EqualTo(roars));

                Assert.That(entMan.System<MobThresholdSystem>().TryGetThresholdForState(berserker, MobState.Critical, out var crit), Is.True);
                var wound = new DamageSpecifier();
                wound.DamageDict["Blunt"] = crit!.Value * 0.6f;
                entMan.System<DamageableSystem>().TryChangeDamage(berserker, wound, ignoreResistances: true);
                Assert.That(entMan.System<MobStateSystem>().IsAlive(berserker), Is.True, "Test setup: still standing");

                Assert.That(rage.TryStartRage((berserker, comp)), Is.True, "under half health it rages");
                Assert.Multiple(() =>
                {
                    Assert.That(rage.RoarsPlayed, Is.EqualTo(roars + 1), "one roar per rage");
                    Assert.That(entMan.HasComponent<BadBloodBloodRageComponent>(berserker), Is.True);
                    Assert.That(entMan.System<SharedPointLightSystem>().TryGetLight(berserker, out _), Is.True, "the rage glows");
                    Assert.That(rage.TryStartRage((berserker, comp)), Is.False, "no second rage while raging");
                });

                // hits a quarter harder
                var scimitar = entMan.SpawnEntity(Scimitar, map.GridCoords);
                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(0, 2));
                spawned.AddRange(new[] { scimitar, marine });
                var baseDamage = new DamageSpecifier();
                baseDamage.DamageDict["Slash"] = 20;
                var hit = new MeleeHitEvent(new List<EntityUid> { marine }, berserker, scimitar, baseDamage, null);
                entMan.EventBus.RaiseLocalEvent(scimitar, hit);
                Assert.That(hit.BonusDamage.GetTotal().Float(), Is.EqualTo(5f).Within(0.01f), "+25% melee damage");

                // counts double toward a seize
                var totems = entMan.System<BadBloodTotemSystem>();
                var here = entMan.System<SharedTransformSystem>().GetMapCoordinates(berserker);
                totems.CountNear(here, 0.5f, out var count, out var weight, null);
                Assert.That(count, Is.EqualTo(1));
                Assert.That(weight, Is.EqualTo(2f).Within(0.001f), "a raging Berserker counts 2");
            });

            await BadBloodTestHelpers.AdvanceTime(Pair, TimeSpan.FromSeconds(11));

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.That(entMan.HasComponent<BadBloodBloodRageComponent>(berserker), Is.False, "the rage ends");
                Assert.That(entMan.System<SharedPointLightSystem>().TryGetLight(berserker, out _), Is.False, "the light it added goes");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task WarCryAndCommandSightFollowLeadership()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rule = entMan.System<BadBloodCampRuleSystem>();
                Assert.That(rule.SetLeader(party.Leader), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<BadBloodCommandSightComponent>(party.Leader), Is.True, "the Leader has command sight");
                    Assert.That(entMan.TryGetComponent(party.Leader, out BadBloodWarCryComponent? cry) && cry.ActionEntity != null,
                        Is.True, "the Leader has the War Cry");
                    Assert.That(entMan.HasComponent<BadBloodWarCryComponent>(grunt), Is.False);
                });

                var leaderCry = entMan.GetComponent<BadBloodWarCryComponent>(party.Leader);
                var heard = entMan.System<BadBloodLeaderPerksSystem>().WarCry((party.Leader, leaderCry));
                Assert.That(heard, Is.EqualTo(2), "the Leader and the grunt beside them hear it");
                Assert.That(entMan.HasComponent<BadBloodWarCriedComponent>(grunt), Is.True);

                Assert.That(rule.SetLeader(grunt), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<BadBloodWarCryComponent>(party.Leader), Is.False, "the deposed Leader loses the War Cry");
                    Assert.That(entMan.HasComponent<BadBloodCommandSightComponent>(party.Leader), Is.False, "and the command sight");
                    Assert.That(entMan.HasComponent<BadBloodWarCryComponent>(grunt), Is.True, "the new Leader gains them");
                    Assert.That(entMan.HasComponent<BadBloodCommandSightComponent>(grunt), Is.True);
                });
            });

            await BadBloodTestHelpers.AdvanceTime(Pair, TimeSpan.FromSeconds(9));
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.EntMan.HasComponent<BadBloodWarCriedComponent>(party.Leader), Is.False, "the cry wears off");
            });
        }
        finally
        {
            await Cleanup(party, new List<EntityUid>());
        }
    }

    [Test]
    public async Task EngineerRaisesABulwarkReconsecratesFastAndCountsMore()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
        });

        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        var engineer = party.Grunts[0];

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.That(entMan.System<BadBloodCasteSystem>().TryTakeCaste(engineer, Engineer), Is.True);
                var comp = entMan.GetComponent<BadBloodEngineerComponent>(engineer);
                Assert.That(comp.BulwarkActionEntity, Is.Not.Null, "the caste grants Raise Bulwark");

                var time = new BadBloodConsecrationTimeEvent(false);
                entMan.EventBus.RaiseLocalEvent(engineer, ref time);
                var desecrate = new BadBloodConsecrationTimeEvent(true);
                entMan.EventBus.RaiseLocalEvent(engineer, ref desecrate);
                var weight = new BadBloodContestWeightEvent();
                entMan.EventBus.RaiseLocalEvent(engineer, ref weight);
                Assert.Multiple(() =>
                {
                    Assert.That(time.Multiplier, Is.EqualTo(0.5f).Within(0.001f), "reconsecrates in half the time");
                    Assert.That(desecrate.Multiplier, Is.EqualTo(1f).Within(0.001f), "desecration is not the Engineer's");
                    Assert.That(weight.Weight, Is.EqualTo(1.5f).Within(0.001f), "counts 1.5 toward a seize");
                });

                var engineers = entMan.System<BadBloodEngineerSystem>();
                Assert.That(engineers.TryRaiseBulwark((engineer, comp)), Is.EqualTo(0), "no bricks, no bulwark");

                // face north from the grid's centre tile, ten bricks in hand
                var xform = entMan.System<SharedTransformSystem>();
                xform.SetCoordinates(engineer, map.GridCoords);
                xform.SetLocalRotation(engineer, Angle.Zero);
                var bricks = entMan.SpawnEntity(Bricks, map.GridCoords);
                spawned.Add(bricks);
                Assert.That(entMan.System<SharedHandsSystem>().TryPickupAnyHand(engineer, bricks), Is.True, "Test setup: bricks in hand");

                var placed = engineers.TryRaiseBulwark((engineer, comp));
                Assert.That(placed, Is.EqualTo(3), "three barricades in front");
                Assert.That(entMan.System<SharedStackSystem>().GetCount(bricks), Is.EqualTo(10 - 3 * comp.BricksPerBarricade),
                    "two bricks each");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task TrackerWarnsBeforeATotemIsContested()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();
        var tracker = party.Grunts[0];

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.That(entMan.System<BadBloodCasteSystem>().TryTakeCaste(tracker, Tracker), Is.True);
                var xformSys = entMan.System<SharedTransformSystem>();
                var origin = xformSys.ToMapCoordinates(map.GridCoords);
                var totem = entMan.System<BadBloodTotemSystem>().SpawnOrderedTotem(origin);
                Assert.That(totem, Is.Not.Null, "Test setup: a Totem stands");
                spawned.Add(totem!.Value);

                var radius = entMan.GetComponent<BadBloodTotemComponent>(totem.Value).ContestRadius;
                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(radius + 3, 0));
                spawned.Add(marine);

                var trackers = entMan.System<BadBloodTrackerSystem>();
                var comp = entMan.GetComponent<BadBloodTrackerComponent>(tracker);
                var before = trackers.EarlyWarningsSent;
                trackers.UpdateEarlyWarning((tracker, comp), origin, Server.Timing.CurTime);
                Assert.That(trackers.EarlyWarningsSent, Is.EqualTo(before + 1), "a marine outside the contest radius warns the Tracker");
                trackers.UpdateEarlyWarning((tracker, comp), origin, Server.Timing.CurTime);
                Assert.That(trackers.EarlyWarningsSent, Is.EqualTo(before + 1), "once per Totem per cooldown");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    private async Task Cleanup(BadBloodTestParty party, List<EntityUid> spawned)
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
