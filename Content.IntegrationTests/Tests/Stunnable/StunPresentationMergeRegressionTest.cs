using Content.IntegrationTests.Fixtures;
using Content.Server.Stunnable;
using Content.Shared.Alert;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Stunnable;

[TestFixture]
[TestOf(typeof(StatusEffectAlertSystem))]
[TestOf(typeof(ExaminableStatusEffectSystem))]
public sealed class StunPresentationMergeRegressionTest : GameTest
{
    [Test]
    public async Task SharedAlertAndExaminePresentationFollowTheLongestRemainingOwner()
    {
        EntityUid removeParalyze = default;
        EntityUid removeStun = default;
        EntityUid removeShortParalyze = default;
        EntityUid removeShortStun = default;
        TimeSpan start = default;

        await Server.WaitAssertion(() =>
        {
            var status = Server.System<StatusEffectsSystem>();
            var stun = Server.System<StunSystem>();
            start = Server.Timing.CurTime;

            removeParalyze = SSpawn("MobHuman");
            Assert.That(stun.TryStun(removeParalyze, TimeSpan.FromSeconds(5), refresh: true), Is.True);
            Assert.That(stun.TryParalyze(removeParalyze, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            AssertPresentation(removeParalyze, start + TimeSpan.FromSeconds(10));

            Assert.That(status.TryRemoveStatusEffect(removeParalyze, SharedStunSystem.ParalyzeId), Is.True);

            removeStun = SSpawn("MobHuman");
            Assert.That(stun.TryStun(removeStun, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.That(stun.TryParalyze(removeStun, TimeSpan.FromSeconds(5), refresh: true), Is.True);
            AssertPresentation(removeStun, start + TimeSpan.FromSeconds(10));

            Assert.That(status.TryRemoveStatusEffect(removeStun, SharedStunSystem.StunId), Is.True);

            removeShortParalyze = SSpawn("MobHuman");
            Assert.That(stun.TryStun(removeShortParalyze, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.That(stun.TryParalyze(removeShortParalyze, TimeSpan.FromSeconds(5), refresh: true), Is.True);
            AssertPresentation(removeShortParalyze, start + TimeSpan.FromSeconds(10));
            Assert.That(
                status.TryRemoveStatusEffect(removeShortParalyze, SharedStunSystem.ParalyzeId),
                Is.True);

            removeShortStun = SSpawn("MobHuman");
            Assert.That(stun.TryParalyze(removeShortStun, TimeSpan.FromSeconds(10), refresh: true), Is.True);
            Assert.That(stun.TryStun(removeShortStun, TimeSpan.FromSeconds(5), refresh: true), Is.True);
            AssertPresentation(removeShortStun, start + TimeSpan.FromSeconds(10));
            Assert.That(status.TryRemoveStatusEffect(removeShortStun, SharedStunSystem.StunId), Is.True);
        });

        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var status = Server.System<StatusEffectsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(status.HasStatusEffect(removeParalyze, SharedStunSystem.StunId), Is.True);
                Assert.That(status.HasStatusEffect(removeParalyze, SharedStunSystem.ParalyzeId), Is.False);
                Assert.That(status.HasStatusEffect(removeStun, SharedStunSystem.StunId), Is.False);
                Assert.That(status.HasStatusEffect(removeStun, SharedStunSystem.ParalyzeId), Is.True);
                Assert.That(status.HasStatusEffect(removeShortParalyze, SharedStunSystem.StunId), Is.True);
                Assert.That(status.HasStatusEffect(removeShortParalyze, SharedStunSystem.ParalyzeId), Is.False);
                Assert.That(status.HasStatusEffect(removeShortStun, SharedStunSystem.StunId), Is.False);
                Assert.That(status.HasStatusEffect(removeShortStun, SharedStunSystem.ParalyzeId), Is.True);
            });

            AssertPresentation(removeParalyze, start + TimeSpan.FromSeconds(5));
            AssertPresentation(removeStun, start + TimeSpan.FromSeconds(5));
            AssertPresentation(removeShortParalyze, start + TimeSpan.FromSeconds(10));
            AssertPresentation(removeShortStun, start + TimeSpan.FromSeconds(10));
        });
    }

    private void AssertPresentation(EntityUid target, TimeSpan expectedEnd)
    {
        var alerts = Server.System<AlertsSystem>();
        var alertKey = SProtoMan.Index<AlertPrototype>("Stun").AlertKey;
        Assert.That(alerts.TryGetAlertState(target, alertKey, out var alert), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(alert.Cooldown.HasValue, Is.True);
            Assert.That(alert.Cooldown!.Value.endTime, Is.EqualTo(expectedEnd));
            Assert.That(CountStunExamineLines(target), Is.EqualTo(1),
                "statuses with the same MessageId must produce one canonical examine line");
        });
    }

    private int CountStunExamineLines(EntityUid target)
    {
        var examined = new ExaminedEvent(new FormattedMessage(), target, target, true, false);
        SEntMan.EventBus.RaiseLocalEvent(target, examined);
        var expected = Loc.GetString(
            "status-effect-examine-stunned",
            ("target", Content.Shared.IdentityManagement.Identity.Entity(target, SEntMan)));
        return examined.GetTotalMessage().ToMarkup().Split(expected, StringSplitOptions.None).Length - 1;
    }
}
