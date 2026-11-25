using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.Gravity;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Construction;
using Content.Shared.CMU14.ConstructionRobot;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction.Events;
using Content.Shared.Gravity;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Construction;

public sealed class CMUConstructionRobotTest : InteractionTest
{
    [Test]
    public async Task OnlyOwnerHasRenameVerb()
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        await Server.WaitAssertion(() =>
        {
            var uid = ToServer(robot);
            var verbs = Server.System<SharedVerbSystem>();
            var rename = Loc.GetString("cmu-construction-robot-rename-verb");
            Assert.That(verbs.GetLocalVerbs(uid, SPlayer, typeof(Verb)).Any(verb => verb.Text == rename), Is.False);
            var claim = verbs.GetLocalVerbs(uid, SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, uid);
            Assert.That(verbs.GetLocalVerbs(uid, SPlayer, typeof(Verb)).Any(verb => verb.Text == rename), Is.True);
            var release = verbs.GetLocalVerbs(uid, SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-release"));
            verbs.ExecuteVerb(release, SPlayer, uid);
            Assert.That(verbs.GetLocalVerbs(uid, SPlayer, typeof(Verb)).Any(verb => verb.Text == rename), Is.False);
        });
    }

    [TestCase("Atlas", true)]
    [TestCase(" Atlas ", true)]
    [TestCase("two words", false)]
    [TestCase("", false)]
    [TestCase("abcdefghijklmnopqrstuvwxy", false)]
    public void RobotNamesFollowDroneNamingRules(string name, bool valid)
    {
        Assert.That(CMUDroneName.TryNormalize(name, 24, out _), Is.EqualTo(valid));
    }

    [Test]
    public async Task KitUnpacksVisorIntoFreeHandAndVendorDeployer()
    {
        var kit = await Spawn("CMUConstructionRobotVendorKit", PlayerCoords);
        await Server.WaitAssertion(() =>
        {
            var uid = ToServer(kit);
            Assert.That(HandSys.TryPickupAnyHand(SPlayer, uid), Is.True);
            var use = new UseInHandEvent(SPlayer);
            SEntMan.EventBus.RaiseLocalEvent(uid, use);
            Assert.That(use.Handled, Is.True);
            var held = HandSys.GetActiveItem((SPlayer, Hands));
            Assert.That(held, Is.Not.Null);
            Assert.That(SEntMan.HasComponent<CMUSmartWeldingVisorComponent>(held), Is.True);
            var nearby = new HashSet<Entity<CMUConstructionRobotKitComponent>>();
            Server.System<EntityLookupSystem>().GetEntitiesInRange(
                Transform.GetMapCoordinates(SPlayer), 2f, nearby);
            Assert.That(nearby.Any(deployer => deployer.Comp.RobotPrototype.Id == "CMUConstructionRobotVendor"), Is.True);
        });
    }

