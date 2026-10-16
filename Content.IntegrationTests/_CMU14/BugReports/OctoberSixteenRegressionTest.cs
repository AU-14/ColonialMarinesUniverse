using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Interaction;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class OctoberSixteenRegressionTest : GameTest
{
    [Test]
    public async Task DefibrillatorsDoNotFitInTheMedicalPouch()
    {
        await Server.WaitAssertion(() =>
        {
            var pouch = SEntMan.SpawnEntity("RMCPouchMedical", MapCoordinates.Nullspace);
            var defib = SEntMan.SpawnEntity("AU14CMDefibrillator", MapCoordinates.Nullspace);
            var storage = SEntMan.System<SharedStorageSystem>();

            Assert.That(storage.CanInsert(pouch, defib, out _, ignoreLocation: true), Is.False);

            SEntMan.DeleteEntity(defib);
            SEntMan.DeleteEntity(pouch);
        });
    }

    // standing on a coat rack's tile blocked clicks on anything past it, like the light fixture
    [Test]
    public async Task CoatRackDoesNotBlockInteractionsPastIt()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var rack = SEntMan.SpawnEntity("RMCCoatRack", map.GridCoords);
            var origin = new MapCoordinates(map.MapCoords.Position - new Vector2(.3f, 0), map.MapId);
            var past = new MapCoordinates(map.MapCoords.Position + new Vector2(1f, 0), map.MapId);

            Assert.That(SEntMan.System<SharedInteractionSystem>().InRangeUnobstructed(origin, past), Is.True);

            SEntMan.DeleteEntity(rack);
        });
    }
}
