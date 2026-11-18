using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Insurgency;
using Content.Shared.CMU14.Insurgency;
using Content.Shared.CMU14.Insurgency.Selection;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
public sealed class CMUInsurgencyBallotTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Destructive = true };

    [Test]
    public async Task CellMembersVotesOutnumberTheFirstNomination()
    {
        var map = await Pair.CreateTestMap();
        var recruits = await Server.AddDummySessions(2);
        await Server.WaitAssertion(() =>
        {
            var ballot = Server.System<InsurgencyFactionSelectionSystem>();
            Assert.That(ballot.TryStartBallot(), Is.True);
            var lead = ServerSession!;
            foreach (var member in recruits.Append(lead))
            {
                var body = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                SEntMan.EnsureComponent<InsurgencyPendingFactionSelectionComponent>(body);
                Server.PlayerMan.SetAttachedEntity(member, body);
            }
            var first = new FactionDefinition();
            first.Metadata.Title = "First cell";
            ballot.Nominate(lead, first);
            var other = new FactionDefinition();
            other.Metadata.Title = "Second cell";
            ballot.Nominate(recruits[0], other);
            var id = ballot.CustomCandidates.Single(candidate => candidate.Definition == other).Id;
            ballot.SetExclusions(recruits[1], [id]);
            Assert.That(ballot.Vote(recruits[1], id), Is.False);
            ballot.SetExclusions(recruits[1], []);
            Assert.That(ballot.Vote(recruits[1], id), Is.True);
        });

        await Pair.RunSeconds(46);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<InsurgencyFactionApplySystem>().GetActiveFaction()?.Metadata.Title, Is.EqualTo("Second cell"));
        });
    }
}
