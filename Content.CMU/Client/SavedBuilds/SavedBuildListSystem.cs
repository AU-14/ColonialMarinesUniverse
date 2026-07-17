// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Content.Shared._AU14.SavedBuilds;
using Robust.Shared.ContentPack;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Client._AU14.SavedBuilds;

/// <summary>
/// LOCAL storage of saved builds. Builds are private files in this client's own user-data folder
/// (<c>saved_builds/</c>): the server never stores or lists them, so no other player can see them.
/// Sharing is manual - players copy .build.yml files between their folders. The server only produces
/// the file content on save (<see cref="SavedBuildDataEvent"/>) and consumes it again on placement.
/// </summary>
public sealed class SavedBuildListSystem : EntitySystem
{
    [Dependency] private readonly IResourceManager _resource = default!;

    public const string SaveDir = "/saved_builds";

    /// <summary>The current locally-stored saved builds.</summary>
    public IReadOnlyList<SavedBuildInfo> Builds => _builds;
    private List<SavedBuildInfo> _builds = new();

    /// <summary>Raised whenever the local list has been re-read.</summary>
    public event Action? ListUpdated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SavedBuildDataEvent>(OnBuildData);
    }

    /// <summary>Sane file name only - the id is used as a path under the save dir.</summary>
    private static bool IsValidBuildId(string? id)
    {
        return !string.IsNullOrEmpty(id)
               && id.EndsWith(".build.yml", StringComparison.Ordinal)
               && !id.Contains('/') && !id.Contains('\\') && !id.Contains("..");
    }

    /// <summary>The server finished serializing a build we asked to save: write it to our local folder.</summary>
    private void OnBuildData(SavedBuildDataEvent ev)
    {
        if (!IsValidBuildId(ev.FileName) || string.IsNullOrEmpty(ev.Yaml))
            return;

        var path = new ResPath(SaveDir).ToRootedPath() / ev.FileName;
        try
        {
            _resource.UserData.CreateDir(path.Directory);
            using var writer = _resource.UserData.OpenWriteText(path);
            writer.Write(ev.Yaml);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to write saved build '{ev.FileName}': {e}");
            return;
        }

        Refresh();
    }

    /// <summary>Reads the YAML of a locally saved build (for placement upload).</summary>
    public bool TryGetYaml(string id, out string yaml)
    {
        yaml = string.Empty;
        if (!IsValidBuildId(id))
            return false;

        var path = new ResPath(SaveDir).ToRootedPath() / id;
        try
        {
            if (!_resource.UserData.Exists(path))
                return false;

            using var reader = _resource.UserData.OpenText(path);
            yaml = reader.ReadToEnd();
            return yaml.Length > 0;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to read saved build '{id}': {e}");
            return false;
        }
    }

    /// <summary>Re-reads the local saved-builds folder and fires <see cref="ListUpdated"/>.</summary>
    public void Refresh()
    {
        var result = new List<SavedBuildInfo>();
        var dir = new ResPath(SaveDir).ToRootedPath();

        try
        {
            if (_resource.UserData.Exists(dir))
            {
                foreach (var entry in _resource.UserData.DirectoryEntries(dir))
                {
                    if (!entry.EndsWith(".build.yml", StringComparison.Ordinal))
                        continue;

                    if (TryReadRoot(dir / entry, out var root) && root.TryGet<MappingDataNode>("meta", out var meta))
                        result.Add(ReadInfo(entry, meta));
                }
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to enumerate saved builds: {e}");
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.InvariantCultureIgnoreCase));
        _builds = result;
        ListUpdated?.Invoke();
    }

    /// <summary>Deletes a locally saved build file, then refreshes.</summary>
    public void Delete(string id)
    {
        if (!IsValidBuildId(id))
            return;

        var path = new ResPath(SaveDir).ToRootedPath() / id;
        try
        {
            if (_resource.UserData.Exists(path))
                _resource.UserData.Delete(path);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to delete saved build '{id}': {e}");
        }

        Refresh();
    }

    /// <summary>Opens THIS client's saved-builds folder in the OS file explorer (how players share builds).</summary>
    public void OpenFolder()
    {
        var dir = new ResPath(SaveDir).ToRootedPath();
        try
        {
            _resource.UserData.CreateDir(dir);
            _resource.UserData.OpenOsWindow(dir);
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to open saved-builds folder: {e}");
        }
    }

    /// <summary>Renames a locally saved build (meta name + file name), then refreshes.</summary>
    public void Rename(string id, string newName)
    {
        newName = (newName ?? string.Empty).Trim();
        if (!IsValidBuildId(id) || string.IsNullOrEmpty(newName))
            return;

        var dir = new ResPath(SaveDir).ToRootedPath();
        var path = dir / id;
        if (!TryReadRoot(path, out var root))
            return;

        // Keep the author prefix if present so shared files keep their origin.
        var sep = id.IndexOf("__", StringComparison.Ordinal);
        var prefix = sep > 0 ? id[..sep] : "local";
        var newId = $"{prefix}__{SanitizeFileName(newName)}.build.yml";
        var newPath = dir / newId;

        // User-data backends and host filesystems differ in case sensitivity. Compare the complete directory
        // case-insensitively and reject any destination owned by a different build before opening it for write.
        if (_resource.UserData.Exists(dir) && _resource.UserData.DirectoryEntries(dir).Any(entry =>
                !string.Equals(entry, id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry, newId, StringComparison.OrdinalIgnoreCase)))
        {
            Log.Warning($"Refusing to rename saved build '{id}' to existing build '{newId}'.");
            return;
        }

        if (root.TryGet<MappingDataNode>("meta", out var meta))
        {
            meta.Remove("name");
            meta.Add("name", new ValueDataNode(newName));
        }

        try
        {
            using (var writer = _resource.UserData.OpenWriteText(newPath))
            {
                var stream = new YamlStream { new YamlDocument(root.ToYaml()) };
                stream.Save(new YamlMappingFix(new Emitter(writer)), false);
            }

            if (newPath != path)
                _resource.UserData.Delete(path);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to rename saved build '{id}' -> '{newName}': {e}");
        }

        Refresh();
    }

    // -------------------------------------------------------------------------
    // File parsing (metadata header only; the "build" entity blob is passed through verbatim)
    // -------------------------------------------------------------------------

    private bool TryReadRoot(ResPath path, out MappingDataNode root)
    {
        root = default!;
        try
        {
            using var reader = _resource.UserData.OpenText(path);
            foreach (var document in DataNodeParser.ParseYamlStream(reader))
            {
                if (document.Root is MappingDataNode mapping)
                {
                    root = mapping;
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to parse saved build '{path}': {e.Message}");
        }

        return false;
    }

    private static SavedBuildInfo ReadInfo(string id, MappingDataNode meta)
    {
        int.TryParse(MetaString(meta, "entityCount"), out var count);
        NetEntity.TryParse(MetaString(meta, "sourceGrid"), out var sourceGrid);
        return new SavedBuildInfo
        {
            Id = id,
            Name = MetaString(meta, "name"),
            Source = MetaString(meta, "source"),
            Author = MetaString(meta, "author"),
            EntityCount = count,
            TileCount = MetaInt(meta, "tileCount"),
            RelMinX = MetaFloat(meta, "relMinX"),
            RelMinY = MetaFloat(meta, "relMinY"),
            RelMaxX = MetaFloat(meta, "relMaxX"),
            RelMaxY = MetaFloat(meta, "relMaxY"),
            Preview = ReadPreview(meta),
            Tiles = ReadTiles(meta),
            SourceGrid = sourceGrid,
            AnchorX = MetaFloat(meta, "anchorX"),
            AnchorY = MetaFloat(meta, "anchorY"),
        };
    }

    private static string MetaString(MappingDataNode meta, string key)
    {
        return meta.TryGet<ValueDataNode>(key, out var node) ? node.Value : string.Empty;
    }

    private static float MetaFloat(MappingDataNode meta, string key)
    {
        float.TryParse(MetaString(meta, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
        return value;
    }

    private static int MetaInt(MappingDataNode meta, string key)
    {
        int.TryParse(MetaString(meta, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value);
        return value;
    }

    private static List<BuildPreviewEntity> ReadPreview(MappingDataNode meta)
    {
        var list = new List<BuildPreviewEntity>();
        if (!meta.TryGet<SequenceDataNode>("preview", out var seq))
            return list;

        foreach (var node in seq)
        {
            if (node is not MappingDataNode m)
                continue;

            list.Add(new BuildPreviewEntity
            {
                Proto = MetaString(m, "proto"),
                X = MetaFloat(m, "x"),
                Y = MetaFloat(m, "y"),
                Rot = MetaFloat(m, "rot"),
                Z = MetaInt(m, "z"),
            });
        }

        return list;
    }

    private static List<BuildPreviewTile> ReadTiles(MappingDataNode meta)
    {
        var list = new List<BuildPreviewTile>();
        if (!meta.TryGet<SequenceDataNode>("tiles", out var seq))
            return list;

        foreach (var node in seq)
        {
            if (node is not MappingDataNode m)
                continue;

            list.Add(new BuildPreviewTile
            {
                Tile = MetaString(m, "tile"),
                X = MetaFloat(m, "x"),
                Y = MetaFloat(m, "y"),
                Z = MetaInt(m, "z"),
            });
        }

        return list;
    }

    private static string SanitizeFileName(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var c in value)
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_');
        return sb.ToString();
    }
}
