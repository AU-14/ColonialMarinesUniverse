using Content.Server._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja;

// serpents called onto the preserve get their own hive. in the prime hive they ate its T2/T3 slots and
// fed the larva queue on a distress signal round, and the preserve is meant to be its own game
public sealed partial class YautjaHuntConsoleSystem
{
    [Dependency] private XenoHiveSystem _hive = default!;

    private static readonly EntProtoId PreserveHivePrototype = "CMUYautjaPreserveHive";

    private EntityUid? _preserveHive;

    public void AssignPreserveHive(EntityUid prey)
    {
        if (!HasComp<XenoComponent>(prey))
            return;

        if (_preserveHive is not { } hive || TerminatingOrDeleted(hive) || !HasComp<HiveComponent>(hive))
        {
            hive = _hive.CreateHive(Loc.GetString("cmu-yautja-preserve-hive-name"), PreserveHivePrototype);
            _preserveHive = hive;
        }

        _hive.SetHive(prey, hive);
    }
}
