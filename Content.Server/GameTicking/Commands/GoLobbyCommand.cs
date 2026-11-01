using Content.Server.Administration;
using Content.Server.GameTicking.Presets;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.GameTicking.Commands
{
    [AdminCommand(AdminFlags.Host)]
    public sealed partial class GoLobbyCommand : LocalizedEntityCommands
    {
        [Dependency] private IConfigurationManager _configManager = default!;
        [Dependency] private GameTicker _gameTicker = default!;

        public override string Command => "golobby";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            // CMU14: golobby restarts the round for everyone; require an explicit confirm argument
            if (args.Length == 0
                || !args[0].Equals("confirm", StringComparison.OrdinalIgnoreCase))
            {
                shell.WriteLine(Loc.GetString("cmu-cmd-golobby-needs-confirm"));
                return;
            }

            GamePresetPrototype? preset = null;
            var presetName = string.Join(" ", args[1..]); // CMU14: confirm takes the first argument

            if (args.Length > 1) // CMU14: preset name shifted behind confirm
            {
                if (!_gameTicker.TryFindGamePreset(presetName, out preset))
                {
                    shell.WriteLine(Loc.GetString($"cmd-forcepreset-no-preset-found", ("preset", presetName)));
                    return;
                }
            }

            _configManager.SetCVar(CCVars.GameLobbyEnabled, true);

            _gameTicker.RestartRound();

            if (preset != null)
                _gameTicker.SetGamePreset(preset);

            shell.WriteLine(Loc.GetString(preset == null ? "cmd-golobby-success" : "cmd-golobby-success-with-preset", ("preset", presetName)));
        }
    }
}
