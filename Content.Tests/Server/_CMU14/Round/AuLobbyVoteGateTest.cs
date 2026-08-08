#nullable enable

using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.Round;

[TestFixture]
public sealed class AuLobbyVoteGateTest
{
    [TestCase(GameRunLevel.InRound)]
    [TestCase(GameRunLevel.PostRound)]
    [TestCase(GameRunLevel.PreRoundLobby)]
    public void NeverStartsOutsideEligiblePreRoundLobby(GameRunLevel runLevel)
    {
        var playerCount = runLevel == GameRunLevel.PreRoundLobby ? 19 : 20;

        Assert.That(
            AuLobbyVoteGate.ShouldStartVoteSequence(
                lobbyEnabled: true,
                runLevel,
                acceptingSelections: true,
                playerCount,
                minimumPlayers: 20),
            Is.False);
    }

    [Test]
    public void StartsAtMinimumPlayersInPreRoundLobby()
    {
        Assert.That(
            AuLobbyVoteGate.ShouldStartVoteSequence(
                lobbyEnabled: true,
                GameRunLevel.PreRoundLobby,
                acceptingSelections: true,
                playerCount: 20,
                minimumPlayers: 20),
            Is.True);
    }

    [Test]
    public void DoesNotStartAfterTheSelectionIsFrozen()
    {
        Assert.That(
            AuLobbyVoteGate.ShouldStartVoteSequence(
                lobbyEnabled: true,
                GameRunLevel.PreRoundLobby,
                acceptingSelections: false,
                playerCount: 20,
                minimumPlayers: 20),
            Is.False);
    }
}
