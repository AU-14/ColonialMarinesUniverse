using Content.Client.CMU14.ColonyEconomy;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     Helpers for ATM tests driven by a connected player who pushes cards into the reader and
///     presses the real keypad buttons in the client window.
/// </summary>
public abstract class ColonyAtmTestBase : InteractionTest
{
    protected const string Atm = "AUColonyATM";
    protected const string IdCard = "AU14IDCardCLFCivilian";
    protected const string Cash = "RMCSpaceCash";

    protected ColonyAtmComponent AtmComp => Comp<ColonyAtmComponent>();

    /// <summary>The card sitting in the target ATM's reader, if any.</summary>
    protected EntityUid? CardInAtm()
        => SEntMan.System<ColonyAtmSystem>().GetCard(STarget!.Value);

    /// <summary>Whatever is in the player's active hand.</summary>
    protected EntityUid? HeldItem()
        => HandSys.GetActiveItem((SPlayer, Hands));

    /// <summary>The last ATM screen the server sent to this client.</summary>
    protected ColonyAtmBuiState ClientAtmState()
    {
        Assert.That(CUiSys.TryGetUiState<ColonyAtmBuiState>(CTarget!.Value, ColonyAtmUi.Key, out var state),
            "The client has no ATM screen");
        return state!;
    }

    /// <summary>Clicks the keypad keys for every digit, then optionally ENTER.</summary>
    protected async Task Type(string digits, bool enter = true)
    {
        foreach (var digit in digits)
            await ClickControl<ColonyAtmWindow>($"Btn{digit}");

        if (enter)
            await ClickControl<ColonyAtmWindow>(nameof(ColonyAtmWindow.BtnEnter));

        await RunTicks(5);
    }

    protected async Task Enter() => await Type(string.Empty);

    /// <summary>Presses CLEAR once: rubs out the last digit.</summary>
    protected async Task Clear()
    {
        await ClickControl<ColonyAtmWindow>(nameof(ColonyAtmWindow.BtnClear));
        await RunTicks(5);
    }

    /// <summary>Presses CANCEL once: backs out of the transaction, or ends the session.</summary>
    protected async Task Cancel()
    {
        await ClickControl<ColonyAtmWindow>(nameof(ColonyAtmWindow.BtnCancel));
        await RunTicks(5);
    }

    /// <summary>
    ///     Puts a new ID card in hand, gives it a balance and pushes it into the target ATM's reader,
    ///     which keeps it.
    /// </summary>
    protected async Task<(NetEntity Card, int Pin, int Account)> InsertNewCard(int balance, EntityUid? owner = null)
    {
        var card = await PlaceInHands(IdCard);
        var comp = Comp<IdCardComponent>(card);
        await Server.WaitPost(() =>
        {
            comp.AccountBalance = balance;
            comp.OriginalOwner = owner;
        });

        // The card already has its PIN before it ever reaches an ATM; inserting must not change it.
        var (pin, account) = (comp.AtmPin, comp.AccountNumber);
        Assert.That(pin, Is.InRange(1000, 9999), "Card had no PIN before it was inserted");

        await Interact();
        Assert.Multiple(() =>
        {
            Assert.That(IsUiOpen(ColonyAtmUi.Key), "Inserting a card did not open the ATM");
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.PinEntry));
            Assert.That(CardInAtm(), Is.EqualTo(ToServer(card)), "The ATM did not take the card in");
            Assert.That(HeldItem(), Is.Null, "The card stayed in the player's hand");
            Assert.That(comp.AtmPin, Is.EqualTo(pin), "Inserting changed the PIN");
            Assert.That(comp.AccountNumber, Is.EqualTo(account), "Inserting changed the account number");
        });
        return (card, pin, account);
    }

    /// <summary>Spawns someone else's ID card on the floor next to the ATM.</summary>
    protected async Task<(EntityUid Uid, IdCardComponent Card, int Account)> SpawnOtherCard(int balance = 0)
    {
        var uid = await SpawnEntity(IdCard, SEntMan.GetCoordinates(TargetCoords));
        var card = SEntMan.GetComponent<IdCardComponent>(uid);
        await Server.WaitPost(() => card.AccountBalance = balance);
        return (uid, card, card.AccountNumber);
    }

    /// <summary>A PIN guaranteed not to be <paramref name="pin"/>.</summary>
    protected static string WrongPin(int pin) => (pin == 1111 ? 2222 : 1111).ToString();

    /// <summary>Total dollars lying loose on the map (not held or stored).</summary>
    protected int CashOnFloor()
    {
        var total = 0;
        var query = SEntMan.EntityQueryEnumerator<StackComponent>();
        while (query.MoveNext(out var uid, out var stack))
        {
            if (stack.StackTypeId == "Dollar" && !SEntMan.System<SharedContainerSystem>().IsEntityInContainer(uid))
                total += stack.Count;
        }

        return total;
    }

    /// <summary>Dollars waiting in the ATM's cash tray.</summary>
    protected int CashInTray()
    {
        var containers = SEntMan.System<SharedContainerSystem>();
        if (!containers.TryGetContainer(STarget!.Value, ColonyAtmComponent.CashTrayId, out var tray))
            return 0;

        var total = 0;
        foreach (var cash in tray.ContainedEntities)
        {
            if (SEntMan.TryGetComponent<StackComponent>(cash, out var stack))
                total += stack.Count;
        }

        return total;
    }

    /// <summary>Dollars in the player's active hand.</summary>
    protected int CashInHand()
    {
        var held = HandSys.GetActiveItem((SPlayer, Hands));
        return held != null && SEntMan.TryGetComponent<StackComponent>(held, out var stack) ? stack.Count : 0;
    }
}
