using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace Content.Tools;

public sealed class Merger
{
    public Map MapOurs { get; }
    public Map MapBased { get; }
    public Map MapOther { get; }

    public Dictionary<int, int> TileMapFromOtherToOurs { get; } = new Dictionary<int, int>();
    public Dictionary<int, int> TileMapFromBasedToOurs { get; } = new Dictionary<int, int>();
    public Dictionary<uint, uint> EntityMapFromOtherToOurs { get; } = new Dictionary<uint, uint>();

    private const int ChunkTileCount = 16 * 16;

    // Tile byte layout by chunk version: 4 bytes (v<6: ushort id + flags + variant),
    // 6 (v6: int32 id + flags + variant), 7 (v7+: adds a rotationMirroring byte).
    private readonly record struct TileData(int Id, byte Flags, byte Variant, byte Rotation);

    public Merger(Map ours, Map based, Map other)
    {
        MapOurs = ours;
        MapBased = based;
        MapOther = other;
    }

    public bool Merge()
    {
        PlanTileMapping(TileMapFromOtherToOurs, MapOther);
        PlanTileMapping(TileMapFromBasedToOurs, MapBased);
        if (!MergeTiles())
            return false;
        PlanEntityMapping();
        return MergeEntities();
    }

    // -- Tiles --

    public void PlanTileMapping(Dictionary<int, int> relativeOtherToOurs, Map relativeOther)
    {
        var mapping = new Dictionary<string, int>();
        var nextAvailable = 0;
        foreach (var kvp in MapOurs.TilemapNode)
        {
            var k = int.Parse(kvp.Key.ToString());
            var v = kvp.Value.ToString();
            mapping[v] = k;
            if (k >= nextAvailable)
                nextAvailable = k + 1;
        }
        foreach (var kvp in relativeOther.TilemapNode)
        {
            var k = int.Parse(kvp.Key.ToString());
            var v = kvp.Value.ToString();
            if (mapping.ContainsKey(v))
            {
                relativeOtherToOurs[k] = mapping[v];
            }
            else
            {
                MapOurs.TilemapNode.Add(nextAvailable.ToString(CultureInfo.InvariantCulture), v);
                relativeOtherToOurs[k] = nextAvailable++;
            }
        }
    }

    public bool MergeTiles()
    {
        if (MapOurs.HasLegacyGrids)
            return MergeTilesLegacy();

        // Format 4 and up: tiles live in the MapGrid component of grid entities,
        // so grids pair up by their stable entity uid and count does not matter.
        foreach (var gridUid in MapOurs.GridIds)
        {
            var ourChunks = MapOurs.GetGridChunks(gridUid);
            if (ourChunks is null)
            {
                Console.WriteLine($"WARNING: grid entity {gridUid} has no MapGrid chunks, skipping its tile merge.");
                continue;
            }

            var aMap = ConvertTileChunks(ourChunks);
            var bMap = ConvertTileChunks(MapBased.GetGridChunks(gridUid) ?? new YamlMappingNode());
            var cMap = ConvertTileChunks(MapOther.GetGridChunks(gridUid) ?? new YamlMappingNode());
            if (!MergeTileChunks(ourChunks, aMap, bMap, cMap, GetTileSize(aMap, cMap)))
                return false;
        }
        return true;
    }

    private bool MergeTilesLegacy()
    {
        // Legacy maps (format 3 and older) keep grid definitions top level and support a single grid.
        if (MapOurs.GridsNode.Children.Count != 1
            || MapBased.GridsNode.Children.Count != 1
            || MapOther.GridsNode.Children.Count != 1
            || MapBased.GridsNode.Children[0] is not YamlMappingNode
            || MapOther.GridsNode.Children[0] is not YamlMappingNode)
        {
            Console.WriteLine("one or more files had an amount of grids not equal to 1");
            return false;
        }

        var a = (YamlMappingNode) MapOurs.GridsNode.Children[0];
        var b = (YamlMappingNode) MapBased.GridsNode.Children[0];
        var c = (YamlMappingNode) MapOther.GridsNode.Children[0];
        var aChunks = (YamlSequenceNode) a["chunks"];
        var bChunks = (YamlSequenceNode) b["chunks"];
        var cChunks = (YamlSequenceNode) c["chunks"];
        var aMap = ConvertTileChunks(aChunks);
        var bMap = ConvertTileChunks(bChunks);
        var cMap = ConvertTileChunks(cChunks);
        return MergeTileChunks(aChunks, aMap, bMap, cMap, GetTileSize(aMap, cMap));
    }

