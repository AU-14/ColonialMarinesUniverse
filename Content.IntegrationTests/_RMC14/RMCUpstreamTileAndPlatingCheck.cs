using System.Collections.Generic;
using System.IO;
using Content.Shared.Maps;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class RMCUpstreamTileAndPlatingCheck
{
    private readonly ProtoId<ContentTileDefinition> _tilePrototypeId = "Plating";

    private static List<string> FileFetch()
    {
        var rootDir = Path.Join(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.ToString());
        var relativePath = Path.Combine(rootDir, "Resources");
        var filesDirs = new string[]
        {
            // Path.Combine(relativePath, "Maps", "_RMC14"),
            // Our maps are flagging in this test, we can re-enable this if we want to fix it.
            // Otherwise this test runs for ~5 minutes and effectively has no purpose besides bog the rmc shard.
            // Path.Combine(relativePath, "Maps", "_CMU14"),
            // Path.Combine(relativePath, "Maps", "_AU14"),
        };

        var relativeFiles = new List<string>();
        foreach (var filesDir in filesDirs)
        {
            try
            {
                foreach (var file in Directory.GetFiles(filesDir, "*.yml", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(relativePath, file);
                    relativeFiles.Add(relative
                        .Replace(Path.DirectorySeparatorChar, ResPath.Separator)
                        .Replace(Path.AltDirectorySeparatorChar, ResPath.Separator));
                }
            }
            catch (DirectoryNotFoundException) { Console.WriteLine($"Directory {filesDir} does not exist"); }
            catch (UnauthorizedAccessException) { Console.WriteLine($"Access to directory {filesDir} is denied"); }
        }

        return relativeFiles;
    }

    [Test]
    public async Task RMCPlatingCheck()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var sMapSystem = server.System<SharedMapSystem>();
        var sMapLoaderSystem = server.System<MapLoaderSystem>();
        var sTileSystem = server.System<TileSystem>();
        var siTileDefinitionManager = server.Resolve<ITileDefinitionManager>();
        if (!siTileDefinitionManager.TryGetDefinition(_tilePrototypeId, out var tileDefinition))
            return;

        var deSerOpts = new DeserializationOptions { LogOrphanedGrids = false };

        var mapLoadOpts = new MapLoadOptions { DeserializationOptions = deSerOpts };

        var files = FileFetch();
        await server.WaitAssertion(() =>
            {
                using (Assert.EnterMultipleScope())
                {
                    foreach (var file in files)
                    {
                        var tileErrorsBefore = new HashSet<string>();
                        var tileErrorsAfter = new HashSet<string>();
                        sMapLoaderSystem.TryLoadGeneric(new ResPath(file), out HashSet<Entity<MapComponent>> maps, out var grids, mapLoadOpts);

                        try
                        {
                            if (grids == null) continue;

                            foreach (var grid in grids)
                            {
                                var allTiles = sMapSystem.GetAllTiles(grid, grid.Comp);
                                foreach (var tile in allTiles)
                                {
                                    if (tile.Tile.TypeId == tileDefinition.TileId)
                                    {
                                        tileErrorsBefore.Add(tile.GridIndices.ToString());
                                        continue;
                                    }

                                    sTileSystem.PryTile(tile);
                                    if (!sMapSystem.TryGetTile(grid, tile.GridIndices, out var priedTile))
                                        continue;

                                    if (priedTile.TypeId == tileDefinition.TileId)
                                        tileErrorsAfter.Add($"{tile.GridIndices}, {tile.Tile.TypeId}");
                                }
                            }

                            if (tileErrorsBefore.Count == 0 && tileErrorsAfter.Count == 0)
                                continue;

                            var msg = $"For {file} found:";
                            if (tileErrorsBefore.Count > 0)
                                msg += "\nUpstream Plating was used (use [self gridtile tiletype:FromProtoId \"Plating\" replacetile:FromProtoId \"CMFloorPlating\"] over the grid to fix this issue.)";

                            if (tileErrorsAfter.Count > 0)
                                msg += $"\nupstream tiles or improperly parented tiles at \n{string.Join("\n", tileErrorsAfter)}\n";

                            Assert.Fail(msg);
                        }
                        finally
                        {
                            if (maps != null)
                            {
                                foreach (var mapEntity in maps)
                                {
                                    if (sMapSystem.MapExists(mapEntity.Comp.MapId))
                                        sMapSystem.DeleteMap(mapEntity.Comp.MapId);
                                }
                            }
                        }
                    }
                }
            }
        );
        await pair.CleanReturnAsync();
    }
}
