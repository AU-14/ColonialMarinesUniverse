using System.Collections.Generic;
using Content.Shared.Materials;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Materials;

/// <summary>Converts reclaimed material identities while preserving their volume.</summary>
[RegisterComponent]
public sealed partial class CMUMaterialReclaimerComponent : Component
{
    [DataField(required: true)]
    public Dictionary<ProtoId<MaterialPrototype>, ProtoId<MaterialPrototype>> Conversions = new();
}

/// <summary>Raised on a reclaimer before depositing recovered material into its storage.</summary>
[ByRefEvent]
public record struct CMUReclaimMaterialEvent(ProtoId<MaterialPrototype> Material);
