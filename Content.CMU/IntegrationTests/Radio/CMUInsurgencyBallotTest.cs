using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Insurgency;
using Content.Server.CMU14.Round;
using Content.Shared.CMU14.Insurgency;
using Content.Shared.CMU14.Insurgency.Selection;
using Content.Shared.CMU14.util;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
public sealed class CMUInsurgencyBallotTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Destructive = true };

    [Test]
    public async Task CellMembersVoteForAnAvailableFactionPrototype()
    {
        var map = await Pair.CreateTestMap();
        var recruits = await Server.AddDummySessions(2);
        await Server.WaitAssertion(() =>
        {
            Server.System<PlatoonSpawnRuleSystem>().SelectedGovforPlatoon = SProtoMan.Index<PlatoonPrototype>("UPP");
            var ballot = Server.System<InsurgencyFactionSelectionSystem>();
            Assert.That(ballot.TryStartBallot(), Is.True);
            var lead = ServerSession!;
            foreach (var member in recruits.Append(lead))
            {
                var body = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                SEntMan.EnsureComponent<InsurgencyPendingFactionSelectionComponent>(body);
                Server.PlayerMan.SetAttachedEntity(member, body);
            }
            var clf = SProtoMan.Index<CMUInsurgencyFactionPrototype>("CMUInsforCLF");
            var atnr = SProtoMan.Index<CMUInsurgencyFactionPrototype>("CMUInsforATNR");
            var unavailable = SProtoMan.Index<CMUInsurgencyFactionPrototype>("CMUInsforUPPCLF");
            Assert.That(ballot.Vote(lead, clf.BallotId), Is.True);
            Assert.That(ballot.Vote(lead, unavailable.BallotId), Is.False);
            Assert.That(ballot.Vote(recruits[0], atnr.BallotId), Is.True);
            ballot.SetExclusions(recruits[1], [atnr.BallotId]);
            Assert.That(ballot.Vote(recruits[1], atnr.BallotId), Is.False);
            ballot.SetExclusions(recruits[1], []);
            Assert.That(ballot.Vote(recruits[1], atnr.BallotId), Is.True);
        });

        await Pair.RunSeconds(46);
        await Server.WaitAssertion(() =>
        {
            var atnr = SProtoMan.Index<CMUInsurgencyFactionPrototype>("CMUInsforATNR");
            var active = Server.System<InsurgencyFactionApplySystem>().GetActiveFaction();
            Assert.That(active?.Metadata.Title, Is.EqualTo(atnr.Definition.Metadata.Title));
        });
    }
}
