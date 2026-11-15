using Content.Shared.CCVar;
using Content.Shared.CMU14.Hearing;
using Robust.Shared.Configuration;

namespace Content.Server.CMU14.Diagnostics.Performance;

/// <summary>Counts existing successful gunfire broadcasts; never retains guns or shooters.</summary>
public sealed partial class CMUCombatMetricsSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;

    private bool _windowEnabled;
    private bool _monitorEnabled;
    internal long GunfireEvents { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_config, CCVars.CMUServerPerformanceWindowEnabled, value => _windowEnabled = value, true);
        Subs.CVar(_config, CCVars.CMUServerPerformanceDiagnosticsEnabled, value => _monitorEnabled = value, true);
        SubscribeLocalEvent<CMUGunFiredEvent>(OnGunfire);
    }

    private void OnGunfire(ref CMUGunFiredEvent args)
    {
        if (_windowEnabled && _monitorEnabled)
            GunfireEvents++;
    }
}
