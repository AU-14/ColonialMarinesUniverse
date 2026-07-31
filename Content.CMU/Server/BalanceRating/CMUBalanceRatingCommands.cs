using System.Globalization;
using System.Linq;
using Content.Server.Administration;
using Content.Shared._CMU14.BalanceRating;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CMU14.BalanceRating;

[AdminCommand(AdminFlags.Round)]
public sealed partial class StartCMUBalanceRatingCommand : LocalizedEntityCommands
{
    [Dependency] private CMUBalanceRatingSystem _ratings = default!;

    public override string Command => "startbalancerating";
    public override string Description => Loc.GetString("cmd-startbalancerating-desc");
    public override string Help => Loc.GetString("cmd-startbalancerating-help", ("command", Command));

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 2 or > 3)
        {
            shell.WriteError(Help);
            return;
        }

        if (!Enum.TryParse(args[1], true, out CMUBalanceRatingMetric metric) || !Enum.IsDefined(metric))
        {
            shell.WriteError(Loc.GetString("cmu-balance-rating-command-invalid-metric"));
            return;
        }

        var duration = CMUBalanceRatingSystem.DefaultDuration;
        if (args.Length == 3)
        {
            if (!double.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
                !double.IsFinite(seconds))
            {
                shell.WriteError(Loc.GetString("cmu-balance-rating-command-invalid-duration"));
                return;
            }

            if (seconds < CMUBalanceRatingSystem.MinimumDuration.TotalSeconds ||
                seconds > CMUBalanceRatingSystem.MaximumDuration.TotalSeconds)
            {
                shell.WriteError(Loc.GetString("cmu-balance-rating-command-duration-range",
                    ("minimum", (int) CMUBalanceRatingSystem.MinimumDuration.TotalSeconds),
                    ("maximum", (int) CMUBalanceRatingSystem.MaximumDuration.TotalSeconds)));
                return;
            }

            duration = TimeSpan.FromSeconds(seconds);
        }

        var result = await _ratings.TryStartRating(
            args[0],
            metric,
            duration,
            shell.Player?.UserId.UserId);

        if (result.Success)
            shell.WriteLine(result.Message);
        else
            shell.WriteError(result.Message);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                return CompletionResult.FromHintOptions(
                    _ratings.GetTargets()
                        .OrderBy(target => target.Name)
                        .ThenBy(target => target.Id)
                        .Select(target => new CompletionOption(
                            target.Id,
                            $"{target.Name} ({target.Target})")),
                    Loc.GetString("cmu-balance-rating-command-target-hint"));
            case 2:
                if (_ratings.TryGetTarget(args[0], out var target) &&
                    target.Target == CMUBalanceRatingTarget.Map)
                {
                    return CompletionResult.FromHintOptions(
                        [CMUBalanceRatingMetric.Fun.ToString().ToLowerInvariant()],
                        Loc.GetString("cmu-balance-rating-command-metric-hint"));
                }

                return CompletionResult.FromHintOptions(
                    Enum.GetNames<CMUBalanceRatingMetric>().Select(name => name.ToLowerInvariant()),
                    Loc.GetString("cmu-balance-rating-command-metric-hint"));
            case 3:
                return CompletionResult.FromHintOptions(
                    ["15", "30", "45", "60"],
                    Loc.GetString("cmu-balance-rating-command-duration-hint"));
            default:
                return CompletionResult.Empty;
        }
    }
}

[AdminCommand(AdminFlags.Round)]
public sealed partial class CancelCMUBalanceRatingCommand : LocalizedEntityCommands
{
    [Dependency] private CMUBalanceRatingSystem _ratings = default!;

    public override string Command => "cancelbalancerating";
    public override string Description => Loc.GetString("cmd-cancelbalancerating-desc");
    public override string Help => Loc.GetString("cmd-cancelbalancerating-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        if (_ratings.CancelActiveRating())
            shell.WriteLine(Loc.GetString("cmu-balance-rating-command-cancelled"));
        else
            shell.WriteError(Loc.GetString("cmu-balance-rating-command-none-active"));
    }
}
