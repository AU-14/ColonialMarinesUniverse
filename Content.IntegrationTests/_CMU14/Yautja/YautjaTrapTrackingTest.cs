using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Yautja;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Eye;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Yautja;

// CMU14 Test: private trap visibility and five-minute hunting-map prey telemetry.
[TestFixture]
public sealed class YautjaTrapTrackingTest
{
    [Test]
    public async Task ArmedTrapIsYautjaOnlyAndTriggeredPreyIsTrackedForFiveMinutes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid trap = default;
        EntityUid prey = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            entities.EnsureComponent<TacticalMapComponent>(map.Grid);
            hunter = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            trap = entities.SpawnEntity("CMUYautjaHuntingTrap", map.GridCoords);
            prey = entities.SpawnEntity("CMMobHuman", map.GridCoords);
        });
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var trapSystem = entities.System<YautjaTrapSystem>();
            var eye = entities.GetComponent<EyeComponent>(hunter);
            var user = entities.GetComponent<TacticalMapUserComponent>(hunter);

            Assert.Multiple(() =>
            {
                Assert.That((eye.VisibilityMask & (int) VisibilityFlags.Yautja) != 0, Is.True);
                Assert.That(user.Yautja, Is.True);
                Assert.That(user.Marines || user.Xenos || user.Opfor || user.Govfor || user.Clf, Is.False);
            });

            Assert.That(trapSystem.TryArmTrap((trap, entities.GetComponent<YautjaTrapComponent>(trap)), hunter), Is.True);
            Assert.That(entities.GetComponent<VisibilityComponent>(trap).Layer,
                Is.EqualTo((ushort) VisibilityFlags.Yautja));

            var beforeTrigger = server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(trapSystem.TryTriggerTrap((trap, entities.GetComponent<YautjaTrapComponent>(trap)), prey), Is.True);
            var tracking = entities.GetComponent<YautjaTrackedPreyComponent>(prey);
            var yautjaBlips = entities.GetComponent<TacticalMapComponent>(map.Grid).YautjaBlips;
            Assert.That(tracking.ExpiresAt - beforeTrigger, Is.EqualTo(TimeSpan.FromMinutes(5)).Within(TimeSpan.FromSeconds(1)));
            Assert.That(yautjaBlips.ContainsKey(prey.Id), Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
