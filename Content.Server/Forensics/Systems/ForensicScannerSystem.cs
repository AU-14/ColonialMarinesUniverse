using Content.Server.CriminalRecords.Systems;
using Content.Server.Station.Systems;
using Content.Shared.CriminalRecords;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;

namespace Content.Server.Forensics.Systems;

/// <summary>
/// Adds successfully scanned humanoids to the station criminal-records console.
/// The scanner interaction and evidence collection remain owned by the shared scanner system.
/// </summary>
public sealed partial class ForensicScannerCriminalRecordsSystem : EntitySystem
{
    [Dependency] private CriminalRecordsConsoleSystem _criminalRecords = default!;
    [Dependency] private StationRecordsSystem _records = default!;
    [Dependency] private StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForensicScannerComponent, ForensicScannerScannedEvent>(OnScanCompleted);
    }

    private void OnScanCompleted(
        Entity<ForensicScannerComponent> scanner,
        ref ForensicScannerScannedEvent args)
    {
        var target = args.Target;

        if (!HasComp<HumanoidProfileComponent>(target))
            return;

        var station = _station.GetOwningStation(scanner);
        if (station == null)
            return;

        var name = MetaData(target).EntityName;
        var dna = CompOrNull<DnaComponent>(target)?.DNA;
        var fingerprint = CompOrNull<FingerprintComponent>(target)?.Fingerprint;

        if (string.IsNullOrEmpty(dna))
            dna = scanner.Comp.DNAs.Count > 0 ? string.Join(", ", scanner.Comp.DNAs) : "N/A";

        if (string.IsNullOrEmpty(fingerprint))
            fingerprint = scanner.Comp.Fingerprints.Count > 0
                ? string.Join(", ", scanner.Comp.Fingerprints)
                : "N/A";

        StationRecordKey key;
        if (_records.GetRecordByName(station.Value, name) is { } recordId)
        {
            key = new StationRecordKey(recordId, station.Value);
            if (_records.TryGetRecord<GeneralStationRecord>(key, out var general))
            {
                general.DNA = dna;
                general.Fingerprint = fingerprint;
            }
        }
        else
        {
            key = _records.AddRecordEntry(station.Value, new GeneralStationRecord
            {
                Name = name,
                DNA = dna,
                Fingerprint = fingerprint,
            });

            if (!key.IsValid())
                return;
        }

        if (!_records.TryGetRecord<CriminalRecord>(key, out _))
        {
            _records.AddRecordEntry(key, new CriminalRecord
            {
                Status = SecurityStatus.None,
                Reason = "Scanned by forensic scanner",
                InitiatorName = "Forensic Scanner",
            });
        }

        _records.Synchronize(key);
        _criminalRecords.AddScannedRecord(key);
    }
}
