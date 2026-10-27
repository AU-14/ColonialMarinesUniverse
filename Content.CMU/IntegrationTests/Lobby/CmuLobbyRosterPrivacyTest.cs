using Content.IntegrationTests.Fixtures;
using Content.Server.CrewManifest;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.CMU14.Lobby;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Components;
using Content.Shared.StationRecords.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using ClientRosterSystem = Content.Client.CMU14.Lobby.CmuLobbyRosterSystem;

namespace Content.IntegrationTests.CMU14.Lobby;

public sealed class CmuLobbyRosterPrivacyTest : GameTest
{
    public override PoolSettings PoolSettings => new() { InLobby = true, Fresh = true, Destructive = true };

    [TestCase("Insurgency", true)]
    [TestCase("ColonyFall", false)]
    public async Task RosterSendsAnonymousClfSlotsOnlyInInsurgency(string preset, bool expectAnonymous)
    {
        EntityUid station = default;
        NetEntity netStation = default;
        ProtoId<JobPrototype> clfJob = "AU14JobCLFGuerilla";
        ProtoId<JobPrototype> civilianJob = "AU14JobCivilianEngineer";
        await Server.WaitPost(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CrewManifestWithoutEntity, true);
            SEntMan.System<GameTicker>().SetGamePreset(preset);
            station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StationDataComponent>(station);
            SEntMan.AddComponent<StationRecordsComponent>(station);
            netStation = SEntMan.GetNetEntity(station);
            var records = SEntMan.System<StationRecordsSystem>();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();
            foreach (var (name, job) in new[]
            {
                ("Hidden insurgent one", clfJob),
                ("Hidden insurgent two", clfJob),
                ("Public engineer", civilianJob),
            })
            {
                var prototype = SProtoMan.Index(job);
                var key = records.AddRecordEntry(station, new GeneralStationRecord
                {
                    Name = name,
                    JobPrototype = job.Id,
                    JobTitle = name + " custom title",
                    JobIcon = prototype.Icon.Id,
                    Squad = name + " squad",
                    SquadColor = Color.Red,
                    CMUPortrait = new CMURecordPortrait
                    {
                        Species = profile.Species, Sex = profile.Sex, Appearance = profile.Appearance,
                    },
                });
                records.Synchronize(key);
            }
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
        {
            var (_, manifest) = SEntMan.System<CrewManifestSystem>().GetCrewManifest(station);
            Assert.That(manifest!.Entries.Select(e => e.Name), Is.EqualTo(new[] { "Public engineer" }),
                "Anonymous join slots must not expose CLF identities through the regular manifest.");
        });

        CmuLobbyRosterResponse? response = null;
        void Receive(CmuLobbyRosterResponse value) => response = value;
        var roster = CEntMan.System<ClientRosterSystem>();
        await Client.WaitPost(() =>
        {
            roster.Updated += Receive;
            roster.Request(netStation);
        });
        try
        {
            await Pair.RunTicksSync(20);
            await Client.WaitAssertion(() =>
            {
                Assert.That(response, Is.Not.Null, "The server must answer the actual roster network request.");
                var civilian = response!.Entries.Single(e => e.Record.JobPrototype == civilianJob.Id);
                Assert.That(civilian.IdentityHidden, Is.False);
                Assert.That(civilian.Record.Name, Is.EqualTo("Public engineer"));
                Assert.That(civilian.Portrait, Is.Not.Null);
                var insurgents = response.Entries.Where(e => e.Record.JobPrototype == clfJob.Id).ToArray();
                Assert.That(insurgents.Length, Is.EqualTo(expectAnonymous ? 2 : 0));
                foreach (var entry in insurgents)
                {
                    Assert.That(entry.IdentityHidden, Is.True);
                    Assert.That(entry.Record.Name, Is.Empty);
                    Assert.That(entry.Record.JobTitle, Is.EqualTo(CProtoMan.Index(clfJob).LocalizedName));
                    Assert.That(entry.Record.Squad, Is.Null);
                    Assert.That(entry.Record.SquadColor, Is.Null);
                    Assert.That(entry.Portrait, Is.Null);
                    Assert.That(entry.Outfit, Is.Null);
                }
            });
        }
        finally
        {
            await Client.WaitPost(() => roster.Updated -= Receive);
        }
    }
}
