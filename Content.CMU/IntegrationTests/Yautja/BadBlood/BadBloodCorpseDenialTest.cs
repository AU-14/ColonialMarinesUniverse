using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// Bad Bloods were fast enough to drag every kill home and remove it for good. They now drag at 35% speed, may only
// skin, behead or take a skull or ribcage from a body that has rotted past revival (limbs stay fair game), get a ghost
// role left behind when they /ghost, carry the clan communicator, and hurt vehicles like xenos do
[TestFixture]
public sealed class BadBloodCorpseDenialTest : GameTest
{
    private const string Human = "CMMobHuman";
    private const string Hunter = "CMUMobYautja";
    private const string Observer = "MobObserver";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task BadBloodsDragBodiesSlowlyAndHonourableHuntersDoNot()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var grunt = party.Grunts[0];
                var hunter = Spawn(entMan, spawned, Hunter, map.GridCoords.Offset(new Vector2(0, 3)));

                var gruntRatio = DragRatio(entMan, spawned, grunt, map.GridCoords.Offset(new Vector2(1, 0)));
                var hunterRatio = DragRatio(entMan, spawned, hunter, map.GridCoords.Offset(new Vector2(1, 3)));

                Assert.Multiple(() =>
                {
                    Assert.That(gruntRatio, Is.EqualTo(0.45f).Within(0.01f), "a Bad Blood drags a body at 45% speed");
                    Assert.That(hunterRatio, Is.EqualTo(1f).Within(0.01f), "an honourable hunter still drags at full speed");
                });
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task BadBloodsOnlyRemoveRottenBodiesButTakeLimbsFresh()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var trophies = Server.System<YautjaTrophySystem>();
                var unrevivable = Server.System<RMCUnrevivableSystem>();
                var xforms = Server.System<SharedTransformSystem>();
                var grunt = party.Grunts[0];
                var limbGrunt = party.Grunts[1];

                xforms.SetCoordinates(grunt, map.GridCoords);
                var fresh = Corpse(entMan, spawned, map.GridCoords.Offset(new Vector2(1, 0)));
                Assert.Multiple(() =>
                {
                    Assert.That(trophies.TryStartButcher(grunt, fresh, YautjaButcherProcedure.Skin), Is.False,
                        "a Bad Blood cannot skin away a body that can still be revived");
                    Assert.That(trophies.TryStartButcher(grunt, fresh, YautjaButcherProcedure.Head), Is.False,
                        "nor take its head");
                    Assert.That(trophies.TryHarvestTrophy(grunt, fresh, YautjaTrophyKind.HumanSkull, out _), Is.False,
                        "nor its skull");
                    Assert.That(trophies.TryHarvestTrophy(grunt, fresh, YautjaTrophyKind.HumanRibcage, out _), Is.False,
                        "nor its ribcage");
                    Assert.That(unrevivable.IsUnrevivable(fresh), Is.False, "the refusals left the body revivable");
                });

                // an arm does not remove the marine from the round
                xforms.SetCoordinates(limbGrunt, map.GridCoords.Offset(new Vector2(0, 3)));
                var armCorpse = Corpse(entMan, spawned, map.GridCoords.Offset(new Vector2(1, 3)));
                Assert.That(trophies.TryStartButcher(limbGrunt, armCorpse, YautjaButcherProcedure.RightArm), Is.True,
                    "a Bad Blood may take a limb from a fresh kill");

                // honourable hunters are unchanged
                var hunter = Spawn(entMan, spawned, Hunter, map.GridCoords.Offset(new Vector2(0, 6)));
                var hunterCorpse = Corpse(entMan, spawned, map.GridCoords.Offset(new Vector2(1, 6)));
                Assert.That(trophies.TryStartButcher(hunter, hunterCorpse, YautjaButcherProcedure.Skin), Is.True,
                    "an honourable hunter may still skin a fresh kill");

                // once the revival window has run out the clan may take everything
                unrevivable.MakeUnrevivable(fresh);
                Assert.That(trophies.TryHarvestTrophy(grunt, fresh, YautjaTrophyKind.HumanSkull, out _), Is.True,
                    "a rotten body gives up its skull");