    public bool MergeTileChunks(
        YamlNode chunks,
        Dictionary<string, YamlMappingNode> aMap,
        Dictionary<string, YamlMappingNode> bMap,
        Dictionary<string, YamlMappingNode> cMap,
        int tileSize)
    {
        // Union of ours and other. Based is deliberately excluded so chunk deletion by other works.
        var inds = aMap.Keys.Union(cMap.Keys).ToHashSet();

        foreach (var ind in inds)
        {
            var aBytes = GetChunkBytes(aMap, ind, tileSize);
            var bBytes = GetChunkBytes(bMap, ind, tileSize);
            var cBytes = GetChunkBytes(cMap, ind, tileSize);
            // A length mismatch means the chunk was serialized with a different chunk version
            // than ours' tiles: merging it would silently corrupt every tile. Fail instead.
            if (aBytes.Length % tileSize != 0 || bBytes.Length % tileSize != 0 || cBytes.Length % tileSize != 0)
            {
                Console.WriteLine($"chunk {ind} has a tile size different from ours, refusing to merge it");
                return false;
            }

            using var a = new MemoryStream(aBytes);
            using var b = new MemoryStream(bBytes);
            using var c = new MemoryStream(cBytes);
            using var aR = new BinaryReader(a);
            using var bR = new BinaryReader(b);
            using var cR = new BinaryReader(c);

            var outB = new byte[ChunkTileCount * tileSize];

            {
                using var outS = new MemoryStream(outB);
                using var outW = new BinaryWriter(outS);

                for (var i = 0; i < ChunkTileCount; i++)
                {
                    var aI = ReadTile(aR, tileSize);
                    var bI = ReadTile(bR, tileSize);
                    var cI = ReadTile(cR, tileSize);
                    bI = bI with { Id = MapTileId(bI.Id, TileMapFromBasedToOurs) };
                    cI = cI with { Id = MapTileId(cI.Id, TileMapFromOtherToOurs) };

                    var result = aI;
                    if (aI == bI)
                    {
                        // If aI == bI then aI did not change anything, so cI always wins
                        result = cI;
                    }
                    else if (bI != cI)
                    {
                        // If bI != cI then cI definitely changed something (conflict, but overrides aI)
                        result = cI;
                        Console.WriteLine("WARNING: Tile (" + ind + ")[" + i + "] was changed by both branches.");
                    }
                    WriteTile(outW, result, tileSize);
                }
            }

            if (aMap.TryGetValue(ind, out var chunk))
            {
                // Actually output chunk
                chunk.Children["tiles"] = Convert.ToBase64String(outB);
            }
            else if (cMap.TryGetValue(ind, out var otherChunk))
            {
                // Chunk added by other, carry it over with merged tiles
                var copy = (YamlMappingNode) YamlTools.CopyYamlNodes(otherChunk);
                copy.Children["tiles"] = Convert.ToBase64String(outB);
                if (chunks is YamlMappingNode mapping)
                    mapping.Add(ind, copy);
                else
                    ((YamlSequenceNode) chunks).Add(copy);
            }
        }
        return true;
    }

    public static int GetTileSize(Dictionary<string, YamlMappingNode> aMap, Dictionary<string, YamlMappingNode> cMap)
    {
        if (aMap.Count > 0)
            return TileSize(aMap.Values.First());
        if (cMap.Count > 0)
            return TileSize(cMap.Values.First());
        return 7;
    }

