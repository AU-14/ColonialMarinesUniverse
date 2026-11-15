#pragma warning disable RA0002 // The cycle copy is checked through a restricted field.

using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Weather;
using Content.Server.Weather;
using Content.Shared._RMC14.Weather;
using Content.Shared.StatusEffectNew;
using Content.Shared.Weather;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZLevelWeatherTest : GameTest
{
    private static readonly EntProtoId Rain = "RMCStrataStormVeryLight";

    [Test]
    public async Task GroundWeatherIsMirroredOntoFollowingFloors()
    {
        EntityUid source = default;
        EntityUid follower = default;
        MapId sourceId = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            source = maps.CreateMap(out sourceId);
            follower = maps.CreateMap();
            Server.System<CMUZLevelWeatherSystem>().FollowMap(follower, source);

            // Set only on the ground, the way an admin weather command does.
            Server.System<WeatherSystem>().TrySetWeather(sourceId, Rain, out _, TimeSpan.FromMinutes(10));
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(EndTime(follower), Is.Not.Null, "The floor must pick up the ground's weather.");
            Assert.That((EndTime(follower)!.Value - EndTime(source)!.Value).Duration(), Is.LessThanOrEqualTo(TimeSpan.FromSeconds(1)),
                "The floor's weather must end with the ground's.");

            Server.System<WeatherSystem>().TrySetWeather(sourceId, null, out _);
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            var now = Server.ResolveDependency<IGameTiming>().CurTime;
            Assert.That(EndTime(follower), Is.LessThanOrEqualTo(now + SharedWeatherSystem.ShutdownTime),
                "Clearing the ground's weather must fade it out on the floor too.");
        });
    }

    [Test]
    public async Task OnlyTheGroundKeepsAWeatherCycle()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var weather = Server.System<CMUZLevelWeatherSystem>();

            var bareGround = maps.CreateMap();
            var cycledFloor = maps.CreateMap();
            SEntMan.EnsureComponent<RMCWeatherCycleComponent>(cycledFloor).MinTimeBetweenEvents = TimeSpan.FromMinutes(7);
            weather.FollowMap(cycledFloor, bareGround);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<RMCWeatherCycleComponent>(cycledFloor), Is.False);
                Assert.That(SEntMan.GetComponent<RMCWeatherCycleComponent>(bareGround).MinTimeBetweenEvents,
                    Is.EqualTo(TimeSpan.FromMinutes(7)), "A ground without a cycle takes over the floor's.");
            });

            var ground = maps.CreateMap();
            SEntMan.EnsureComponent<RMCWeatherCycleComponent>(ground).MinTimeBetweenEvents = TimeSpan.FromMinutes(3);
            var floor = maps.CreateMap();
            SEntMan.EnsureComponent<RMCWeatherCycleComponent>(floor).MinTimeBetweenEvents = TimeSpan.FromMinutes(9);
            weather.FollowMap(floor, ground);
            weather.FollowMap(ground, ground);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<RMCWeatherCycleComponent>(floor), Is.False,
                    "A floor's own cycle would roll competing events for the whole network.");
                Assert.That(SEntMan.GetComponent<RMCWeatherCycleComponent>(ground).MinTimeBetweenEvents,
                    Is.EqualTo(TimeSpan.FromMinutes(3)), "The ground keeps its own cycle.");
                Assert.That(SEntMan.HasComponent<CMUZLevelWeatherComponent>(ground), Is.False);
            });

            // These cycles have no events to roll, so they can't be left running.
            foreach (var map in new[] { bareGround, cycledFloor, ground, floor })
                SEntMan.DeleteEntity(map);
        });
    }

    private TimeSpan? EndTime(EntityUid map)
    {
        var statusEffects = Server.System<StatusEffectsSystem>();
        if (!statusEffects.TryGetTime(map, Rain, out var time))
            return null;

        return time.EndEffectTime;
    }
}
