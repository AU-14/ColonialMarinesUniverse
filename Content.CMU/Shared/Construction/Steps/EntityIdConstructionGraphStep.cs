// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.Construction;
using Content.Shared.Construction.Steps;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Shared.Construction.Steps;

/// <summary>
/// A construction step that requires a held/nearby entity matching a specific entity prototype id,
/// regardless of its components (so it works for ANY item — food, seeds, etc. — not just stacks or
/// tagged items). Written by the AU14 in-game construction editor for "custom material" / "custom tool"
/// steps via the <c>entityId:</c> YAML key.
///
/// <para>
/// When <see cref="Consume"/> is true (custom material) the matched entity is consumed into the build,
/// like a normal material insert. When false (custom tool) the entity must merely be present and is NOT
/// consumed — see the handling in <c>ConstructionSystem.Construct</c>.
/// </para>
/// </summary>
[DataDefinition]
public sealed partial class EntityIdConstructionGraphStep : ArbitraryInsertConstructionGraphStep
{
    [DataField("entityId", required: true)]
    public string EntityId { get; private set; } = string.Empty;

    /// <summary>False = the entity must be present but is not consumed (tool-like).</summary>
    [DataField]
    public bool Consume { get; private set; } = true;

    public override bool EntityValid(EntityUid uid, IEntityManager entityManager, IComponentFactory compFactory)
    {
        return !string.IsNullOrEmpty(EntityId)
               && entityManager.TryGetComponent(uid, out MetaDataComponent? meta)
               && meta.EntityPrototype?.ID == EntityId;
    }

    /// <summary>
    /// Generates a guide ("Steps") entry from the target entity's own display name. The base arbitrary-insert
    /// step expects a localized <c>Name</c> and feeds it through <c>{LOC($name)}</c>; the in-game editor emits
    /// these steps with no Name, and an empty value broke the entire steps list. Resolving the entity name here
    /// (and using a plain-text loc string) keeps multi-step / custom recipes showing their steps correctly.
    /// </summary>
    public override ConstructionGuideEntry GenerateGuideEntry()
    {
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var label = protoMan.TryIndex<EntityPrototype>(EntityId, out var proto) ? proto.Name : EntityId;

        return new ConstructionGuideEntry
        {
            // Consumed = "insert" (a material); not consumed = "use" (a tool that stays in hand).
            Localization = Consume ? "construction-step-insert-entity" : "construction-step-use-entity",
            Arguments = new (string, object)[] { ("name", label) },
            Icon = Icon,
        };
    }
}
