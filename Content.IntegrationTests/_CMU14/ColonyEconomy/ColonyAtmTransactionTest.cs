using Content.Client.CMU14.ColonyEconomy;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Insurgency.Sapper;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     ATM money rules and refusals, driven through the real keypad: tax, overdrafts, bad accounts,
///     keypad navigation, what the client is told, and machines that must not act as an ATM.
/// </summary>
public sealed class ColonyAtmTransactionTest : ColonyAtmTestBase
{
    [Test]
    public async Task BalanceStaysHiddenUntilThePinIsEntered()
    {
        await SpawnTarget(Atm);
        var (_, pin, _) = await InsertNewCard(750);

        Assert.That(ClientAtmState().Balance, Is.Zero, "The balance was sent before the PIN was entered");

        await Type(pin.ToString());
        Assert.That(ClientAtmState().Balance, Is.EqualTo(750));
    }

    [Test]
    public async Task WithdrawalIsTaxedIntoTheColonyBudget()
    {
        await SpawnTarget(Atm);
        await Server.WaitPost(() =>
        {
            var console = SEntMan.SpawnEntity(null, SEntMan.GetCoordinates(TargetCoords));
            SEntMan.AddComponent<AdminConsoleComponent>(console).IncomeTaxPercent = 20;
        });

        var budget = SEntMan.System<ColonyBudgetSystem>();
        var budgetBefore = budget.GetBudget();
        var (card, pin, _) = await InsertNewCard(500);
        await Type(pin.ToString());

        await Type("1", enter: false);              // 1) WITHDRAW
        await Type("100");
        await Enter();

        Assert.Multiple(() =>
        {
            Assert.That(Comp<IdCardComponent>(card).AccountBalance, Is.EqualTo(400), "The full amount was not taken from the account");
            Assert.That(CashOnFloor(), Is.EqualTo(80), "The cash dispensed was not net of tax");
            Assert.That(budget.GetBudget() - budgetBefore, Is.EqualTo(20), "The tax did not reach the colony budget");
        });
    }