    [TestCase("CMSandbagFull50")]
    [TestCase("AU14ConcreteBag")]
    [TestCase("AU14ReinforcedConcreteBag")]
    [TestCase("CMUCableThinLVStack")]
    public async Task RobotCanStoreConstructionMaterials(string prototype)
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        var material = await Spawn(prototype, TargetCoords);
        await Server.WaitAssertion(() =>
        {
            var storage = Server.System<SharedStorageSystem>();
            var robotUid = ToServer(robot);
            var materialUid = ToServer(material);
            Assert.That(storage.CanInsert(robotUid, materialUid, out _), Is.True);
            Assert.That(storage.Insert(robotUid, materialUid, out _), Is.True);
            Assert.That(Comp<CMUConstructionRobotComponent>(robot).Materials!.ContainedEntities,
                Does.Contain(materialUid));
        });
    }

    [Test]
    public async Task RobotStorageStillRejectsNonStacks()
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        var item = await Spawn("CMUConstructionRobotKit", TargetCoords);
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<SharedStorageSystem>().CanInsert(ToServer(robot), ToServer(item), out _),
                Is.False));
    }

    [Test]
    public async Task OwnerHudTracksAllRobotsAndTheirIntegrity()
    {
        var first = await Spawn("CMUConstructionRobot", PlayerCoords);
        var second = await Spawn("CMUConstructionRobot", TargetCoords);
        await Server.WaitPost(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            foreach (var robot in new[] { first, second })
            {
                var uid = ToServer(robot);
                var claim = verbs.GetLocalVerbs(uid, SPlayer, typeof(Verb))
                    .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
                verbs.ExecuteVerb(claim, SPlayer, uid);
            }

            Server.System<DamageableSystem>().SetDamage(
                (ToServer(second), Comp<DamageableComponent>(second)),
                new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(100)));
        });

        await RunSeconds(2);
        await Client.WaitAssertion(() =>
        {
            var entries = CEntMan.GetComponent<CMUConstructionRobotOwnerComponent>(CPlayer).Robots;
            Assert.That(entries.Single(entry => entry.Robot == first).IntegrityPercent, Is.EqualTo(100));
            Assert.That(entries.Single(entry => entry.Robot == second).IntegrityPercent, Is.EqualTo(50));
        });

        await Server.WaitPost(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var release = verbs.GetLocalVerbs(ToServer(first), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-release"));
            verbs.ExecuteVerb(release, SPlayer, ToServer(first));
        });
        await RunSeconds(2);
        await Client.WaitAssertion(() =>
        {
            var entries = CEntMan.GetComponent<CMUConstructionRobotOwnerComponent>(CPlayer).Robots;
            Assert.That(entries.Select(entry => entry.Robot), Is.EquivalentTo(new[] { second }));
        });
    }

    [Test]
    public async Task RobotPausesForFireAndCanDisableAvoidance()
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        await Server.WaitPost(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));
        });
        await Spawn("RMCTileFire", PlayerCoords);
        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var component = Comp<CMUConstructionRobotComponent>(robot);
            Assert.That(component.AvoidingHazard, Is.True);
            Assert.That(component.CurrentFollowTarget, Is.Null);
            Assert.That(SEntMan.GetComponent<CMUConstructionRobotOwnerComponent>(SPlayer).Robots
                .Single(entry => entry.Robot == robot).Activity, Is.EqualTo(CMUConstructionRobotActivity.Hazard));
            component.AvoidHazards = false;
        });
        await RunSeconds(1);
        await Server.WaitAssertion(() => Assert.That(Comp<CMUConstructionRobotComponent>(robot).AvoidingHazard, Is.False));
    }

    [TestPrototypes]
    private const string SchedulingPrototypes = @"
- type: constructionGraph
  id: CMUTestTwoMaterialGraph
  start: start
  graph:
  - node: start
    edges:
    - to: built
      steps:
      - material: CMSteel
        amount: 3
      - material: CMSteel
        amount: 3
      - material: CMGlass
        amount: 4
  - node: built
    entity: CMUTestTwoMaterialBarricade
- type: entity
  parent: AU14BarricadeMetal
  id: CMUTestTwoMaterialBarricade
  components:
  - type: Construction
    graph: CMUTestTwoMaterialGraph
    node: built
- type: construction
  id: CMUTestTwoMaterialRecipe
  name: test barricade
  description: Test barricade.
  graph: CMUTestTwoMaterialGraph
  startNode: start
  targetNode: built
  category: construction-category-structures
  objectType: Structure
  isCM: true
