using Content.Client.CMU14.ColonyEconomy;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     Helpers for ATM tests driven by a connected player who swipes cards and presses the real
///     keypad buttons in the client window.
/// </summary>
public abstract class ColonyAtmTestBase : InteractionTest
{
    protected const string Atm = "AUColonyATM";
    protected const string IdCard = "AU14IDCardCLFCivilian";
    protected const string Cash = "RMCSpaceCash";

    protected ColonyAtmComponent AtmComp => Comp<ColonyAtmComponent>();

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

    /// <summary>Presses the DEL key once.</summary>
    protected async Task Delete()
    {
        await ClickControl<ColonyAtmWindow>(nameof(ColonyAtmWindow.BtnDel));
        await RunTicks(5);
    }

    /// <summary>Puts a new ID card in hand, gives it a balance and swipes it on the target ATM.</summary>
    protected async Task<(NetEntity Card, int Pin, int Account)> SwipeNewCard(int balance, EntityUid? owner = null)
    {
        var card = await PlaceInHands(IdCard);
        var comp = Comp<IdCardComponent>(card);
        await Server.WaitPost(() =>
        {
            comp.AccountBalance = balance;
            comp.OriginalOwner = owner;
        });

        // The card already has its PIN before it ever reaches an ATM; swiping must not change it.
        var (pin, account) = (comp.AtmPin, comp.AccountNumber);
        Assert.That(pin, Is.InRange(1000, 9999), "Card had no PIN before it was swiped");

        await Interact();
        Assert.Multiple(() =>
        {
            Assert.That(IsUiOpen(ColonyAtmUi.Key), "Swiping a card did not open the ATM");
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.PinEntry));
            Assert.That(comp.AtmPin, Is.EqualTo(pin), "Swiping changed the PIN");
            Assert.That(comp.AccountNumber, Is.EqualTo(account), "Swiping changed the account number");
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

    /// <summary>Dollars in the player's active hand.</summary>
    protected int CashInHand()
    {
        var held = HandSys.GetActiveItem((SPlayer, Hands));
        return held != null && SEntMan.TryGetComponent<StackComponent>(held, out var stack) ? stack.Count : 0;
    }
}
