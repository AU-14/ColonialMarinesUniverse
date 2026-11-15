using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Ghost;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaSelfDestructGhostStandInTest : GameTest
{
    // observers from the lobby carry a hidden body dressed in their job. a pred SD next to them gibbed it and
    // dropped the gear on the floor
    [Test]
    public async Task SelfDestructLeavesGhostStandInsAlone()
    {
        var map = await Pair.CreateTestMap();
        EntityUid pred = default;
        EntityUid standIn = default;

        await Server.WaitAssertion(() =>
        {
            pred = SSpawnAtPosition("CMUMobYautja", map.GridCoords);
            standIn = SSpawnAtPosition("AppearanceCMUHuman", map.GridCoords.Offset(new System.Numerics.Vector2(1, 0)));
            SEntMan.EnsureComponent<CMUGhostAppearanceStandInComponent>(standIn);

            Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(pred, "gloves", out var worn), Is.True);
            var bracer = worn!.Value;
            Assert.That(Server.System<YautjaSelfDestructSystem>().TryArmSelfDestruct(
                (bracer, SEntMan.GetComponent<YautjaBracerComponent>(bracer)), pred, TimeSpan.Zero), Is.True);
        });

        await Pair.RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(pred), Is.True, "the self-destruct never went off");
            Assert.That(SEntMan.Deleted(standIn), Is.False, "a ghost's stand-in got gibbed");
        });
    }
}