    public static int TileSize(YamlMappingNode chunk)
    {
        if (!chunk.Children.ContainsKey("version"))
            return 4;
        var version = int.Parse(chunk["version"].ToString());
        return version switch
        {
            < 6 => 4,
            6 => 6,
            _ => 7
        };
    }

    private static TileData ReadTile(BinaryReader r, int tileSize)
    {
        var id = tileSize == 4 ? r.ReadUInt16() : r.ReadInt32();
        return new TileData(id, r.ReadByte(), r.ReadByte(), tileSize >= 7 ? r.ReadByte() : (byte) 0);
    }

    private static void WriteTile(BinaryWriter w, TileData tile, int tileSize)
    {
        if (tileSize == 4)
            w.Write((ushort) tile.Id);
        else
            w.Write(tile.Id);
        w.Write(tile.Flags);
        w.Write(tile.Variant);
        if (tileSize >= 7)
            w.Write(tile.Rotation);
    }

    private int MapTileId(int id, Dictionary<int, int> mapping)
        => mapping.TryGetValue(id, out var ours)
            ? ours
            : throw new KeyNotFoundException($"tile id {id} is missing from the tile map");

    public static Dictionary<string, YamlMappingNode> ConvertTileChunks(YamlNode chunks)
    {
        var map = new Dictionary<string, YamlMappingNode>();
        switch (chunks)
        {
            case YamlSequenceNode sequence:
                foreach (var chunk in sequence)
                    map[chunk["ind"].ToString()] = (YamlMappingNode) chunk;
                break;
            case YamlMappingNode mapping:
                foreach (var kvp in mapping.Children)
                    map[kvp.Key.ToString()] = (YamlMappingNode) kvp.Value!;
                break;
        }
        return map;
    }

    public static byte[] GetChunkBytes(Dictionary<string, YamlMappingNode> chunks, string ind, int tileSize)
    {
        if (!chunks.TryGetValue(ind, out var chunk))
            return new byte[ChunkTileCount * tileSize];
        return Convert.FromBase64String(chunk["tiles"].ToString());
    }

    // -- Entities --

    public void PlanEntityMapping()
    {
        // Ok, so here's how it works:
        // 1. Entities that do not exist in "based" are additions.
        // 2. Entities that exist in "based" but do not exist in the one map or the other are removals.

        // Find modifications and deletions
        foreach (var kvp in MapBased.Entities)
        {
            var deletedByOurs = !MapOurs.Entities.ContainsKey(kvp.Key);
            var deletedByOther = !MapOther.Entities.ContainsKey(kvp.Key);
            if (deletedByOther && !deletedByOurs)
            {
                // Delete
                MapOurs.Entities.Remove(kvp.Key);
            }
            else if (!(deletedByOurs || deletedByOther))
            {
                // Modify
                EntityMapFromOtherToOurs[kvp.Key] = kvp.Key;
            }
        }

        // Find additions
        foreach (var kvp in MapOther.Entities)
        {
            if (MapBased.Entities.ContainsKey(kvp.Key))
                continue;

            if (MapOurs.Entities.ContainsKey(kvp.Key) && YamlNodesEqual(kvp.Value, MapOurs.Entities[kvp.Key]))
            {
                // Both sides added the same uid with identical content, keep ours' copy
                EntityMapFromOtherToOurs[kvp.Key] = kvp.Key;
                continue;
            }

            // New
            var newId = MapOurs.NextAvailableEntityId++;
            EntityMapFromOtherToOurs[kvp.Key] = newId;
        }
    }

    // Entities on both sides are only deduplicated when byte-identical, a uid collision
    // between different entities must stay a renumbered import.
    private static bool YamlNodesEqual(YamlNode a, YamlNode b)
    {
        using var aWriter = new StringWriter();
        new YamlStream(new YamlDocument(a)).Save(aWriter, false);
        using var bWriter = new StringWriter();
        new YamlStream(new YamlDocument(b)).Save(bWriter, false);
        return aWriter.ToString() == bWriter.ToString();
    }

