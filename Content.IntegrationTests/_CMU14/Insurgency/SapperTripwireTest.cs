using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.CMU14.Threats.Mobs.CLF;
using Content.Shared.StepTrigger.Systems;

namespace Content.IntegrationTests.CMU14.Insurgency;

[TestFixture]
public sealed class SapperTripwireTest
{
    [TestCase("AU14SapperTripwireSegment")]
    [TestCase("AU14SapperTripwireEnd")]
    public async Task TripwireIgnoresDraggedMachinesAndFriendlies(string segmentPrototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var device = entities.SpawnEntity("AU14SapperTripwireTrap", map.GridCoords);
            var segment = entities.SpawnEntity(segmentPrototype, map.GridCoords);
            var machine = entities.SpawnEntity("SeedExtractor", map.GridCoords);
            var human = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            try
            {
                entities.GetComponent<SapperTripwireSegmentComponent>(segment).Device = device;
                var trap = entities.GetComponent<SapperTrapComponent>(device);
                trap.Armed = true;
                server.System<SharedTransformSystem>().Unanchor(machine);
                Assert.That(entities.GetComponent<TransformComponent>(machine).Anchored, Is.False);
                Assert.That(CanTrip(machine), Is.False, "A dragged seed extractor must not detonate the wire.");
                Assert.That(CanTrip(human), Is.True, "An enemy crossing still triggers the armed wire.");

                entities.EnsureComponent<CLFMemberComponent>(human);
                Assert.That(CanTrip(human), Is.False, "The sapper's faction remains exempt.");
                entities.RemoveComponent<CLFMemberComponent>(human);
                trap.Armed = false;
                Assert.That(CanTrip(human), Is.False, "Disarming must disable the wire.");
            }
            finally
            {
                entities.DeleteEntity(segment);
                entities.DeleteEntity(device);
                entities.DeleteEntity(machine);
                entities.DeleteEntity(human);
            }

            bool CanTrip(EntityUid tripper)
            {
                var attempt = new StepTriggerAttemptEvent { Source = segment, Tripper = tripper };
                entities.EventBus.RaiseLocalEvent(segment, ref attempt);
                return attempt.Continue && !attempt.Cancelled;
            }
        });
        await pair.CleanReturnAsync();
    }
}
