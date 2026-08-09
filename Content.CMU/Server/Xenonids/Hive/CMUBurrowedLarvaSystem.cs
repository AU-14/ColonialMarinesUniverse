using Content.Server.GameTicking;
using Content.Shared._RMC14.Xenonids.Hive;

namespace Content.Server._CMU14.Xenonids.Hive;

public sealed partial class CMUBurrowedLarvaSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HiveComponent, ComponentStartup>(OnHiveStartup);
    }

    private void OnHiveStartup(Entity<HiveComponent> ent, ref ComponentStartup args)
    {
        var preset = _gameTicker.CurrentPreset ?? _gameTicker.Preset;
        _hive.SetBurrowedLarvaEnabled(ent, preset?.BurrowedLarvaEnabled ?? true);
    }
}
