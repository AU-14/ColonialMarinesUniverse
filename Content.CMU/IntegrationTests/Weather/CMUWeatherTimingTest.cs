#pragma warning disable RA0002 // Exercise restored timestamps that the normal status API cannot construct.

using Content.IntegrationTests.Fixtures;
using Content.Server.Weather;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Weather;

[TestFixture]
public sealed class CMUWeatherTimingTest : GameTest
{
    [Test]
    public async Task WeatherStrengthHandlesExtremeAndDelayedEffectTimes()
    {
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var weather = Server.System<WeatherSystem>();
            // Restored effects can carry extreme timestamps. Sample the actual weather API,
            // including both indefinite effects and a finite duration that overflows TimeSpan.
            var status = new StatusEffectComponent { StartEffectTime = TimeSpan.MinValue };
            SEntMan.AddComponent(uid, status);
            Assert.That(weather.GetWeatherPercent((uid, status)), Is.EqualTo(1f));
            status.EndEffectTime = TimeSpan.MaxValue;
            Assert.That(weather.GetWeatherPercent((uid, status)), Is.EqualTo(1f));

            var now = SGameTiming.CurTime;
            status.StartEffectTime = now + TimeSpan.FromSeconds(1);
            status.EndEffectTime = null;
            Assert.That(weather.GetWeatherPercent((uid, status)), Is.Zero);

            status.StartEffectTime = now - TimeSpan.FromSeconds(7.5);
            Assert.That(weather.GetWeatherPercent((uid, status)), Is.EqualTo(0.5f).Within(0.001f));
            status.StartEffectTime = TimeSpan.MinValue;
            status.EndEffectTime = now + TimeSpan.FromSeconds(7.5);
            Assert.That(weather.GetWeatherPercent((uid, status)), Is.EqualTo(0.5f).Within(0.001f));
            status.EndEffectTime = now - TimeSpan.FromSeconds(1);
            Assert.That(weather.GetWeatherPercent((uid, status)), Is.Zero);
            SEntMan.DeleteEntity(uid);
        });
    }
}
