using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chemistry.Components;
using Content.Server.Chemistry.EntitySystems;
using Content.Server._RMC14.Xenonids.Acid;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Xenonids.Acid;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(VaporSystem))]
[TestOf(typeof(AcidExtinguisherCleanupSystem))]
public sealed class VaporInteractionTest : GameTest
{
    private const string TestReagent = "VaporInteractionTestReagent";
    private const string DefaultVapor = "RMCExtinguisherSpray";
    private const string PoweredVapor = "RMCExtinguisherSpraySpec";

    [TestPrototypes]
    private const string Prototypes = $"""
        - type: reagent
          id: {TestReagent}
          name: reagent-name-nothing
          desc: reagent-desc-nothing
          physicalDesc: reagent-physical-desc-nothing
        """;

    [SidedDependency(Side.Server)] private readonly VaporSystem _vapor = default!;
    [SidedDependency(Side.Server)] private readonly XenoAcidSystem _acid = default!;
    [SidedDependency(Side.Server)] private readonly SharedTransformSystem _transform = default!;

    [TestCase(DefaultVapor, 7)]
    [TestCase(PoweredVapor, 48)]
    public async Task CollisionRaisesOneTouchAndVaporHitWithItsSolution(string vaporPrototype, int expectedPower)
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        EntityUid vapor = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<VaporInteractionProbeSystem>();

            target = SSpawnAtPosition("XenoAcidNormal", map.GridCoords);
            SEntMan.EnsureComponent<ReactiveComponent>(target);
            SEntMan.EnsureComponent<VaporInteractionProbeComponent>(target);

