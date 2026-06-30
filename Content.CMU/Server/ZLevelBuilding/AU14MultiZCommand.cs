// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Content.Server.Administration;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): lists every map with its AU14 "Multi Z-Level" status (whether players may build
/// the overhaul's stairs / vertical floors there) and lets an admin toggle it per map or globally at runtime.
///
/// This is the live counterpart to the mapper opt-out (<see cref="ZBuildableMapComponent"/> <c>enabled: false</c>
/// in a map file): use it to confirm which maps allow z-building and to switch a map off so players can't build
/// under it. The toggle is networked (the build condition + cave-in vignette respect it immediately) but, like
/// any runtime change, it is not persisted - bake it into the map prototype to make it permanent.
/// </summary>
[AdminCommand(AdminFlags.Mapping)]
public sealed class AU14MultiZCommand : IConsoleCommand
{
    public string Command => "au_multiz";
    public string Description => "List maps with their AU14 Multi Z-Level (vertical building) status, or toggle it per map / globally.";
    public string Help => "au_multiz  (list)  |  au_multiz <mapId> <on|off>  |  au_multiz global <on|off>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var building = entMan.System<ZLevelBuildingSystem>();

        // No args: list every map.
        if (args.Length == 0)
        {
            shell.WriteLine($"Global AU14 z-building: {(building.GloballyEnabled ? "ENABLED" : "DISABLED")}  (toggle: au_multiz global on|off)");
            var query = entMan.AllEntityQueryEnumerator<MapComponent>();
            while (query.MoveNext(out var uid, out var map))
            {
                var yes = building.IsEnabledOn(uid);
                shell.WriteLine($"  MapId {map.MapId,-4} {entMan.ToPrettyString(uid),-28} - Multi Z-Level: {(yes ? "Yes" : "No")}");
            }
            return;
        }

        if (args.Length != 2)
        {
            shell.WriteError("Usage: au_multiz <mapId|global> <on|off>");
            return;
        }

        var on = args[1].Equals("on", StringComparison.OrdinalIgnoreCase);
        if (!on && !args[1].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            shell.WriteError("Second argument must be 'on' or 'off'.");
            return;
        }

        // Global switch.
        if (args[0].Equals("global", StringComparison.OrdinalIgnoreCase))
        {
            building.GloballyEnabled = on;
            shell.WriteLine($"Global AU14 z-building is now {(on ? "ENABLED" : "DISABLED")}.");
            return;
        }

        if (!int.TryParse(args[0], out var mapIdInt))
        {
            shell.WriteError("Map argument must be a numeric MapId (run 'au_multiz' to list them) or 'global'.");
            return;
        }

        var mapManager = IoCManager.Resolve<IMapManager>();
        var mapId = new MapId(mapIdInt);
        if (!mapManager.MapExists(mapId))
        {
            shell.WriteError($"No map with MapId {mapIdInt}.");
            return;
        }

        var mapUid = mapManager.GetMapEntityId(mapId);
        var comp = entMan.EnsureComponent<ZBuildableMapComponent>(mapUid);
        comp.Enabled = on;
        entMan.Dirty(mapUid, comp);

        shell.WriteLine($"Map {mapIdInt} Multi Z-Level set to {(on ? "Yes" : "No")}. Players {(on ? "can now" : "can no longer")} build AU14 z-level stairs/floors here.");
    }
}
