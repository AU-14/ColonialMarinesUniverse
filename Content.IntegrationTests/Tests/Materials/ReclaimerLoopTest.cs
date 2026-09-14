using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.IntegrationTests.Utility;
using Content.Server.Materials;
using Content.Server.Power.EntitySystems;
using Content.Shared.Materials;
using Content.Shared.Power.Components;
using Content.Shared.Tiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Materials;

/// <summary>
/// Tests to prevent Recycler loops, where the product of one recycling can be recycled again.
/// </summary>
[TestOf(typeof(MaterialReclaimerSystem))]
[TestOf(typeof(MaterialReclaimerComponent))]
public sealed class ReclaimerLoopTest : InteractionTest
{
    // ProtoIDs we need
    private static readonly EntProtoId ApcId = "APCBasic";
    private static readonly EntProtoId FloorTileId = "CMTileItemSteel"; // CMU14

    private static readonly string[] Reclaimers = GameDataScrounger.EntitiesWithComponent("MaterialReclaimer");

    [SidedDependency(Side.Server)] private readonly SharedMaterialReclaimerSystem _materialReclaimerSystem = null!;

    [Test]
    [TestCaseSource(nameof(Reclaimers))]
    [TestOf(typeof(MaterialReclaimerSystem))]
    [TestOf(typeof(MaterialReclaimerComponent))]
    [Description("For every material that a reclaimer can spawn, make sure that it cannot get stuck in a loop of spawning then recycling.")]
    [TrackingIssue("https://github.com/space-wizards/space-station-14/issues/39691")]
    public async Task MaterialSpawnLoopTest(string reclaimerId)
    {
        // Spawn the reclaimer
        await SpawnTarget(reclaimerId, PlayerCoords);
        Assume.That(STarget, Is.Not.Null, "STarget was null, did the reclaimer spawn correctly?");

        var reclaimComp = Comp<MaterialReclaimerComponent>(Target);

        // Power the reclaimer
        var apc = await SpawnEntity(ApcId, SEntMan.GetCoordinates(TargetCoords));
        // CMU14: fork APCs spawn fully charged, which would power the reclaimer and make this a live
        // loop check. Upstream spawns them empty; drain the battery to keep the no-supply precondition.
        await Server.WaitPost(() =>
        {
            var battery = SEntMan.GetComponent<BatteryComponent>(apc);
            SEntMan.System<BatterySystem>().SetCharge((apc, battery), 0);
        });
        await RunTicks(1);
        // Set reclaimer to enabled
        await Server.WaitPost(() =>
        {
            _materialReclaimerSystem.SetReclaimerEnabled((EntityUid)STarget, true);
        });

        // Check that reclaimer enabled
        Assume.That(reclaimComp.Enabled, "The reclaimer did not get or stay enabled");

        // Put a floor tile down
        await InteractUsing(FloorTileId);

        // Reclaimer can't reclaim materials? Job's done.
        if (!reclaimComp.ReclaimMaterials)
            Assert.Ignore("Cannot reclaim materials");

        using (Assert.EnterMultipleScope())
        {
            // For each material, assert that it is not recyclable (and would thus cause a recycling loop)
            foreach (var material in ProtoMan.EnumeratePrototypes<MaterialPrototype>())
            {
                var matStack = material.StackEntity;
                Assert.That(
                    matStack,
                    Is.Not.Null,
                    $"The material, {material.ID}, did not have a stackentity associated with it. You may need to add a stackEntity to its Reagents/Materials yml file.");

                var matInHands = await PlaceInHands(matStack);
                var matInHandsUid = ToServer(matInHands);

                // CMU14: glass stacks double as placeable floor tiles. With the recycler unpowered the
                // interaction falls through to tile placement, which consumes the stack; that is tile
                // behavior, not a recycler loop.
                if (SEntMan.HasComponent<FloorTileComponent>(matInHandsUid))
                    continue;

                // Assert we're holding material
                Assert.That(
                    HandSys.GetActiveItem((SPlayer, Hands)),
                    Is.EqualTo(matInHandsUid),
                    $"The material, {matStack}, never got put in our hands.");

                await Interact();

                // Assert Hands not empty
                Assert.That(
                    HandSys.GetActiveItem((SPlayer, Hands)),
                    Is.Not.Null,
                    $"The material that should not have been reclaimed, {matStack}, is no longer in our hands. The reclaimer was {reclaimerId}");
            }
        }
    }
}
