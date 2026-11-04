using System.Linq;
using Content.Shared.CMU14.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

public enum ANPRCSeverity : byte
{
    Good,
    Info,
    Warn,
    Bad,
}

/// <summary>One thing wrong with the set, why it matters, and the press that fixes it.</summary>
public sealed class ANPRCIssue
{
    public required ANPRCSeverity Severity;
    public required string Title;
    public string Detail = string.Empty;

    public string? ActionText;
    public Action? Action;

    /// <summary>The action is the quick setup, so nothing else on the panel needs to offer it.</summary>
    public bool IsQuickSetup;
}

/// <summary>
///     Reads the set the way an experienced operator would glance at it: what stops it working,
///     in the order the faults stack, and what to press about it. The panel shows the first of
///     these as its next step, so the order here is the order a new operator is walked through
///     getting on the air.
/// </summary>
public static class ANPRCDiagnosis
{
    public static List<ANPRCIssue> Diagnose(ANPRCPanelData data, ANPRCRadioActions radio)
    {
        var issues = new List<ANPRCIssue>();
        var state = data.State;

        if (!state.HasBattery)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Bad,
                Title = Loc.GetString("anprc-op-issue-no-battery"),
                Detail = Loc.GetString("anprc-op-issue-no-battery-detail"),
            });

            return issues;
        }

        if (!state.Enabled)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Bad,
                Title = Loc.GetString("anprc-op-issue-off"),
                Detail = Loc.GetString("anprc-op-issue-off-detail"),
                ActionText = Loc.GetString(data.MissingStandardNets.Count > 0
                    ? "anprc-op-action-quick-setup"
                    : "anprc-op-action-power-on"),
                Action = data.MissingStandardNets.Count > 0 ? radio.QuickSetup : radio.TogglePower,
                IsQuickSetup = data.MissingStandardNets.Count > 0,
            });

            return issues;
        }

        if (!data.Deployed)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Warn,
                Title = Loc.GetString("anprc-op-issue-stowed"),
                Detail = Loc.GetString("anprc-op-issue-stowed-detail"),
            });
        }
        else if (!data.Relay.WearerTrained)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Bad,
                Title = Loc.GetString("anprc-op-issue-untrained"),
                Detail = Loc.GetString("anprc-op-issue-untrained-detail"),
            });
        }

        if (state.SweepEnabled)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Warn,
                Title = Loc.GetString("anprc-op-issue-searching"),
                Detail = Loc.GetString("anprc-op-issue-searching-detail"),
                ActionText = Loc.GetString("anprc-op-action-stop-search"),
                Action = () => radio.SetSweep(false),
            });
        }

        if (data.MissingStandardNets.Count > 0)
        {
            var squad = data.MissingStandardNets.FirstOrDefault(net => net.Squad);
            var names = string.Join(", ", data.MissingStandardNets.Select(net => net.Label));

            issues.Add(new ANPRCIssue
            {
                Severity = state.Presets.Count == 0 || squad != null ? ANPRCSeverity.Bad : ANPRCSeverity.Warn,
                Title = squad != null
                    ? Loc.GetString("anprc-op-issue-squad-missing", ("net", data.ChannelName(squad.Channel)))
                    : Loc.GetString("anprc-op-issue-standard-missing", ("nets", names)),
                Detail = Loc.GetString("anprc-op-issue-standard-missing-detail"),
                ActionText = Loc.GetString("anprc-op-action-load-nets", ("nets", names)),
                Action = radio.QuickSetup,
                IsQuickSetup = true,
            });
        }
        else if (state.Presets.Count == 0 && state.FrequencyOverrides.Count == 0)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Bad,
                Title = Loc.GetString("anprc-op-issue-no-nets"),
                Detail = Loc.GetString("anprc-op-issue-no-nets-detail"),
                ActionText = Loc.GetString("anprc-op-action-open-nets"),
                Action = () => radio.ShowPage(ANPRCPage.Nets),
            });
        }

        if (!data.HasActiveNet && !state.SweepEnabled && data.FirstLoadedSlot() is { } loaded)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Warn,
                Title = Loc.GetString("anprc-op-issue-no-active"),
                Detail = Loc.GetString("anprc-op-issue-no-active-detail"),
                ActionText = Loc.GetString("anprc-op-action-use-net", ("label", data.SlotLabel(loaded))),
                Action = () => radio.SelectSlot(loaded),
            });
        }

        if (state.MonitorEnabled)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Warn,
                Title = Loc.GetString("anprc-op-issue-monitor"),
                Detail = Loc.GetString("anprc-op-issue-monitor-detail"),
                ActionText = Loc.GetString("anprc-op-action-monitor-off"),
                Action = radio.ToggleMonitor,
            });
        }

        if (state.Mode == RadioMode.CipherText && !data.HasFill)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Bad,
                Title = Loc.GetString("anprc-op-issue-ct-no-fill"),
                Detail = Loc.GetString("anprc-op-issue-ct-no-fill-detail"),
                ActionText = Loc.GetString("anprc-op-action-mode-fh"),
                Action = () => radio.SetMode(RadioMode.FrequencyHopping),
            });
        }
        else if (!data.HasFill && state.Mode != RadioMode.PlainText)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Warn,
                Title = Loc.GetString("anprc-op-issue-no-fill"),
                Detail = Loc.GetString("anprc-op-issue-no-fill-detail"),
                ActionText = Loc.GetString("anprc-op-action-open-security"),
                Action = () => radio.ShowPage(ANPRCPage.Security),
            });
        }
        else if (state.CryptoStale)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Warn,
                Title = Loc.GetString("anprc-op-issue-stale-fill"),
                Detail = Loc.GetString("anprc-op-issue-stale-fill-detail"),
                ActionText = Loc.GetString("anprc-op-action-open-security"),
                Action = () => radio.ShowPage(ANPRCPage.Security),
            });
        }

        if (state.Mode == RadioMode.PlainText)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Warn,
                Title = Loc.GetString("anprc-op-issue-plain"),
                Detail = Loc.GetString("anprc-op-issue-plain-detail"),
                ActionText = Loc.GetString("anprc-op-action-mode-fh"),
                Action = () => radio.SetMode(RadioMode.FrequencyHopping),
            });
        }

        // a gated net the set can barely reach. zero is the set standing outside every anchor
        // that carries it, which happens when it is not relaying the net itself
        if (data.Ready && data.Relay.LinkQuality is >= 0f and < 0.35f)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = data.Relay.LinkQuality <= 0f ? ANPRCSeverity.Bad : ANPRCSeverity.Warn,
                Title = Loc.GetString(data.Relay.LinkQuality <= 0f
                    ? "anprc-op-issue-no-link"
                    : "anprc-op-issue-weak-link"),
                Detail = Loc.GetString("anprc-op-issue-link-detail"),
            });
        }

        if (state.Battery() is > 0f and <= 0.2f)
        {
            issues.Add(new ANPRCIssue
            {
                Severity = ANPRCSeverity.Warn,
                Title = Loc.GetString("anprc-op-issue-low-battery"),
                Detail = Loc.GetString("anprc-op-issue-low-battery-detail"),
            });
        }

        // worst first, but keep the walk-through order among equals: a stable sort
        return issues
            .Select((issue, index) => (issue, index))
            .OrderByDescending(pair => pair.issue.Severity)
            .ThenBy(pair => pair.index)
            .Select(pair => pair.issue)
            .ToList();
    }

    private static float Battery(this ANPRCRadioState state) => state.HasBattery ? state.BatteryFraction : -1f;
}
