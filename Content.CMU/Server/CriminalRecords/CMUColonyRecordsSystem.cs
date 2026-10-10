using Content.Server.CriminalRecords.Systems;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.CriminalRecords;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.CriminalRecords;

public sealed partial class CMUColonyRecordsSystem : EntitySystem
{
    [Dependency] private CriminalRecordsConsoleSystem _criminalRecordsConsole = default!;
    [Dependency] private StationRecordsSystem _records = default!;
    [Dependency] private CMUUniversalRecordsSystem _universalRecords = default!;

    private static readonly ProtoId<DepartmentPrototype>[] RecordedDepartments =
    {
        "AU14DepartmentCivilian",
        "AU14DepartmentColonyCommand",
        "AU14DepartmentColonyMedical",
        "AU14DepartmentColonySecurity",
        "AU14DepartmentCorporate",
        "AU14DepartmentCriminal",
        "AU14DepartmentEngineering",
        "AU14DepartmentHydroponics",
        "AU14DepartmentLabor",
        "AU14DepartmentLumbermill",
        "AU14DepartmentServices",
        "AU14DepartmentColonialLiberationFront",
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete, after: [typeof(StationRecordsSystem)]);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null || !IsRecordedJob(args.JobId))
            return;

        EnsureCharacterRecord(args.Mob, args.Profile.Name, args.Profile.Age, args.Profile.Gender);
    }

    /// <summary>
    /// Makes sure a character has their normal record on the criminal records consoles, separate from any antag alias.
    /// </summary>
    public StationRecordKey? EnsureCharacterRecord(EntityUid mob, string name, int age, Gender gender)
    {
        if (_universalRecords.GetRecords() is not { } station)
            return null;

        var dna = CompOrNull<DnaComponent>(mob)?.DNA;
        var fingerprint = CompOrNull<FingerprintComponent>(mob)?.Fingerprint;

        StationRecordKey key;
        if (_records.GetRecordByName(station, name) is { } recordId)
        {
            key = new StationRecordKey(recordId, station);
            if (_records.TryGetRecord<GeneralStationRecord>(key, out var general))
            {
                general.DNA ??= dna;
                general.Fingerprint ??= fingerprint;
            }
        }
        else
        {
            key = _records.AddRecordEntry(station, new GeneralStationRecord
            {
                Name = name,
                Age = age,
                Gender = gender,
                DNA = dna,
                Fingerprint = fingerprint,
            });

            if (!key.IsValid())
                return null;
        }

        if (!_records.TryGetRecord<CriminalRecord>(key, out _))
            _records.AddRecordEntry(key, new CriminalRecord());

        _records.Synchronize(key);
        _criminalRecordsConsole.AddScannedRecord(key);
        return key;
    }

    private bool IsRecordedJob(string jobId)
    {
        foreach (var departmentId in RecordedDepartments)
        {
            if (ProtoMan.TryIndex(departmentId, out var department) &&
                department.Roles.Contains(jobId))
            {
                return true;
            }
        }

        return false;
    }
}