";

    [Test]
    public async Task RobotPrioritizesJobsWithAllKnownMaterials()
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        var metal = await Spawn("CMSheetMetal", TargetCoords);
        await Server.WaitPost(() =>
        {
            Stack.SetCount((ToServer(metal), null), 4);
            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));
        });

        await Client.WaitPost(() =>
        {
            var incomplete = ProtoMan.Index<Content.Shared.Construction.Prototypes.ConstructionPrototype>("CMUTestTwoMaterialRecipe");
            var complete = ProtoMan.Index<Content.Shared.Construction.Prototypes.ConstructionPrototype>("AU14BarricadeMetalRecipe");
            Assert.That(CConSys.TrySpawnGhost(incomplete, CEntMan.GetCoordinates(PlayerCoords), Direction.South, out _), Is.True);
            Assert.That(CConSys.TrySpawnGhost(complete, CEntMan.GetCoordinates(TargetCoords), Direction.South, out var ghost), Is.True);
            Target = CEntMan.GetNetEntity(ghost!.Value);
            ConstructionGhostId = ghost.Value.GetHashCode();
        });

        await RunSeconds(7);
        await CheckTargetChange();
        AssertPrototype("AU14BarricadeMetal");
    }

    [Test]
    public async Task RobotRetriesUnreachableConstruction()
    {
        await Server.WaitPost(() =>
        {
            var gravity = SEntMan.EnsureComponent<GravityComponent>(MapData.Grid.Owner);
            Server.System<GravitySystem>().EnableGravity(MapData.Grid.Owner, gravity);
            for (var x = -1; x <= 4; x++)
            for (var y = -1; y <= 1; y++)
                MapSystem.SetTile(MapData.Grid, new Vector2i(x, y), MapData.Tile.Tile);
        });
        TargetCoords = SEntMan.GetNetCoordinates(Transform.WithEntityId(
            MapData.GridCoords.Offset(new Vector2(4.5f, 0.5f)), MapData.MapUid));
        var robot = await Spawn("CMUConstructionRobotVendor", PlayerCoords);
        await Server.WaitPost(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.Grid.Owner, new Vector2(-0.5f, 0.5f)));
        });
        await StartConstruction("AU14BarricadeMetalRecipe");
        var blockedTarget = Target;
        var blockedGhostId = ConstructionGhostId;
        await RunSeconds(0.25f);
        var walls = new List<NetEntity>();
        for (var y = -1; y <= 1; y++)
        {
            var coordinates = SEntMan.GetNetCoordinates(Transform.WithEntityId(
                MapData.GridCoords.Offset(new Vector2(2.5f, y + 0.5f)), MapData.MapUid));
            walls.Add(await Spawn("WallSolid", coordinates));
        }
        await RunSeconds(9);
        await Server.WaitAssertion(() =>
            Assert.That(Comp<CMUConstructionRobotComponent>(robot).CurrentFollowTarget?.EntityId, Is.EqualTo(SPlayer),
                "A blocked job should be released while its destination is cooling down."));

        TargetCoords = SEntMan.GetNetCoordinates(Transform.WithEntityId(
            MapData.GridCoords.Offset(new Vector2(0.5f, 1.5f)), MapData.MapUid));
        await StartConstruction("AU14BarricadeMetalRecipe");
        await RunSeconds(5);
        await CheckTargetChange();
        AssertPrototype("AU14BarricadeMetal");
        var nearbyBarricade = STarget!.Value;

        Target = blockedTarget;
        ConstructionGhostId = blockedGhostId;
        await RunSeconds(5);
        await Server.WaitAssertion(() =>
        {
            var follow = Comp<CMUConstructionRobotComponent>(robot).CurrentFollowTarget;
            Assert.That(follow, Is.Not.Null);
            Assert.That(follow!.Value.EntityId, Is.Not.EqualTo(SPlayer), "The blocked destination should be retried.");
            foreach (var wall in walls)
                SEntMan.DeleteEntity(ToServer(wall));
            SEntMan.DeleteEntity(nearbyBarricade);
            Transform.SetCoordinates(ToServer(robot),
                new EntityCoordinates(MapData.Grid.Owner, new Vector2(3.6f, 0.5f)));
        });
        await RunSeconds(7);
        await CheckTargetChange();
        AssertPrototype("AU14BarricadeMetal");
    }

    [Test]
    public async Task RobotUsesConstructionSkillAndRecordsMaterialSavings()
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        var metal = await Spawn("CMSheetMetal", TargetCoords);
        var glass = await Spawn("CMSheetGlass", TargetCoords);
        await Server.WaitPost(() =>
        {
            Stack.SetCount((ToServer(metal), null), 10);
            Stack.SetCount((ToServer(glass), null), 10);
            Assert.That(Server.System<SkillsSystem>().GetSkill(ToServer(robot), "RMCSkillConstruction"), Is.EqualTo(4));
            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));
        });
        await StartConstruction("CMUTestTwoMaterialRecipe");
        await RunSeconds(7);
        await CheckTargetChange();
        AssertPrototype("CMUTestTwoMaterialBarricade");
        await Server.WaitAssertion(() =>
        {
            Assert.That(Comp<StackComponent>(metal).Count, Is.EqualTo(8));
            Assert.That(Comp<StackComponent>(glass).Count, Is.EqualTo(6));
            var shortfall = SEntMan.GetComponent<AU14MaterialShortfallComponent>(STarget!.Value);
            Assert.That(shortfall.MissingByStack["CMSteel"], Is.EqualTo(4));
            Assert.That(shortfall.MissingByStack.ContainsKey("CMGlass"), Is.False);
        });
    }

    [Test]
    public async Task RobotRepairsStructuresInAssignedArea()
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        var structure = await Spawn("AU14BarricadeMetal", TargetCoords);
        await Server.WaitPost(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));

            var component = Comp<CMUConstructionRobotComponent>(robot);
            component.RepairAreaCenter = SEntMan.GetComponent<TransformComponent>(ToServer(structure)).Coordinates;
            component.IdlePosition = null;
            Server.System<DamageableSystem>().SetDamage(
                (ToServer(structure), Comp<DamageableComponent>(structure)),
                new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(100)));
        });

        await RunSeconds(7);
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(ToServer(structure)).Float(),
                Is.LessThan(100)));
    }

    [Test]
    public async Task RobotRepairsSyntheticsWithoutAnAssignedArea()
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        var synthetic = await Spawn("AU14MobWorkingJoeGOVFOR", TargetCoords);
        await Server.WaitPost(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));
            Server.System<DamageableSystem>().SetDamage(
                (ToServer(synthetic), Comp<DamageableComponent>(synthetic)),
                new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(50)));
        });

        await RunSeconds(12);
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<DamageableSystem>().GetTotalDamage(ToServer(synthetic)).Float(),
                Is.LessThan(50)));
    }

    [Test]
    public async Task RobotBuildsHescoWalls()
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        var sandbags = await Spawn("CMSandbagFull50", TargetCoords);
        await Server.WaitPost(() =>
        {
            Stack.SetCount((ToServer(sandbags), null), 25);
            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));
        });

        await StartConstruction("AU14HescoUGVRecipe");
        await RunSeconds(35);
        await CheckTargetChange();
        AssertPrototype("AU14HESCOBarrier");
    }

    [Test]
    public async Task OwnerCanAssignAndReleaseCrate()
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        var crate = await Spawn("RMCCrateConstruction", TargetCoords);
        await Server.WaitPost(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));
            var assign = verbs.GetLocalVerbs(ToServer(crate), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-pull-crate"));
            verbs.ExecuteVerb(assign, SPlayer, ToServer(crate));
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Comp<PullableComponent>(crate).Puller, Is.EqualTo(ToServer(robot)));
            var verbs = Server.System<SharedVerbSystem>();
            var drop = verbs.GetLocalVerbs(ToServer(crate), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-drop-crate"));
            verbs.ExecuteVerb(drop, SPlayer, ToServer(crate));
            Assert.That(Comp<PullableComponent>(crate).Puller, Is.Null);
        });
    }

    [TestCase("RMCCrateMetalSheets", true)]
    [TestCase("RMCCrateSecure", false)]
    public async Task RobotOpensOnlyAccessibleCratesForMaterials(string cratePrototype, bool expectedOpen)
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        var crate = await Spawn(cratePrototype, TargetCoords);
        await Server.WaitPost(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));
        });

        await StartConstruction("AU14BarricadeMetalRecipe");
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Comp<EntityStorageComponent>(crate).Open, Is.EqualTo(expectedOpen));
            if (expectedOpen)
            {
                var cargo = Comp<StorageComponent>(robot).Container.ContainedEntities;
                Assert.That(cargo.Any(item => SEntMan.TryGetComponent<StackComponent>(item, out var stack) &&
                                              stack.StackTypeId.Id == "CMSteel"), Is.True);
            }
        });
    }

    [Test]
    public async Task VendorRobotStartsWithFullAccessibleMaterialStorage()
    {
        var robot = await Spawn("CMUConstructionRobotVendor", PlayerCoords);
        var sandbags = await Spawn("CMSandbagFull50", TargetCoords);
        await Server.WaitAssertion(() =>
        {
            var storage = Comp<StorageComponent>(robot);
            Assert.That(Server.System<SharedStorageSystem>().CanInsert(ToServer(robot), ToServer(sandbags), out _),
                Is.False);
            var stacks = storage.Container.ContainedEntities.Select(item => SEntMan.GetComponent<StackComponent>(item)).ToArray();
            Assert.That(stacks, Has.Length.EqualTo(20));
            foreach (var type in new[] { "CMSteel", "CMPlasteel", "RMCWood", "CMGlass" })
            {
                var matching = stacks.Where(stack => stack.StackTypeId.Id == type).ToArray();
                Assert.That(matching, Has.Length.EqualTo(5), type);
                Assert.That(matching.All(stack => stack.Count == 50), Is.True, type);
            }

            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));
            var release = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-release"));
            verbs.ExecuteVerb(release, SPlayer, ToServer(robot));
            Assert.That(storage.Container.ContainedEntities, Has.Count.EqualTo(20));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RobotCommitsMaterialsOnlyIfItSurvivesConstruction(bool destroyDuringBuild)
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        await Server.WaitPost(() =>
        {
            var verbs = Server.System<SharedVerbSystem>();
            var claim = verbs.GetLocalVerbs(ToServer(robot), SPlayer, typeof(Verb))
                .Single(verb => verb.Text == Loc.GetString("cmu-construction-robot-verb-take"));
            verbs.ExecuteVerb(claim, SPlayer, ToServer(robot));
        });
        var metal = await Spawn("CMSheetMetal", TargetCoords);
        await Server.WaitPost(() => Stack.SetCount((ToServer(metal), null), 10));
        await StartConstruction("AU14BarricadeMetalRecipe");

        if (destroyDuringBuild)
        {
            for (var i = 0; i < 12 && !Comp<CMUConstructionRobotComponent>(robot).IsBuilding; i++)
                await RunSeconds(0.25f);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Comp<CMUConstructionRobotComponent>(robot).IsBuilding, Is.True);
                var damage = new DamageSpecifier(SProtoMan.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(500));
                Server.System<DamageableSystem>().SetDamage((ToServer(robot), Comp<DamageableComponent>(robot)), damage);
            });
            await RunSeconds(4);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.HasComponent<CMUConstructionRobotOwnerComponent>(SPlayer), Is.False);
                Assert.That(SEntMan.EntityQuery<StackComponent>().Where(s => s.StackTypeId == "CMSteel").Sum(s => s.Count),
                    Is.EqualTo(10), "Destroying a working robot must release its cargo intact.");
            });
            Assert.That((await FindEntity("AU14BarricadeMetal", shouldSucceed: false)).IsValid(), Is.False);
            return;
        }

        await RunSeconds(7);
        await CheckTargetChange();
        AssertPrototype("AU14BarricadeMetal");
        await Server.WaitAssertion(() => Assert.That(Comp<StackComponent>(metal).Count, Is.EqualTo(8)));
        await Server.WaitPost(() =>
        {
            Transform.Unanchor(STarget!.Value);
            SConstruction.SetPathfindingTarget(STarget.Value, "start");
        });
        await InteractUsing(Screw);
        await InteractUsing(Pry);
        await FindEntity(("CMSheetMetal1", 2));
        await Server.WaitAssertion(() => Assert.That(
            SEntMan.EntityQuery<StackComponent>().Where(s => s.StackTypeId == "CMSteel").Sum(s => s.Count),
            Is.EqualTo(10), "Deconstruction must not refund the robot's material discount."));
    }

    [Test]
    public async Task DraggingFuelTankTransfersOnlyTheMissingFuel()
    {
        var robot = await Spawn("CMUConstructionRobot", PlayerCoords);
        var tank = await Spawn("RMCTankReagentFuel", TargetCoords);
        await Server.WaitPost(() => Comp<CMUConstructionRobotComponent>(robot).WeldingFuel -= 125);

        await DragDrop(tank, robot);
        await Server.WaitAssertion(() =>
        {
            Assert.That(ActiveDoAfters.Select(action => action.Args.Event),
                Has.One.TypeOf<CMUConstructionRobotRefuelDoAfterEvent>());
            Assert.That(Comp<CMUConstructionRobotComponent>(robot).WeldingFuel, Is.EqualTo(875));
        });
        await AwaitDoAfters();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Comp<CMUConstructionRobotComponent>(robot).WeldingFuel, Is.EqualTo(1000));
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(ToServer(tank), "tank", out _, out var solution));
            Assert.That(solution!.GetReagentQuantity(new ReagentId("WeldingFuel", null)).Float(), Is.EqualTo(875));
        });
    }
}
