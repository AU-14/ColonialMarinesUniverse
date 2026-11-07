using System.Linq;
using Content.Client.CMU14.Insurgency.Sapper;
using Content.Server.CMU14.Insurgency.Sapper;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Insurgency.Sapper;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     The ATM's memory of recent logins and the Sapper's siphon rig that leaks it.
/// </summary>
public sealed class SapperSiphonRigAtmTest : ColonyAtmTestBase
{
    private const string SiphonRig = "AU14SapperSiphonRig";

    /// <summary>Puts a new card in the target ATM, logs in with its PIN, takes the card back and walks away.</summary>
    private async Task<(int Account, int Pin)> LogInAndLeave()
    {
        var (_, pin, account) = await InsertNewCard(100);
        await Type(pin.ToString());
        Assert.That(AtmComp.PinAuthenticated, "Login failed");
        await Type("6", enter: false);              // 6) EXIT, card back in hand
        await CloseBui(ColonyAtmUi.Key);
        return (account, pin);
    }

    /// <summary>Makes the player a trained sapper holding a quick siphon rig.</summary>
    private async Task<SapperAtmHackingComponent> HoldRig(bool trained = true)
    {
        if (trained)
            await Server.WaitPost(() => SEntMan.EnsureComponent<SapperComponent>(SPlayer));

        var rig = await PlaceInHands(SiphonRig);
        var rigComp = Comp<SapperAtmHackingComponent>(rig);
        await Server.WaitPost(() => rigComp.AtmHackTime = 0.5f);
        return rigComp;
    }

    [Test]
    public async Task RigCollectsLoginsFromSeveralAtmsWithoutDuplicates()
    {
        // ATM one: Alice. ATM two: Alice again and Bob.
        await SpawnTarget(Atm);
        var firstAtm = Target!.Value;
        var (_, alicePin, alice) = await InsertNewCard(100);
        await Type(alicePin.ToString());
        await Type("6", enter: false);              // 6) EXIT, Alice's card back in hand
        await CloseBui(ColonyAtmUi.Key);

        var secondAtm = await SpawnTarget(Atm);
        await Interact();                            // Alice's card into the second ATM
        await Type(alicePin.ToString());
        await Type("6", enter: false);
        await CloseBui(ColonyAtmUi.Key);
        var (bob, bobPin) = await LogInAndLeave();

        var rig = await HoldRig();
        Target = firstAtm;
        await Interact();
        Target = secondAtm;
        await Interact();

        var captured = rig.CapturedAccounts.ToDictionary(a => a.AccountNumber, a => a.Pin);
        Assert.Multiple(() =>
        {
            Assert.That(rig.CapturedAccounts, Has.Count.EqualTo(2), "Alice was captured twice or someone was missed");
            Assert.That(captured.GetValueOrDefault(alice), Is.EqualTo(alicePin));
            Assert.That(captured.GetValueOrDefault(bob), Is.EqualTo(bobPin));
            Assert.That(SEntMan.HasComponent<SapperAtmHackedComponent>(ToServer(firstAtm)));
            Assert.That(SEntMan.HasComponent<SapperAtmHackedComponent>(ToServer(secondAtm)));
        });
    }

    [Test]
    public async Task UntrainedPersonCannotSiphon()
    {
        await SpawnTarget(Atm);
        var (account, _) = await LogInAndLeave();

        var rig = await HoldRig(trained: false);
        await Interact();

        Assert.Multiple(() =>
        {
            Assert.That(rig.CapturedAccounts, Is.Empty, "An untrained person leaked logins");
            Assert.That(AtmComp.RecentLogins.Select(l => l.AccountNumber), Does.Contain(account), "The ATM's logins were wiped");
            Assert.That(SEntMan.HasComponent<SapperAtmHackedComponent>(STarget), Is.False, "An untrained person hacked the ATM");
        });
    }

    /// <summary>
    ///     Once a siphoned ATM repairs itself it works as normal, but its screen keeps the skimmer
    ///     card-reader art so a sharp-eyed colonist can tell it was tampered with.
    /// </summary>
    [Test]
    public async Task RepairedAtmWorksButShowsTheSkimmer()
    {
        await SpawnTarget(Atm);
        var (_, firstPin, _) = await InsertNewCard(100);
        Assert.That(ClientAtmState().Tampered, Is.False, "An untouched ATM showed the skimmer");
        await Type(firstPin.ToString());
        await Type("6", enter: false);              // 6) EXIT, card back in hand
        await CloseBui(ColonyAtmUi.Key);

        await HoldRig();
        await Interact();
        Assert.That(SEntMan.HasComponent<SapperAtmHackedComponent>(STarget), "The ATM was not hacked");

        // Skip ahead to the end of the outage.
        await Server.WaitPost(() =>
            SEntMan.GetComponent<SapperAtmHackedComponent>(STarget!.Value).RecoverAt = STiming.CurTime + TimeSpan.FromSeconds(1));
        await RunSeconds(2);

        var (card, pin, _) = await InsertNewCard(100);
        await Type(pin.ToString());
        await Type("1", enter: false);              // 1) WITHDRAW
        await Type("40");
        await Enter();

        Assert.Multiple(() =>
        {
            Assert.That(ClientAtmState().Tampered, "The repaired ATM does not show the skimmer");
            Assert.That(Comp<IdCardComponent>(card).AccountBalance, Is.EqualTo(60), "The repaired ATM did not pay out");
        });
    }



}
