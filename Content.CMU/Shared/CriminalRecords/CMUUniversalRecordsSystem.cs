using Content.Shared.GameTicking;
using Content.Shared.StationRecords.Components;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.CriminalRecords;

/// <summary>
/// One records database for the whole round, independent of any map, grid or station.
/// </summary>
public sealed partial class CMUUniversalRecordsSystem : EntitySystem
{
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private INetManager _net = default!;

    private EntityUid? _records;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        if (_records is { } records && !TerminatingOrDeleted(records))
            QueueDel(records);

        _records = null;
    }

    /// <summary>
    /// The holder if something already made it this round, without spawning one. For lookups that only read.
    /// </summary>
    public EntityUid? GetExistingRecords()
    {
        return _records is { } existing && !TerminatingOrDeleted(existing) ? existing : null;
    }

    /// <summary>
    /// The universal records holder. Always null on the client.
    /// </summary>
    public EntityUid? GetRecords()
    {
        if (_net.IsClient)
            return null;

        if (_records is { } existing && !TerminatingOrDeleted(existing))
            return existing;

        var records = Spawn(null, MapCoordinates.Nullspace);
        _metaData.SetEntityName(records, "universal records");
        AddComp<StationRecordsComponent>(records);
        _records = records;
        return records;
    }
}
