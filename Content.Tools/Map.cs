using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Tools;

public sealed class Map
{
    // Top-level uid reference lists. Dangling entries after a merge fail map load in the engine.
    public static readonly string[] UidListNames = { "maps", "grids", "orphans", "nullspace" };

    public Map(string path)
    {
        Path = path;

        using var reader = new StreamReader(path);
        var stream = new YamlStream();
        stream.Load(reader);

        Root = stream.Documents[0].RootNode;
        TilemapNode = (YamlMappingNode) Root["tilemap"];
        GridsNode = RootMap.Children.ContainsKey("grids")
            ? (YamlSequenceNode) RootMap["grids"]
            : new YamlSequenceNode();
        EntitiesNode = (YamlSequenceNode) Root["entities"];

        foreach (var entry in EntitiesNode)
        {
            var mapping = (YamlMappingNode) entry;
            // Format 7 groups entities by prototype, the group node holds proto + entities.
            // Older flat formats list the entities directly.
            if (mapping.Children.ContainsKey("proto"))
            {
                HasProtoWrapper = true;
                var proto = mapping["proto"].ToString();
                foreach (var e in (YamlSequenceNode) mapping["entities"])
                {
                    var uid = uint.Parse(e["uid"].ToString());
                    if (uid >= NextAvailableEntityId)
                        NextAvailableEntityId = uid + 1;
                    Entities[uid] = (YamlMappingNode) e;
                    // The proto only exists on the group, keep it per entity for Save.
                    Protos[uid] = proto;
                }
            }
            else
            {
                var uid = uint.Parse(entry["uid"].ToString());
                if (uid >= NextAvailableEntityId)
                    NextAvailableEntityId = uid + 1;
                Entities[uid] = mapping;
            }
        }

        // Format 7 lists grid entities by uid. Format 4-6 saves have no such list,
        // grids are identified by their MapGrid component instead.
        foreach (var child in GridsNode)
            if (child is YamlScalarNode scalar && uint.TryParse(scalar.Value, out var uid))
                GridIds.Add(uid);
        if (GridIds.Count == 0 && !HasLegacyGrids)
            foreach (var uid in Entities.Keys)
                if (GetComponent(uid, "MapGrid") != null)
                    GridIds.Add(uid);
    }

    // Core

    public string Path { get; }

    public YamlNode Root { get; }

    // Useful

    public YamlMappingNode TilemapNode { get; }

    // uid list (format 7 and up) or legacy top-level grid definitions (format 3 and older)
    public YamlSequenceNode GridsNode { get; }

    private YamlSequenceNode EntitiesNode { get; }

    private bool HasProtoWrapper { get; set; }

    // Entities lookup

    public Dictionary<uint, YamlMappingNode> Entities { get; } = new Dictionary<uint, YamlMappingNode>();

    // Entity uid -> proto of the group it was parsed from. "" means no prototype.
    public Dictionary<uint, string> Protos { get; } = new Dictionary<uint, string>();

    public List<uint> GridIds { get; } = new List<uint>();

    public uint NextAvailableEntityId { get; set; }

    public bool HasLegacyGrids
        => GridsNode.Children.Count > 0 && GridsNode.Children[0] is YamlMappingNode;

    private YamlMappingNode RootMap => (YamlMappingNode) Root;

    // ----

    public YamlMappingNode? GetComponent(uint uid, string type)
    {
        if (!Entities.TryGetValue(uid, out var entity))
            return null;
        if (!entity.Children.ContainsKey("components"))
            return null;
        foreach (var component in (YamlSequenceNode) entity["components"])
            if (component["type"].ToString() == type)
                return (YamlMappingNode) component;
        return null;
    }

    public YamlMappingNode? GetGridChunks(uint uid)
    {
        var grid = GetComponent(uid, "MapGrid");
        if (grid is null || !grid.Children.ContainsKey("chunks"))
            return null;
        return (YamlMappingNode) grid["chunks"];
    }

    public bool HasUidRef(string list, uint uid)
    {
        if (!RootMap.Children.ContainsKey(list) || RootMap[list] is not YamlSequenceNode seq)
            return false;
        return seq.Children.Any(entry => uint.TryParse(entry.ToString(), out var v) && v == uid);
    }

    public void AddUidRef(string list, uint uid)
    {
        if (RootMap.Children.ContainsKey(list))
            ((YamlSequenceNode) RootMap[list]).Add(uid.ToString(CultureInfo.InvariantCulture));
    }

    public void Save(string fileName)
    {
        PruneDanglingUidRefs();

        // Preserve format: proto-wrapped if original had it
        EntitiesNode.Children.Clear();
        if (HasProtoWrapper)
        {
            // Each entity must return to its own proto group, the proto is not stored per entity.
            var groups = new Dictionary<string, YamlSequenceNode>();
            foreach (var kvp in Entities)
            {
                if (!groups.TryGetValue(Protos[kvp.Key], out var list))
                {
                    list = new YamlSequenceNode();
                    groups[Protos[kvp.Key]] = list;
                }
                list.Add(kvp.Value);
            }
            foreach (var kvp in groups)
            {
                var group = new YamlMappingNode();
                group.Add("proto", kvp.Key);
                group.Add("entities", kvp.Value);
                EntitiesNode.Add(group);
            }
        }
        else
        {
            foreach (var kvp in Entities)
                EntitiesNode.Add(kvp.Value);
        }

        using var writer = new StreamWriter(fileName);
        var document = new YamlDocument(Root);
        var stream = new YamlStream(document);
        var emitter = new Emitter(writer);
        var fixer = new TypeTagPreserver(emitter);

        stream.Save(fixer, false);

        writer.Flush();
    }

    public void Save()
        => Save(Path);

    private void PruneDanglingUidRefs()
    {
        foreach (var list in UidListNames)
        {
            if (!RootMap.Children.ContainsKey(list) || RootMap[list] is not YamlSequenceNode seq)
                continue;
            for (var i = seq.Children.Count - 1; i >= 0; i--)
            {
                if (uint.TryParse(seq.Children[i].ToString(), out var uid) && Entities.ContainsKey(uid))
                    continue;
                seq.Children.RemoveAt(i);
            }
        }
    }
}
