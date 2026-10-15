#pragma warning disable RA0002 // Exercise direct field changes and pause boundaries in the update loops.

using Content.IntegrationTests.Fixtures;
using Content.Shared.IgnitionSource.Components;
using Content.Shared.IgnitionSource.EntitySystems;
using Content.Shared.Metabolism;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Smoking;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class IdleUpdateRegressionTest : GameTest
{
    [Test]
    public async Task ExpiredMatchWaitsForUnpauseAndDirectStateChangesAreObserved()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity("Matchstick", map.GridCoords);
            var match = SEntMan.GetComponent<MatchstickComponent>(uid);
            var system = Server.System<MatchstickSystem>();
            var metadata = Server.System<MetaDataSystem>();
            match.TimeMatchWillBurnOut = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
            system.Update(0f);
            Assert.That(match.State, Is.EqualTo(SmokableState.Unlit));

            match.State = SmokableState.Lit;
            metadata.SetEntityPaused(uid, true);
            system.Update(0f);
            Assert.That(match.State, Is.EqualTo(SmokableState.Lit));
            metadata.SetEntityPaused(uid, false);
            system.Update(0f);
            Assert.That(match.State, Is.EqualTo(SmokableState.Burnt));
        });
    }

    [Test]
    public async Task RateChangingBatteryUpdatesOnlyWhenUnpaused()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity(null, map.GridCoords);
            var battery = SEntMan.AddComponent<BatteryComponent>(uid);
            var system = Server.System<SharedBatterySystem>();
            var metadata = Server.System<MetaDataSystem>();
            battery.MaxCharge = 100;
            battery.LastCharge = 100;
            battery.ChargeRate = 0;
            battery.State = BatteryState.Neither;
            system.Update(0f);
            Assert.That(battery.State, Is.EqualTo(BatteryState.Neither));

            battery.ChargeRate = 1;
            metadata.SetEntityPaused(uid, true);
            system.Update(0f);
            Assert.That(battery.State, Is.EqualTo(BatteryState.Neither));
            metadata.SetEntityPaused(uid, false);
            system.Update(0f);
            Assert.That(battery.State, Is.EqualTo(BatteryState.Full));
        });
    }

    [Test]
    public async Task DueMetabolizerKeepsItsDeadlineWhilePaused()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var uid = SEntMan.SpawnEntity(null, map.GridCoords);
            var metabolizer = SEntMan.AddComponent<MetabolizerComponent>(uid);
            metabolizer.Stages.Clear();
            metabolizer.NextUpdate = SGameTiming.CurTime;
            var deadline = metabolizer.NextUpdate;
            var system = Server.System<MetabolizerSystem>();
            var metadata = Server.System<MetaDataSystem>();
            metadata.SetEntityPaused(uid, true);
            system.Update(0f);
            Assert.That(metabolizer.NextUpdate, Is.EqualTo(deadline));
            metadata.SetEntityPaused(uid, false);
            system.Update(0f);
            Assert.That(metabolizer.NextUpdate, Is.EqualTo(deadline + metabolizer.AdjustedUpdateInterval));
            system.Update(0f);
            Assert.That(metabolizer.NextUpdate, Is.EqualTo(deadline + metabolizer.AdjustedUpdateInterval),
                "Future work must remain deferred even when several systems update in the same simulation tick.");
        });
    }
}
