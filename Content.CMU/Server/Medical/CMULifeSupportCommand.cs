using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Toolshed;

namespace Content.Server.CMU14.Medical;

/// <summary>
/// Toggles the life support set (respiration, barotrauma, heat damage, crit asphyxiation)
/// that the species base keeps disabled.
/// </summary>
[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed partial class CMULifeSupportCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entities = default!;

    public string Command => "cmulifesupport";
    public string Description => Loc.GetString("cmu-cmd-lifesupport-desc");
    public string Help => Loc.GetString("cmu-cmd-lifesupport-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Help);
            return;
        }

        EntityUid target;
        if (args.Length == 1)
        {
            if (!NetEntity.TryParse(args[0], out var net)
                || !_entities.TryGetEntity(net, out var parsed))
            {
                shell.WriteError(Loc.GetString("cmu-cmd-lifesupport-bad-entity", ("entity", args[0])));
                return;
            }

            target = parsed.Value;
        }
        else if (shell.Player?.AttachedEntity is { } self)
            target = self;
        else
        {
            shell.WriteError(Loc.GetString("cmu-cmd-lifesupport-player-only"));
            return;
        }

        var name = _entities.GetComponent<MetaDataComponent>(target).EntityName;
        if (_entities.HasComponent<CMULifeSupportComponent>(target))
        {
            _entities.RemoveComponent<CMULifeSupportComponent>(target);
            shell.WriteLine(Loc.GetString("cmu-cmd-lifesupport-off", ("target", name)));
        }
        else
        {
            _entities.AddComponent<CMULifeSupportComponent>(target);
            shell.WriteLine(Loc.GetString("cmu-cmd-lifesupport-on", ("target", name)));
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromHint(Loc.GetString("cmu-cmd-lifesupport-hint"))
            : CompletionResult.Empty;
    }
}
