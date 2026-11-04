using Content.IntegrationTests.Tests.Interaction;
using Content.Server.GameTicking;
using Content.Shared.Access.Components;
using ClientCharacterInfoSystem = Content.Client.CharacterInfo.CharacterInfoSystem;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     The character notes tell a colonist their own account number and PIN, and never anyone else's.
/// </summary>
public sealed class ColonyCharacterNotesTest : InteractionTest
{
    private const string IdCard = "AU14IDCardCLFCivilian";

    // A real game ticker, so the round can be set to a colony game mode.
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
        DummyTicker = false,
    };

    /// <summary>Asks the server for this character's notes, as opening the character menu does.</summary>
    private async Task<List<string>> RequestNotes()
    {
        List<string> lines = null;
        var info = CEntMan.System<ClientCharacterInfoSystem>();
        await Client.WaitPost(() =>
        {
            info.OnCharacterUpdate += data => lines = data.LorePrimerLines;
            info.RequestCharacterInfo();
        });
        await RunTicks(15);

        Assert.That(lines, Is.Not.Null, "The server never answered the character info request");
        return lines;
    }

    [Test]
    public async Task NotesShowYourOwnCardNotTheOneYouHold()
    {
        await Server.WaitPost(() => SEntMan.System<GameTicker>().SetGamePreset("ColonyFall"));

        // Your own card lies on the floor; you are holding a card that belongs to someone else.
        var ownUid = await SpawnEntity(IdCard, SEntMan.GetCoordinates(TargetCoords));
        var own = SEntMan.GetComponent<IdCardComponent>(ownUid);
        await Server.WaitPost(() => own.OriginalOwner = SPlayer);

        var stranger = await SpawnEntity("InteractionTestMob", SEntMan.GetCoordinates(TargetCoords));
        var heldUid = await PlaceInHands(IdCard);
        var held = Comp<IdCardComponent>(heldUid);
        await Server.WaitPost(() => held.OriginalOwner = stranger);

        var (ownAccount, ownPin) = (own.AccountNumber, own.AtmPin);
        var (heldAccount, heldPin) = (held.AccountNumber, held.AtmPin);

        var notes = string.Join("\n", await RequestNotes());

        Assert.Multiple(() =>
        {
            Assert.That(notes, Does.Contain($"#{ownAccount}"), "Your own account number is missing");
            Assert.That(notes, Does.Contain($"PIN: {ownPin}"), "Your own PIN is missing");
            Assert.That(notes, Does.Not.Contain($"#{heldAccount}"), "Someone else's account number was shown");
            Assert.That(notes, Does.Not.Contain($"PIN: {heldPin}"), "Someone else's PIN was shown");
        });
    }
}
