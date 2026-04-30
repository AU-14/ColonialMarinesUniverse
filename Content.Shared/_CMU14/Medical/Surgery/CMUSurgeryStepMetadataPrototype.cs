using System.Collections.Generic;
using Content.Shared.Body.Part;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Surgery;

/// <summary>
///     Tool categories are resolved at click time. Allowed values include:
///     <c>scalpel</c>, <c>hemostat</c>, <c>retractor</c>, <c>cautery</c>,
///     <c>bone_saw</c>, <c>bone_setter</c>, <c>bone_gel</c>,
///     <c>bone_graft</c>, <c>organ_clamp</c>.
/// </summary>
[Prototype("cmuSurgeryStepMetadata")]
public sealed partial class CMUSurgeryStepMetadataPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     The CMU surgery prototype id (matches a <c>CMSurgeryComponent</c>
    ///     entity prototype) this metadata describes.
    /// </summary>
    [DataField(required: true)]
    public string Surgery = string.Empty;

    /// <summary>
    ///     Leave null to fall back to the surgery prototype's
    ///     <c>MetaDataComponent.EntityName</c>.
    /// </summary>
    [DataField]
    public string? DisplayName;

    [DataField]
    public List<BodyPartType> ValidParts = new() { BodyPartType.Head, BodyPartType.Torso, BodyPartType.Arm, BodyPartType.Leg };

    [DataField]
    public string Category = "general";

    /// <summary>
    ///     Order MUST match the surgery prototype's <c>steps:</c> list. The
    ///     V2 system reads this by index — entries beyond the list length
    ///     fall back to "no specific tool required" / unlabelled.
    /// </summary>
    [DataField]
    public List<CMUSurgeryStepMetadataEntry> Steps = new();
}

[Serializable, NetSerializable, DataDefinition]
public sealed partial class CMUSurgeryStepMetadataEntry
{
    [DataField]
    public string StepId = string.Empty;

    [DataField]
    public string Label = string.Empty;

    [DataField]
    public string? ToolCategory;
}
