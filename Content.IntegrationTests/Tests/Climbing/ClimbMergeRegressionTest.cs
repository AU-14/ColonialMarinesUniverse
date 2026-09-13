using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Gravity;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Events;
using Content.Shared.Climbing.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Gravity;
using Content.Shared.Physics;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using DoAfterData = Content.Shared.DoAfter.DoAfter;

namespace Content.IntegrationTests.Tests.Climbing;

[TestFixture]
[TestOf(typeof(ClimbSystem))]
public sealed class ClimbMergeRegressionTest : GameTest
{
    private const string ClimbFixtureName = "climb";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: Table
          id: ClimbMergeTarget
          components:
          - type: Climbable
            range: 3
            delay: 0.25
            startClimbSound:
              path: /Audio/Effects/falling.ogg
          - type: ClimbMergeProbe

        - type: entity
          id: ClimbMergeBarbedObstacle
          components:
          - type: Physics
            bodyType: Static
          - type: Fixtures
            fixtures:
              fix1:
                shape:
                  !type:PhysShapeAabb
                  bounds: "-0.2,-0.2,0.2,0.2"
                hard: false
                layer:
                - None
                mask:
                - None
          - type: Barbed
            isBarbed: true
            thornsDamage:
              types:
                Slash: 1
          - type: ClimbMergeProbe
        """;

    [Test]
    public async Task ObstaclePrecedesEffectsAndCompletedClimbRestoresBarricadeMask()
    {
        var map = await Pair.CreateTestMap();
        EntityUid climber = default;
        EntityUid target = default;
        EntityUid obstacle = default;
        string hardFixtureId = string.Empty;
        var barricadeMask = (int) CollisionGroup.BarricadeImpassable;

        await Server.WaitAssertion(() =>
        {
            var climb = Server.System<ClimbSystem>();
            var mapSystem = Server.System<SharedMapSystem>();
            var physics = Server.System<SharedPhysicsSystem>();
            var gravity = SEntMan.EnsureComponent<GravityComponent>(map.Grid);

            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), map.Tile.Tile);
            Server.System<GravitySystem>().EnableGravity(map.Grid, gravity);

            climber = SEntMan.SpawnEntity(
                "MobHuman",
                // Match the canonical climb test after its blocked walk into the table: the
                // 0.35-radius mob touches the table's 0.45 AABB before the climb fixture is added.
                map.GridCoords.Offset(new Vector2(0.7f, 0.5f)));
            obstacle = SEntMan.SpawnEntity(
                "ClimbMergeBarbedObstacle",
                map.GridCoords.Offset(new Vector2(1, 0.5f)));
            target = SEntMan.SpawnEntity(
                "ClimbMergeTarget",
                map.GridCoords.Offset(new Vector2(1.5f, 0.5f)));

            var fixtures = SEntMan.GetComponent<FixturesComponent>(climber);
            var hardFixture = fixtures.Fixtures.First(entry => entry.Value.Hard);
            hardFixtureId = hardFixture.Key;
            physics.SetCollisionMask(
                climber,
                hardFixture.Key,
                hardFixture.Value,
                hardFixture.Value.CollisionMask | barricadeMask,
                fixtures);

            var audioBefore = SEntMan.EntityQuery<AudioComponent>().Count();
            Assert.That(climb.TryClimb(climber, climber, target, out var blockedId), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(blockedId, Is.Null);
                Assert.That(SEntMan.GetComponent<ClimbMergeProbeComponent>(obstacle).Attempts, Is.EqualTo(1));
                Assert.That(ActiveDoAfters(climber), Is.Empty,
                    "the obstacle check must happen before creating the climb do-after");
                Assert.That(SEntMan.EntityQuery<AudioComponent>().Count(), Is.EqualTo(audioBefore),
                    "the obstacle check must happen before predicted climb audio");
            });

            SEntMan.DeleteEntity(obstacle);
            SEntMan.GetComponent<ClimbMergeProbeComponent>(target).Attempts = 0;

            Assert.That(climb.TryClimb(climber, climber, target, out var climbId), Is.True);
            Assert.That(climbId, Is.Not.Null);
            Assert.That(physics.WakeBody(climber), Is.True,
                "the climb transition must run on an awake dynamic body, as in the canonical movement path");
            var active = ActiveDoAfters(climber);
            Assert.That(active, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(active[0].Args.BreakOnMove, Is.True);
                Assert.That(active[0].Args.BreakOnDamage, Is.False);
                Assert.That(SEntMan.GetComponent<ClimbMergeProbeComponent>(target).Attempts, Is.EqualTo(1),
                    "includeTarget must raise one AttemptClimb event without duplicating the lookup hit");
            });

            var blunt = SProtoMan.Index<DamageTypePrototype>("Blunt");
            var damage = Server.System<DamageableSystem>().TryChangeDamage(
                climber,
                new DamageSpecifier(blunt, FixedPoint2.New(1)),
                interruptsDoAfters: true);
            Assert.That(damage, Is.Not.Null);
            Assert.That(ActiveDoAfters(climber).Single().Cancelled, Is.False,
                "interrupting damage must not cancel the climb do-after");
        });

        await WaitForClimbContact(target);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<ClimbMergeProbeComponent>(target).StartedContacts, Is.EqualTo(1),
                "the climb fixture must produce one real physics contact with the climbable target");
        });

        await WaitForClimbCompletion(climber);
        await Server.WaitAssertion(() =>
        {
            var climbing = SEntMan.GetComponent<ClimbingComponent>(climber);
            var fixtures = SEntMan.GetComponent<FixturesComponent>(climber);
            Assert.Multiple(() =>
            {
                Assert.That(climbing.IsClimbing, Is.True);
                Assert.That(climbing.DisabledFixtureMasks[hardFixtureId] & barricadeMask, Is.EqualTo(barricadeMask));
                Assert.That(fixtures.Fixtures[hardFixtureId].CollisionMask & barricadeMask, Is.Zero,
                    "the completed climb must remove BarricadeImpassable from the original hard fixture");
            });

            Server.System<SharedTransformSystem>().SetCoordinates(
                climber,
                map.GridCoords.Offset(new Vector2(5, 0)));
            Assert.That(Server.System<SharedPhysicsSystem>().WakeBody(climber), Is.True,
                "the test teleport must wake physics so the ended climb contact is processed");
        });

        await WaitForClimbEnd(climber);
        await Server.WaitAssertion(() =>
        {
            var climbing = SEntMan.GetComponent<ClimbingComponent>(climber);
            var fixtures = SEntMan.GetComponent<FixturesComponent>(climber);
            Assert.Multiple(() =>
            {
                Assert.That(climbing.IsClimbing, Is.False);
                Assert.That(climbing.DisabledFixtureMasks, Is.Empty);
                Assert.That(fixtures.Fixtures[hardFixtureId].CollisionMask & barricadeMask,
                    Is.EqualTo(barricadeMask),
                    "ending the climb must restore BarricadeImpassable to the same hard fixture");
                Assert.That(fixtures.Fixtures, Does.Not.ContainKey(ClimbFixtureName));
                Assert.That(SEntMan.GetComponent<ClimbMergeProbeComponent>(target).EndedContacts, Is.EqualTo(1),
                    "moving away must end the exact climb-fixture contact before restoring masks");
            });
        });
    }

    private async Task WaitForClimbCompletion(EntityUid uid)
    {
        const int maxTicks = 120;
        for (var i = 0; i < maxTicks; i++)
        {
            var completed = false;
            await Server.WaitAssertion(() =>
            {
                var climbing = SEntMan.GetComponent<ClimbingComponent>(uid);
                completed = ActiveDoAfters(uid).Count == 0 && climbing.NextTransition == null;
            });

            if (completed)
                return;

            await RunTicksSync(1);
        }

        Assert.Fail("the climb do-after and transition did not complete within 120 ticks");
    }

    private async Task WaitForClimbContact(EntityUid target)
    {
        const int maxTicks = 120;
        for (var i = 0; i < maxTicks; i++)
        {
            var contacted = false;
            await Server.WaitAssertion(() =>
                contacted = SEntMan.GetComponent<ClimbMergeProbeComponent>(target).StartedContacts > 0);

            if (contacted)
                return;

            await RunTicksSync(1);
        }

        Assert.Fail("the climb fixture did not contact the climbable target within 120 ticks");
    }

    private async Task WaitForClimbEnd(EntityUid uid)
    {
        const int maxTicks = 120;
        for (var i = 0; i < maxTicks; i++)
        {
            var ended = false;
            await Server.WaitAssertion(() =>
            {
                var climbing = SEntMan.GetComponent<ClimbingComponent>(uid);
                var fixtures = SEntMan.GetComponent<FixturesComponent>(uid);
                ended = !climbing.IsClimbing
                    && climbing.DisabledFixtureMasks.Count == 0
                    && !fixtures.Fixtures.ContainsKey(ClimbFixtureName);
            });

            if (ended)
                return;

            await RunTicksSync(1);
        }

        Assert.Fail("the climb state and replacement fixture were not cleaned up within 120 ticks");
    }

    private List<DoAfterData> ActiveDoAfters(EntityUid uid)
    {
        return SEntMan.GetComponent<DoAfterComponent>(uid).DoAfters.Values
            .Where(doAfter => !doAfter.Cancelled && !doAfter.Completed)
            .ToList();
    }
}

[RegisterComponent]
public sealed partial class ClimbMergeProbeComponent : Component
{
    public int Attempts;
    public int StartedContacts;
    public int EndedContacts;
}

public sealed class ClimbMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClimbMergeProbeComponent, AttemptClimbEvent>(OnAttemptClimb);
        SubscribeLocalEvent<ClimbMergeProbeComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<ClimbMergeProbeComponent, EndCollideEvent>(OnEndCollide);
    }

    private static void OnAttemptClimb(Entity<ClimbMergeProbeComponent> ent, ref AttemptClimbEvent args)
    {
        ent.Comp.Attempts++;
    }

    private static void OnStartCollide(Entity<ClimbMergeProbeComponent> ent, ref StartCollideEvent args)
    {
        if (args.OtherFixtureId == "climb")
            ent.Comp.StartedContacts++;
    }

    private static void OnEndCollide(Entity<ClimbMergeProbeComponent> ent, ref EndCollideEvent args)
    {
        if (args.OtherFixtureId == "climb")
            ent.Comp.EndedContacts++;
    }
}
