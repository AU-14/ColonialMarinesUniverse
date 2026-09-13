using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Requisitions;

/// <summary>Private, off-map cargo being prepared for an elevator's current trip.</summary>
[RegisterComponent]
public sealed partial class RequisitionsDeliveryComponent : Component
{
    public readonly List<EntityUid> Roots = new();
    public readonly Queue<EntProtoId> Remaining = new();
    public EntityUid CurrentRoot;
}
