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

    private static readonly ProtoId<JobPrototype> GovforAutomaticRifleman = "AU14JobGOVFORSquadAutomaticRifleman";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMUFoxtrotSquadTestStation
  components:
  - type: StationJobs
    availableJobs: {}
";

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

}
