using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.AU14.Origin;

/// <summary>
/// Represents a character origin that can add components, accents, items, and traits to a character at spawn.
/// </summary>
[Prototype("Origin")]
public sealed partial class OriginPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    /// <summary>
    /// Display name for the origin.
    /// </summary>
    [DataField(required: true)]
    public LocId Name { get; private set; } = string.Empty;

    /// <summary>
    /// Description shown in the UI.
    /// </summary>
    [DataField]
    public LocId Description { get; private set; } = string.Empty;

    /// <summary>
    /// Components to add to the character entity at spawn.
    /// </summary>
    [DataField]
    public ComponentRegistry Components { get; private set; } = new();

    /// <summary>
    /// Accent component IDs to add at spawn.
    /// </summary>
    [DataField]
    public List<string> Accents { get; private set; } = new();

    /// <summary>
    /// Entity prototypes to spawn and put in inventory at spawn.
    /// </summary>
    [DataField]
    public List<EntProtoId> StartingItems { get; private set; } = new();

    /// <summary>
    /// Trait prototype IDs to apply at spawn.
    /// </summary>
    [DataField]
    public List<ProtoId<Content.Shared.Traits.TraitPrototype>> Traits { get; private set; } = new();

    /// <summary>
    /// Whether this origin can be selected at round start.
    /// </summary>
    [DataField]
    public bool RoundStart { get; private set; } = true;
}