            vapor = SSpawnAtPosition(vaporPrototype, map.GridCoords);
            SComp<SolutionComponent>(vapor).Solution.AddReagent(TestReagent, 1);
        });

        await PoolManager.WaitUntil(Server,
            () => SComp<VaporInteractionProbeComponent>(target).VaporHits > 0,
            maxTicks: 10);
        await Server.WaitRunTicks(3);

        await Server.WaitAssertion(() =>
        {
            var probe = SComp<VaporInteractionProbeComponent>(target);
            var solution = SComp<SolutionComponent>(vapor);

            Assert.Multiple(() =>
            {
                Assert.That(probe.TouchReactions, Is.EqualTo(1));
                Assert.That(probe.VaporHits, Is.EqualTo(1));
                Assert.That(probe.SolutionEntity, Is.EqualTo(vapor));
                Assert.That(probe.SolutionComponent, Is.SameAs(solution));
                Assert.That(probe.Power, Is.EqualTo(expectedPower));
            });
        });
    }

    [Test]
    public async Task WaterOnlyWashesWeakAcid()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var water = SpawnSolution(DefaultVapor, "Water");
            var nonWater = SpawnSolution(DefaultVapor, TestReagent);

            var weakWaterTarget = SEntMan.SpawnEntity(null, map.GridCoords);
            ApplyAcidAndRaiseVapor(weakWaterTarget, XenoAcidStrength.Weak, water);
            Assert.That(_acid.TryGetAcidStrength(weakWaterTarget, out _), Is.False,
                "water must remove weak acid");

            var strongWaterTarget = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, 0)));
            ApplyAcidAndRaiseVapor(strongWaterTarget, XenoAcidStrength.Strong, water);
            Assert.That(_acid.TryGetAcidStrength(strongWaterTarget, out var strong), Is.True);
            Assert.That(strong, Is.EqualTo(XenoAcidStrength.Strong),
                "water must not remove strong acid");

            var weakNonWaterTarget = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(2, 0)));
            ApplyAcidAndRaiseVapor(weakNonWaterTarget, XenoAcidStrength.Weak, nonWater);
            Assert.That(_acid.TryGetAcidStrength(weakNonWaterTarget, out var weak), Is.True);
            Assert.That(weak, Is.EqualTo(XenoAcidStrength.Weak),
                "a solution without water must not remove weak acid");
        });
    }

    [Test]
    public async Task StartPreservesLifetimeCalculationWithoutRecoil()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity(null, map.GridCoords);
            var initialRotation = Angle.FromDegrees(90);
            _transform.SetLocalRotation(user, initialRotation);

            var vapor = SSpawnAtPosition(DefaultVapor, map.GridCoords);
            var vaporComponent = SComp<VaporComponent>(vapor);
            var vaporTransform = SComp<TransformComponent>(vapor);
            var target = _transform.GetMapCoordinates(vapor).Offset(new Vector2(4, 0));

            _vapor.Start((vapor, vaporComponent),
                vaporTransform,
                Vector2.UnitX,
                2,
                target,
                10,
                user);

            var velocity = SComp<PhysicsComponent>(vapor).LinearVelocity.Length();
            var lifetime = SComp<TimedDespawnComponent>(vapor).Lifetime;
            var expectedLifetime = MathF.Min(10, 4 / velocity);

            Assert.Multiple(() =>
            {
                Assert.That(vaporComponent.Active, Is.True);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(vapor), Is.True);
                Assert.That(velocity, Is.GreaterThan(0));
                Assert.That(lifetime, Is.EqualTo(expectedLifetime).Within(0.001f));
                Assert.That(lifetime, Is.EqualTo(2).Within(0.001f));
                Assert.That(SComp<TransformComponent>(user).LocalRotation, Is.EqualTo(initialRotation),
                    "starting vapor must not rotate/lunge the user as throw recoil");
            });
        });
    }

    private Entity<SolutionComponent> SpawnSolution(string prototype, string reagent)
    {
        var uid = SSpawn(prototype);
        var solution = SComp<SolutionComponent>(uid);
        solution.Solution.AddReagent(reagent, 1);
        return (uid, solution);
    }

    private void ApplyAcidAndRaiseVapor(
        EntityUid target,
        XenoAcidStrength strength,
        Entity<SolutionComponent> vapor)
    {
        var acidPrototype = strength switch
        {
            XenoAcidStrength.Weak => "XenoAcidWeak",
            XenoAcidStrength.Normal => "XenoAcidNormal",
            XenoAcidStrength.Strong => "XenoAcidStrong",
            _ => throw new ArgumentOutOfRangeException(nameof(strength), strength, null),
        };

        _acid.ApplyAcid(acidPrototype, strength, target, 1, 1, TimeSpan.FromMinutes(1));
        var link = FindAcidLink(target);
        var hit = new VaporHitEvent(vapor, 7);
        SEntMan.EventBus.RaiseLocalEvent(link, ref hit);
    }

    private EntityUid FindAcidLink(EntityUid target)
    {
        var query = SEntMan.EntityQueryEnumerator<CorrosiveAcidLinkComponent>();
        while (query.MoveNext(out var uid, out var link))
        {
            if (link.Target == target)
                return uid;
        }

        Assert.Fail($"Acid link for {target} was not spawned");
        return default;
    }
}

[RegisterComponent]
public sealed partial class VaporInteractionProbeComponent : Component
{
    public int TouchReactions;
    public int VaporHits;
    public EntityUid SolutionEntity;
    public SolutionComponent? SolutionComponent;
    public int Power;
}

public sealed class VaporInteractionProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VaporInteractionProbeComponent, ReactionEntityEvent>(OnReaction);
        SubscribeLocalEvent<VaporInteractionProbeComponent, VaporHitEvent>(OnVaporHit);
    }

    private static void OnReaction(Entity<VaporInteractionProbeComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method == ReactionMethod.Touch)
            ent.Comp.TouchReactions++;
    }

    private static void OnVaporHit(Entity<VaporInteractionProbeComponent> ent, ref VaporHitEvent args)
    {
        ent.Comp.VaporHits++;
        ent.Comp.SolutionEntity = args.Solution.Owner;
        ent.Comp.SolutionComponent = args.Solution.Comp;
        ent.Comp.Power = args.Power;
    }
}
