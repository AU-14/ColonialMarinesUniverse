using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server._CMU14.Threats;
using Content.Shared._RMC14.Xenonids.Hive;

namespace Content.Server._CMU14.Xenonids.Hive;

public sealed partial class CMUBurrowedLarvaSystem : EntitySystem
{
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HiveComponent, ComponentStartup>(OnHiveStartup);
        SubscribeLocalEvent<ThreatSelectedEvent>(OnThreatSelected);
    }

    private void OnHiveStartup(Entity<HiveComponent> ent, ref ComponentStartup args)
    {
        RefreshBurrowedLarva(ent);
    }

    private void OnThreatSelected(ref ThreatSelectedEvent ev)
    {
        var hives = EntityQueryEnumerator<HiveComponent>();
        while (hives.MoveNext(out var uid, out var hive))
            RefreshBurrowedLarva((uid, hive));
    }

    private void RefreshBurrowedLarva(Entity<HiveComponent> hive)
    {
        var preset = _gameTicker.CurrentPreset ?? _gameTicker.Preset;
        var threat = _auRound.SelectedThreat;
        _hive.SetBurrowedLarvaEnabled(hive, (preset?.BurrowedLarvaEnabled ?? true)
            && (threat == null || threat.BurrowedLarvaEnabled));
    }
}
