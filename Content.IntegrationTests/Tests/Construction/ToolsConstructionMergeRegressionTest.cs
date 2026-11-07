using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Construction;
using Content.Shared._RMC14.Construction.Prototypes;
using Content.Shared.Construction;
using Content.Shared.Construction.Components;
using Content.Shared.Construction.EntitySystems;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Tools;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests.Construction;

[TestFixture]
public sealed class ToolsConstructionMergeRegressionTest : GameTest
{

    [Test]
    public async Task AnchorableSkipsSelfAndHonorsRmcTileFreeOverride()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var anchorable = SEntMan.System<AnchorableSystem>();
            var subject = SEntMan.SpawnEntity("Table", map.GridCoords);
            var blocker = SEntMan.SpawnEntity("Table", map.GridCoords);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(SComp<TransformComponent>(subject).Anchored, Is.True,
                    "Table must start anchored for the blocker setup to exercise the anchorability contract.");
                Assert.That(SComp<TransformComponent>(blocker).Anchored, Is.True,
                    "Table must start anchored for the blocker setup to exercise the anchorability contract.");
            }

            var physics = SComp<PhysicsComponent>(subject);
            Assert.That(anchorable.CanAnchorAt((subject, physics), map.GridCoords), Is.False,
                "The self exclusion must not hide another hard anchored blocker on the tile.");

            SEntMan.AddComponent<AnchorableTileFreeMergeProbeComponent>(subject);
            Assert.That(anchorable.CanAnchorAt((subject, physics), map.GridCoords), Is.True,
                "The fork RMCCheckTileFree override must be evaluated after the entity excludes itself.");
            var probe = SComp<AnchorableTileFreeMergeProbeComponent>(subject);
            using (Assert.EnterMultipleScope())
            {
                Assert.That(probe.Checks, Is.EqualTo(1));
                Assert.That(probe.SawSelf, Is.False,
                    "The subject must be skipped before the RMC override is raised for other blockers.");
            }
        });
    }

}

[RegisterComponent]
public sealed partial class AnchorableTileFreeMergeProbeComponent : Component
{
    public int Checks;
    public bool SawSelf;
}

public sealed class AnchorableTileFreeMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnchorableTileFreeMergeProbeComponent, RMCCheckTileFreeEvent>(OnCheckTileFree);
    }

    private static void OnCheckTileFree(
        Entity<AnchorableTileFreeMergeProbeComponent> ent,
        ref RMCCheckTileFreeEvent args)
    {
        ent.Comp.Checks++;
        ent.Comp.SawSelf |= args.AnchoredEntity == ent.Owner;
        args.IsTileFree = args.AnchoredEntity != ent.Owner;
    }
}