    [Test]
    public async Task CannotWithdrawMoreThanTheBalance()
    {
        await SpawnTarget(Atm);
        var (card, pin, _) = await InsertNewCard(50);
        await Type(pin.ToString());

        await Type("1", enter: false);              // 1) WITHDRAW
        await Type("100");

        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.StatusMessage, Does.Contain("Insufficient funds"));
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Withdraw), "The ATM moved on to confirm an overdraft");
            Assert.That(Comp<IdCardComponent>(card).AccountBalance, Is.EqualTo(50));
            Assert.That(CashOnFloor(), Is.Zero);
        });
    }

    [Test]
    public async Task CannotDepositMoreCashThanYouHold()
    {
        await SpawnTarget(Atm);
        var (card, pin, _) = await InsertNewCard(100);
        await Type(pin.ToString());

        await PlaceInHands(Cash, 20);
        await Type("2", enter: false);              // 2) DEPOSIT
        await Type("50");

        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.StatusMessage, Does.Contain("Insufficient cash"));
            Assert.That(Comp<IdCardComponent>(card).AccountBalance, Is.EqualTo(100));
            Assert.That(CashInHand(), Is.EqualTo(20), "Cash was taken for a refused deposit");
        });
    }

    [Test]
    public async Task TransferRefusesOwnUnknownAndOverdrawnAccounts()
    {
        await SpawnTarget(Atm);
        var (card, pin, account) = await InsertNewCard(100);
        var (_, other, otherAccount) = await SpawnOtherCard(10);
        var unknownAccount = 0;
        await Server.WaitPost(() =>
        {
            var bank = SEntMan.System<ColonyBankSystem>();
            unknownAccount = 99999;
            while (bank.FindAccount(unknownAccount) != null)
                unknownAccount--;
        });

        await Type(pin.ToString());
        await Type("3", enter: false);              // 3) TRANSFER

        await Type(account.ToString());
        Assert.That(AtmComp.StatusMessage, Does.Contain("Cannot transfer to own account"));
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Transfer));

        await Type(unknownAccount.ToString());
        Assert.That(AtmComp.StatusMessage, Does.Contain("Account not found"));
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Transfer));

        await Type(otherAccount.ToString());
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.TransferAmount));
        await Type("150");

        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.StatusMessage, Does.Contain("Insufficient funds"));
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.TransferAmount));
            Assert.That(Comp<IdCardComponent>(card).AccountBalance, Is.EqualTo(100));
            Assert.That(other.AccountBalance, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task CardFreeDepositToAnUnknownAccountKeepsYourCash()
    {
        await SpawnTarget(Atm);
        var unknownAccount = 0;
        await Server.WaitPost(() =>
        {
            var bank = SEntMan.System<ColonyBankSystem>();
            unknownAccount = 99999;
            while (bank.FindAccount(unknownAccount) != null)
                unknownAccount--;
        });

        await PlaceInHands(Cash, 30);
        await Activate();
        await Type("1", enter: false);              // 1) REMOTE DEPOSIT
        await Type(unknownAccount.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.StatusMessage, Does.Contain("Account not found"));
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.RemoteDeposit));
            Assert.That(CashInHand(), Is.EqualTo(30));
        });
    }

    [Test]
    public async Task ClearEditsCancelBacksOutAndExitReturnsTheCard()
    {
        await SpawnTarget(Atm);
        var (card, pin, _) = await InsertNewCard(100);
        await Type(pin.ToString());

        await Type("1", enter: false);              // 1) WITHDRAW
        await Type("12", enter: false);
        Assert.That(AtmComp.KeypadBuffer, Is.EqualTo("12"));

        await Clear();
        Assert.That(AtmComp.KeypadBuffer, Is.EqualTo("1"), "CLEAR did not remove the last digit");
        await Clear();
        await Clear();
        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.KeypadBuffer, Is.Empty);
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Withdraw), "CLEAR on an empty entry left the screen");
        });

        await Cancel();
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.MainMenu), "CANCEL did not back out of the withdrawal");

        await Type("6", enter: false);              // 6) EXIT
        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Welcome));
            Assert.That(CardInAtm(), Is.Null, "EXIT kept the card");
            Assert.That(HeldItem(), Is.EqualTo(ToServer(card)), "EXIT did not hand the card back");
            Assert.That(AtmComp.PinAuthenticated, Is.False);
        });
    }

    /// <summary>CANCEL abandons a transaction from any step of it, not just the last one.</summary>
    [Test]
    public async Task CancelAbandonsATransferHalfway()
    {
        await SpawnTarget(Atm);
        var (_, pin, _) = await InsertNewCard(100);
        var (_, other, otherAccount) = await SpawnOtherCard();
        await Type(pin.ToString());

        await Type("3", enter: false);              // 3) TRANSFER
        await Type(otherAccount.ToString());
        await Type("40");
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.TransferConfirm));

        await Cancel();
        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.MainMenu));
            Assert.That(AtmComp.PendingAmount, Is.Zero, "The cancelled transfer was left staged");
            Assert.That(other.AccountBalance, Is.Zero);
        });

        // At the menu, CANCEL ends the session and gives the card back.
        await Cancel();
        Assert.That(CardInAtm(), Is.Null);
    }

    /// <summary>00 types two zeros, and only while both still fit.</summary>
    [Test]
    public async Task DoubleZeroKeyTypesTwoZeros()
    {
        await SpawnTarget(Atm);
        var (_, pin, _) = await InsertNewCard(100);
        await Type(pin.ToString());

        await Type("1", enter: false);              // 1) WITHDRAW
        await Type("5", enter: false);
        await ClickControl<ColonyAtmWindow>(nameof(ColonyAtmWindow.Btn00));
        await RunTicks(5);
        Assert.That(AtmComp.KeypadBuffer, Is.EqualTo("500"));
    }

    [Test]
    public async Task ClosingTheAtmMeansThePinIsNeededAgain()
    {
        await SpawnTarget(Atm);
        var (_, pin, _) = await InsertNewCard(100);
        await Type(pin.ToString());
        Assert.That(AtmComp.PinAuthenticated);

        await CloseBui(ColonyAtmUi.Key);
        await Interact();                            // come back; the card is still in the reader

        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.PinEntry));
            Assert.That(AtmComp.PinAuthenticated, Is.False, "The ATM remembered the PIN after it was closed");
        });
    }

    /// <summary>The lock is on the card, so walking to another ATM does not get around it.</summary>
    [Test]
    public async Task LockedCardIsLockedAtEveryAtm()
    {
        await SpawnTarget(Atm);
        var (_, pin, _) = await InsertNewCard(100);
        for (var i = 0; i < ColonyBankSystem.MaxPinAttempts; i++)
            await Type(WrongPin(pin));
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.PinLocked));
        await Enter();                               // take the locked card back
        await CloseBui(ColonyAtmUi.Key);

        await SpawnTarget(Atm);                      // a second ATM
        await Interact();
        await Type(pin.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.PinLocked));
            Assert.That(AtmComp.PinAuthenticated, Is.False);
        });
    }

    /// <summary>
    ///     A siphoned ATM is out of order until it repairs itself. It refuses cards; a click shows its
    ///     seized screen - the CLF's mark - and every key on it is dead.
    /// </summary>
    [Test]
    public async Task HackedAtmIsOutOfOrderUntilItRepairsItself()
    {
        await SpawnTarget(Atm);
        await Server.WaitPost(() =>
        {
            var hacked = SEntMan.EnsureComponent<SapperAtmHackedComponent>(STarget!.Value);
            hacked.RecoverAt = STiming.CurTime + TimeSpan.FromSeconds(3);
        });

        await PlaceInHands(IdCard);
        await Interact();
        Assert.That(IsUiOpen(ColonyAtmUi.Key), Is.False, "An out-of-order ATM accepted a card");

        await Activate();
        Assert.Multiple(() =>
        {
            Assert.That(IsUiOpen(ColonyAtmUi.Key), "A click on the seized ATM showed nothing");
            Assert.That(ClientAtmState().OutOfService, "The seized ATM showed the terminal");
            Assert.That(AtmComp.CurrentUser, Is.Null, "The seized ATM started a session");
        });
        await Type("1", enter: false);              // 1) REMOTE DEPOSIT, if anything worked
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Welcome), "A key on the seized screen did something");

        await RunSeconds(4);
        Assert.That(SEntMan.HasComponent<SapperAtmHackedComponent>(STarget), Is.False, "The ATM never repaired itself");
        await Interact();
        Assert.Multiple(() =>
        {
            Assert.That(IsUiOpen(ColonyAtmUi.Key), "The repaired ATM refused the card");
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.PinEntry));
        });
    }

    /// <summary>
    ///     The ASRS console also carries the ATM component (for cash intake) but has no ATM screen,
    ///     so it must never start an ATM session.
    /// </summary>
    [Test]
    public async Task AsrsConsoleIsNotTreatedAsAnAtm()
    {
        await SpawnTarget("CMASRSConsole");
        await PlaceInHands(IdCard);
        await Interact();
        await Activate();

        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.CurrentUser, Is.Null, "The ASRS console started an ATM session");
            Assert.That(CardInAtm(), Is.Null, "The ASRS console took the card");
            Assert.That(HeldItem(), Is.Not.Null, "The card left the player's hand");
            Assert.That(IsUiOpen(ColonyAtmUi.Key), Is.False);
        });
    }
}
