using Content.IntegrationTests.Fixtures;
using Content.Server.Voting.Managers;
using Content.Shared.CCVar;
using Content.Shared.Voting;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests.Voting;

[TestFixture]
public sealed class PlayerVoteCreationTest : GameTest
{
    [Test]
    public async Task PlayersCannotCallStandardVotes()
    {
        await Server.WaitAssertion(() =>
        {
            var cfg = Server.ResolveDependency<IConfigurationManager>();
            var voteManager = Server.ResolveDependency<IVoteManager>();
            var originalVoteEnabled = cfg.GetCVar(CCVars.VoteEnabled);

            cfg.SetCVar(CCVars.VoteEnabled, true);

            try
            {
                Assert.That(ServerSession, Is.Not.Null);
                Assert.That(voteManager.CanCallVote(ServerSession!), Is.False);

                foreach (var type in Enum.GetValues<StandardVoteType>())
                    Assert.That(voteManager.CanCallVote(ServerSession!, type), Is.False, $"{type} votes must be disabled");

                var activeVotes = voteManager.ActiveVotes.Count();
                voteManager.CreateStandardVote(ServerSession!, StandardVoteType.Restart);
                Assert.That(voteManager.ActiveVotes.Count(), Is.EqualTo(activeVotes));
            }
            finally
            {
                cfg.SetCVar(CCVars.VoteEnabled, originalVoteEnabled);
            }
        });
    }
}
