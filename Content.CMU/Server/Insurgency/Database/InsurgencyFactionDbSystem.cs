using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Shared._AU14.Insurgency;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server._AU14.Insurgency.Database;

/// <summary>
///     Persistence for host-authored Default factions. Converts a <see cref="FactionDefinition"/>
///     to and from a stored YAML blob and exposes CRUD in domain terms, so callers never touch the
///     raw DB row or the serializer.
///
///     Serialization stays here (Content.Server) rather than in the low-level DB project, which must
///     not depend on content. Only the known schema is ever deserialized, never executable content.
/// </summary>
public sealed class InsurgencyFactionDbSystem : EntitySystem
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ISerializationManager _serialization = default!;

    /// <summary>
    ///     A stored faction with its DB id and the deserialized definition. Rows that fail to parse
    ///     are skipped rather than returned, so one corrupt blob cannot break the whole list.
    /// </summary>
    public readonly record struct StoredFaction(int Id, bool IsDefault, FactionDefinition Definition);

    /// <summary>
    ///     Loads all stored factions, skipping any row whose blob fails to parse.
    /// </summary>
    public async Task<List<StoredFaction>> GetFactionsAsync()
    {
        var rows = await _db.GetFactionDefinitions();
        var result = new List<StoredFaction>(rows.Count);
        foreach (var row in rows)
        {
            var def = Deserialize(row.Data);
            if (def == null)
                continue;

            result.Add(new StoredFaction(row.Id, row.IsDefault, def));
        }

        return result;
    }

    public async Task<FactionDefinition?> GetFactionAsync(int id)
    {
        var row = await _db.GetFactionDefinition(id);
        return row == null ? null : Deserialize(row.Data);
    }

    /// <summary>
    ///     Saves a new faction and returns its assigned DB id.
    /// </summary>
    public async Task<int> AddFactionAsync(FactionDefinition definition, bool isDefault)
    {
        var now = System.DateTime.UtcNow;
        var row = new AU14FactionDefinition
        {
            Title = Truncate(definition.Metadata.Title, FactionDefinition.MaxTitleLength),
            SchemaVersion = definition.SchemaVersion,
            IsDefault = isDefault,
            Data = Serialize(definition),
            CreatedAt = now,
            LastEditedAt = now,
        };

        return await _db.AddFactionDefinition(row);
    }

    /// <summary>
    ///     Overwrites an existing faction. Returns false if the id no longer exists.
    /// </summary>
    public async Task<bool> UpdateFactionAsync(int id, FactionDefinition definition, bool isDefault)
    {
        var row = new AU14FactionDefinition
        {
            Id = id,
            Title = Truncate(definition.Metadata.Title, FactionDefinition.MaxTitleLength),
            SchemaVersion = definition.SchemaVersion,
            IsDefault = isDefault,
            Data = Serialize(definition),
            LastEditedAt = System.DateTime.UtcNow,
        };

        return await _db.UpdateFactionDefinition(row);
    }

    public async Task<bool> DeleteFactionAsync(int id)
    {
        var row = await _db.GetFactionDefinition(id);
        if (row == null)
            return false;

        var definition = Deserialize(row.Data);
        if (definition?.Metadata.BuiltinOverrideOf != null)
            return false;

        return await _db.DeleteFactionDefinition(id);
    }

    // ---------------------------------------------------------------------
    // Serialization. YAML text is version-tolerant and human-inspectable, which matters for a blob
    // that may outlive several schema versions.
    // ---------------------------------------------------------------------

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

        try
        {
            using var reader = new StringReader(data);
            var documents = DataNodeParser.ParseYamlStream(reader).ToArray();
            if (documents.Length == 0)
                return null;

            return _serialization.Read<FactionDefinition>(documents[0].Root, notNullableOverride: true);
        }
        catch (System.Exception e)
        {
            Log.Error($"Failed to deserialize stored INSFOR faction: {e}");
            return null;
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