    public bool MergeEntities()
    {
        var success = true;
        foreach (var kvp in EntityMapFromOtherToOurs)
        {
            // For debug use.
            // Console.WriteLine("Entity C/" + kvp.Key + " -> A/" + kvp.Value);
            YamlMappingNode oursEnt;
            if (MapOurs.Entities.ContainsKey(kvp.Value))
            {
                oursEnt = MapOurs.Entities[kvp.Value];
                if (!MapBased.Entities.TryGetValue(kvp.Value, out var basedEnt))
                {
                    basedEnt = oursEnt;
                }

                // A proto group rename by other exists only in Protos, not in the entity
                // node, so carry it over here or it is silently dropped.
                if (MapOurs.Protos.TryGetValue(kvp.Value, out var oursProto)
                    && MapBased.Protos.TryGetValue(kvp.Value, out var basedProto)
                    && MapOther.Protos.TryGetValue(kvp.Key, out var otherProto))
                {
                    if (oursProto == basedProto && otherProto != basedProto)
                    {
                        MapOurs.Protos[kvp.Value] = otherProto;
                        Console.WriteLine($"WARNING: Entity{kvp.Value} proto renamed by other ({basedProto} -> {otherProto}), taking other's value.");
                    }
                    else if (oursProto != basedProto && otherProto != basedProto && oursProto != otherProto)
                        Console.WriteLine($"WARNING: Entity{kvp.Value} proto renamed by both branches ({oursProto} vs {otherProto}), keeping ours.");
                }

                if (!MergeEntityNodes(oursEnt, basedEnt, MapOther.Entities[kvp.Key]))
                {
                    Console.WriteLine("Unable to successfully merge entity C/" + kvp.Key);
                    success = false;
                }
            }
            else
            {
                oursEnt = (YamlMappingNode) YamlTools.CopyYamlNodes(MapOther.Entities[kvp.Key]);
                if (!MapEntity(oursEnt))
                {
                    Console.WriteLine("Unable to successfully import entity C/" + kvp.Key);
                    success = false;
                }
                else
                {
                    MapOurs.Entities[kvp.Value] = oursEnt;
                    MapOurs.Protos[kvp.Value] = MapOther.Protos[kvp.Key];
                    // Root uid lists (maps, grids, orphans, nullspace) must gain the new id too
                    foreach (var list in Map.UidListNames)
                        if (MapOther.HasUidRef(list, kvp.Key))
                            MapOurs.AddUidRef(list, kvp.Value);
                }
            }
            oursEnt.Children["uid"] = kvp.Value.ToString(CultureInfo.InvariantCulture);
        }
        return success;
    }

    public bool MergeEntityNodes(YamlMappingNode ours, YamlMappingNode based, YamlMappingNode other)
    {
        // Copy to intermediate
        var otherMapped = (YamlMappingNode) YamlTools.CopyYamlNodes(other);
        if (!MapEntity(otherMapped))
            return false;
        // Merge stuff that isn't components
        var path = "Entity" + other["uid"];
        YamlTools.MergeYamlMappings(ours, based, otherMapped, path, new[] { "components" });
        // Components are special
        var ourComponents = new Dictionary<string, YamlMappingNode>();
        var basedComponents = new Dictionary<string, YamlMappingNode>();
        var ourComponentsNode = (YamlSequenceNode) ours["components"];
        var basedComponentsNode = (YamlSequenceNode) based["components"];
        var otherComponentsNode = (YamlSequenceNode) otherMapped["components"];
        foreach (var component in ourComponentsNode)
        {
            var name = component["type"].ToString();
            ourComponents[name] = (YamlMappingNode) component;
        }
        foreach (var component in basedComponentsNode)
        {
            var name = component["type"].ToString();
            basedComponents[name] = (YamlMappingNode) component;
        }
        foreach (var otherComponent in otherComponentsNode)
        {
            var name = otherComponent["type"].ToString();
            if (ourComponents.ContainsKey(name))
            {
                var ourComponent = ourComponents[name];
                if (!basedComponents.TryGetValue(name, out var basedComponent))
                    basedComponent = new YamlMappingNode();

                if (name == "MapGrid")
                {
                    // Chunks were already merged tile-wise in MergeTiles and their tile ids
                    // reference different tile maps, only merge the remaining fields.
                    YamlTools.MergeYamlMappings(
                        ourComponent,
                        basedComponent,
                        (YamlMappingNode) otherComponent,
                        path + "/components/" + name,
                        new[] { "chunks" });
                }
                else
                {
                    YamlTools.MergeYamlNodes(ourComponent, basedComponent, otherComponent, path + "/components/" + name);
                }
            }
            else
            {
                ourComponentsNode.Add(otherComponent);
            }
        }
        return true;
    }

