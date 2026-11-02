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
