using System.Linq;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     The ATM keeps the card it is given. Whoever put it in gets it back the moment they ask from
///     the screen; anyone else - including its owner once they have walked away - has to pull it out,
///     which takes a do-after.
/// </summary>
public sealed class ColonyAtmCardSlotTest : ColonyAtmTestBase
{
    private const string Stranger = "InteractionTestMob";

    [Test]
    public async Task InsertedCardStaysInTheMachine()
    {
        await SpawnTarget(Atm);
        var (card, _, _) = await InsertNewCard(100);
        await RunTicks(10);

        Assert.Multiple(() =>
        {
            // The ATM entity also carries the requisitions cash intake, which used to delete anything
            // put into one of its containers as a payment.
            Assert.That(SEntMan.EntityExists(ToServer(card)), "The ATM destroyed the card");
            Assert.That(CardInAtm(), Is.EqualTo(ToServer(card)));
            Assert.That(ClientAtmState().CardInserted, "The screen was not told a card is in");
            Assert.That(ClientAtmState().CardPrototype, Is.EqualTo(IdCard), "The screen cannot show which card it is");
        });

        await CloseBui(ColonyAtmUi.Key);
        Assert.That(CardInAtm(), Is.EqualTo(ToServer(card)), "Walking away took the card with it");
    }

    [Test]
    public async Task OnlyOneCardFitsTheReader()
    {
        await SpawnTarget(Atm);
        var (first, _, _) = await InsertNewCard(100);
        await CloseBui(ColonyAtmUi.Key);

        var second = await PlaceInHands(IdCard);
        await Interact();
        Assert.Multiple(() =>
        {
            Assert.That(CardInAtm(), Is.EqualTo(ToServer(first)));
            Assert.That(HeldItem(), Is.EqualTo(ToServer(second)), "A second card was taken while the reader was full");
        });
    }

    [Test]
    public async Task InserterAtTheScreenGetsTheCardBackInstantly()
    {
        await SpawnTarget(Atm);
        var (card, _, _) = await InsertNewCard(100);

        await Cancel();                             // at the PIN prompt: end the session
        Assert.Multiple(() =>
        {
            Assert.That(ActiveDoAfters, Is.Empty, "Taking your own card back from the screen took a do-after");
            Assert.That(CardInAtm(), Is.Null);
            Assert.That(HeldItem(), Is.EqualTo(ToServer(card)));
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Welcome));
        });
    }

    [Test]
    public async Task TakingSomeoneElsesCardTakesADoAfter()
    {
        await SpawnTarget(Atm);
        var atm = STarget!.Value;
        var owner = await SpawnEntity(Stranger, SEntMan.GetCoordinates(TargetCoords));
        var (cardUid, _, _) = await SpawnOtherCard(100);
        await Server.WaitPost(() =>
            Assert.That(SEntMan.System<ColonyAtmSystem>().TryInsertCard(atm, AtmComp, cardUid, owner), "The ATM refused the card"));

        // The player walks up and pulls the stranger's card.
        await Server.WaitPost(() => SEntMan.System<ColonyAtmSystem>().TryTakeCard(atm, AtmComp, SPlayer));
        await RunTicks(5);
        Assert.Multiple(() =>
        {
            Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1), "Pulling someone else's card did not take a do-after");
            Assert.That(CardInAtm(), Is.EqualTo(cardUid), "The card came out straight away");
        });

        await AwaitDoAfters();
        Assert.Multiple(() =>
        {
            Assert.That(CardInAtm(), Is.Null);
            Assert.That(HeldItem(), Is.EqualTo(cardUid), "The card did not end up with whoever pulled it");
        });
    }

    [Test]
    public async Task OwnerWhoWalkedAwayAlsoHasToPullTheCard()
    {
        await SpawnTarget(Atm);
        var (card, _, _) = await InsertNewCard(100);
        await CloseBui(ColonyAtmUi.Key);

        await Server.WaitPost(() => SEntMan.System<ColonyAtmSystem>().TryTakeCard(STarget!.Value, AtmComp, SPlayer));
        await RunTicks(5);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1), "A card left behind came out without a do-after");

        await AwaitDoAfters();
        Assert.That(HeldItem(), Is.EqualTo(ToServer(card)));
    }

    /// <summary>A card pulled out mid-session ends that session: no one carries on with it.</summary>
    [Test]
    public async Task PullingTheCardEndsTheSession()
    {
        await SpawnTarget(Atm);
        var (_, pin, _) = await InsertNewCard(100);
        await Type(pin.ToString());
        Assert.That(AtmComp.PinAuthenticated);

        await Server.WaitPost(() =>
        {
            var system = SEntMan.System<ColonyAtmSystem>();
            var card = system.GetCard(STarget!.Value)!.Value;
            SEntMan.System<Robust.Shared.Containers.SharedContainerSystem>().TryRemoveFromContainer(card);
        });
        await RunTicks(5);

        Assert.Multiple(() =>
        {
            Assert.That(AtmComp.PinAuthenticated, Is.False, "The session outlived its card");
            Assert.That(AtmComp.Screen, Is.EqualTo(AtmScreen.Welcome));
            Assert.That(ClientAtmState().CardInserted, Is.False);
        });
    }
}
