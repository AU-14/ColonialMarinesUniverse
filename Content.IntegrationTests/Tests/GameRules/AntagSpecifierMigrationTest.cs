using Content.Server.Antag.Components;
using Content.Server.Antag.Selectors;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Threats;
using Content.Shared.Antag;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture] // CMU14
public sealed class AntagSpecifierMigrationTest : AntagTest
// CMU14 Owned Class / Test Fixture: I sure love a good pinning test
{
    private static readonly Robust.Shared.Prototypes.ProtoId<AntagSpecifierPrototype> CLFVeteranPrototype = "CLFVeteran";

    private static readonly Robust.Shared.Prototypes.ProtoId<AntagSpecifierPrototype> AntagMigrationReplacementPrototype = "AntagMigrationReplacement";
    private static readonly Robust.Shared.Prototypes.ProtoId<AntagJobBlacklistPrototype> AllGovforJobsPrototype = "AllGovforJobs";
    private static readonly Robust.Shared.Prototypes.ProtoId<AntagJobBlacklistPrototype> AllOpforJobsPrototype = "AllOpforJobs";

    private static readonly Dictionary<string, string> Roles = new()
    {
        ["RunawaySynth"] = "RunawaySynthRole",
        ["Fugitive"] = "FugitiveRole",
        ["DrugDealer"] = "DrugDealerRole",
        ["CorporateSpy"] = "CorporateSpyRole",
        ["CLFVeteran"] = "CLFVeteranRole",
        ["StrikeOrganizer"] = "StrikeOrganizerRole",
        ["Cannibal"] = "CannibalRole",
        ["SerialKiller"] = "SerialKillerRole",
        ["CLFSleeperAgent"] = "CLFSleeperAgentRole",
    };

    private static readonly Dictionary<string, string[]> Components = new()
    {
        ["RunawaySynth"] = ["RunawaySynth", "Synth", "ColonyBounty"],
        ["Fugitive"] = ["Fugitive", "ColonyBounty"],
        ["DrugDealer"] = ["DrugDealer", "ColonyBounty"],
        ["CorporateSpy"] = ["CorporateAgent", "ColonyBounty", "SynthGunAccess"],
        ["CLFVeteran"] = ["CLFVeteran", "ColonyBounty"],
        ["StrikeOrganizer"] = ["StrikeOrganizer", "SynthGunAccess"],
        ["Cannibal"] = ["Cannibal", "SynthGunAccess"],
        ["SerialKiller"] = ["SerialKiller", "ColonyBounty"],
        ["CLFSleeperAgent"] = ["CLFSleeperAgent"],
    };

    [TestPrototypes]
    private const string Prototypes = @"
- type: antagSpecifier
  id: AntagMigrationReplacement
  prefRoles: [ RunawaySynthRole ]
  jobBlacklist: [ AU14JobCLFGuerilla ]
  jobBlacklistGroup: [ AllGovforJobs, AllOpforJobs ]

- type: entity
  id: AntagMigrationReplacementRule
  parent: BaseGameRule
  components:
  - type: GameRule
  - type: AntagSelection
    selectionTime: JobsAssigned
    antags:
    - !type:FixedAntagCount
      proto: AntagMigrationReplacement
";

    [Test]
    public async Task GroupAndDirectJobDenialsDoNotConsumeTheReplacementSlot()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var session = ServerSession!;
            var mind = SMind.GetOrCreateMind(session.UserId);
            var roles = Server.System<SharedRoleSystem>();
            var candidate = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SMind.TransferTo(mind.Owner, candidate);
            var ruleUid = SEntMan.Spawn("AntagMigrationReplacementRule");
            var selection = SEntMan.GetComponent<AntagSelectionComponent>(ruleUid);
            var rule = new Entity<AntagSelectionComponent>(ruleUid, selection);
            var replacement = SProtoMan.Index<AntagSpecifierPrototype>(AntagMigrationReplacementPrototype);
            var veteran = SProtoMan.Index<AntagSpecifierPrototype>(CLFVeteranPrototype);

            roles.MindAddJobRole(mind.Owner, jobPrototype: "AU14JobGOVFORPlatCo");
            Assert.Multiple(() =>
            {
                Assert.That(AntagSys.IsMindValid(session, replacement), Is.False,
                    "a grouped GOVFOR job must be denied");
                Assert.That(AntagSys.TryMakeAntag(rule, replacement, session, checkPref: false), Is.False);
                Assert.That(selection.PreSelectedSessions, Is.Empty,
                    "a denied candidate must not consume the one fixed slot");
            });

            roles.MindRemoveRole<JobRoleComponent>(mind.Owner);
            roles.MindAddJobRole(mind.Owner, jobPrototype: "AU14JobCLFGuerilla");
            Assert.That(AntagSys.IsMindValid(session, veteran), Is.False,
                "the veteran's direct CLF job blacklist must remain effective alongside grouped restrictions");

            roles.MindRemoveRole<JobRoleComponent>(mind.Owner);
            roles.MindAddJobRole(mind.Owner, jobPrototype: "AU14JobCivilianFoodServiceWorker");
            Assert.Multiple(() =>
            {
                Assert.That(AntagSys.IsMindValid(session, replacement), Is.True);
                Assert.That(AntagSys.TryMakeAntag(rule, replacement, session, checkPref: false), Is.True,
                    "the same fixed slot must remain available to the next eligible candidate");
                Assert.That(selection.PreSelectedSessions["AntagMigrationReplacement"],
                    Is.EquivalentTo(new[] { session }));
            });

            var govfor = SProtoMan.Index<AntagJobBlacklistPrototype>(AllGovforJobsPrototype);
            var opfor = SProtoMan.Index<AntagJobBlacklistPrototype>(AllOpforJobsPrototype);
            var expectedGroupJobs = govfor.Jobs.Concat(opfor.Jobs).ToHashSet();
            expectedGroupJobs.Add("AU14JobCLFGuerilla");
            var playerJobs = AntagSys.GetAntagJobs(session);
            var allPlayersJobs = AntagSys.GetAntagJobs();
            Assert.Multiple(() =>
            {
                Assert.That(playerJobs.Whitelist, Is.Null);
                Assert.That(playerJobs.Blacklist, Is.EquivalentTo(expectedGroupJobs),
                    "the player overload must surface grouped restrictions before assignment");
                Assert.That(allPlayersJobs.TryGetValue(session, out var jobs), Is.True);
                Assert.That(jobs.Whitelist, Is.Null);
                Assert.That(jobs.Blacklist, Is.EquivalentTo(expectedGroupJobs),
                    "the all-players overload must surface the same grouped restrictions");
            });

            var unrelated = (ProtoId<JobPrototype>) "AU14JobCivilianFoodServiceWorker";
            allPlayersJobs[session].Blacklist!.Add(unrelated);
            Assert.Multiple(() =>
            {
                Assert.That(govfor.Jobs, Does.Not.Contain(unrelated));
                Assert.That(opfor.Jobs, Does.Not.Contain(unrelated));
                Assert.That(replacement.JobBlacklist,
                    Is.EquivalentTo(new[] { (ProtoId<JobPrototype>) "AU14JobCLFGuerilla" }),
                    "collecting grouped restrictions must not mutate the specifier's direct set");
                Assert.That(playerJobs.Blacklist, Does.Not.Contain(unrelated),
                    "results from the two overloads must not alias each other or prototype storage");
            });
        });
    }
}
