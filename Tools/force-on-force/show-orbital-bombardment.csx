using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Mind;
using Content.Server.CMU14.ForceOnForce;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.Rules;
using Content.Shared.Mobs.Components;
{
    var startingVariant = 0;
    var maps = ressys<SharedMapSystem>();
    var map = maps.CreateMap(out var mapId, runMapInit: true);
    ent.EnsureComponent<RMCPlanetComponent>(map);
    maps.SetAmbientLight(mapId, Color.FromHex("#BECAD6"));
    var grid = maps.CreateGridEntity(mapId);
    ent.EnsureComponent<RMCPlanetComponent>(grid.Owner);
    var floor = new Tile(res<ITileDefinitionManager>()["RMCFloorVehicleInteriorDarkSterile"].TileId);
    for (var x = -30; x <= 30; x++)
    for (var y = -24; y <= 24; y++)
        maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), floor);
    var session = res<Robust.Server.Player.IPlayerManager>().Sessions.Single();
    var viewer = ent.SpawnEntity("MobObserver", new EntityCoordinates(grid.Owner, 0, 0));
    ent.EnsureComponent<MobStateComponent>(viewer);
    ent.EnsureComponent<MarineComponent>(viewer).Faction = "govfor";
    ressys<SharedRankSystem>().SetRank(viewer, res<IPrototypeManager>().EnumeratePrototypes<RankPrototype>().First(r => r.Paygrade?.StartsWith("O") == true));
    ressys<MindSystem>().ControlMob(session.UserId, viewer);
    ressys<GameTicker>().PlayerJoinGame(session);
    ressys<SharedEyeSystem>().SetZoom(viewer, new Vector2(1.7f));
    typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset)).SetValue(ressys<GameTicker>(), res<IPrototypeManager>().Index<GamePresetPrototype>("ForceOnForce"));
    foreach (var position in new[] { new Vector2(-3, -1), new Vector2(-1, 2), new Vector2(2, -2), new Vector2(3, 2) })
    {
        var soldier = ent.SpawnEntity("MobHuman", new EntityCoordinates(grid.Owner, position));
        ent.EnsureComponent<Content.Shared.Damage.Components.GodmodeComponent>(soldier);
        ent.EnsureComponent<MarineComponent>(soldier).Faction = "opfor";
    }
    var console = ent.SpawnEntity("AU14TabletGovfor", new EntityCoordinates(grid.Owner, 1, 0));
    var system = ressys<ForceOnForceBombardmentSystem>();
    var cooldowns = typeof(ForceOnForceBombardmentSystem).GetField("_readyAt", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("This build does not contain the FoF bombardment system.");
    Robust.Shared.Timing.Timer.Spawn(15000, () =>
    {
        try
        {
            ((System.Collections.IDictionary) cooldowns.GetValue(system)).Clear();
            ent.EventBus.RaiseLocalEvent(console, new ForceOnForceBombardmentMessage(startingVariant) { Actor = viewer, UiKey = MarineCommunicationsComputerUI.Key });
        }
        catch (Exception error) { System.Console.WriteLine("Bombardment showcase failed: " + (error.InnerException ?? error)); }
    });
    write("Siren starts in 15 seconds, then an 8-second warning and four bombardment passes. Close the scripting window and console. All nearby bodies are protected; this demonstration resets the faction cooldown so you can replay it.");
}
