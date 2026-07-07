using System.Globalization;
using System.Numerics;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Spawners;

namespace Content.Server._CMU14.Util.Admin.Console;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class NukeLightCommand : LocalizedEntityCommands
{
    [Dependency] private PointLightSystem _lights = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TransformSystem _transform = default!;

    private const float DefaultRadius = 80f;
    private const float DefaultEnergy = 80f;
    private const float DefaultDuration = 4f;
    private static readonly Color DefaultColor = Color.Orange;

    public override string Command => "nukelight";

    public override string Help =>
        "Usage: nukelight [radius=80] [energy=80] [duration=4] [x y mapId] [color=Orange]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is 4 or 5 or > 7)
        {
            shell.WriteError(Help);
            return;
        }

        var radius = DefaultRadius;
        var energy = DefaultEnergy;
        var duration = DefaultDuration;
        var color = DefaultColor;

        if (!TryParseFloat(shell, args, 0, "radius", ref radius) ||
            !TryParseFloat(shell, args, 1, "energy", ref energy) ||
            !TryParseFloat(shell, args, 2, "duration", ref duration))
        {
            return;
        }

        if (radius <= 0f || energy <= 0f || duration <= 0f)
        {
            shell.WriteError("Radius, energy, and duration must be greater than zero.");
            return;
        }

        MapCoordinates coords;
        if (args.Length >= 6)
        {
            if (!TryParseCoordinates(shell, args, out coords))
                return;
        }
        else if (shell.Player?.AttachedEntity is { } attached &&
                 EntityManager.TryGetComponent(attached, out TransformComponent? xform))
        {
            coords = _transform.GetMapCoordinates(attached, xform: xform);
        }
        else
        {
            shell.WriteError("No attached entity. Provide x y mapId explicitly.");
            return;
        }

        if (args.Length == 7 && !Color.TryParse(args[6], out color))
        {
            shell.WriteError($"Failed to parse color '{args[6]}'. Use a name like Orange or a hex value like #ff8a00.");
            return;
        }

        if (!_map.MapExists(coords.MapId))
        {
            shell.WriteError($"Map {coords.MapId} does not exist.");
            return;
        }

        var uid = EntityManager.SpawnEntity("ExplosionLight", coords);
        var light = _lights.EnsureLight(uid);
        _lights.SetRadius(uid, radius, light);
        _lights.SetEnergy(uid, energy, light);
        _lights.SetColor(uid, color, light);
        _lights.SetEnabled(uid, true, light);

        var timed = EntityManager.EnsureComponent<TimedDespawnComponent>(uid);
        timed.Lifetime = duration;

        _pvs.AddGlobalOverride(uid);

        shell.WriteLine($"Spawned global nuke light {uid} at {coords.Position} on map {coords.MapId} for {duration:0.###}s.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("radius"),
            2 => CompletionResult.FromHint("energy"),
            3 => CompletionResult.FromHint("duration"),
            4 => CompletionResult.FromHint("x"),
            5 => CompletionResult.FromHint("y"),
            6 => CompletionResult.FromHint("mapId"),
            7 => CompletionResult.FromHint("color"),
            _ => CompletionResult.Empty
        };
    }

    private static bool TryParseFloat(IConsoleShell shell, string[] args, int index, string name, ref float value)
    {
        if (args.Length <= index)
            return true;

        if (float.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        shell.WriteError($"Failed to parse {name} '{args[index]}'.");
        return false;
    }

    private static bool TryParseCoordinates(IConsoleShell shell, string[] args, out MapCoordinates coords)
    {
        coords = default;

        if (!float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            shell.WriteError($"Failed to parse coordinates '{args[3]}' '{args[4]}'.");
            return false;
        }

        if (!int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mapId))
        {
            shell.WriteError($"Failed to parse map ID '{args[5]}'.");
            return false;
        }

        coords = new MapCoordinates(new Vector2(x, y), new MapId(mapId));
        return true;
    }
}
