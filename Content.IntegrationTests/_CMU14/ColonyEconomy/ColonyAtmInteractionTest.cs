using System.Linq;
using Content.Client.CMU14.ColonyEconomy;
using Content.Client.CMU14.Insurgency.Sapper;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     End-to-end ATM tests: a connected player pushes a card into the reader and presses the real
///     keypad buttons in the client window, and the server-side balances, cash and ATM state are checked.
/// </summary>
public sealed class ColonyAtmInteractionTest : ColonyAtmTestBase
{
    private const string SiphonRig = "AU14SapperSiphonRig";

    [Test]
    public async Task WithdrawWithCorrectPin()
    {
        await SpawnTarget(Atm);
        var (card, pin, _) = await InsertNewCard(500);

        await Type(pin.ToString());
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.MainMenu));

        var tax = SEntMan.System<AdminConsoleSystem>().GetIncomeTax();
        var expectedCash = 100 - (int) Math.Floor(100 * tax);

        await Type("1", enter: false);              // 1) WITHDRAW
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Withdraw));
        await Type("100");
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.WithdrawConfirm));
        await Enter();

        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Result));
            Assert.That(Comp<IdCardComponent>(card).AccountBalance, Is.EqualTo(400));
            Assert.That(CashInTray(), Is.EqualTo(expectedCash), "The cash did not wait in the tray");
            Assert.That(CashOnFloor(), Is.Zero, "The cash was dropped instead of waiting in the tray");
            // The cash slot shows a stack as thick as what was paid out.
            Assert.That(ClientAtmState().CashAmount, Is.EqualTo(expectedCash), "The screen was not told how much came out");
            Assert.That(ClientAtmState().CashWaiting, Is.EqualTo(expectedCash), "The screen was not told the cash is waiting");
        });
    }

    /// <summary>Clicking the bills in the tray puts the cash in hand.</summary>
    [Test]
    public async Task ClickingTheCashTakesItInHand()
    {
        await SpawnTarget(Atm);
        var (_, pin, _) = await InsertNewCard(500);
        await Type(pin.ToString());

        var tax = SEntMan.System<AdminConsoleSystem>().GetIncomeTax();
        var expectedCash = 100 - (int) Math.Floor(100 * tax);

        await Type("1", enter: false);              // 1) WITHDRAW
        await Type("100");
        await Enter();
        Assert.That(GetWindow<ColonyAtmWindow>().BtnCash.Visible, "The bills coming out cannot be clicked");

        await ClickControl<ColonyAtmWindow>(nameof(ColonyAtmWindow.BtnCash));
        await RunTicks(5);

        Assert.Multiple(() =>
        {
            Assert.That(CashInHand(), Is.EqualTo(expectedCash), "The cash did not go into the hand");
            Assert.That(CashOnFloor(), Is.Zero, "Cash was dropped on the floor");
            Assert.That(CashInTray(), Is.Zero, "Cash was left in the tray");
            Assert.That(GetWindow<ColonyAtmWindow>().BtnCash.Visible, Is.False, "The bills can be taken twice");
        });
    }

    /// <summary>
    ///     Cash nobody takes is drawn back into the machine and paid back into the account it came
    ///     out of. Until then no more cash comes out.
    /// </summary>
    [Test]
    public async Task CashLeftInTheTrayGoesBackToTheAccount()
    {
        await SpawnTarget(Atm);
        var (card, pin, _) = await InsertNewCard(500);
        await Type(pin.ToString());

        var tax = SEntMan.System<AdminConsoleSystem>().GetIncomeTax();
        var expectedCash = 100 - (int) Math.Floor(100 * tax);

        await Type("1", enter: false);              // 1) WITHDRAW
        await Type("100");
        await Enter();
        Assert.That(CashInTray(), Is.EqualTo(expectedCash));

        await Enter();                               // back to the menu
        await Type("1", enter: false);              // 1) WITHDRAW again
        await Type("50");
        Assert.That(AtmComp.StatusMessage, Does.Contain("take your cash"), "More cash came out over cash still waiting");

        await RunSeconds(11);
        Assert.Multiple(() =>
        {
            Assert.That(CashInTray(), Is.Zero, "The cash was never drawn back in");
            Assert.That(CashOnFloor(), Is.Zero);
            Assert.That(Comp<IdCardComponent>(card).AccountBalance, Is.EqualTo(400 + expectedCash),
                "The cash drawn back in was not paid back into the account");
            Assert.That(ClientAtmState().CashWaiting, Is.Zero);
        });
    }

    /// <summary>A card left signed in signs itself out when nobody touches the machine.</summary>
    [Test]
    public async Task IdleSignedInCardSignsOut()
    {
        await SpawnTarget(Atm);
        var (_, pin, _) = await InsertNewCard(100);
        await Type(pin.ToString());
        Assert.That(AtmComp.PinAuthenticated);

        await RunSeconds(5);
        await Type("5", enter: false);              // 5) HISTORY: pressing a key keeps it signed in
        await RunSeconds(7);
        Assert.That(AtmComp.PinAuthenticated, "The card signed out while it was being used");

        await RunSeconds(4);
        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.PinAuthenticated, Is.False, "The idle card stayed signed in");
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.PinEntry));
            Assert.That(CardInAtm(), Is.Not.Null, "Signing out took the card out of the machine");
        });
    }

    /// <summary>
    ///     A stolen card works for whoever holds it, as long as they know its PIN.
    /// </summary>
    [Test]
    public async Task StolenCardWorksWithItsPin()
    {
        await SpawnTarget(Atm);
        var victim = await SpawnEntity("InteractionTestMob", SEntMan.GetCoordinates(TargetCoords));
        var (card, pin, _) = await InsertNewCard(300, owner: victim);

        await Type(pin.ToString());
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.MainMenu));

        await Type("1", enter: false);              // 1) WITHDRAW
        await Type("300");
        await Enter();

        Assert.That(Comp<IdCardComponent>(card).AccountBalance, Is.Zero);
    }

    [Test]
    public async Task WrongPinThreeTimesLocksTheCard()
    {
        await SpawnTarget(Atm);
        var (card, pin, _) = await InsertNewCard(500);
        var wrong = (pin == 1111 ? 2222 : 1111).ToString();

        await Type(wrong);
        Assert.That(AtmComp.StatusMessage, Does.Contain("Incorrect PIN. Attempt 1/3"));
        await Type(wrong);
        await Type(wrong);
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.PinLocked));

        // Take the card back and put it in again: even the right PIN is refused while locked.
        await Enter();
        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Welcome));
            Assert.That(HeldItem(), Is.EqualTo(ToServer(card)), "The locked card was not handed back");
        });
        await Interact();
        await Type(pin.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.PinLocked));
            Assert.That(AtmComp.PinAuthenticated, Is.False);
            Assert.That(Comp<IdCardComponent>(card).AccountBalance, Is.EqualTo(500));
        });
    }

    [Test]
    public async Task DepositAndTransfer()
    {
        await SpawnTarget(Atm);
        var (card, pin, _) = await InsertNewCard(200);
        await Type(pin.ToString());

        // The card is in the machine, so the (single) hand is free to hold the cash to deposit.
        await PlaceInHands(Cash, 50);
        await Type("2", enter: false);              // 2) DEPOSIT
        await Type("50");
        Assert.Multiple(() =>
        {
            Assert.That(Comp<IdCardComponent>(card).AccountBalance, Is.EqualTo(250));
            Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null, "Deposited cash was not taken");
            Assert.That(ClientAtmState().CashAmount, Is.EqualTo(50), "The screen was not told how much went in");
        });

        // Transfer to another card lying on the floor.
        var otherUid = await SpawnEntity(IdCard, SEntMan.GetCoordinates(TargetCoords));
        var other = SEntMan.GetComponent<IdCardComponent>(otherUid);

        await Enter();                              // back to the main menu from the result screen
        await Type("3", enter: false);              // 3) TRANSFER
        var otherAccount = other.AccountNumber;
        await Type(otherAccount.ToString());
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.TransferAmount));
        await Type("75");
        await Enter();

        Assert.Multiple(() =>
        {
            Assert.That(Comp<IdCardComponent>(card).AccountBalance, Is.EqualTo(175));
            Assert.That(other.AccountBalance, Is.EqualTo(75));
        });
    }

    [Test]
    public async Task RemoteDepositNeedsNoCard()
    {
        await SpawnTarget(Atm);
        var otherUid = await SpawnEntity(IdCard, SEntMan.GetCoordinates(TargetCoords));
        var other = SEntMan.GetComponent<IdCardComponent>(otherUid);

        await PlaceInHands(Cash, 30);
        await Activate();
        Assert.That(IsUiOpen(ColonyAtmUi.Key));
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Welcome));

        await Type("1", enter: false);              // 1) REMOTE DEPOSIT
        var otherAccount = other.AccountNumber;
        await Type(otherAccount.ToString());
        await Type("30");
        await Enter();

        Assert.Multiple(() =>
        {
            Assert.That(other.AccountBalance, Is.EqualTo(30));
            Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.Null);
        });
    }

    [Test]
    public async Task SecondPersonCannotUseBusyAtm()
    {
        await SpawnTarget(Atm);
        var (_, pin, _) = await InsertNewCard(100);
        await Type(pin.ToString());

        var atm = STarget!.Value;
        var stranger = await SpawnEntity("InteractionTestMob", SEntMan.GetCoordinates(TargetCoords));
        await Server.WaitPost(() => InteractSys.InteractionActivate(stranger, atm));
        await RunTicks(5);

        Assert.Multiple(() =>
        {
            Assert.That(SUiSys.IsUiOpen(atm, ColonyAtmUi.Key, stranger), Is.False, "A stranger opened a busy ATM");
            Assert.That(AtmComp.CurrentUser, Is.EqualTo(SPlayer));
            Assert.That(AtmComp.PinAuthenticated, Is.True, "The owner's session was reset");
        });

        // Once the owner closes the screen the machine is free - and their card stays in the reader,
        // still signed in.
        var card = CardInAtm();
        await CloseBui(ColonyAtmUi.Key);
        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.CurrentUser, Is.Null);
            Assert.That(CardInAtm(), Is.EqualTo(card), "Walking away took the card out of the machine");
            Assert.That(AtmComp.PinAuthenticated, Is.True, "Walking away signed the card out");
        });

        // Now the next person can take the ATM, and finds the card left signed in.
        await Server.WaitPost(() => InteractSys.InteractionActivate(stranger, atm));
        await RunTicks(5);
        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.CurrentUser, Is.EqualTo(stranger));
            Assert.That(AtmComp.PinAuthenticated, Is.True);
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.MainMenu));
        });
    }

    [Test]
    public async Task SiphonRigLeaksRecentLogins()
    {
        await SpawnTarget(Atm);
        var (_, pin, account) = await InsertNewCard(100);
        await Type(pin.ToString());
        await CloseBui(ColonyAtmUi.Key);
        Assert.That(AtmComp.RecentLogins.Select(l => l.AccountNumber), Does.Contain(account));

        // A trained sapper clamps a (quick) siphon rig onto the ATM.
        await Server.WaitPost(() => SEntMan.EnsureComponent<SapperComponent>(SPlayer));
        var rig = await PlaceInHands(SiphonRig);
        var rigComp = Comp<SapperAtmHackingComponent>(rig);
        await Server.WaitPost(() => rigComp.AtmHackTime = 0.5f);
        await Interact();

        Assert.Multiple(() =>
        {
            Assert.That(rigComp.CapturedAccounts, Has.Count.EqualTo(1));
            Assert.That(rigComp.CapturedAccounts[0].AccountNumber, Is.EqualTo(account));
            Assert.That(rigComp.CapturedAccounts[0].Pin, Is.EqualTo(pin));
            Assert.That(AtmComp.RecentLogins, Is.Empty, "Leaked logins were not wiped from the ATM");
            Assert.That(SEntMan.HasComponent<SapperAtmHackedComponent>(STarget), "ATM was not hacked");
            Assert.That(SEntMan.HasComponent<ColonyAtmTamperedComponent>(STarget), "ATM was not marked tampered");
        });

        // A plain click shows the seized screen, but starts no session on it.
        await Activate();
        Assert.Multiple(() =>
        {
            Assert.That(IsUiOpen(ColonyAtmUi.Key), "The seized ATM showed nothing");
            Assert.That(ClientAtmState().OutOfService, "The hacked ATM did not show its seized screen");
            Assert.That(AtmComp.CurrentUser, Is.Null, "The hacked ATM started a session");
        });

        // Reading the rig in hand shows the stolen login.
        await UseInHand();
        Assert.That(IsUiOpen(SapperSiphonRigUiKey.Key), "Siphon rig data window did not open");
        await RunTicks(5);
        var window = GetWindow<SapperSiphonRigWindow>();
        Assert.That(window.DataRows.ChildCount, Is.EqualTo(1));
    }
}
