using System.Numerics;
using Content.Server.CMU14.Expeditions;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests._CMU14.Expeditions;

[TestFixture]
public sealed class CMUExpeditionHearingTest
{
    // hearing only looks up agents now instead of every entity in range (it runs on every
    // gunshot and door in every round), so make sure agents still hear and the range still holds
    [Test]
    public async Task AgentsHearADoorOnlyWithinRange()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid door = default, near = default, far = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var now = server.ResolveDependency<IGameTiming>().CurTime;
            door = entMan.SpawnEntity("CMAirlock", map.GridCoords);
            near = entMan.SpawnEntity("CMUExpeditionScavenger", map.GridCoords.Offset(new Vector2(4, 0)));
            far = entMan.SpawnEntity("CMUExpeditionScavenger", map.GridCoords.Offset(new Vector2(9, 0)));

            // keep them from thinking (and investigating the noise away) mid-test
            foreach (var uid in new[] { near, far })
                entMan.GetComponent<CMUExpeditionAgentComponent>(uid).NextThink = now + TimeSpan.FromMinutes(1);

            entMan.System<SharedDoorSystem>().StartOpening(door);
        });

        await pair.RunTicksSync(60);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.GetComponent<DoorComponent>(door).State, Is.EqualTo(DoorState.Open));
            Assert.That(entMan.GetComponent<CMUExpeditionAgentComponent>(near).HeardPoint, Is.Not.Null,
                "an agent 4 tiles from an opening door should hear it");
            Assert.That(entMan.GetComponent<CMUExpeditionAgentComponent>(far).HeardPoint, Is.Null,
                "door noise only carries 5 tiles");

            foreach (var uid in new[] { door, near, far })
                entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }
}
