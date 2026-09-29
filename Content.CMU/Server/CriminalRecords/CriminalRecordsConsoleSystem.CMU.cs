using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.StationRecords.Events;

namespace Content.Server.CriminalRecords.Systems;

public sealed partial class CriminalRecordsConsoleSystem
{
    [Dependency] private CMUUniversalRecordsSystem _cmuRecords = default!;

    private void CMUInitialize()
    {
        SubscribeLocalEvent<RecordModifiedEvent>(OnCMURecordModified);
    }

    private void OnCMURecordModified(ref RecordModifiedEvent args)
    {
        var query = EntityQueryEnumerator<CriminalRecordsConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateUserInterface((uid, comp));
        }
    }

    private EntityUid? CMUGetRecordsStation(EntityUid console)
    {
        return _cmuRecords.GetRecords();
    }
}
