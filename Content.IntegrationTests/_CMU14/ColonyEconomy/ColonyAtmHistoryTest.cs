using System.Linq;
using Content.Client.CMU14.ColonyEconomy;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     The ATM's account history screen: what gets listed, who gets to see it, and how much it keeps.
/// </summary>
public sealed class ColonyAtmHistoryTest : ColonyAtmTestBase
{
    [Test]
    public async Task HistoryListsWithdrawalsDepositsAndTransfers()
    {
        await SpawnTarget(Atm);
        var (card, pin, account) = await SwipeNewCard(500);
        var (otherUid, _, otherAccount) = await SpawnOtherCard();
        await Type(pin.ToString());

        await Type("1", enter: false);              // 1) WITHDRAW
        await Type("100");
        await Enter();
        await Enter();                              // back to the main menu

        await Drop();
        await PlaceInHands(Cash, 30);
        await Type("2", enter: false);              // 2) DEPOSIT
        await Type("30");
        await Enter();

        await Type("3", enter: false);              // 3) TRANSFER
        await Type(otherAccount.ToString());
        await Type("50");
        await Enter();
        await Enter();

        await Type("5", enter: false);              // 5) HISTORY
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.History));

        // Newest first.
        var shown = ClientAtmState().History;
        Assert.Multiple(() =>
        {
            Assert.That(shown.Select(e => (e.Kind, e.Amount)), Is.EqualTo(new[]
            {
                (AtmHistoryKind.TransferOut, 50),
                (AtmHistoryKind.Deposit, 30),
                (AtmHistoryKind.Withdrawal, 100),
            }), "The history does not match what was done at the ATM");
            Assert.That(shown.First().OtherAccount, Is.EqualTo(otherAccount), "The transfer does not name who it went to");
        });

        // The recipient sees the same transfer from the other side.
        var received = SEntMan.System<ColonyBankSystem>().GetHistory(otherUid);
        Assert.That(received.Select(e => (e.Kind, e.Amount, e.OtherAccount)),
            Is.EqualTo(new[] { (AtmHistoryKind.TransferIn, 50, account) }), "The recipient's history is wrong");

        await Enter();
        Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.MainMenu), "ENTER did not leave the history screen");
    }

    [Test]
    public async Task CardFreeDepositShowsInTheRecipientsHistory()
    {
        await SpawnTarget(Atm);
        var (otherUid, _, otherAccount) = await SpawnOtherCard();

        await PlaceInHands(Cash, 40);
        await Activate();
        await Type("1", enter: false);              // 1) REMOTE DEPOSIT
        await Type(otherAccount.ToString());
        await Type("40");
        await Enter();

        var received = SEntMan.System<ColonyBankSystem>().GetHistory(otherUid);
        Assert.That(received.Select(e => (e.Kind, e.Amount)), Is.EqualTo(new[] { (AtmHistoryKind.CashDeposit, 40) }));
    }

    /// <summary>Like the balance, the history stays on the server until the PIN is in and the screen is open.</summary>
    [Test]
    public async Task HistoryIsOnlySentOnTheHistoryScreenAfterThePin()
    {
        await SpawnTarget(Atm);
        var (card, pin, _) = await SwipeNewCard(100);
        await Server.WaitPost(() =>
            SEntMan.System<ColonyBankSystem>().RecordTransaction(ToServer(card), AtmHistoryKind.Deposit, 25));

        Assert.That(ClientAtmState().History, Is.Empty, "The history was sent before the PIN was entered");

        await Type(pin.ToString());
        Assert.That(ClientAtmState().History, Is.Empty, "The history was sent to the main menu");

        await Type("5", enter: false);              // 5) HISTORY
        Assert.That(ClientAtmState().History, Has.Length.EqualTo(1));

        await Delete();
        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.MainMenu), "DEL did not leave the history screen");
            Assert.That(ClientAtmState().History, Is.Empty, "The history stayed on screen after leaving it");
        });
    }

    /// <summary>The arrows page through everything the account keeps, newest first, without gaps or repeats.</summary>
    [Test]
    public async Task ScrollArrowsPageThroughOlderHistory()
    {
        await SpawnTarget(Atm);
        var (card, pin, _) = await SwipeNewCard(100);
        var amounts = Enumerable.Range(1, 14).ToList();
        await Server.WaitPost(() =>
        {
            foreach (var amount in amounts)
                SEntMan.System<ColonyBankSystem>().RecordTransaction(ToServer(card), AtmHistoryKind.Deposit, amount);
        });

        await Type(pin.ToString());
        await Type("5", enter: false);              // 5) HISTORY
        var window = GetWindow<ColonyAtmWindow>();

        var firstPage = ClientAtmState().History.Select(e => e.Amount).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(firstPage, Has.Count.LessThan(amounts.Count), "Everything fit on one page, nothing to scroll");
            Assert.That(window.BtnScrollDown.Visible && !window.BtnScrollDown.Disabled, "No way to scroll to older entries");
            Assert.That(window.BtnScrollUp.Disabled, "Could scroll up past the newest entry");
        });

        // Scroll down to the end, collecting every page.
        var seen = new List<int>(firstPage);
        for (var page = 0; page < amounts.Count && !window.BtnScrollDown.Disabled; page++)
        {
            await ClickControl<ColonyAtmWindow>(nameof(ColonyAtmWindow.BtnScrollDown));
            await RunTicks(5);
            seen.AddRange(ClientAtmState().History.Select(e => e.Amount));
        }

        Assert.That(seen, Is.EqualTo(Enumerable.Reverse(amounts)), "Scrolling skipped or repeated entries");

        // And back up to the newest page.
        while (!window.BtnScrollUp.Disabled)
        {
            await ClickControl<ColonyAtmWindow>(nameof(ColonyAtmWindow.BtnScrollUp));
            await RunTicks(5);
        }
        Assert.That(ClientAtmState().History.Select(e => e.Amount), Is.EqualTo(firstPage), "Scrolling up did not return to the newest entries");
    }

    [Test]
    public async Task HistoryKeepsOnlyTheLatestEntries()
    {
        var bank = SEntMan.System<ColonyBankSystem>();
        var (cardUid, _, _) = await SpawnOtherCard();
        var amounts = Enumerable.Range(1, ColonyBankSystem.MaxHistoryEntries + 2).ToList();

        await Server.WaitAssertion(() =>
        {
            foreach (var amount in amounts)
                bank.RecordTransaction(cardUid, AtmHistoryKind.Deposit, amount);

            var kept = bank.GetHistory(cardUid).Select(e => e.Amount).ToList();
            Assert.That(kept, Has.Count.LessThan(amounts.Count), "The history grows without limit");
            Assert.That(kept, Is.EqualTo(amounts.TakeLast(kept.Count)), "The history did not keep the newest entries in order");
        });
    }
}
