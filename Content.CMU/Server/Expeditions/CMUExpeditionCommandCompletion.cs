using System.Globalization;
using System.Linq;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Expeditions;

internal static class CMUExpeditionCommandCompletion
{
    public static CompletionResult Maps(IEntityManager entities, IConsoleShell shell, bool here = false, bool expeditionsOnly = false)
    {
        var options = new List<CompletionOption>();
        if (here && shell.Player?.AttachedEntity != null)
            options.Add(new CompletionOption("here", Loc.GetString("cmu-expedition-hint-here")));
        var maps = entities.EntityQueryEnumerator<MapComponent, MetaDataComponent>();
        while (maps.MoveNext(out var uid, out var map, out var metadata))
        {
            if (map.MapId == MapId.Nullspace || expeditionsOnly && !entities.HasComponent<CMUExpeditionMapComponent>(uid))
                continue;
            options.Add(new CompletionOption(map.MapId.ToString(), metadata.EntityName));
        }
        return CompletionResult.FromHintOptions(options, Loc.GetString("cmu-expedition-hint-map"));
    }

    public static bool TryMap(IEntityManager entities, SharedMapSystem maps, IConsoleShell shell, string value, out EntityUid map)
    {
        map = default;
        if (value == "here" && shell.Player?.AttachedEntity is { } player &&
            entities.TryGetComponent<TransformComponent>(player, out var transform) && transform.MapUid is { } current)
        {
            map = current;
            return true;
        }
        if (!int.TryParse(value, out var number) || !maps.MapExists(new MapId(number)))
            return false;
        map = maps.GetMap(new MapId(number));
        return true;
    }

    public static CompletionResult Variants() => CompletionResult.FromHintOptions(
        CMUExpeditionAgentSystem.SquadVariants.OrderBy(value => value).Select(value =>
            new CompletionOption(value, Loc.GetString($"cmu-expedition-variant-{value}"))),
        Loc.GetString("cmu-expedition-hint-variant"));

    public static CompletionResult Count() => CompletionResult.FromHintOptions(
        Enumerable.Range(1, 12).Select(count => count.ToString(CultureInfo.InvariantCulture)),
        Loc.GetString("cmu-expedition-hint-count"));
}
