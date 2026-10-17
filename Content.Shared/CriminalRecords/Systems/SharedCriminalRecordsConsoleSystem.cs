using Content.Shared.IdentityManagement;
using Content.Shared.Security;
using Content.Shared.Security.Components;
using Content.Shared.Station;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;

namespace Content.Shared.CriminalRecords.Systems;

public abstract partial class SharedCriminalRecordsConsoleSystem : EntitySystem
{
    [Dependency] private SharedCriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private StationRecordsSystem _records = default!;
    [Dependency] private SharedStationSystem _station = default!;

    /// <summary>
    /// Checks if the new identity's name has a criminal record attached to it, and gives the entity the icon that
    /// belongs to the status if it does.
    /// </summary>
    public void CheckNewIdentity(EntityUid uid)
    {
        var name = Identity.Name(uid, EntityManager);
        var xform = Transform(uid);

        // cmu edit start
        var station = EntityManager.System<Content.Shared.CMU14.CriminalRecords.CMUUniversalRecordsSystem>().GetRecords();
        if (station == null)
            return;
        // cmu edit end

        if (station != null && _records.GetRecordByName(station.Value, name) is { } id)
        {
            if (_records.TryGetRecord<CriminalRecord>(new StationRecordKey(id, station.Value),
                    out var record))
            {
                if (record.Status != SecurityStatus.None)
                {
                    _criminalRecords.SetCriminalIcon(name, record.Status, uid);
                    return;
                }
            }
        }
        RemComp<CriminalRecordComponent>(uid);
    }
}
