#pragma warning disable RA0002 // Inspects restricted PainShockComponent state.

using Content.Shared.CMU14.Chemistry;
using Content.Shared.CMU14.Hypospray;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical;

[TestFixture]
public sealed class CMUHyposprayTest
{
    /// <summary>
    /// Repeated injections keep adding pain but never push the patient past the configured tier.
    /// </summary>
    [Test]
    public async Task InjectionPainNeverExceedsMaxPainTier()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;
        EntityUid injector = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            injector = entMan.SpawnEntity("CMUHypospray", MapCoordinates.Nullspace);
        });

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            for (var i = 0; i < 3; i++)
            {
                var ev = new CMUBeforeHyposprayInjectEvent(human, injector);
                entMan.EventBus.RaiseLocalEvent(human, ref ev);
            }

            var pain = entMan.GetComponent<PainShockComponent>(human);
            Assert.That(pain.Pain, Is.GreaterThan(FixedPoint2.Zero), "Injections must add pain.");
            Assert.That(
                pain.Pain,
                Is.LessThan(PainTierThresholds.ModerateThreshold),
                "Spamming injections must not push pain past the Mild ceiling.");
            Assert.That(pain.InShock, Is.False);
            entMan.DeleteEntity(human);
            entMan.DeleteEntity(injector);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A deployed injector beyond the tether range snaps back into its station.
    /// </summary>
    [Test]
    public async Task InjectorSnapsBackBeyondTetherRange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid station = default;
        EntityUid injector = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            station = entMan.SpawnEntity("CMUHyposprayStation", MapCoordinates.Nullspace);
            var stationComp = entMan.GetComponent<CMUHyposprayStationComponent>(station);
            injector = stationComp.Injector!.Value;

            var container = entMan.System<SharedContainerSystem>();
            Assert.That(container.TryGetContainer(station, stationComp.ContainerId, out var held), Is.True);
            container.Remove(injector, held!);
            entMan.AddComponent<CMUHyposprayDeployedComponent>(injector);
            entMan.System<SharedTransformSystem>().SetCoordinates(
                injector,
                new EntityCoordinates(station, 10f, 0f));
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stationComp = entMan.GetComponent<CMUHyposprayStationComponent>(station);
            var container = entMan.System<SharedContainerSystem>();

            Assert.That(stationComp.Injector, Is.EqualTo(injector), "The injector must stay bound to its station.");
            Assert.That(
                container.TryGetContainer(station, stationComp.ContainerId, out var held) && held.Contains(injector),
                Is.True,
                "The injector must be back inside the station container.");
            Assert.That(
                entMan.HasComponent<CMUHyposprayDeployedComponent>(injector),
                Is.False,
                "A returned injector must lose its deployed state.");
            entMan.DeleteEntity(station);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// A station whose container already holds its injector binds it instead of spawning a second one.
    /// </summary>
    [Test]
    public async Task StationWithSavedInjectorDoesNotDoubleSpawn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid station = default;
        EntityUid injector = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            station = entMan.SpawnEntity("CMUHyposprayStation", MapCoordinates.Nullspace);
            injector = entMan.GetComponent<CMUHyposprayStationComponent>(station).Injector!.Value;
        });

        // Saved maps re-run MapInit on load with the injector already in the container.
        await server.WaitPost(() => server.EntMan.EventBus.RaiseLocalEvent(station, new MapInitEvent()));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var container = entMan.System<SharedContainerSystem>();
            var stationComp = entMan.GetComponent<CMUHyposprayStationComponent>(station);

            Assert.That(stationComp.Injector, Is.EqualTo(injector), "The bound injector must survive a reload.");
            Assert.That(
                container.TryGetContainer(station, stationComp.ContainerId, out var held) && held.ContainedEntities.Count == 1,
                Is.True,
                "Reloading a saved station must not spawn a second injector.");
            entMan.DeleteEntity(station);
        });

        await pair.CleanReturnAsync();
    }
}
