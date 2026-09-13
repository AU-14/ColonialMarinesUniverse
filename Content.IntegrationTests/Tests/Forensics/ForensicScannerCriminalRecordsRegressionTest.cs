using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Forensics.Systems;
using Content.Server.Station.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Maps;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Forensics;

[TestFixture]
[TestOf(typeof(ForensicScannerCriminalRecordsSystem))]
public sealed class ForensicScannerCriminalRecordsRegressionTest : GameTest
{
    private const string MapId = "ForensicScannerRecordsMap";

    [TestPrototypes]
    private const string Prototypes = $"""
- type: gameMap
  id: {MapId}
  minPlayers: 0
  mapName: {MapId}
  mapPath: /Maps/Test/empty.yml
  stations:
    Records:
      stationProto: StandardNanotrasenStation
      components:
      - type: StationNameSetup
        mapNameTemplate: Records

- type: entity
  id: ForensicScannerRecordsNonHumanoid
""";

    [Test]
    public async Task PostScanCreatesAndUpdatesOneGeneralAndCriminalRecord()
    {
        var map = await Pair.CreateTestMap();
        var stations = Server.System<StationSystem>();
        var records = Server.System<StationRecordsSystem>();
        var metadata = Server.System<MetaDataSystem>();
        var gameMap = SProtoMan.Index<GameMapPrototype>(MapId);

        EntityUid station = default;
        EntityUid scanner = default;
        EntityUid humanoid = default;
        EntityUid nonHumanoid = default;

        await Server.WaitPost(() =>
        {
            station = stations.InitializeNewStation(
                gameMap.Stations["Records"],
                [map.Grid.Owner],
                "Records",
                gameMap);
            scanner = SEntMan.SpawnEntity("ForensicScanner", map.GridCoords);
            humanoid = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            nonHumanoid = SEntMan.SpawnEntity("ForensicScannerRecordsNonHumanoid", map.GridCoords);
            metadata.SetEntityName(humanoid, "Forensic Merge Subject");
            metadata.SetEntityName(nonHumanoid, "Not A Humanoid");
        });

        try
        {
            await Server.WaitAssertion(() =>
            {
                var scannerComponent = SEntMan.GetComponent<ForensicScannerComponent>(scanner);
                var dna = SEntMan.EnsureComponent<DnaComponent>(humanoid);
                var fingerprint = SEntMan.EnsureComponent<FingerprintComponent>(humanoid);
                dna.DNA = null;
                fingerprint.Fingerprint = null;
                scannerComponent.DNAs.Clear();
                scannerComponent.DNAs.Add("DNA-ONE");
                scannerComponent.Fingerprints.Clear();
                scannerComponent.Fingerprints.Add("PRINT-ONE");

                var firstScan = new ForensicScannerScannedEvent(humanoid);
                SEntMan.EventBus.RaiseLocalEvent(scanner, ref firstScan);

                var general = records.GetRecordsOfType<GeneralStationRecord>(station).ToArray();
                var criminal = records.GetRecordsOfType<CriminalRecord>(station).ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(general, Has.Length.EqualTo(1));
                    Assert.That(criminal, Has.Length.EqualTo(1));
                    Assert.That(general[0].Item2.Name, Is.EqualTo("Forensic Merge Subject"));
                    Assert.That(general[0].Item2.DNA, Is.EqualTo("DNA-ONE"));
                    Assert.That(general[0].Item2.Fingerprint, Is.EqualTo("PRINT-ONE"));
                    Assert.That(criminal[0].Item1, Is.EqualTo(general[0].Item1),
                        "the scanner must add criminal data to the same station record key");
                });

                scannerComponent.DNAs.Clear();
                scannerComponent.DNAs.Add("DNA-TWO");
                scannerComponent.Fingerprints.Clear();
                scannerComponent.Fingerprints.Add("PRINT-TWO");
                var rescan = new ForensicScannerScannedEvent(humanoid);
                SEntMan.EventBus.RaiseLocalEvent(scanner, ref rescan);

                general = records.GetRecordsOfType<GeneralStationRecord>(station).ToArray();
                criminal = records.GetRecordsOfType<CriminalRecord>(station).ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(general, Has.Length.EqualTo(1), "rescan must update rather than duplicate");
                    Assert.That(criminal, Has.Length.EqualTo(1), "rescan must not duplicate criminal data");
                    Assert.That(general[0].Item2.DNA, Is.EqualTo("DNA-TWO"));
                    Assert.That(general[0].Item2.Fingerprint, Is.EqualTo("PRINT-TWO"));
                });

                var nonHumanoidScan = new ForensicScannerScannedEvent(nonHumanoid);
                SEntMan.EventBus.RaiseLocalEvent(scanner, ref nonHumanoidScan);
                Assert.Multiple(() =>
                {
                    Assert.That(records.GetRecordsOfType<GeneralStationRecord>(station).ToArray(), Has.Length.EqualTo(1));
                    Assert.That(records.GetRecordsOfType<CriminalRecord>(station).ToArray(), Has.Length.EqualTo(1));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                SEntMan.DeleteEntity(scanner);
                SEntMan.DeleteEntity(humanoid);
                SEntMan.DeleteEntity(nonHumanoid);
            });
        }
    }
}
