using Content.Client.CMU14.ColonyEconomy;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     The card reader drawn on the ATM is a button too. Clicking it while it is empty puts in the
///     player's card (the one in hand, else the one they wear). Clicking their card once the lights
///     are green logs them off and hands it back.
/// </summary>
public sealed class ColonyAtmReaderTest : ColonyAtmTestBase
{
    private const string MobWithIdSlot = "ColonyAtmTestMobWithIdSlot";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  parent: InteractionTestMob
  id: {MobWithIdSlot}
  components:
  - type: Inventory
    templateId: human
  - type: InventorySlots
";

    protected override string PlayerPrototype => MobWithIdSlot;

    private async Task ClickReader()
    {
        await ClickControl<ColonyAtmWindow>(nameof(ColonyAtmWindow.BtnReader));
        await RunTicks(5);
    }

    /// <summary>Waits out the reader's "reading" lights after a card goes in, so the card is clickable.</summary>

    [Test]
    public async Task ClickingTheReaderTakesTheCardInHand()
    {
        await SpawnTarget(Atm);
        var card = await PlaceInHands(IdCard);
        await Activate();
        Assert.Multiple(() =>
        {
            Assert.That(IsUiOpen(ColonyAtmUi.Key), "Activating the ATM did not open it");
            Assert.That(CardInAtm(), Is.Null, "Opening the screen took the card");
        });

        await ClickReader();
        Assert.Multiple(() =>
        {
            Assert.That(CardInAtm(), Is.EqualTo(ToServer(card)), "The reader did not take the card in hand");
            Assert.That(HeldItem(), Is.Null);
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.PinEntry));
            Assert.That(ClientAtmState().CardInserted, "The screen was not told a card is in");
        });
    }

    [Test]
    public async Task ClickingTheReaderTakesTheWornIdWhenTheHandIsEmpty()
    {
        await SpawnTarget(Atm);
        var worn = await SpawnEntity(IdCard, SEntMan.GetCoordinates(PlayerCoords));
        await Server.WaitPost(() => Assert.That(
            SEntMan.System<InventorySystem>().TryEquip(SPlayer, worn, "id", silent: true, force: true),
            "Could not put the ID in the player's ID slot"));

        await Activate();
        await ClickReader();
        Assert.Multiple(() =>
        {
            Assert.That(CardInAtm(), Is.EqualTo(worn), "The reader did not take the worn ID");
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.PinEntry));
        });
    }

}