                var rotten = Corpse(entMan, spawned, map.GridCoords.Offset(new Vector2(-1, 0)));
                unrevivable.MakeUnrevivable(rotten);
                Assert.That(trophies.TryStartButcher(grunt, rotten, YautjaButcherProcedure.Skin), Is.True,
                    "and can be skinned");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task ABadBloodWhoGhostsLeavesAGhostRoleAndADeadOneDoesNot()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 2);
        var spawned = new List<EntityUid>();

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var minds = Server.System<SharedMindSystem>();
                var alive = party.Grunts[0];
                var dead = party.Grunts[1];

                Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(alive), Is.False,
                    "Test setup: a body a player was put in has no ghost role yet");

                Assert.That(minds.TryGetMind(alive, out var aliveMind, out _), Is.True);
                minds.TransferTo(aliveMind, Spawn(entMan, spawned, Observer, map.GridCoords));
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<GhostRoleComponent>(alive), Is.True,
                        "a Bad Blood who ghosts leaves a ghost role behind");
                    Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(alive), Is.True);
                });

                Server.System<MobStateSystem>().ChangeMobState(dead, MobState.Dead);
                Assert.That(minds.TryGetMind(dead, out var deadMind, out _), Is.True);
                minds.TransferTo(deadMind, Spawn(entMan, spawned, Observer, map.GridCoords));
                Assert.That(entMan.HasComponent<GhostTakeoverAvailableComponent>(dead), Is.False,
                    "a dead Bad Blood is not offered to ghosts");
            });
        }
        finally
        {
            await Cleanup(party, spawned);
        }
    }

    [Test]
    public async Task EveryBadBloodCarriesTheCommunicatorAndHurtsVehicles()
    {
        var map = await Pair.CreateTestMap();
        var party = await BadBloodTestHelpers.SpawnParty(Pair, map, grunts: 1);

        try
        {
            await Server.WaitAssertion(() =>
            {
                var entMan = Server.EntMan;
                var inventory = Server.System<InventorySystem>();
                foreach (var badBlood in party.All)
                {
                    Assert.That(inventory.TryGetSlotEntity(badBlood, "ears", out var ears), Is.True,
                        $"{entMan.ToPrettyString(badBlood)} spawns with something on its ears");
                    Assert.Multiple(() =>
                    {
                        Assert.That(entMan.GetComponent<MetaDataComponent>(ears!.Value).EntityPrototype?.ID,
                            Is.EqualTo("CMUYautjaBadBloodCommunicator"));
                        Assert.That(entMan.GetComponent<VehicleDamageMultiplierComponent>(badBlood).Multiplier, Is.EqualTo(3f),
                            "Bad Bloods hit vehicles at the xeno rate");
                    });
                }
            });
        }
        finally
        {
            await Cleanup(party, new List<EntityUid>());
        }
    }

    // sprint speed while pulling a dead body over sprint speed with empty hands
    private float DragRatio(IEntityManager entMan, List<EntityUid> spawned, EntityUid puller, EntityCoordinates bodyAt)
    {
        var speed = Server.System<MovementSpeedModifierSystem>();
        var move = entMan.GetComponent<MovementSpeedModifierComponent>(puller);
        speed.RefreshMovementSpeedModifiers(puller);
        var free = move.CurrentSprintSpeed;

        var body = Corpse(entMan, spawned, bodyAt);
        var pulling = Server.System<PullingSystem>();
        Assert.That(pulling.TryStartPull(puller, body), Is.True, "Test setup: the pull starts");
        var dragging = move.CurrentSprintSpeed / free;

        // letting go gives the speed back on the pull system's own refresh, nothing else refreshes it here
        Assert.That(pulling.TryStopPull(body, entMan.GetComponent<PullableComponent>(body), puller), Is.True);
        Assert.That(move.CurrentSprintSpeed, Is.EqualTo(free).Within(0.01f), "the slowdown ends with the drag");
        return dragging;
    }

    private EntityUid Corpse(IEntityManager entMan, List<EntityUid> spawned, EntityCoordinates at)
    {
        var body = Spawn(entMan, spawned, Human, at);
        Server.System<MobStateSystem>().ChangeMobState(body, MobState.Dead);
        return body;
    }

    private static EntityUid Spawn(IEntityManager entMan, List<EntityUid> spawned, string prototype, EntityCoordinates at)
    {
        var uid = entMan.SpawnEntity(prototype, at);
        spawned.Add(uid);
        return uid;
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
