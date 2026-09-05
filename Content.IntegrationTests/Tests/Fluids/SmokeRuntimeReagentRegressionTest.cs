using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Fluids;

[TestFixture]
[TestOf(typeof(SmokeSystem))]
[TestOf(typeof(RMCReagentSystem))]
public sealed class SmokeRuntimeReagentRegressionTest : GameTest
{
    private static readonly ProtoId<ReagentPrototype> FirstReagent = "SmokeRuntimeReactionFirst";
    private static readonly ProtoId<ReagentPrototype> SecondReagent = "SmokeRuntimeReactionSecond";

    [TestPrototypes]
    private const string Prototypes = """
- type: reagent
  id: SmokeRuntimeReactionFirst
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing

- type: reagent
  id: SmokeRuntimeReactionSecond
  name: reagent-name-nothing
  desc: reagent-desc-nothing
  physicalDesc: reagent-physical-desc-nothing
""";

    [Test]
    public async Task StartSmokeUsesRuntimeReagentCloneAndPreservesTileQuantityAndOrder()
    {
        var map = await Pair.CreateTestMap();
        var calls = new List<SmokeRuntimeTileReactionCall>();
        SmokeRuntimeTileReaction? firstReaction = null;
        SmokeRuntimeTileReaction? secondReaction = null;
        Reagent? firstRuntime = null;
        Reagent? secondRuntime = null;
        EntityUid smoke = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var smokeSystem = Server.System<SmokeSystem>();
                var reagents = Server.System<RMCReagentSystem>();
                firstRuntime = reagents.Index(FirstReagent);
                secondRuntime = reagents.Index(SecondReagent);
                firstReaction = new SmokeRuntimeTileReaction("first", calls);
                secondReaction = new SmokeRuntimeTileReaction("second", calls);
                firstRuntime.TileReactions.Add(firstReaction);
                secondRuntime.TileReactions.Add(secondReaction);

                var firstPrototype = SProtoMan.Index(FirstReagent);
                var secondPrototype = SProtoMan.Index(SecondReagent);
                Assert.Multiple(() =>
                {
                    Assert.That(firstRuntime, Is.Not.SameAs(firstPrototype));
                    Assert.That(secondRuntime, Is.Not.SameAs(secondPrototype));
                    Assert.That(firstPrototype.TileReactions, Is.Empty,
                        "the runtime-only reaction must not mutate the source prototype");
                    Assert.That(secondPrototype.TileReactions, Is.Empty,
                        "the runtime-only reaction must not mutate the source prototype");
                });

                smoke = SEntMan.SpawnEntity("Smoke", map.GridCoords);
                var solution = new Solution(new[]
                {
                    new ReagentQuantity(FirstReagent, FixedPoint2.New(7)),
                    new ReagentQuantity(SecondReagent, FixedPoint2.New(3)),
                });

                smokeSystem.StartSmoke(smoke, solution, duration: 10, spreadAmount: 0);

                Assert.That(calls, Has.Count.EqualTo(2));
                Assert.Multiple(() =>
                {
                    Assert.That(calls.Select(call => call.Label),
                        Is.EqualTo(new[] { "first", "second" }),
                        "tile reactions must retain solution declaration order");
                    Assert.That(calls.Select(call => call.Reagent),
                        Is.EqualTo(new[] { FirstReagent.Id, SecondReagent.Id }));
                    Assert.That(calls.Select(call => call.Quantity),
                        Is.EqualTo(new[] { FixedPoint2.New(7), FixedPoint2.New(3) }));
                    Assert.That(calls.All(call => call.Grid == map.Tile.GridUid), Is.True);
                    Assert.That(calls.All(call => call.Tile == map.Tile.GridIndices), Is.True,
                        "ReactionTile must receive the smoke entity's exact grid tile");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (firstRuntime != null && firstReaction != null)
                    firstRuntime.TileReactions.Remove(firstReaction);
                if (secondRuntime != null && secondReaction != null)
                    secondRuntime.TileReactions.Remove(secondReaction);
                if (SEntMan.EntityExists(smoke))
                    SEntMan.DeleteEntity(smoke);
            });
        }
    }
}

public readonly record struct SmokeRuntimeTileReactionCall(
    string Label,
    string Reagent,
    FixedPoint2 Quantity,
    EntityUid Grid,
    Vector2i Tile);

public sealed class SmokeRuntimeTileReaction(
    string label,
    ICollection<SmokeRuntimeTileReactionCall> calls) : ITileReaction
{
    public FixedPoint2 TileReact(
        TileRef tile,
        ReagentPrototype reagent,
        FixedPoint2 reactVolume,
        IEntityManager entityManager,
        List<ReagentData>? data = null)
    {
        calls.Add(new SmokeRuntimeTileReactionCall(
            label,
            reagent.ID,
            reactVolume,
            tile.GridUid,
            tile.GridIndices));
        return FixedPoint2.Zero;
    }
}
