using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja.BadBlood;
using Content.Server.Destructible;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.Construction;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Construction;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// FLOW-512 / FLOW-513: the Lodge quarry's stone, Bad Blood bricks, the build range around the Lodge and the Totems,
// and the crumble when the Lodge or a Totem falls
[TestFixture]
public sealed class BadBloodQuarryTest : GameTest
{
    private const string Lodge = "CMUBadBloodCamp";
    private const string Quarry = "CMUBadBloodCampQuarry";
    private const string Totem = "CMUBadBloodTotem";
    private const string Bricks = "CMUSheetBadBloodBrick10";
    private const string Wall = "CMUBadBloodWall";
    private const string Door = "CMUBadBloodDoor";
    private const string BadBloodChip = "CMUYautjaBadBloodBracerIdChip";
    private const string WallRecipe = "CMUBadBloodWallBuild";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task QuarryStockGainsDrawsAndRefunds()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var quarries = Server.System<BadBloodQuarrySystem>();
                var stacks = Server.System<SharedStackSystem>();

                Assert.That(quarries.TryGetQuarry(out _), Is.False, "no Lodge, no quarry");

                var lodge = entMan.SpawnEntity(Lodge, map.GridCoords);
                var quarry = entMan.SpawnEntity(Quarry, map.GridCoords.Offset(new Vector2(-1, 1)));
                Assert.That(quarries.TryGetQuarry(out var found), Is.True);
                Assert.That(found.Owner, Is.EqualTo(quarry));
                var comp = found.Comp;

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BadBloodCampPartComponent>(quarry).Kind, Is.EqualTo(BadBloodCampPartKind.Quarry));
                    Assert.That(comp.Stone, Is.EqualTo(20), "a new quarry holds its starting stone");
                    Assert.That(comp.StoneCap, Is.EqualTo(40));
                    Assert.That(comp.BuildRadius, Is.EqualTo(12f));
                });

                var offering = new BadBloodOfferingCreditedEvent(lodge, party.Leader, party.Leader);
                entMan.EventBus.RaiseEvent(EventSource.Local, ref offering);
                Assert.That(comp.Stone, Is.EqualTo(25), "a credited offering adds 5 stone");

                var paid = new BadBloodTotemPaidEvent(EntityUid.Invalid, 2);
                entMan.EventBus.RaiseEvent(EventSource.Local, ref paid);
                Assert.That(comp.Stone, Is.EqualTo(27), "Totem income adds 1 stone per whole SP");

                var sabotage = new BadBloodSiteSabotagedEvent(lodge, party.Leader);
                entMan.EventBus.RaiseEvent(EventSource.Local, ref sabotage);
                Assert.That(comp.Stone, Is.EqualTo(30), "a sabotage adds 3 stone");

                for (var i = 0; i < 5; i++)
                {
                    var more = new BadBloodSiteSabotagedEvent(lodge, party.Leader);
                    entMan.EventBus.RaiseEvent(EventSource.Local, ref more);
                }

                Assert.That(comp.Stone, Is.EqualTo(40), "the quarry stops at its cap");

                // a marine gets nothing
                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords);
                Assert.That(quarries.TryDraw(found, marine), Is.Null);
                Assert.That(comp.Stone, Is.EqualTo(40));

                // a Bad Blood draws ten
                var bricks = quarries.TryDraw(found, party.Leader);
                Assert.That(bricks, Is.Not.Null);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<BadBloodBrickComponent>(bricks!.Value), Is.True);
                    Assert.That(stacks.GetCount((bricks.Value, null)), Is.EqualTo(10));
                    Assert.That(comp.Stone, Is.EqualTo(30));
                });

                // and hands them back
                var use = new InteractUsingEvent(party.Leader, bricks!.Value, quarry, entMan.GetComponent<TransformComponent>(quarry).Coordinates);
                entMan.EventBus.RaiseLocalEvent(quarry, use);
                Assert.That(use.Handled, Is.True);
                Assert.That(comp.Stone, Is.EqualTo(40), "returned bricks go back into the stock");

                // the last of the stock
                comp.Stone = 3;
                var last = quarries.TryDraw(found, party.Leader);
                Assert.That(last, Is.Not.Null);
                Assert.That(stacks.GetCount((last!.Value, null)), Is.EqualTo(3), "a draw takes what is left");
                Assert.That(comp.Stone, Is.Zero);
                Assert.That(quarries.TryDraw(found, party.Leader), Is.Null, "an empty quarry gives nothing");
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }

    [Test]
    public async Task BricksBuildOnlyNearTheLodgeOrATotemAndCrumbleWithThem()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        EntityUid lodge = default;
        EntityUid totem = default;
        EntityUid wallNearTotem = default;
        EntityUid wallNearLodge = default;
        EntityUid looseBricks = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var quarries = Server.System<BadBloodQuarrySystem>();

                lodge = entMan.SpawnEntity(Lodge, map.GridCoords);
                looseBricks = entMan.SpawnEntity(Bricks, map.GridCoords.Offset(new Vector2(1, 0)));
                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords);
                var sandstone = entMan.SpawnEntity("CMUSheetSandstone10", map.GridCoords.Offset(new Vector2(1, 0)));

                Assert.Multiple(() =>
                {
                    Assert.That(Attempt(marine, looseBricks, map.GridCoords), Is.False, "a marine cannot build with clan bricks");
                    Assert.That(Attempt(party.Leader, looseBricks, At(map, 12)), Is.True, "12 tiles from the Lodge is in range");
                    Assert.That(Attempt(party.Leader, looseBricks, At(map, 13)), Is.False, "13 tiles from the Lodge is out of range");
                    Assert.That(Attempt(marine, sandstone, At(map, 13)), Is.True, "plain sandstone is not the clan's business");
                });

                // a registered Totem far from the Lodge holds ground of its own
                totem = entMan.SpawnEntity(Totem, At(map, 40));
                var totemComp = entMan.GetComponent<BadBloodTotemComponent>(totem);
                totemComp.Registered = true;
                totemComp.Desecrated = false;

                Assert.Multiple(() =>
                {
                    Assert.That(Attempt(party.Leader, looseBricks, At(map, 52)), Is.True, "12 tiles from a Totem is in range");
                    Assert.That(Attempt(party.Leader, looseBricks, At(map, 53)), Is.False, "13 tiles from the Totem and far from the Lodge");
                    Assert.That(Attempt(party.Leader, looseBricks, At(map, 26.5f)), Is.False, "13.5 tiles from both");
                });

                totemComp.Desecrated = true;
                Assert.That(Attempt(party.Leader, looseBricks, At(map, 45)), Is.False, "a desecrated Totem holds nothing");
                totemComp.Desecrated = false;
                Assert.That(quarries.IsInBuildRange(At(map, 45)), Is.True);

                wallNearTotem = entMan.SpawnEntity(Wall, At(map, 43));
                wallNearLodge = entMan.SpawnEntity(Wall, At(map, 3));
                entMan.DeleteEntity(sandstone);
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 1);

            await Server.WaitPost(() => Server.EntMan.DeleteEntity(totem));
            await BadBloodTestHelpers.AdvanceClock(Pair, 1);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.EntityExists(wallNearTotem), Is.False, "the wall the fallen Totem held crumbles");
                    Assert.That(entMan.EntityExists(wallNearLodge), Is.True, "the Lodge still holds its own wall");
                    Assert.That(entMan.EntityExists(looseBricks), Is.True);
                });

                Assert.That(Server.System<SharedDestructibleSystem>().DestroyEntity(lodge), Is.True);
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 1);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.EntityExists(wallNearLodge), Is.False, "the Lodge's fall takes its walls");
                    Assert.That(entMan.EntityExists(looseBricks), Is.False, "and every loose brick");
                    Assert.That(entMan.Count<BadBloodFortificationComponent>(), Is.Zero);
                    Assert.That(entMan.Count<BadBloodBrickComponent>(), Is.Zero);
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }

    [Test]
    public async Task WallAndDoorStats()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var entMan = Server.EntMan;
            var wall = entMan.SpawnEntity(Wall, map.GridCoords);
            var door = entMan.SpawnEntity(Door, map.GridCoords.Offset(new Vector2(2, 0)));
            var chip = entMan.SpawnEntity(BadBloodChip, map.GridCoords.Offset(new Vector2(4, 0)));
            var marine = BadBloodTestHelpers.SpawnMarineNear(Server, map.GridCoords, new Vector2(6, 0));
            var access = Server.System<AccessReaderSystem>();

            var destructible = entMan.GetComponent<DestructibleComponent>(wall);
            Assert.Multiple(() =>
            {
                Assert.That(destructible.Thresholds.Any(t => t.Trigger is DamageTrigger { } trigger &&
                                                             trigger.Damage == FixedPoint2.New(1000)), Is.True,
                    "the clan wall breaks at 1000 damage");
                Assert.That(entMan.HasComponent<RMCWallExplosionDeletableComponent>(wall), Is.True, "charges take it down");
                Assert.That(entMan.HasComponent<DestroyedByExplosionTypeComponent>(wall), Is.True);
                Assert.That(entMan.HasComponent<BadBloodFortificationComponent>(wall), Is.True);
                Assert.That(entMan.HasComponent<BadBloodFortificationComponent>(door), Is.True);

                Assert.That(access.IsAllowed(chip, door), Is.True, "the Bad Blood chip opens the clan door");
                Assert.That(access.IsAllowed(marine, door), Is.False, "a marine does not");
            });

            entMan.DeleteEntity(wall);
            entMan.DeleteEntity(door);
            entMan.DeleteEntity(chip);
            entMan.DeleteEntity(marine);
        });
    }

    // the real RMC Build path runs the brick check (the CMU hook in RMCConstructionSystem.Build), and a fortification
    // that completes out of range of the Lodge and every Totem crumbles at once
    [Test]
    public async Task RealBuildRunsTheBrickCheckAndCompletionRechecksRange()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var tile = map.Tile.GridIndices;
        // tile centres measured from the Lodge, which stands on the centre of the test tile
        EntityCoordinates Spot(int x) => new(map.Grid.Owner, (Vector2) (tile + new Vector2i(x, 0)) + new Vector2(0.5f, 0.5f));

        EntityUid wallOut = default;
        EntityUid wallIn = default;

        await Server.WaitPost(() =>
        {
            // floor under every builder, so the build is refused by the brick check and not by the placement check
            var maps = Server.System<SharedMapSystem>();
            foreach (var x in new[] { 0, 1, 11, 14 })
            {
                maps.SetTile(map.Grid, tile + new Vector2i(x, 0), map.Tile.Tile);
            }
        });

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var construction = Server.System<RMCConstructionSystem>();
                var skills = Server.System<SkillsSystem>();
                var xforms = Server.System<SharedTransformSystem>();

                entMan.SpawnEntity(Lodge, Spot(0));
                Assert.That(Server.System<BadBloodCampRuleSystem>().HasLodge(), Is.True);
                Assert.That(entMan.Count<BadBloodTotemComponent>(), Is.Zero, "no Totem holds ground here");

                var near = party.Grunts[0];
                var far = party.Leader;
                xforms.SetCoordinates(near, Spot(11));
                xforms.SetCoordinates(far, Spot(14));
                var marine = BadBloodTestHelpers.SpawnMarineNear(Server, Spot(1), Vector2.Zero);

                // the recipe needs Construction 2: give it to everyone, so the skill gate is not what refuses
                foreach (var builder in new[] { near, far, marine })
                {
                    skills.SetSkill(builder, "RMCSkillConstruction", 2);
                }

                var bricks = entMan.SpawnEntity(Bricks, Spot(11));
                var item = (bricks, entMan.GetComponent<RMCConstructionItemComponent>(bricks));

                Assert.Multiple(() =>
                {
                    Assert.That(construction.Build(item, marine, WallRecipe, 1), Is.False,
                        "a marine in range of the Lodge cannot build with clan bricks");
                    Assert.That(ActiveDoAfters(marine), Is.Zero);

                    Assert.That(construction.Build(item, far, WallRecipe, 1), Is.False,
                        "a Bad Blood 14 tiles from the Lodge and every Totem cannot build");
                    Assert.That(ActiveDoAfters(far), Is.Zero);

                    Assert.That(construction.Build(item, near, WallRecipe, 1), Is.True,
                        "a Bad Blood 11 tiles from the Lodge builds");
                });

                // the completion re-check, raised the way RMCConstructionSystem raises it on a finished build
                wallOut = entMan.SpawnEntity(Wall, Spot(20));
                wallIn = entMan.SpawnEntity(Wall, Spot(3));
                foreach (var wall in new[] { wallOut, wallIn })
                {
                    var completed = new ConstructionCompletedEvent(wall, near);
                    entMan.EventBus.RaiseLocalEvent(wall, ref completed, broadcast: true);
                }
            });

            await BadBloodTestHelpers.AdvanceClock(Pair, 1);

            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.EntityExists(wallOut), Is.False, "a wall finished out of range crumbles");
                    Assert.That(entMan.EntityExists(wallIn), Is.True, "a wall finished in range stands");
                });
            });
        }
        finally
        {
            await BadBloodTestHelpers.EndBadBloodRule(Pair, party);
        }
    }

    private int ActiveDoAfters(EntityUid uid)
    {
        return Server.EntMan.TryGetComponent(uid, out DoAfterComponent? doAfters)
            ? doAfters.DoAfters.Values.Count(doAfter => !doAfter.Cancelled && !doAfter.Completed)
            : 0;
    }

    private static EntityCoordinates At(TestMapData map, float x)
    {
        return map.GridCoords.Offset(new Vector2(x, 0));
    }

    // raises the build attempt the RMC construction menu raises, without driving the menu
    private bool Attempt(EntityUid user, EntityUid item, EntityCoordinates coords)
    {
        var ev = new CMUConstructionBuildAttemptEvent(user, item, WallRecipe, coords);
        Server.EntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
        return !ev.Cancelled;
    }
}
