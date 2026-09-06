using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared.GameTicking;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server._CMU14.Round;

public sealed class CMUHostnameSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;

    private string _baseHostname = string.Empty;

    public override void Initialize()
    {
        // Captured before first rewrite, so server name is inherited from server cfg
        _baseHostname = _cfg.GetCVar(CVars.GameHostName);

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        if ((_gameTicker.CurrentPreset ?? _gameTicker.Preset) is not { } preset)
            return;

        var mode = Loc.GetString(preset.ModeTitle).Replace(' ', '-');
        var day = DateTime.Now.DayOfWeek;
        var session = day is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday
            ? "Weekend"
            : day.ToString();

        var separator = _baseHostname.IndexOf('|');
        var prefix = (separator == -1 ? _baseHostname : _baseHostname[..separator]).TrimEnd();

        _cfg.SetCVar(CVars.GameHostName, $"{prefix} | {mode} {session} Playtest");
    }
}
