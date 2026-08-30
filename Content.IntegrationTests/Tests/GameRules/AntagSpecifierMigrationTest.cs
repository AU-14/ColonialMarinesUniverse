using Content.Server.Antag.Components;
using Content.Server.Antag.Selectors;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Threats;
using Content.Shared.Antag;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
public sealed class AntagSpecifierMigrationTest : AntagTest
{
    private static readonly string[] MigratedRules =
    [
        "RunawaySynth",
        "SubvertedSynth",
        "Fugitive",
        "DrugDealer",
        "CorporateSpy",
        "CLFVeteran",
        "StrikeOrganizer",
        "Cannibal",
        "SerialKiller",
        "CLFSleeperAgent",
    ];

    private static readonly HashSet<string> GroupRestricted =
    [
        "RunawaySynth",
        "Fugitive",
        "DrugDealer",
        "CorporateSpy",
        "CLFVeteran",
        "StrikeOrganizer",
        "SerialKiller",
    ];

    private static readonly Dictionary<string, string> Roles = new()
    {
        ["RunawaySynth"] = "RunawaySynthRole",
        ["SubvertedSynth"] = "CLFSubvertedSynthRole",
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
        ["RunawaySynth"] = ["RunawaySynth", "Skills", "Synth"],
        ["SubvertedSynth"] = [],
        ["Fugitive"] = ["Fugitive"],
        ["DrugDealer"] = ["DrugDealer"],
        ["CorporateSpy"] = ["CorporateSpy"],
        ["CLFVeteran"] = ["CLFVeteran", "Skills"],
        ["StrikeOrganizer"] = ["StrikeOrganizer"],
        ["Cannibal"] = ["Cannibal"],
        ["SerialKiller"] = ["SerialKiller"],
        ["CLFSleeperAgent"] = ["CLFSleeperAgent"],
    };

    private static readonly Dictionary<string, string?> MindRoles = new()
    {
        ["RunawaySynth"] = null,
        ["SubvertedSynth"] = null,
        ["Fugitive"] = "MindRoleFugitive",
        ["DrugDealer"] = "MindRoleDrugDealer",
        ["CorporateSpy"] = "MindRoleCorporateSpy",
        ["CLFVeteran"] = "MindRoleCLFVeteran",
        ["StrikeOrganizer"] = "MindRoleStrikeOrganizer",
        ["Cannibal"] = "MindRoleCannibal",
        ["SerialKiller"] = "MindRoleSerialKiller",
        ["CLFSleeperAgent"] = "MindRoleCLFSleeperAgent",
    };

    private static readonly Dictionary<string, string?> StartingGear = new()
    {
        ["RunawaySynth"] = null,
        ["SubvertedSynth"] = null,
        ["Fugitive"] = "AU14GearFugitive",
        ["DrugDealer"] = "AU14GearDrugDealer",
        ["CorporateSpy"] = "AU14GearCorporateSpy",
        ["CLFVeteran"] = "AU14GearCLFVeteran",
        ["StrikeOrganizer"] = "AU14GearStrikeOrganizer",
        ["Cannibal"] = null,
        ["SerialKiller"] = null,
        ["CLFSleeperAgent"] = null,
    };

    private static readonly Dictionary<string, (string Text, Color Color)> Briefings = new()
    {
        ["RunawaySynth"] = ("runawaysynth-role-greeting", Color.CornflowerBlue),
        ["Fugitive"] = ("fugitive-role-greeting", Color.CornflowerBlue),
        ["DrugDealer"] = ("dealer-role-greeting", Color.MediumVioletRed),
        ["CorporateSpy"] = ("spy-role-greeting", Color.DarkSlateGray),
        ["CLFVeteran"] = ("clfveteran-role-greeting", Color.OliveDrab),
        ["StrikeOrganizer"] = ("strikeorganizer-role-greeting", Color.DarkRed),
        ["Cannibal"] = ("cannibal-role-greeting", Color.DarkRed),
        ["SerialKiller"] = ("serialkiller-role-greeting", Color.DarkRed),
        ["CLFSleeperAgent"] = ("clf-sleeper-agent-role-greeting", Color.OliveDrab),
    };

