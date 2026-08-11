using Content.Server._RMC14.Xenonids.Hive;
using Content.Shared._CMU14.Hiveless;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.GameTicking;

namespace Content.Server._CMU14.Threats.Mobs.Xeno;

/// <summary>
/// Ensures xenos spawned outside the legacy RMC distress signal setup have a hive.
/// </summary>
public sealed partial class CMUXenoHiveAssignmentSystem : EntitySystem
{
    private const string DefaultHiveName = "xenonid hive";

    [Dependency] private XenoHiveSystem _hive = default!;

    private readonly HashSet<EntityUid> _pending = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoMapInitializedEvent>(OnXenoMapInitialized);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _pending.Clear());
    }

    private void OnXenoMapInitialized(ref XenoMapInitializedEvent args)
    {
        if (!HasComp<HivelessComponent>(args.Xeno))
            _pending.Add(args.Xeno);
    }

    public override void Update(float frameTime)
    {
        if (_pending.Count == 0)
            return;

        var pending = new HashSet<EntityUid>(_pending);
        _pending.Clear();

        EntityUid? hive = null;
        foreach (var xeno in pending)
        {
            if (TerminatingOrDeleted(xeno) ||
                HasComp<HivelessComponent>(xeno) ||
                _hive.HasHive(xeno))
            {
                continue;
            }

            if (hive == null)
            {
                var hives = EntityQueryEnumerator<HiveComponent>();
                while (hives.MoveNext(out var hiveId, out var hiveComponent))
                {
                    if (!hiveComponent.Corrupted)
                    {
                        hive = hiveId;
                        break;
                    }
                }

                hive ??= _hive.CreateHive(DefaultHiveName);
            }

            _hive.SetHive(xeno, hive);
        }
    }
}
