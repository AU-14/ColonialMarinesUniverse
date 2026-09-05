using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Stun;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Speech;

[TestFixture]
[TestOf(typeof(StutteringSystem))]
public sealed class StutteringMergeRegressionTest : GameTest
{
    [Test]
    public async Task AddRefreshReduceAndExpiryUseTheStutterStatusEntity()
    {
        EntityUid target = default;
        await Server.WaitAssertion(() =>
        {
            var timing = Server.ResolveDependency<IGameTiming>();
            var stuttering = Server.System<StutteringSystem>();
            var status = Server.System<StatusEffectsSystem>();
            target = SEntMan.Spawn("CMMobHuman", MapCoordinates.Nullspace);
            var now = timing.CurTime;

            stuttering.DoStutter(target, TimeSpan.FromSeconds(2), refresh: false);
            Assert.That(status.TryGetTime(target, StutteringSystem.StutterEffect, out var first), Is.True);
            Assert.That(first.EndEffectTime, Is.EqualTo(now + TimeSpan.FromSeconds(2)));

            stuttering.DoStutter(target, TimeSpan.FromSeconds(1), refresh: false);
            Assert.That(status.TryGetTime(target, StutteringSystem.StutterEffect, out var added), Is.True);
            Assert.That(added.EffectEnt, Is.EqualTo(first.EffectEnt));
            Assert.That(added.EndEffectTime, Is.EqualTo(now + TimeSpan.FromSeconds(3)),
                "refresh=false adds time to the existing status entity");

            stuttering.DoStutter(target, TimeSpan.FromSeconds(5), refresh: true);
            Assert.That(status.TryGetTime(target, StutteringSystem.StutterEffect, out var refreshed), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(refreshed.EffectEnt, Is.EqualTo(first.EffectEnt));
                Assert.That(refreshed.EndEffectTime, Is.EqualTo(now + TimeSpan.FromSeconds(5)),
                    "refresh=true extends to the requested remaining duration instead of adding it");
                Assert.That(status.HasEffectComp<StutteringAccentComponent>(target), Is.True);
                Assert.That(status.HasStatusEffect(target, "StatusEffectSlurred"), Is.False,
                    "stuttering must use StatusEffectStutter, not the slurred-speech successor");
            });

            stuttering.DoRemoveStutterTime(target, 2);
            Assert.That(status.TryGetTime(target, StutteringSystem.StutterEffect, out var reduced), Is.True);
            Assert.That(reduced.EndEffectTime, Is.EqualTo(now + TimeSpan.FromSeconds(3)));
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(3.2f));

        await Server.WaitAssertion(() =>
        {
            var status = Server.System<StatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(target, StutteringSystem.StutterEffect), Is.False);
                Assert.That(status.HasEffectComp<StutteringAccentComponent>(target), Is.False);
            });
            SEntMan.DeleteEntity(target);
        });
    }

    [Test]
    public async Task RmcDazeStutterUsesTheNewStutterEffect()
    {
        await Server.WaitAssertion(() =>
        {
            var dazed = Server.System<RMCDazedSystem>();
            var status = Server.System<StatusEffectsSystem>();
            var target = SEntMan.Spawn("CMMobHuman", MapCoordinates.Nullspace);

            Assert.That(dazed.TryDaze(target, TimeSpan.FromSeconds(2), stutter: true), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(target, RMCDazedSystem.StatusEffectDazed), Is.True);
                Assert.That(status.HasStatusEffect(target, StutteringSystem.StutterEffect), Is.True);
                Assert.That(status.HasEffectComp<StutteringAccentComponent>(target), Is.True);
                Assert.That(status.HasStatusEffect(target, "StatusEffectSlurred"), Is.False);
            });

            SEntMan.DeleteEntity(target);
        });
    }
}
