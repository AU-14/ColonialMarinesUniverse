using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Buckle;
using Content.Shared.Foldable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class MedevacStretcherFoldTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task BlockedStretcherCanBeRecoveredAfterUnbuckling()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var stretcher = SEntMan.SpawnEntity("RMCMedevacStretcher", map.GridCoords);
            var patient = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var buckle = Server.System<SharedBuckleSystem>();
            var fold = Server.System<FoldableSystem>();
            var foldable = SEntMan.GetComponent<FoldableComponent>(stretcher);
            Assert.That(buckle.TryBuckle(patient, patient, stretcher, popup: false), Is.True);

            SEntMan.SpawnEntity("CMBarricadeMetal", map.GridCoords);
            Assert.That(fold.TrySetFolded(stretcher, foldable, true), Is.False,
                "A patient must still prevent folding, even on an obstructed tile.");
            buckle.Unbuckle(patient, patient);
            Assert.That(fold.TrySetFolded(stretcher, foldable, true), Is.True,
                "An empty stretcher must be recoverable from a barricade.");
            Assert.That(fold.TrySetFolded(stretcher, foldable, false), Is.False,
                "Recovering a stretcher must not allow deploying it into a barricade.");

            Server.System<SharedTransformSystem>().SetCoordinates(stretcher, map.GridCoords.Offset(new Vector2(3, 0)));
            Assert.That(fold.TrySetFolded(stretcher, foldable, false), Is.True,
                "The recovered stretcher must be usable on a clear tile.");
        });
    }
}
