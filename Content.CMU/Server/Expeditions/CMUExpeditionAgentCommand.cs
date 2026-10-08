using System.Numerics;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Expeditions;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class CMUExpeditionAgentCommand : LocalizedEntityCommands
{
    [Dependency] private CMUExpeditionAgentSystem _agents = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override string Command => "cmu-expedition-ai";
    public override string Description => Loc.GetString("cmd-cmu-expedition-ai-desc");
    public override string Help => Loc.GetString("cmd-cmu-expedition-ai-help");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var count = 3;
        var variant = args.Length == 3 ? args[2].ToLowerInvariant() : "mixed";
        if (args.Length is < 1 or > 3 || args.Length >= 2 && !int.TryParse(args[1], out count) ||
            count is < 1 or > 12 || !CMUExpeditionAgentSystem.IsSquadVariant(variant))
        {
            shell.WriteError(Help);
            return;
        }
        EntityUid map;
        EntityCoordinates center;
        if (args[0] == "here")
        {
            if (shell.Player?.AttachedEntity is not { } player ||
                !EntityManager.TryGetComponent<TransformComponent>(player, out var transform) || transform.MapUid is not { } currentMap)
            {
                shell.WriteError(Loc.GetString("cmu-expedition-here-no-player"));
                return;
            }
            map = currentMap;
            center = _transform.ToCoordinates(map, _transform.GetMapCoordinates(player));
        }
        else if (int.TryParse(args[0], out var number) && _map.MapExists(new MapId(number)))
        {
            map = _map.GetMap(new MapId(number));
            center = new EntityCoordinates(map, Vector2.Zero);
        }
        else
        {
            shell.WriteError(Help);
            return;
        }
        EntityManager.TryGetComponent<CMUExpeditionMapComponent>(map, out var expedition);
        if (expedition is { Ready: false })
        {
            shell.WriteError(Loc.GetString("cmu-expedition-not-ready"));
            return;
        }
        if (args[0] != "here")
        {
            if (expedition == null)
            {
                shell.WriteError(Loc.GetString("cmu-expedition-ai-use-here"));
                return;
            }
            center = new EntityCoordinates(map, new Vector2(expedition.Plan.Objective.X + 0.5f, expedition.Plan.Objective.Y + 0.5f));
        }
        var spawned = _agents.SpawnSquad(center, count, variant, out var squad);
        if (spawned == 0)
        {
            shell.WriteError(Loc.GetString("cmu-expedition-ai-no-space"));
            return;
        }
        shell.WriteLine(Loc.GetString("cmu-expedition-ai-deployed", ("count", spawned), ("requested", count),
            ("variant", variant), ("squad", squad), ("map", EntityManager.GetComponent<TransformComponent>(map).MapID.ToString())));
    }
}
