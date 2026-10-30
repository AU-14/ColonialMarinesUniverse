#pragma warning disable RA0002 // The test sets up vendor points directly.

using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Squads.Foxtrot;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Squads;

[TestFixture]
[TestOf(typeof(CMUFoxtrotSquadSystem))]
public sealed class CMUFoxtrotSquadTest : GameTest
{
    private static readonly ProtoId<JobPrototype> GovforCombatTech = "AU14JobGOVFORSquadCombatTech";
    private static readonly ProtoId<JobPrototype> GovforSquadLeader = "AU14JobGOVFORSquadSergeant";
    private static readonly ProtoId<JobPrototype> GovforRto = "AU14JobGOVFORRadioTrafficOperator";
    private static readonly ProtoId<JobPrototype> GovforRifleman = "AU14JobGOVFORSquadRifleman";
    private static readonly ProtoId<JobPrototype> GovforCorpsman = "AU14JobGOVFORPlatoonCorpsman";
    private static readonly ProtoId<JobPrototype> GovforAutomaticRifleman = "AU14JobGOVFORSquadAutomaticRifleman";
    private static readonly ProtoId<JobPrototype> OpforCombatTech = "AU14JobOPFORSquadCombatTech";
    private static readonly ProtoId<JobPrototype> OpforSquadLeader = "AU14JobOPFORSquadSergeant";
    private static readonly ProtoId<JobPrototype> OpforRto = "AU14JobOPFORRadioTrafficOperator";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMUFoxtrotSquadTestStation
  components:
  - type: StationJobs
    availableJobs: {}
";

    [Test]
    public async Task SecuritySlotsAreAddedOnTopOfEachSide()
    {
        await Server.WaitAssertion(() =>
        {
            var foxtrot = Server.System<CMUFoxtrotSquadSystem>();

            var govfor = new Dictionary<ProtoId<JobPrototype>, int>
            {
                [GovforCombatTech] = 6,
                [GovforSquadLeader] = 3,
                [GovforRto] = 3,
                [GovforRifleman] = 75,
                [GovforCorpsman] = 6,
                // the other side's jobs are left alone
                [OpforCombatTech] = 6,
            };
            foxtrot.AddReservedSlots(govfor, RoundJobSide.Govfor);

            var opfor = new Dictionary<ProtoId<JobPrototype>, int>
            {
                [OpforCombatTech] = 6,
                [OpforSquadLeader] = 3,
                [OpforRto] = 0,
            };
            foxtrot.AddReservedSlots(opfor, RoundJobSide.Opfor);

            Assert.Multiple(() =>
            {
                Assert.That(govfor[GovforCombatTech], Is.EqualTo(8));
                Assert.That(govfor[GovforSquadLeader], Is.EqualTo(4));
                Assert.That(govfor[GovforRto], Is.EqualTo(4));
                Assert.That(govfor[GovforRifleman], Is.EqualTo(77));
                Assert.That(govfor[GovforCorpsman], Is.EqualTo(7));
                Assert.That(govfor[OpforCombatTech], Is.EqualTo(6));

                Assert.That(opfor[OpforCombatTech], Is.EqualTo(8));
                Assert.That(opfor[OpforSquadLeader], Is.EqualTo(4));
                Assert.That(opfor[OpforRto], Is.EqualTo(0), "a role the side does not have gets no Foxtrot slot");

                Assert.That(foxtrot.TryGetSquad(RoundJobSide.Govfor, out var govforSquad), Is.True);
                Assert.That(govforSquad.Id, Is.EqualTo("SquadGovforFoxtrot"));
                Assert.That(foxtrot.TryGetSquad(RoundJobSide.Opfor, out var opforSquad), Is.True);
                Assert.That(opforSquad.Id, Is.EqualTo("SquadOpforVictor"));
            });
        });
    }

