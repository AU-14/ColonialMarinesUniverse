using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Metabolism;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects;

/// <summary>
/// Chemistry-specific context for an <see cref="EntityEffect"/>.
/// </summary>
/// <remarks>
/// This is local runtime context. It is intentionally not serialized or stored on an entity.
/// </remarks>
public readonly record struct ReagentEffectContext(
    ReagentPrototype Reagent,
    Solution? Source,
    EntityUid? SourceEntity,
    EntityUid? Organ,
    ReagentQuantity Quantity,
    ProtoId<MetabolismStagePrototype>? Stage,
    ReactionMethod? Method,
    ReagentEffectOrigin Origin);

public enum ReagentEffectOrigin : byte
{
    Metabolism,
    Reaction,
    Hydroponics,
}
