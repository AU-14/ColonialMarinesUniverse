using Content.IntegrationTests.Fixtures;
using Content.Server.Shuttles.Components;
using Content.Shared._RMC14.CCVar;
using Content.Shared.Access.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class ThirdPartyDropshipInitialDelayTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ThirdPartyConsoleIgnoresPreFlightLockout()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipInitialDelayMinutes, 15f);
            SEntMan.EnsureComponent<ShuttleComponent>(map.Grid);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            var thirdParty = SEntMan.SpawnEntity("CMComputerDropshipNavigationThirdParty", map.GridCoords);
            var thirdPartyAttempt = new ActivatableUIOpenAttemptEvent(user, false);
            SEntMan.EventBus.RaiseLocalEvent(thirdParty, thirdPartyAttempt);
            Assert.That(thirdPartyAttempt.Cancelled, Is.False,
                "a third-party ship should launch before the 15 minute fueling lockout ends");

            // control case: drop the access gate so only the lockout can cancel the govfor console
            var govfor = SEntMan.SpawnEntity("CMComputerDropshipNavigationGovfor", map.GridCoords);
            SEntMan.RemoveComponent<AccessReaderComponent>(govfor);
            var govforAttempt = new ActivatableUIOpenAttemptEvent(user, false);
            SEntMan.EventBus.RaiseLocalEvent(govfor, govforAttempt);
            Assert.That(govforAttempt.Cancelled, Is.True,
                "govfor dropships still have to wait out the fueling lockout");
        });
    }
}
