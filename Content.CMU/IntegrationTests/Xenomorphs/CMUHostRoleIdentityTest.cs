using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.IdentityManagement;
using Content.Shared._RMC14.Roles;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Name;
using Content.Shared.IdentityManagement;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

// Seed the viewer's number to make the identity suffix deterministic.
#pragma warning disable RA0002
[TestFixture]
public sealed class CMUHostRoleIdentityTest : GameTest
{
    [Test]
    public async Task RoleIdentityReplicatesToClient()
    {
        var map = await Pair.CreateTestMap();
        EntityUid host = default;
        EntityUid viewer = default;
        EntityUid? originalAttached = null;
        NetEntity hostNet = default;
        NetEntity viewerNet = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                originalAttached = ServerSession!.AttachedEntity;
                host = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                viewer = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords);
                SEntMan.EnsureComponent<OriginalRoleComponent>(host).Job = "AU14JobGOVFORPlatCo";
                Server.PlayerMan.SetAttachedEntity(ServerSession, viewer);
                hostNet = SEntMan.GetNetEntity(host);
                viewerNet = SEntMan.GetNetEntity(viewer);
            });
            await Pair.RunTicksSync(10);

            string expected = string.Empty;
            await Server.WaitAssertion(() =>
            {
                expected = Identity.Name(host, SEntMan, viewer).Name;
                Assert.That(expected, Does.Contain("Empress Host"));
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(Identity.Name(CEntMan.GetEntity(hostNet), CEntMan, CEntMan.GetEntity(viewerNet)).Name,
                    Is.EqualTo(expected));
            });

            await Server.WaitAssertion(() =>
            {
                var role = SEntMan.GetComponent<OriginalRoleComponent>(host);
                role.Job = "AU14JobGOVFORRadioTrafficOperator";
                SEntMan.Dirty(host, role);
                expected = Identity.Name(host, SEntMan, viewer).Name;
                Assert.That(expected, Does.Contain("Antennae Host"));
            });
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                Assert.That(Identity.Name(CEntMan.GetEntity(hostNet), CEntMan, CEntMan.GetEntity(viewerNet)).Name,
                    Is.EqualTo(expected));
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, originalAttached));
        }
    }

    [TestCase("CMXenoDrone")]
    [TestCase("CMU14XenoNeomorph")]
    public async Task HostRolesKeepNumbersAndRespectViewerIdentity(string viewerPrototype)
    {
        var map = await Pair.CreateTestMap();
        EntityUid host = default;
        EntityUid viewer = default;
        EntityUid humanViewer = default;
        await Server.WaitAssertion(() =>
        {
            host = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            humanViewer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.System<MetaDataSystem>().SetEntityName(host, "Host true name");
        });
        await Pair.RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            var localization = Server.ResolveDependency<ILocalizationManager>();
            viewer = SEntMan.SpawnEntity(viewerPrototype, map.GridCoords);
            SEntMan.EnsureComponent<XenoNameComponent>(viewer).Number = 123;
            var role = SEntMan.EnsureComponent<OriginalRoleComponent>(host);

            (string Job, string Name)[] roles =
            [
                ("AU14JobGOVFORPlatCo", "Empress Host"),
                ("AU14JobOPFORPlatCo", "Empress Host"),
                ("AU14JobGOVFORPlatCoUPP", "Empress Host"),
                ("AU14JobGOVFORAdjutant", "Queen Host"),
                ("AU14JobGOVFORRadioTelephoneOperator", "Leader Host"),
                ("AU14JobGOVFORSquadSergeant", "Leader Host"),
                ("AU14JobGOVFORSectionSergeant", "Leader Host"),
                ("AU14JobGOVFORRadioTrafficOperator", "Antennae Host"),
                ("AU14JobOPFORRadioTrafficOperator", "Antennae Host"),
                ("AU14JobGOVFORSquadAutomaticRifleman", "Fast-Spitter Host"),
                ("AU14JobGOVFORSquadAutomaticRiflemanWYPMC", "Fast-Spitter Host"),
                ("AU14JobCLFMachineGunner", "Fast-Spitter Host"),
                ("CMCommandingOfficer", "Empress Host"),
                ("CMExecutiveOfficer", "Queen Host"),
                ("CMFireteamLeader", "Leader Host"),
                ("CMSquadLeader", "Leader Host"),
                ("CMSmartGunOperator", "Fast-Spitter Host"),
                ("CMHospitalCorpsman", "Healer Host"),
                ("CMCombatTech", "Builder Host"),
                ("CMRifleman", "Warrior Host"),
                ("CMWeaponsSpecialist", "Heavy-Spitter Host"),
                ("CMPilotDropship", "Sky Host"),
                ("CMQuartermaster", "Provider Host"),
                ("CMResearcher", "Experimenter Host"),
                ("CMMilitaryPolice", "Guard Host"),
            ];

            string WithNumber(string name) => localization.GetString("rmc-xeno-name-number",
                ("baseName", name),
                ("prefix", "XX"),
                ("number", 123),
                ("postfix", string.Empty));

            foreach (var (job, name) in roles)
            {
                Assert.That(SProtoMan.HasIndex<Content.Shared.Roles.JobPrototype>(job), Is.True, job);
                role.Job = job;
                Assert.That(Identity.Name(host, SEntMan, viewer).Name, Is.EqualTo(WithNumber(name)), job);
            }

            Assert.That(Identity.Name(host, SEntMan, humanViewer).Name, Is.EqualTo("Host true name"));
            Assert.That(Identity.Name(host, SEntMan).Name, Is.EqualTo("Host true name"));

            role.Job = null;
            Assert.That(Identity.Name(host, SEntMan, viewer).Name,
                Is.EqualTo(WithNumber(localization.GetString("cm-chatsan-replacement-human"))));
            role.Job = "CMSurvivor";
            Assert.That(Identity.Name(host, SEntMan, viewer).Name,
                Is.EqualTo(WithNumber(localization.GetString("cm-chatsan-replacement-human"))));

            role.Job = "CMCommandingOfficer";
            var hive = SEntMan.SpawnEntity("CMXenoHive", map.GridCoords);
            var hives = Server.System<SharedXenoHiveSystem>();
            hives.SetHive(viewer, hive);
            hives.SetHiveIndividualAlly(host, hive, true);
            Assert.That(Identity.Name(host, SEntMan, viewer).Name, Is.EqualTo("Host true name"));
            hives.SetHiveIndividualAlly(host, hive, false);
            Assert.That(Identity.Name(host, SEntMan, viewer).Name, Is.EqualTo(WithNumber("Empress Host")));
            SEntMan.GetComponent<HiveComponent>(hive).Corrupted = true;
            Assert.That(Identity.Name(host, SEntMan, viewer).Name, Is.EqualTo("Host true name"));
            SEntMan.GetComponent<HiveComponent>(hive).Corrupted = false;

            SEntMan.EnsureComponent<XenoOmitNumberComponent>(viewer);
            var withoutNumber = localization.GetString("rmc-xeno-name",
                ("baseName", "Empress Host"),
                ("prefix", "XX"),
                ("postfix", string.Empty));
            Assert.That(Identity.Name(host, SEntMan, viewer).Name, Is.EqualTo(withoutNumber));

            var synth = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<SynthComponent>(synth);
            SEntMan.EnsureComponent<OriginalRoleComponent>(synth).Job = "CMCommandingOfficer";
            var synthName = SEntMan.GetComponent<FixedIdentityComponent>(synth).Name;
            var expectedSynth = localization.GetString("rmc-xeno-name",
                ("baseName", localization.GetString(synthName!.Value)),
                ("prefix", "XX"),
                ("postfix", string.Empty));
            Assert.That(Identity.Name(synth, SEntMan, viewer).Name, Is.EqualTo(expectedSynth));
            SEntMan.DeleteEntity(viewer);
        });
    }
}