    [TestPrototypes]
    private const string Prototypes = @"
- type: antagSpecifier
  id: AntagMigrationReplacement
  prefRoles: [ RunawaySynthRole ]
  jobBlacklist: [ AU14JobCLFGuerilla ]
  jobBlacklistGroup: [ AllGovforJobs ]

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
    public async Task TenRulesUseSameIdSpecifiersWithFullFieldParity()
    {
        await Server.WaitAssertion(() =>
        {
            foreach (var id in MigratedRules)
            {
                var specifier = SProtoMan.Index<AntagSpecifierPrototype>(id);
                var rule = SProtoMan.Index<EntityPrototype>(id);
                Assert.That(rule.TryGetComponent<AntagSelectionComponent>(out var selection, SEntMan.ComponentFactory),
                    Is.True, id);
                var selector = selection!.Antags.Single();
                Assert.Multiple(() =>
                {
                    Assert.That(selection.SelectionTime, Is.EqualTo(AntagSelectionTime.JobsAssigned), id);
                    Assert.That(selector, Is.TypeOf<FixedAntagCount>(), id);
                    Assert.That(((FixedAntagCount) selector).Count, Is.EqualTo(1), id);
                    Assert.That(selector.Proto.Id, Is.EqualTo(id), id);
                    Assert.That(specifier.PrefRoles.Select(role => role.Id), Is.EqualTo(new[] { Roles[id] }), id);
                    Assert.That(specifier.Components.Keys, Is.EquivalentTo(Components[id]), id);
                    Assert.That(specifier.StartingGear?.Id, Is.EqualTo(StartingGear[id]), id);
                });

                if (MindRoles[id] is { } mindRole)
                    Assert.That(specifier.MindRoles?.Select(role => role.Id), Is.EqualTo(new[] { mindRole }), id);
                else
                    Assert.That(specifier.MindRoles, Is.Null, id);

                if (GroupRestricted.Contains(id))
                {
                    Assert.That(specifier.JobBlacklistGroup?.Select(group => group.Id),
                        Is.EquivalentTo(new[] { "AllGovforJobs", "AllOpforJobs" }), id);
                }
                else
                {
                    Assert.That(specifier.JobBlacklistGroup, Is.Null, id);
                }

                if (Briefings.TryGetValue(id, out var briefing))
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(specifier.Briefing, Is.Not.Null, id);
                        Assert.That(specifier.Briefing!.Value.Text?.Id, Is.EqualTo(briefing.Text), id);
                        Assert.That(specifier.Briefing.Value.Color, Is.EqualTo(briefing.Color), id);
                        Assert.That(specifier.Briefing.Value.Sound, Is.Not.Null, id);
                    });
                }
                else
                {
                    Assert.That(specifier.Briefing, Is.Null, id);
                }
            }

            var veteran = SProtoMan.Index<AntagSpecifierPrototype>("CLFVeteran");
            Assert.That(veteran.JobBlacklist?.Select(job => job.Id), Is.EquivalentTo(new[]
            {
                "AU14JobCLFGuerilla",
                "AU14JobCLFSapper",
                "AU14JobCLFCellLeader",
                "AU14JobCLFRadioOperator",
                "AU14JobCLFPhysician",
                "AU14JobCLFSurgeon",
            }));
            Assert.That(SProtoMan.Index<AntagSpecifierPrototype>("Cannibal").JobWhitelist?.Select(job => job.Id),
                Is.EquivalentTo(new[] { "AU14JobCivilianFoodServiceWorker" }));
            Assert.That(SProtoMan.Index<AntagSpecifierPrototype>("CLFSleeperAgent").JobWhitelist?.Select(job => job.Id),
                Is.EquivalentTo(new[]
                {
                    "AU14JobGOVFORSquadSergeant",
                    "AU14JobGOVFORSectionSergeant",
                    "AU14JobGOVFORPlatOp",
                    "AU14JobGOVFORMilitaryPoliceMan",
                }));

