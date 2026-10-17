using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Audio;

namespace Content.Server._RMC14.Marines;

[AdminCommand(AdminFlags.Fun)]
public sealed class AresAnnounceCommand : IConsoleCommand
{
    public string Command => "aresannounce";
    public string Description => Loc.GetString("rmc-command-aresannounce-description");
    public string Help => Loc.GetString("rmc-command-aresannounce-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var marineAnnounce = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<MarineAnnounceSystem>();
        if (args.Length == 0)
        {
            shell.WriteError("Not enough arguments! Need at least 1.");
            return;
        }

        // CMU14: Force on Force roles, hijacking, announcements and identification.
        string? faction = null;
        var start = 0;
        if (args[0].Equals("govfor", StringComparison.OrdinalIgnoreCase) ||
            args[0].Equals("opfor", StringComparison.OrdinalIgnoreCase))
        {
            faction = args[0].ToLowerInvariant();
            start = 1;
        }

        var message = string.Join(' ', args[start..]);
        if (string.IsNullOrWhiteSpace(message))
        {
            shell.WriteError(Help);
            return;
        }
        var soundSpecifier = new SoundPathSpecifier("/Audio/_RMC14/AI/announce.ogg");
        // CMU14: Force on Force roles, hijacking, announcements and identification.
        marineAnnounce.AnnounceARES(null, message, soundSpecifier, faction);
        shell.WriteLine("Sent!");
    }
}
