using Content.Shared._RMC14.Sentry;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobDefenseTest
{
    /// <summary>An Early Warning Joe's Unwind lockout holds a FOB gun off even though the network keeps it powered.</summary>
    [Test]
    public async Task EWJoeSentryLockoutHoldsFobGunsOff()
    {
        await SetupGuns(ammo: 3);
        await Server.WaitAssertion(() =>
        {
            var timing = Server.ResolveDependency<IGameTiming>();
            SEntMan.EnsureComponent<EWJoeSentryLockoutComponent>(_gun).Until = timing.CurTime + TimeSpan.FromSeconds(30);
            Assert.That(Reserve.TryActivate(_network), Is.True);
        });
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo, Is.EqualTo(3), "a locked-out gun does not fire");
            Assert.That(SEntMan.GetComponent<SentryComponent>(_gun).Mode, Is.EqualTo(SentryMode.Off),
                "the FOB loop does not switch it back on");
        });
    }
}
