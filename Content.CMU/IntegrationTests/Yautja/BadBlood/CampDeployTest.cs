using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-110: only the Leader pitches the Lodge, the Lodge takes its parts with it, and it stands up to rifles
[TestFixture]
public sealed class CampDeployTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string LeaderBracer = "CMUYautjaBadBloodLeaderBracer";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task OnlyTheLeaderPitchesAndTheLodgeTakesItsPartsWithIt()
    {
        var map = await Pair.CreateTestMap();

        // the kit only deploys on a planet, and the Lodge and its parts need floor to stand on
        await Server.WaitPost(() =>
        {
            Server.EntMan.EnsureComponent<RMCPlanetComponent>(map.Grid);
            var maps = Server.System<SharedMapSystem>();
            for (var x = -3; x <= 3; x++)
            {
                for (var y = -3; y <= 5; y++)
                {
                    maps.SetTile(map.Grid, map.Tile.GridIndices + new Vector2i(x, y), map.Tile.Tile);
                }
            }
        });

        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var grunt = party.Grunts[0];
        EntityUid kit = default;
        EntityUid lodge = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(party.Leader, "gloves", out var bracer), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(bracer!.Value).EntityPrototype?.ID, Is.EqualTo(LeaderBracer),
                        "the Leader loadouts carry the leader bracer");
                    Assert.That(entMan.HasComponent<BadBloodLeaderBracerComponent>(bracer.Value), Is.True);
                    Assert.That(entMan.GetComponent<BadBloodLeaderComponent>(party.Leader).PitchCampActionEntity, Is.Not.Null,
                        "the Leader holds the Pitch Camp action");
                    Assert.That(entMan.HasComponent<BadBloodLeaderComponent>(grunt), Is.False);
                });

                Assert.That(camp.TryPitchCamp(grunt), Is.False, "Pitch Camp is the Leader's alone");
                Assert.That(rule.Comp.KitOut, Is.False);

                Assert.That(camp.TryPitchCamp(party.Leader), Is.True);
                Assert.That(rule.Comp.KitOut, Is.True);
                Assert.That(rule.Comp.Kit, Is.Not.Null);
                kit = rule.Comp.Kit!.Value;

                // a grunt holding the kit gets nowhere with it
                var use = new UseInHandEvent(grunt);
                entMan.EventBus.RaiseLocalEvent(kit, use);
                Assert.That(use.Handled, Is.True, "the kit refuses a grunt before the deploy starts");
            });

            await Pair.RunSeconds(12);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<BadBloodCampRuleSystem>().HasLodge(), Is.False, "a grunt cannot deploy the kit");
                Assert.That(Server.EntMan.EntityExists(kit), Is.True);
            });

            await Server.WaitPost(() =>
            {
                Server.System<SharedInteractionSystem>().UseInHandInteraction(party.Leader, kit,
                    checkCanUse: false, checkCanInteract: false, checkUseDelay: false);
            });

            await Pair.RunSeconds(12);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);
                Assert.That(rules.GetLodge(), Is.Not.Null, "the Leader deploys the Lodge");
                lodge = rules.GetLodge()!.Value;

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<MetaDataComponent>(lodge).EntityPrototype?.ID, Is.EqualTo(Lodge));
                    Assert.That(rule.Comp.CampPlaced, Is.True);
                    Assert.That(rule.Comp.KitOut, Is.False, "the kit is used up");
                    Assert.That(rule.Comp.FirstCampPitched, Is.True);
                    Assert.That(rule.Comp.FreeTotems, Is.EqualTo(1), "the first Lodge grants one free Totem");
                    Assert.That(entMan.GetComponent<TransformComponent>(kit).ParentUid, Is.EqualTo(lodge),
                        "the kit is inside the Lodge, not on the ground");

                    foreach (var kind in Enum.GetValues<BadBloodCampPartKind>())
                    {
                        Assert.That(camp.TryGetPart(kind, out _), Is.True, $"the Lodge comes with its {kind}");
                    }

                    Assert.That(camp.TryPitchCamp(party.Leader), Is.False, "one Lodge at a time");
                });

                // hidden from marines until something reveals it
                Assert.That(entMan.HasComponent<TacticalMapAlwaysVisibleComponent>(lodge), Is.False);
                Assert.That(camp.RevealLodge(TimeSpan.FromSeconds(60)), Is.True);
                Assert.That(entMan.TryGetComponent(lodge, out TacticalMapAlwaysVisibleComponent? visible), Is.True);
                Assert.That(visible!.VisibleToMarines && visible.VisibleToGovfor, Is.True);
                camp.HideLodge();
                Assert.That(entMan.HasComponent<TacticalMapAlwaysVisibleComponent>(lodge), Is.False);

                Assert.That(Server.System<SharedDestructibleSystem>().DestroyEntity(lodge), Is.True);
            });

            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var rules = Server.System<BadBloodCampRuleSystem>();
                var camp = Server.System<BadBloodCampSystem>();
                Assert.That(rules.TryGetRule(out var rule), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.EntityExists(lodge), Is.False);
                    Assert.That(entMan.EntityExists(kit), Is.False, "the kit goes with the Lodge");
                    Assert.That(entMan.Count<BadBloodCampPartComponent>(), Is.Zero, "the parts go with the Lodge");
                    Assert.That(rules.HasLodge(), Is.False);
                    Assert.That(rule.Comp.CampPlaced, Is.False);
                    Assert.That(rule.Comp.NextCampAt, Is.Not.Null);
                    Assert.That(rule.Comp.FreeTotems, Is.EqualTo(1), "the clan's state outlives the Lodge");
                });

                Assert.That(camp.TryPitchCamp(party.Leader), Is.False, "no rebuild before the cooldown is over");

                rule.Comp.NextCampAt = null;
                Assert.That(camp.TryPitchCamp(party.Leader), Is.True, "the Leader rebuilds once the cooldown is over");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }

    [Test]
    public async Task LodgeShrugsOffRiflesAndFallsToFourCharges()
    {
        var map = await Pair.CreateTestMap();
        EntityUid lodge = default;

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var damageable = Server.System<DamageableSystem>();
            lodge = entMan.SpawnEntity(Lodge, map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<RMCWallExplosionDeletableComponent>(lodge), Is.False,
                    "one charge must not delete the Lodge outright");
                Assert.That(entMan.GetComponent<ExplosionResistanceComponent>(lodge).DamageCoefficient, Is.EqualTo(2f));
            });

            // an MK1 ball round: 44 piercing
            var bullet = new DamageSpecifier(Server.ProtoMan.Index<DamageTypePrototype>("Piercing"), 44);
            damageable.TryChangeDamage(lodge, bullet);
            Assert.That(damageable.GetTotalDamage(lodge), Is.LessThanOrEqualTo(FixedPoint2.New(2)),
                "a rifle round barely marks the Lodge");
            Assert.That(damageable.GetTotalDamage(lodge), Is.GreaterThan(FixedPoint2.Zero));

            // a C4 or HEDP charge at the Lodge: intensity 20 of the RMC explosion, doubled by the Lodge's explosion
            // weakness, is 200 blunt and 200 heat before its resistances
            for (var i = 0; i < 3; i++)
            {
                damageable.TryChangeDamage(lodge, Charge());
            }
        });

        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.EntityExists(lodge), Is.True, "three charges are not enough");
            Server.System<DamageableSystem>().TryChangeDamage(lodge, Charge());
        });

        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.EntMan.EntityExists(lodge), Is.False, "the fourth charge destroys the Lodge");
        });
    }

    private static DamageSpecifier Charge()
    {
        var charge = new DamageSpecifier();
        charge.DamageDict["Blunt"] = 200;
        charge.DamageDict["Heat"] = 200;
        return charge;
    }
}
