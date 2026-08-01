using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Content.Shared._AU14.Insurgency;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Client._AU14.Insurgency.CustomFactions;

/// <summary>
///     Reads and writes the player's own Custom INSFOR factions as YAML files under their user data.
///     These files never leave the machine until the player picks one, at which point the parsed
///     definition is sent to the server and fully re-validated there. Storing them as plain YAML keeps
///     them inspectable and shareable.
///
///     Mirrors the server DB system's serialize/deserialize so a Custom faction and a Default faction
///     use the exact same on-disk shape.
/// </summary>
public sealed class InsurgencyCustomFactionStore
{
    // ---------------------------------------------------------------------
    // Tunables. Where local custom factions live and their file extension.
    // ---------------------------------------------------------------------
    private static readonly ResPath Directory = new("/insfor_custom_factions");
    private const string Extension = ".yml";

    private readonly IResourceManager _resource;
    private readonly ISerializationManager _serialization;
    private readonly ISawmill _log;

    public InsurgencyCustomFactionStore()
    {
        _resource = IoCManager.Resolve<IResourceManager>();
        _serialization = IoCManager.Resolve<ISerializationManager>();
        _log = Logger.GetSawmill("insfor.custom");
    }

    /// <summary>A local custom faction: the file name (without extension) and its parsed definition.</summary>
    public readonly record struct StoredCustomFaction(string Name, FactionDefinition Definition);

    /// <summary>
    ///     Loads every local custom faction, skipping any file that fails to parse so one bad file
    ///     cannot hide the rest.
    /// </summary>
    public List<StoredCustomFaction> List()
    {
        var result = new List<StoredCustomFaction>();
        if (!_resource.UserData.Exists(Directory))
            return result;

        foreach (var path in _resource.UserData.Find($"{Directory.ToRelativePath()}/*{Extension}").Item1)
        {
            var def = TryLoad(path);
            if (def != null)
                result.Add(new StoredCustomFaction(path.FilenameWithoutExtension, def));
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }

    /// <summary>
    ///     Saves a definition under the given file-safe name, overwriting an existing file of that name.
    /// </summary>
    public void Save(string name, FactionDefinition definition)
    {
        _resource.UserData.CreateDir(Directory);
        var path = PathFor(name);

        using var writer = _resource.UserData.OpenWriteText(path);
        writer.Write(Serialize(definition));
    }

    public void Delete(string name)
    {
        var path = PathFor(name);
        if (_resource.UserData.Exists(path))
            _resource.UserData.Delete(path);
    }

    // Strips anything that is not safe in a file name so a faction title cannot escape the directory.
    public static string SanitizeName(string name)
    {
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "faction" : cleaned;
    }

    private ResPath PathFor(string name) => Directory / (SanitizeName(name) + Extension);

    private FactionDefinition? TryLoad(ResPath path)
    {
        try
        {
            using var reader = _resource.UserData.OpenText(path);
            return Deserialize(reader.ReadToEnd());
        }
        catch (Exception e)
        {
            _log.Error($"Failed to read custom INSFOR faction {path}: {e}");
            return null;
        }
    }

    private string Serialize(FactionDefinition definition)
    {
        var node = _serialization.WriteValue(definition, alwaysWrite: true, notNullableOverride: true);
        var document = new YamlDocument(node.ToYamlNode());
        var stream = new YamlStream { document };

        using var writer = new StringWriter(new StringBuilder());
        stream.Save(new YamlNoDocEndDotsFix(new YamlMappingFix(new Emitter(writer))), false);
        return writer.ToString();
    }

    private FactionDefinition? Deserialize(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return null;

        using var reader = new StringReader(data);
        var documents = DataNodeParser.ParseYamlStream(reader).ToArray();
        if (documents.Length == 0)
            return null;

        return _serialization.Read<FactionDefinition>(documents[0].Root, notNullableOverride: true);
    }
}