            var runaway = SProtoMan.Index<AntagSpecifierPrototype>("RunawaySynth");
            var synth = (SynthComponent) runaway.Components["Synth"].Component;
            var runawaySkills = (SkillsComponent) runaway.Components["Skills"].Component;
            var veteranSkills = (SkillsComponent) veteran.Components["Skills"].Component;
            Assert.Multiple(() =>
            {
                Assert.That(synth.ChangeBrain, Is.False);
                Assert.That(synth.CanUseGuns, Is.True);
                Assert.That(synth.HideGeneration, Is.True);
                Assert.That(synth.UseHumanHealthIcons, Is.True);
                Assert.That(runawaySkills.Skills.ToDictionary(pair => pair.Key.Id, pair => pair.Value),
                    Is.EquivalentTo(new Dictionary<string, int>
                    {
                        ["RMCSkillCqc"] = 4,
                        ["RMCSkillEngineer"] = 4,
                        ["RMCSkillConstruction"] = 3,
                        ["RMCSkillOverwatch"] = 1,
                        ["RMCSkillMedical"] = 4,
                        ["RMCSkillSurgery"] = 3,
                        ["RMCSkillResearch"] = 1,
                        ["RMCSkillMeleeWeapons"] = 2,
                        ["RMCSkillPilot"] = 2,
                        ["RMCSkillPolice"] = 2,
                        ["RMCSkillFireman"] = 5,
                        ["RMCSkillFirearms"] = 2,
                        ["RMCSkillPowerLoader"] = 2,
                        ["RMCSkillVehicles"] = 2,
                        ["RMCSkillJtac"] = 3,
                        ["RMCSkillIntel"] = 2,
                        ["RMCSkillDomestics"] = 2,
                        ["RMCSkillNavigations"] = 1,
                    }));
                Assert.That(veteranSkills.Skills.ToDictionary(pair => pair.Key.Id, pair => pair.Value),
                    Is.EquivalentTo(new Dictionary<string, int>
                    {
                        ["RMCSkillFirearms"] = 3,
                        ["RMCSkillMeleeWeapons"] = 3,
                        ["RMCSkillCqc"] = 3,
                        ["RMCSkillEndurance"] = 3,
                        ["RMCSkillFireman"] = 3,
                        ["RMCSkillConstruction"] = 3,
                        ["RMCSkillLeadership"] = 3,
                        ["RMCSkillMedical"] = 3,
                        ["RMCSkillPolice"] = 1,
                        ["RMCSkillSurgery"] = 3,
                    }));
            });
        });
    }

    [Test]
    public async Task GamemodePreferencesOverrideAndOtherwiseFallBackToDefault()
    {
        await Server.WaitAssertion(() =>
        {
            var profile = HumanoidCharacterProfile.DefaultWithSpecies()
                .WithAntagPreferences([(ProtoId<AntagPrototype>) "RunawaySynthRole"])
                .WithGamemodeAntagPreference("cm", "RunawaySynthRole", false)
                .WithGamemodeAntagPreference("cm", "DrugDealerRole", true);

            Assert.Multiple(() =>
            {
                Assert.That(profile.GetAntagPreferencesForGamemode("cm").Select(role => role.Id),
                    Is.EquivalentTo(new[] { "DrugDealerRole" }));
                Assert.That(profile.GetAntagPreferencesForGamemode("other").Select(role => role.Id),
                    Is.EquivalentTo(new[] { "RunawaySynthRole" }));
                Assert.That(profile.GetAntagPreferencesForGamemode(null).Select(role => role.Id),
                    Is.EquivalentTo(new[] { "RunawaySynthRole" }));
            });
        });
    }

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
            var replacement = SProtoMan.Index<AntagSpecifierPrototype>("AntagMigrationReplacement");
            var veteran = SProtoMan.Index<AntagSpecifierPrototype>("CLFVeteran");

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

            var govfor = SProtoMan.Index<AntagJobBlacklistPrototype>("AllGovforJobs");
            var opfor = SProtoMan.Index<AntagJobBlacklistPrototype>("AllOpforJobs");
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
