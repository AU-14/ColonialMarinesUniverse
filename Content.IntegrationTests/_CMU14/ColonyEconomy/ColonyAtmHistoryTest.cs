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

    /// <summary>Like the balance, the history stays on the server until the PIN is in and the screen is open.</summary>
    [Test]
    public async Task HistoryIsOnlySentOnTheHistoryScreenAfterThePin()
    {
        await SpawnTarget(Atm);
        var (card, pin, _) = await InsertNewCard(100);
        await Server.WaitPost(() =>
            SEntMan.System<ColonyBankSystem>().RecordTransaction(ToServer(card), AtmHistoryKind.Deposit, 25));

        Assert.That(ClientAtmState().History, Is.Empty, "The history was sent before the PIN was entered");

        await Type(pin.ToString());
        Assert.That(ClientAtmState().History, Is.Empty, "The history was sent to the main menu");

        await Type("5", enter: false);              // 5) HISTORY
        Assert.That(ClientAtmState().History, Has.Length.EqualTo(1));

        await Cancel();
        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.MainMenu), "CANCEL did not leave the history screen");
            Assert.That(ClientAtmState().History, Is.Empty, "The history stayed on screen after leaving it");
        });
    }


}
