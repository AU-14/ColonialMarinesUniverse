#pragma warning disable RA0002 // Drive recipient filtering and component-removal publication through actual PVS ticks.
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Medical.Injuries.Pain.Penalties;
using Robust.Shared;
using Robust.Shared.Enums;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMUPvsStateReplicationTest : GameTest
{
    [Test]
    public async Task OwnerOnlyChangesAndRemovalWithoutVisibleChangesReachTheCorrectClient()
    {
        var map = await Pair.CreateTestMap();
        EntityUid viewer = default;
        EntityUid foreign = default;
        await Server.WaitPost(() =>
        {
            viewer = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EnsureComponent<EyeComponent>(viewer);
            SEntMan.AddComponent<CMUAimAccuracyComponent>(viewer);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, viewer);
            Server.PlayerMan.SetStatus(ServerSession!, SessionStatus.InGame);
            Server.CfgMan.SetCVar(CVars.NetPVS, true);
            foreign = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<CMUAimAccuracyComponent>(foreign);
            SEntMan.AddComponent<AppearanceComponent>(foreign);
        });
        await Pair.RunTicksSync(12);
        var clientViewer = Pair.ToClientUid(viewer);
        var clientForeign = Pair.ToClientUid(foreign);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.HasComponent<AppearanceComponent>(clientForeign), Is.True);
            Assert.That(CEntMan.HasComponent<CMUAimAccuracyComponent>(clientForeign), Is.False,
                "The other entity's owner-only state must remain private.");
        });

        await Server.WaitPost(() =>
        {
            var ownerState = SEntMan.GetComponent<CMUAimAccuracyComponent>(viewer);
            ownerState.SpreadMultiplier = 3;
            SEntMan.Dirty(viewer, ownerState);
            var hiddenState = SEntMan.GetComponent<CMUAimAccuracyComponent>(foreign);
            hiddenState.SpreadMultiplier = 7;
            SEntMan.Dirty(foreign, hiddenState);
        });
        await Pair.RunTicksSync(12);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.GetComponent<CMUAimAccuracyComponent>(clientViewer).SpreadMultiplier, Is.EqualTo(3));
            Assert.That(CEntMan.HasComponent<CMUAimAccuracyComponent>(clientForeign), Is.False);
        });

        await Server.WaitPost(() => SEntMan.RemoveComponent<AppearanceComponent>(foreign));
        await Pair.RunTicksSync(12);
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.HasComponent<AppearanceComponent>(clientForeign), Is.False,
                "A removal list must be sent even when there are no visible component changes.");
            Assert.That(CEntMan.HasComponent<CMUAimAccuracyComponent>(clientForeign), Is.False);
            Assert.That(CEntMan.GetComponent<CMUAimAccuracyComponent>(clientViewer).SpreadMultiplier, Is.EqualTo(3));
        });
    }
}
