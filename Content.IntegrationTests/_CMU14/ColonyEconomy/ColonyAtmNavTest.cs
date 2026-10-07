using Content.Client.CMU14.ColonyEconomy;
using Content.Shared.Access.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     The ATM's nav bar reminds whoever is at the screen of their own card's PIN, as the character
///     notes do - and never shows the PIN of the card in the reader, which may be someone else's.
/// </summary>
public sealed class ColonyAtmNavTest : ColonyAtmTestBase
{
    private string NavText()
        => GetWindow<ColonyAtmWindow>().NavPin.Text ?? string.Empty;

    [Test]
    public async Task NavShowsYourOwnPinNotTheCardInTheReader()
    {
        await SpawnTarget(Atm);

        // Your own card lies on the floor; the card you push into the reader belongs to a stranger.
        var ownUid = await SpawnEntity(IdCard, SEntMan.GetCoordinates(TargetCoords));
        var own = SEntMan.GetComponent<IdCardComponent>(ownUid);
        await Server.WaitPost(() => own.OriginalOwner = SPlayer);
        var stranger = await SpawnEntity("InteractionTestMob", SEntMan.GetCoordinates(TargetCoords));

        var (_, pin, _) = await InsertNewCard(100, owner: stranger);
        await RunTicks(5);

        Assert.Multiple(() =>
        {
            Assert.That(NavText(), Is.EqualTo($"Your card #{own.AccountNumber} - PIN {own.AtmPin}"),
                "The nav bar does not show your own card's PIN");
            Assert.That(NavText(), Does.Not.Contain($"PIN {pin}"), "The PIN of the card in the reader was shown");
        });
    }

    [Test]
    public async Task NavSaysSoWhenYouHaveNoCardOfYourOwn()
    {
        await SpawnTarget(Atm);
        await Activate();
        await RunTicks(5);

        Assert.That(NavText(), Is.EqualTo("You have no card of your own"));
    }
}