    public bool MapEntity(YamlMappingNode other)
    {
        var path = "Entity" + other["uid"];
        if (other.Children.ContainsKey("components"))
        {
            var components = (YamlSequenceNode) other["components"];
            foreach (var component in components)
            {
                var type = component["type"].ToString();
                if (type == "Transform")
                {
                    if (!MapEntityProperty((YamlMappingNode) component, "parent", path))
                        return false;
                }
                else if (type == "ContainerContainer")
                {
                    if (!MapEntityRecursiveAndBadly(component, path))
                        return false;
                }
                else if (type == "MapGrid")
                {
                    // Imported grid entities carry tile ids from the other file's tile map
                    if (!RemapGridTiles((YamlMappingNode) component))
                        return false;
                }
            }
        }
        return true;
    }

    public bool RemapGridTiles(YamlMappingNode mapGrid)
    {
        if (!mapGrid.Children.ContainsKey("chunks"))
            return true;

        foreach (var kvp in ((YamlMappingNode) mapGrid["chunks"]).Children)
        {
            var chunk = (YamlMappingNode) kvp.Value!;
            var bytes = Convert.FromBase64String(chunk["tiles"].ToString());
            var tileSize = TileSize(chunk);

            using var input = new MemoryStream(bytes);
            using var reader = new BinaryReader(input);
            using var output = new MemoryStream(bytes.Length);
            using var writer = new BinaryWriter(output);
            for (var i = 0; i < bytes.Length / tileSize; i++)
            {
                var tile = ReadTile(reader, tileSize);
                tile = tile with { Id = MapTileId(tile.Id, TileMapFromOtherToOurs) };
                WriteTile(writer, tile, tileSize);
            }

            chunk.Children["tiles"] = Convert.ToBase64String(output.ToArray());
        }
        return true;
    }

    public bool MapEntityProperty(YamlMappingNode node, string property, string path)
    {
        if (node.Children.ContainsKey(property))
        {
            var prop = node[property];
            if (prop is YamlScalarNode yamlProp)
                return MapEntityProperty(yamlProp, path + "/" + property);
        }
        return true;
    }

    public bool MapEntityProperty(YamlScalarNode node, string path)
    {
        if (uint.TryParse(node.ToString(), out var uid))
        {
            if (EntityMapFromOtherToOurs.ContainsKey(uid))
            {
                node.Value = EntityMapFromOtherToOurs[uid].ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                Console.WriteLine($"Error finding UID in MapEntityRecursiveAndBadly {path}. To fix this, the merge driver needs to be improved.");
                return false;
            }
        }
        return true;
    }

    public bool MapEntityRecursiveAndBadly(YamlNode node, string path)
    {
        switch (node)
        {
            case YamlSequenceNode subSequence:
                var idx = 0;
                foreach (var val in subSequence)
                    if (!MapEntityRecursiveAndBadly(val, path + "/" + idx++))
                        return false;
                return true;
            case YamlMappingNode subMapping:
                foreach (var kvp in subMapping)
                    if (!MapEntityRecursiveAndBadly(kvp.Key, path))
                        return false;
                foreach (var kvp in subMapping)
                    if (!MapEntityRecursiveAndBadly(kvp.Value, path + "/" + kvp.Key))
                        return false;
                return true;
            case YamlScalarNode subScalar:
                return MapEntityProperty(subScalar, path);
            default:
                throw new ArgumentException($"Unrecognized YAML node type: {node.GetType()} at {path}");
        }
    }
}
