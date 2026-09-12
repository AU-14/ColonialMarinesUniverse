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

namespace Content.Server.CMU14.Util.Admin.Console;

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

    public override string Command => "nuke:lights";
    public override string Description => Loc.GetString("cmu-cmd-nuke-lights-desc");
    public override string Help => Loc.GetString("cmu-cmd-nuke-lights-help");

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

        if (!TryParseFloat(shell, args, 0, Loc.GetString("cmu-cmd-nuke-lights-hint-radius"), ref radius) ||
            !TryParseFloat(shell, args, 1, Loc.GetString("cmu-cmd-nuke-lights-hint-energy"), ref energy) ||
            !TryParseFloat(shell, args, 2, Loc.GetString("cmu-cmd-nuke-lights-hint-duration"), ref duration))
        {
            return;
        }

        if (radius <= 0f || energy <= 0f || duration <= 0f)
        {
            shell.WriteError(Loc.GetString("cmu-cmd-nuke-lights-positive"));
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
            shell.WriteError(Loc.GetString("cmu-cmd-nuke-lights-no-attached"));
            return;
        }

        if (args.Length == 7 && !Color.TryParse(args[6], out color))
        {
            shell.WriteError(Loc.GetString("cmu-cmd-nuke-lights-color-error", ("color", args[6])));
            return;
        }

        if (!_map.MapExists(coords.MapId))
        {
            shell.WriteError(Loc.GetString("cmu-cmd-nuke-lights-map-missing", ("mapId", coords.MapId)));
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

        shell.WriteLine(Loc.GetString("cmu-cmd-nuke-lights-spawned",
            ("uid", uid),
            ("position", coords.Position),
            ("mapId", coords.MapId),
            ("duration", duration.ToString("0.###", CultureInfo.InvariantCulture))));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint(Loc.GetString("cmu-cmd-nuke-lights-hint-radius")),
            2 => CompletionResult.FromHint(Loc.GetString("cmu-cmd-nuke-lights-hint-energy")),
            3 => CompletionResult.FromHint(Loc.GetString("cmu-cmd-nuke-lights-hint-duration")),
            4 => CompletionResult.FromHint(Loc.GetString("cmu-cmd-nuke-lights-hint-x")),
            5 => CompletionResult.FromHint(Loc.GetString("cmu-cmd-nuke-lights-hint-y")),
            6 => CompletionResult.FromHint(Loc.GetString("cmu-cmd-nuke-lights-hint-map-id")),
            7 => CompletionResult.FromHint(Loc.GetString("cmu-cmd-nuke-lights-hint-color")),
            _ => CompletionResult.Empty
        };
    }

    private static bool TryParseFloat(IConsoleShell shell, string[] args, int index, string name, ref float value)
    {
        if (args.Length <= index)
            return true;

        if (float.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        shell.WriteError(Loc.GetString("cmu-cmd-nuke-lights-parse-value", ("name", name), ("value", args[index])));
        return false;
    }

    private static bool TryParseCoordinates(IConsoleShell shell, string[] args, out MapCoordinates coords)
    {
        coords = default;

        if (!float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
            !float.TryParse(args[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
        {
            shell.WriteError(Loc.GetString("cmu-cmd-nuke-lights-parse-coordinates", ("x", args[3]), ("y", args[4])));
            return false;
        }

        if (!int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mapId))
        {
            shell.WriteError(Loc.GetString("cmu-cmd-nuke-lights-parse-map-id", ("mapId", args[5])));
            return false;
        }

        coords = new MapCoordinates(new Vector2(x, y), new MapId(mapId));
        return true;
    }
}
