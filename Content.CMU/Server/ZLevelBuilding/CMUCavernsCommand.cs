using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.CMU14.ZLevelBuilding;
using Robust.Shared.Console;

namespace Content.Server.CMU14.ZLevelBuilding;

[AdminCommand(AdminFlags.Admin)]
public sealed class CMUCavernsCommand : IConsoleCommand
{
    public string Command => "cmu_caverns";
    public string Description => Loc.GetString("cmu-caverns-description");
    public string Help => Loc.GetString("cmu-caverns-help") + "\n" + Loc.GetString("cmu-caverns-choices",
        ("variants", string.Join(", ", Enum.GetNames<CMUCavernVariant>())),
        ("layouts", string.Join(", ", Enum.GetNames<CMUCavernLayout>())),
        ("presets", string.Join(", ", Enum.GetNames<CMUCavernBiomePreset>())));

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(
                new[] { "status", "seed", "preset", "biome", "variant", "layout", "size", "height", "preview", "reset" }
                    .Select(option => new CompletionOption(option, Loc.GetString($"cmu-caverns-complete-{option}"))),
                Loc.GetString("cmu-caverns-complete-setting"));

        if (args.Length == 2)
            return args[0].ToLowerInvariant() switch
            {
                "preset" => CompletionResult.FromHintOptions(Enum.GetNames<CMUCavernBiomePreset>(), Loc.GetString("cmu-caverns-complete-preset")),
                "biome" => CompletionResult.FromHintOptions(Enum.GetNames<CMUCavernVariant>(), Loc.GetString("cmu-caverns-complete-biome")),
                "variant" => CompletionResult.FromHintOptions(Enum.GetNames<CMUCavernVariant>().Prepend("mixed"), Loc.GetString("cmu-caverns-complete-variant")),
                "layout" => CompletionResult.FromHintOptions(Enum.GetNames<CMUCavernLayout>().Prepend("mixed"), Loc.GetString("cmu-caverns-complete-layout")),
                "seed" => CompletionResult.FromHintOptions(["random"], Loc.GetString("cmu-caverns-complete-seed")),
                "size" => CompletionResult.FromHintOptions(["48", "64", "96", "128", "160"], Loc.GetString("cmu-caverns-complete-size")),
                "height" => CompletionResult.FromHintOptions(Enumerable.Range(4, 6).Select(height => height.ToString()), Loc.GetString("cmu-caverns-complete-height")),
                "preview" => CompletionResult.FromHintOptions(["-3", "-5", "-10"], Loc.GetString("cmu-caverns-complete-preview")),
                _ => CompletionResult.Empty,
            };

        if (args.Length == 3 && args[0].Equals("biome", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<CMUCavernVariant>(args[1], true, out var biome) && Enum.IsDefined(biome))
            return CompletionResult.FromHintOptions(["0", "1", "2", "5", "10", "100", "1000"],
                Loc.GetString("cmu-caverns-complete-weight"));

        return CompletionResult.Empty;
    }

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<CMUCavernConfigSystem>();
        if (args.Length == 0 || args.Length == 1 && args[0] == "status")
        {
            shell.WriteLine(system.Status());
            return;
        }
        if (args.Length is < 1 or > 3)
        {
            shell.WriteError(Help);
            return;
        }
        if (args[0].Equals("preview", StringComparison.OrdinalIgnoreCase) && args.Length <= 2)
        {
            if (system.Preview(args.Length == 2 ? args[1] : "", out var preview))
                shell.WriteLine(preview);
            else
                shell.WriteError(preview);
            return;
        }
        if (system.Configure(args[0], args.Length >= 2 ? args[1] : "", args.Length == 3 ? args[2] : null,
            out var feedback))
            shell.WriteLine(feedback);
        else
            shell.WriteError(feedback + "\n" + Help);
    }
}
