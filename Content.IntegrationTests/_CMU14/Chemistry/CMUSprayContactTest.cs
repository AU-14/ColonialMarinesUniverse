using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chemistry.Components;
using Content.Shared._RMC14.Synth;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Nutrition;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Chemistry;

// FLOW-20: a spray puff flies the full spray distance, so mobs beside or past the click point are
// touched, and clicking a mob with a spray bottle sprays it instead of force-feeding it.
[TestFixture]
public sealed class CMUSprayContactTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private const string RepairReagent = "CMUSprayContactTestRepairer";
    private const string Spray = "SprayBottle";

    // the user stands on tile 0, the click lands on tile 1, the synth stands 2.5 tiles out.
    // Before FLOW-20 the puff stopped at the click point + 0.25 (its edge at 1.5 tiles), well short of the synth.
    private static readonly (float X, float Y) UserPos = (0.5f, 0.5f);
    private static readonly (float X, float Y) ClickPos = (1.5f, 0.5f);
    private static readonly (float X, float Y) SynthPos = (3f, 0.5f);

    [TestPrototypes]
    private const string Prototypes = $"""
        - type: reagent
          id: {RepairReagent}
          name: reagent-name-nothing
          desc: reagent-desc-nothing
          physicalDesc: reagent-physical-desc-nothing
          metabolisms:
            Bloodstream:
              metabolismRate: 0.1
              effects:
              - !type:Repairing
                potency: 2
        """;

    [Test]
    public async Task PuffPassesTheClickPointAndRepairsASynthBeyondIt()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid spray = default;
        EntityUid synth = default;
        FixedPoint2 before = default;

        await Server.WaitAssertion(() =>
        {
            (user, spray, synth, before) = Setup(map);

            // click empty floor between the user and the synth
            var ev = new AfterInteractEvent(user, spray, null, Coords(map.Grid, ClickPos), true);
            SEntMan.EventBus.RaiseLocalEvent(spray, ev);
            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, Is.True, "the spray did not handle a floor click");
                Assert.That(VaporCount(), Is.GreaterThan(0), "spraying at the floor produced no vapor");
            });
        });

        await RunSeconds(2.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SComp<CMUSprayContactProbeComponent>(synth).TouchReactions, Is.GreaterThan(0),
                    "the puff stopped at the click point and never touched the synth standing past it");
                Assert.That(Server.System<DamageableSystem>().GetTotalDamage(synth), Is.LessThan(before),
                    "the synth past the click point was not repaired by the Repairing spray");
            });
        });
    }

    [Test]
    public async Task ClickingASynthSpraysInsteadOfForceFeeding()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid spray = default;
        EntityUid synth = default;
        FixedPoint2 before = default;

        await Server.WaitAssertion(() =>
        {
            (user, spray, synth, before) = Setup(map);

            // the synth is the click target; the click itself lands short of it, as on its sprite's near edge
            var ev = new AfterInteractEvent(user, spray, synth, Coords(map.Grid, ClickPos), true);
            SEntMan.EventBus.RaiseLocalEvent(spray, ev);
            Assert.Multiple(() =>
            {
                Assert.That(ev.Handled, Is.True, "nothing handled clicking the synth with the spray");
                Assert.That(VaporCount(), Is.GreaterThan(0), "clicking the synth with the spray produced no vapor");
                Assert.That(HasEatingDoAfter(user), Is.False, "clicking the synth with the spray started a force-feed");
            });
        });

        await RunSeconds(2.5f);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(HasEatingDoAfter(user), Is.False, "a force-feed started after spraying the synth");
                Assert.That(SComp<CMUSprayContactProbeComponent>(synth).TouchReactions, Is.GreaterThan(0),
                    "the puff aimed at the clicked synth never touched it");
                Assert.That(Server.System<DamageableSystem>().GetTotalDamage(synth), Is.LessThan(before),
                    "the clicked synth was not repaired by the Repairing spray");
            });
        });
    }

    private (EntityUid User, EntityUid Spray, EntityUid Synth, FixedPoint2 Before) Setup(TestMapData map)
    {
        _ = Server.System<CMUSprayContactProbeSystem>();
        var grid = map.Grid.Owner;
        var maps = Server.System<SharedMapSystem>();
        for (var x = 0; x < 6; x++)
            maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

        var user = SSpawnAtPosition("CMMobHuman", Coords(grid, UserPos));
        var spray = SSpawnAtPosition(Spray, Coords(grid, UserPos));
        var solutions = Server.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetSolution(spray, SComp<SprayComponent>(spray).Solution, out var soln, out _), Is.True);
        Assert.That(solutions.TryAddReagent(soln!.Value, RepairReagent, 10, out _), Is.True);
        Assert.That(Server.System<SharedHandsSystem>().TryPickup(user, spray), Is.True, "the user could not hold the spray");

        var synth = SSpawnAtPosition("CMMobHuman", Coords(grid, SynthPos));
        SEntMan.EnsureComponent<SynthComponent>(synth);
        SEntMan.EnsureComponent<CMUSprayContactProbeComponent>(synth);
        var damage = Server.System<DamageableSystem>();
        damage.TryChangeDamage(synth, new DamageSpecifier { DamageDict = { ["Blunt"] = 20, ["Heat"] = 20 } }, true);
        var before = damage.GetTotalDamage(synth);
        Assert.That(before, Is.GreaterThan(FixedPoint2.Zero), "the synth took no damage to repair");
        return (user, spray, synth, before);
    }

    private static EntityCoordinates Coords(EntityUid grid, (float X, float Y) pos)
    {
        return new EntityCoordinates(grid, pos.X, pos.Y);
    }

    private int VaporCount()
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<VaporComponent>();
        while (query.MoveNext(out _))
        {
            count++;
        }

        return count;
    }

    private bool HasEatingDoAfter(EntityUid user)
    {
        return SEntMan.TryGetComponent(user, out DoAfterComponent? doAfters) &&
               doAfters.DoAfters.Values.Any(d => d.Args.Event is EatingDoAfterEvent);
    }
}

[RegisterComponent]
public sealed partial class CMUSprayContactProbeComponent : Component
{
    public int TouchReactions;
}

public sealed class CMUSprayContactProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUSprayContactProbeComponent, ReactionEntityEvent>(OnReaction);
    }

    private static void OnReaction(Entity<CMUSprayContactProbeComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method == ReactionMethod.Touch)
            ent.Comp.TouchReactions++;
    }
}
