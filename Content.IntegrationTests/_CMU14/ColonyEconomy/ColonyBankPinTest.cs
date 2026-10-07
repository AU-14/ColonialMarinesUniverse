using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.Access.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     PIN rules that live on the ID card itself: lockout after wrong guesses, recovery once the
///     lock runs out, and keeping PINs off every client.
/// </summary>
public sealed class ColonyBankPinTest : InteractionTest
{
    private const string IdCard = "AU14IDCardCLFCivilian";

    private async Task<(EntityUid Uid, IdCardComponent Card, int Pin)> SpawnCard()
    {
        var uid = await SpawnEntity(IdCard, SEntMan.GetCoordinates(TargetCoords));
        var card = SEntMan.GetComponent<IdCardComponent>(uid);
        return (uid, card, card.AtmPin);
    }

    private static int WrongPin(int pin) => pin == 1111 ? 2222 : 1111;

    [Test]
    public async Task WrongPinsLockTheCardUntilTheLockRunsOut()
    {
        var bank = SEntMan.System<ColonyBankSystem>();
        var (uid, card, pin) = await SpawnCard();
        var wrong = WrongPin(pin);
        TimeSpan unlockAt = default;

        await Server.WaitAssertion(() =>
        {
            for (var i = 1; i < ColonyBankSystem.MaxPinAttempts; i++)
            {
                Assert.That(bank.TryAuthenticatePin(uid, card, wrong, out var early), Is.False);
                Assert.That(early, Is.False, $"Card locked after only {i} wrong PIN(s)");
            }

            Assert.That(bank.TryAuthenticatePin(uid, card, wrong, out var locked), Is.False);
            Assert.That(locked, "The last allowed wrong PIN did not lock the card");
            Assert.That(bank.IsLocked(card, out var until), "Card is not reported as locked");
            unlockAt = until!.Value;

            // While locked even the right PIN is refused.
            Assert.That(bank.TryAuthenticatePin(uid, card, pin, out _), Is.False, "Right PIN worked on a locked card");
        });

        // Still locked just before the lock is due to end...
        await RunSeconds((float) (unlockAt - STiming.CurTime).TotalSeconds - 2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(bank.IsLocked(card, out _), "Lock ended early");
            Assert.That(bank.TryAuthenticatePin(uid, card, pin, out _), Is.False);
        });

        // ...and usable again once it has.
        await RunSeconds(4f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(bank.IsLocked(card, out _), Is.False, "Card still locked after the lock ran out");
            Assert.That(bank.TryAuthenticatePin(uid, card, pin, out _), "Right PIN refused after the lock ran out");

            // A fresh count: a single mistake no longer locks the card.
            Assert.That(bank.TryAuthenticatePin(uid, card, wrong, out var relocked), Is.False);
            Assert.That(relocked, Is.False, "One mistake re-locked the card after it recovered");
        });
    }

    [Test]
    public async Task RightPinForgivesEarlierMistakes()
    {
        var bank = SEntMan.System<ColonyBankSystem>();
        var (uid, card, pin) = await SpawnCard();
        var wrong = WrongPin(pin);

        await Server.WaitAssertion(() =>
        {
            for (var i = 1; i < ColonyBankSystem.MaxPinAttempts; i++)
                bank.TryAuthenticatePin(uid, card, wrong, out _);

            Assert.That(bank.TryAuthenticatePin(uid, card, pin, out _), "Right PIN refused after a few mistakes");

            // The earlier mistakes no longer count towards a lock.
            for (var i = 1; i < ColonyBankSystem.MaxPinAttempts; i++)
            {
                Assert.That(bank.TryAuthenticatePin(uid, card, wrong, out var locked), Is.False);
                Assert.That(locked, Is.False, "Mistakes from before a successful login still counted");
            }

            Assert.That(bank.IsLocked(card, out _), Is.False);
        });
    }

    /// <summary>
    ///     The PIN and account number stay on the server: a client that can see a card never
    ///     receives them, so a modified client cannot read other players' PINs.
    /// </summary>
    [Test]
    public async Task PinsAreNeverSentToClients()
    {
        var (uid, card, pin) = await SpawnCard();
        var account = card.AccountNumber;
        await RunTicks(10);

        var clientUid = ToClient(uid);
        Assert.That(CEntMan.TryGetComponent<IdCardComponent>(clientUid, out var clientCard), "Card never reached the client");
        var (clientPin, clientAccount) = (clientCard!.AtmPin, clientCard.AccountNumber);

        Assert.Multiple(() =>
        {
            Assert.That(pin, Is.Not.Zero, "Server card has no PIN");
            Assert.That(account, Is.Not.Zero, "Server card has no account number");
            Assert.That(clientPin, Is.Zero, "The PIN was sent to a client");
            Assert.That(clientAccount, Is.Zero, "The account number was sent to a client");
        });
    }
}
