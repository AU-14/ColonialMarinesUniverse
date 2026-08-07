using Content.Server._CMU14.RoundStatistics;
using Content.Server._RMC14.Xenonids.Hive;
using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Timing;
using HiveCollapseRuleComponent = Content.Shared._CMU14.Threats.Rules.HiveCollapseRuleComponent;

namespace Content.Server._CMU14.Threats.Rules;

public sealed partial class HiveCollapseRuleSystem : GameRuleSystem<HiveCollapseRuleComponent>
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AuRoundSystem _auRoundSystem = default!;
    [Dependency] private CMURoundStatisticsSystem _roundStats = default!;

    private const string DefaultWinMsg = "The hive has collapsed!";
    private TimeSpan? _hiveCollapseTime;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoHiveQueenChangedEvent>(OnQueenChanged);
    }

    protected override void Started(EntityUid uid, HiveCollapseRuleComponent component,
        GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        _hiveCollapseTime = AnyHiveHasQueen()
            ? null
            : _timing.CurTime + component.HiveCollapseDuration;
    }

    private bool AnyHiveHasQueen()
    {
        var query = EntityQueryEnumerator<HiveComponent>();
        while (query.MoveNext(out _, out var hive))
        {
            if (hive.CurrentQueen != null)
                return true;
        }

        return false;
    }

    private void OnQueenChanged(XenoHiveQueenChangedEvent ev)
    {
        if (!_gameTicker.IsGameRuleActive<HiveCollapseRuleComponent>())
            return;

        EntityQueryEnumerator<ActiveGameRuleComponent, HiveCollapseRuleComponent, GameRuleComponent> queryRule
            = QueryActiveRules();
        if (!ThreatRuleHelper.TryGetActiveRule(ref queryRule, out HiveCollapseRuleComponent ruleComp, out _))
            return;

        if (AnyHiveHasQueen())
            _hiveCollapseTime = null;
        else
            _hiveCollapseTime ??= _timing.CurTime + ruleComp.HiveCollapseDuration;
    }

    protected override void ActiveTick(EntityUid uid, HiveCollapseRuleComponent component, GameRuleComponent gameRule,
        float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (_hiveCollapseTime == null || _timing.CurTime < _hiveCollapseTime)
            return;

        string? winMessage = _auRoundSystem.SelectedThreat?.WinMessage;
        _roundStats.RecordThreatDefeatedRule("HiveCollapseRule");
        _gameTicker.EndRound(string.IsNullOrEmpty(winMessage) ? DefaultWinMsg : winMessage);
    }
}
