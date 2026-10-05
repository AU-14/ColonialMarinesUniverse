using System.Linq;
using Content.Client.CMU14.Insurgency.Sapper;
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
    public async Task AtmOnlyRemembersItsLatestLogins()
    {
        await SpawnTarget(Atm);

        var logins = new List<(int Account, int Pin)>();
        for (var i = 0; i < 12; i++)
            logins.Add(await LogInAndLeave());

        var remembered = AtmComp.RecentLogins.Select(l => l.AccountNumber).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(remembered, Has.Count.LessThan(logins.Count), "The ATM kept every login ever made");
            Assert.That(remembered, Does.Contain(logins[^1].Account), "The newest login was not kept");
            Assert.That(remembered, Does.Not.Contain(logins[0].Account), "The oldest login was kept over newer ones");
            Assert.That(remembered, Is.Unique);
            Assert.That(AtmComp.RecentLogins.Single(l => l.AccountNumber == logins[^1].Account).Pin, Is.EqualTo(logins[^1].Pin));
        });
    }

    [Test]
    public async Task SameCardAtTheSameAtmIsRememberedOnce()
    {
        await SpawnTarget(Atm);
        var (_, pin, account) = await InsertNewCard(100);
        await Type(pin.ToString());
        await CloseBui(ColonyAtmUi.Key);

        await Interact();                            // back to the ATM; the card is still in it
        await Type(pin.ToString());
        await CloseBui(ColonyAtmUi.Key);

        Assert.That(AtmComp.RecentLogins.Count(l => l.AccountNumber == account), Is.EqualTo(1));
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

    [Test]
    public async Task SiphoningAnUnusedAtmLeaksNothing()
    {
        await SpawnTarget(Atm);
        var rig = await HoldRig();
        await Interact();

        Assert.Multiple(() =>
        {
            Assert.That(rig.CapturedAccounts, Is.Empty);
            Assert.That(SEntMan.HasComponent<SapperAtmHackedComponent>(STarget), "The ATM was not hacked");
            Assert.That(SEntMan.HasComponent<ColonyAtmTamperedComponent>(STarget), "The ATM was not marked tampered");
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

    /// <summary>A captured rig is evidence: anyone holding it can read the stolen logins.</summary>
    [Test]
    public async Task AnyoneHoldingTheRigCanReadIt()
    {
        await SpawnTarget(Atm);
        var (account, pin) = await LogInAndLeave();
        var rig = await HoldRig();
        await Interact();
        Assert.That(rig.CapturedAccounts, Has.Count.EqualTo(1));

        // The sapper is caught; someone without the training picks the rig up and reads it.
        await Server.WaitPost(() => SEntMan.RemoveComponent<SapperComponent>(SPlayer));
        await UseInHand();
        Assert.That(IsUiOpen(SapperSiphonRigUiKey.Key), "The rig's data window did not open");
        await RunTicks(5);

        var window = GetWindow<SapperSiphonRigWindow>();
        var rows = window.DataRows.Children.SelectMany(r => r.Children).OfType<Label>()
            .Select(l => l.Text).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(rows, Does.Contain($"#{account}"));
            Assert.That(rows, Does.Contain(pin.ToString()));
        });
    }
}
