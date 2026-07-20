using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.SubFloor;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Interaction;

[TestFixture]
[TestOf(typeof(SharedSubFloorHideSystem))]
public sealed class SubfloorAdminInteractionTest
{
    [Test]
    public async Task BypassMarkerAllowsCoveredInteraction()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap(tile: "FloorSteel");

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var normalUser = entMan.SpawnEntity(null, map.GridCoords);
            var bypassUser = entMan.SpawnEntity(null, map.GridCoords);
            entMan.EnsureComponent<BypassInteractionChecksComponent>(bypassUser);
            var cable = entMan.SpawnEntity("CableHV", map.GridCoords);
            var hidden = entMan.GetComponent<SubFloorHideComponent>(cable);

            Assert.That(hidden.IsUnderCover, Is.True);

            var normalAttempt = new GettingInteractedWithAttemptEvent(normalUser, cable);
            entMan.EventBus.RaiseLocalEvent(cable, ref normalAttempt);

            var bypassAttempt = new GettingInteractedWithAttemptEvent(bypassUser, cable);
            entMan.EventBus.RaiseLocalEvent(cable, ref bypassAttempt);

            Assert.Multiple(() =>
            {
                Assert.That(normalAttempt.Cancelled, Is.True);
                Assert.That(bypassAttempt.Cancelled, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }
}
