using Content.Server.Administration;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.ZLevels.Mapping;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CMUDeleteZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _entities = default!;

    public override string Command => "znetwork-delete";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var options = new List<CompletionOption>();
        var query = _entities.EntityQueryEnumerator<CMUZLevelsNetworkComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var meta))
        {
            options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
        }
        return CompletionResult.FromHintOptions(options, Loc.GetString("cmu-cmd-znetwork-entity-hint"));
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        EntityUid? target;
        if (!NetEntity.TryParse(args[0], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out target))
        {
            shell.WriteError(Loc.GetString("cmu-cmd-znetwork-entity-missing", ("entity", args[0])));
            return;
        }

        if (!_entities.TryGetComponent<CMUZLevelsNetworkComponent>(target, out var levelComp))
        {
            shell.WriteError(Loc.GetString("cmu-cmd-znetwork-component-missing", ("entity", args[0])));
            return;
        }

        foreach (var (_, mapUid) in levelComp.ZLevels)
            _entities.QueueDeleteEntity(mapUid);

        _entities.QueueDeleteEntity(target);
        shell.WriteLine(Loc.GetString("cmu-cmd-znetwork-delete-success"));
    }
}
