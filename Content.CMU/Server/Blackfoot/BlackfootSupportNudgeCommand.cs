using System.Globalization;
using System.Numerics;
using Content.Server.Administration;
using Content.Shared._CMU14.Blackfoot;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._CMU14.Blackfoot;

[AdminCommand(AdminFlags.Mapping)]
public sealed partial class BlackfootSupportNudgeCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entManager = default!;

    public string Command => "blackfootnudge";
    public string Description => "Nudges the nearest Blackfoot support object by pixel offsets.";
    public string Help =>
        "Usage: blackfootnudge <fuel|computer|light> <xPixels> <yPixels> [range]\n" +
        "Nudges the nearest matching Blackfoot support object on your map. " +
        "Positive X moves right, positive Y moves up. Range is in tiles and defaults to 8.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 3 or > 4)
        {
            shell.WriteError(Help);
            return;
        }

        if (!TryGetPlayerEntity(shell, out var player))
            return;

        if (!TryParsePixels(shell, args[1], out var xPixels) ||
            !TryParsePixels(shell, args[2], out var yPixels))
        {
            return;
        }

        var range = 8f;
        if (args.Length == 4 &&
            !float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out range))
        {
            shell.WriteError("Range must be a number.");
            return;
        }

        if (range <= 0f)
        {
            shell.WriteError("Range must be greater than zero.");
            return;
        }

        var xform = _entManager.System<SharedTransformSystem>();
        if (!_entManager.TryGetComponent(player, out TransformComponent? playerXform) ||
            playerXform.MapUid == null)
        {
            shell.WriteError("Your attached entity is not on a map.");
            return;
        }

        var target = args[0].ToLowerInvariant() switch
        {
            "fuel" or "pump" or "fuelpump" => FindNearest<BlackfootFuelPumpComponent>(player, range, xform),
            "computer" or "cpu" or "flightcomputer" => FindNearest<BlackfootFlightComputerComponent>(player, range, xform),
            "light" or "padlight" => FindNearest<BlackfootLandingPadLightComponent>(player, range, xform),
            _ => null,
        };

        if (target == null)
        {
            shell.WriteError("Target must be fuel, computer, or light.");
            return;
        }

        if (target == EntityUid.Invalid)
        {
            shell.WriteError($"No matching Blackfoot support object found within {range.ToString(CultureInfo.InvariantCulture)} tiles.");
            return;
        }

        var delta = new Vector2(xPixels, yPixels) / 32f;
        var targetXform = _entManager.GetComponent<TransformComponent>(target.Value);
        xform.SetLocalPosition(target.Value, targetXform.LocalPosition + delta, targetXform);

        shell.WriteLine($"Nudged {target.Value} by {xPixels.ToString(CultureInfo.InvariantCulture)},{yPixels.ToString(CultureInfo.InvariantCulture)} px.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(new[] { "fuel", "computer", "light" }, "<target>"),
            2 => CompletionResult.FromHintOptions(new[] { "1", "-1", "2", "-2", "6", "-6", "15", "-15" }, "<xPixels>"),
            3 => CompletionResult.FromHintOptions(new[] { "0", "1", "-1", "2", "-2" }, "<yPixels>"),
            4 => CompletionResult.FromHintOptions(new[] { "8", "16", "32" }, "[range]"),
            _ => CompletionResult.Empty,
        };
    }

    private bool TryGetPlayerEntity(IConsoleShell shell, out EntityUid player)
    {
        player = EntityUid.Invalid;
        var session = shell.Player;
        if (session == null)
        {
            shell.WriteError("Only players can use this command.");
            return false;
        }

        if (session.Status != SessionStatus.InGame ||
            session.AttachedEntity is not { Valid: true } attached)
        {
            shell.WriteError("You are not in-game.");
            return false;
        }

        player = attached;
        return true;
    }

    private static bool TryParsePixels(IConsoleShell shell, string value, out float pixels)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out pixels))
            return true;

        shell.WriteError("Pixel offsets must be numbers.");
        return false;
    }

    private EntityUid? FindNearest<T>(EntityUid player, float range, SharedTransformSystem xform)
        where T : IComponent
    {
        if (!_entManager.TryGetComponent(player, out TransformComponent? playerXform) ||
            playerXform.MapUid is not { } mapUid)
        {
            return null;
        }

        var playerPosition = xform.GetWorldPosition(playerXform);
        var maxDistance = range * range;
        var bestDistance = maxDistance;
        var best = EntityUid.Invalid;
        var query = _entManager.AllEntityQueryEnumerator<T, TransformComponent>();

        while (query.MoveNext(out var uid, out _, out var targetXform))
        {
            if (targetXform.MapUid != mapUid)
                continue;

            var distance = Vector2.DistanceSquared(playerPosition, xform.GetWorldPosition(targetXform));
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            best = uid;
        }

        return best;
    }
}