    [Test]
    public async Task ReservedSlotsGoToSecurityFirst()
    {
        await Server.WaitAssertion(() =>
        {
            var foxtrot = Server.System<CMUFoxtrotSquadSystem>();
            var stationJobs = Server.System<StationJobsSystem>();
            var station = SEntMan.SpawnEntity("CMUFoxtrotSquadTestStation", MapCoordinates.Nullspace);

            // 6 regular combat technician slots plus the 2 Foxtrot ones.
            Assert.That(stationJobs.TrySetJobSlot(station, GovforCombatTech, 8, createSlot: true), Is.True);

            var users = new List<NetUserId>();
            NetUserId NewUser()
            {
                var user = new NetUserId(Guid.NewGuid());
                users.Add(user);
                return user;
            }

            void Join(NetUserId user, bool optedIn, CMUFoxtrotSlot expected, string because)
            {
                var slot = foxtrot.Pick(station, GovforCombatTech, user, optedIn);
                Assert.That(slot, Is.EqualTo(expected), because);
                if (slot is CMUFoxtrotSlot.Foxtrot or CMUFoxtrotSlot.OnlyFoxtrotLeft)
                    foxtrot.AddMember(station, GovforCombatTech, user);

                Assert.That(stationJobs.TryAssignJob(station, GovforCombatTech, user), Is.True);
            }

            var optedIn = NewUser();
            Assert.That(foxtrot.Pick(station, GovforCombatTech, optedIn, true), Is.EqualTo(CMUFoxtrotSlot.Foxtrot),
                "an opted-in character goes to Foxtrot while it has room");

            for (var i = 0; i < 6; i++)
                Join(NewUser(), false, CMUFoxtrotSlot.Regular, "the regular squads keep their own six slots");

            var latecomer = NewUser();
            Assert.That(foxtrot.Pick(station, GovforCombatTech, latecomer, false),
                Is.EqualTo(CMUFoxtrotSlot.OnlyFoxtrotLeft),
                "with the regular slots gone, only Foxtrot's slots are left");

            Join(optedIn, true, CMUFoxtrotSlot.Foxtrot, "the opted-in character still has room in Foxtrot");
            Join(latecomer, false, CMUFoxtrotSlot.OnlyFoxtrotLeft, "a confirmed late joiner takes a Foxtrot slot");

            Assert.That(foxtrot.Pick(station, GovforCombatTech, latecomer, false),
                Is.EqualTo(CMUFoxtrotSlot.OnlyFoxtrotLeft),
                "a player's own slot is not counted against them when they join again");

            Assert.Multiple(() =>
            {
                Assert.That(stationJobs.TryGetJobSlot(station, GovforCombatTech, out var left), Is.True);
                Assert.That(left, Is.EqualTo(0));
                Assert.That(foxtrot.Pick(station, GovforCombatTech, NewUser(), true), Is.EqualTo(CMUFoxtrotSlot.Regular),
                    "a full Foxtrot squad sends opted-in characters to the regular squads");
                Assert.That(foxtrot.Pick(station, GovforAutomaticRifleman, NewUser(), true), Is.EqualTo(CMUFoxtrotSlot.None),
                    "automatic riflemen have no Foxtrot slots");
            });

            // A refunded regular slot opens up for regular joiners again.
            stationJobs.RefundPlayerJobs(users[1]);
            Assert.That(foxtrot.Pick(station, GovforCombatTech, NewUser(), false), Is.EqualTo(CMUFoxtrotSlot.Regular));

            SEntMan.DeleteEntity(station);
        });
    }

    [Test]
    public async Task SecurityMembersGetTheirPerks()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var foxtrot = Server.System<CMUFoxtrotSquadSystem>();
            var skills = Server.System<SkillsSystem>();
            var squad = SEntMan.SpawnEntity("SquadGovforFoxtrot", map.GridCoords);

            var rto = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            foxtrot.ApplyPerks(rto, squad, SProtoMan.Index(GovforRto));

            var tech = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<CMVendorUserComponent>(tech).Points = 50;
            foxtrot.ApplyPerks(tech, squad, SProtoMan.Index(GovforCombatTech));

            var corpsman = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            foxtrot.ApplyPerks(corpsman, squad, SProtoMan.Index(GovforCorpsman));

            var rifleman = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            foxtrot.ApplyPerks(rifleman, squad, SProtoMan.Index(GovforRifleman));

            Assert.Multiple(() =>
            {
                Assert.That(skills.GetSkill(rto, "RMCSkillConstruction"), Is.EqualTo(3));
                Assert.That(skills.GetSkill(rto, "RMCSkillEngineer"), Is.EqualTo(3));
                Assert.That(skills.GetSkill(corpsman, "RMCSkillConstruction"), Is.EqualTo(3));
                Assert.That(skills.GetSkill(corpsman, "RMCSkillEngineer"), Is.EqualTo(3));
                Assert.That(skills.GetSkill(tech, "RMCSkillConstruction"), Is.EqualTo(0), "combat technicians keep their own skills");
                Assert.That(SEntMan.GetComponent<CMVendorUserComponent>(tech).Points, Is.EqualTo(65));
                Assert.That(skills.GetSkill(rifleman, "RMCSkillConstruction"), Is.EqualTo(3));
                Assert.That(skills.GetSkill(rifleman, "RMCSkillEngineer"), Is.EqualTo(3));
            });
        });
    }
    [Test]
    public async Task FoxtrotSquadExistsWithTheRegularSquads()
    {
        await Server.WaitAssertion(() =>
        {
            var foxtrot = Server.System<CMUFoxtrotSquadSystem>();

            // Any GOVFOR squad assignment creates the side's Foxtrot squad, even for someone who does not join it,
            // so overwatch consoles, the callsign directory and the ID console list it from the start.
            Assert.That(foxtrot.TryPickFoxtrotSquad(null, GovforRifleman, null, null, out _), Is.False);

            var found = false;
            var query = SEntMan.EntityQueryEnumerator<SquadTeamComponent, MetaDataComponent>();
            while (query.MoveNext(out _, out var team, out var meta))
            {
                if (meta.EntityPrototype?.ID != "SquadGovforFoxtrot")
                    continue;

                found = true;
                Assert.That(team.Group, Is.EqualTo("GOVFOR"));
                Assert.That(team.Radio?.Id, Is.EqualTo("radioGovforFoxtrot"));
            }

            Assert.That(found, Is.True);
        });
    }
}
