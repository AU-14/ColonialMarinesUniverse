// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Linq;
using Content.Server.Construction;
using Content.Server.Construction.Completions;
using Content.Server.Stack;
using Content.Shared._AU14.Construction;
using Content.Shared.Construction;
using Content.Shared.Prototypes;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Construction;

/// <summary>
/// Enforces "deconstruction output = materials actually invested" for skill-discounted builds. When a
/// discounted structure is deconstructed, its exact refund action is reduced before it runs. This never
/// searches for or mutates unrelated stacks in the world.
/// </summary>
public sealed class AU14MaterialShortfallSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AU14MaterialShortfallComponent, ConstructionSystem.BeforeConstructionActionsEvent>(OnBeforeActions);
        SubscribeLocalEvent<AU14MaterialShortfallComponent, ConstructionChangeEntityEvent>(OnConstructionChangeEntity);
    }

    private void OnBeforeActions(Entity<AU14MaterialShortfallComponent> ent, ref ConstructionSystem.BeforeConstructionActionsEvent args)
    {
        foreach (var stackType in ent.Comp.MissingByStack.Keys.ToArray())
        {
            var remaining = ent.Comp.MissingByStack[stackType];
            for (var i = 0; i < args.Actions.Count && remaining > 0; i++)
            {
                if (args.Actions[i] is not SpawnPrototype spawn ||
                    !_prototypes.TryIndex<EntityPrototype>(spawn.Prototype, out var prototype) ||
                    !prototype.TryGetComponent<StackComponent>(out var stack, _componentFactory) ||
                    stack.StackTypeId != stackType)
                {
                    continue;
                }

                var deducted = Math.Min(remaining, spawn.Amount);
                var refund = spawn.Amount - deducted;
                remaining -= deducted;

                if (refund > 0)
                    args.Actions[i] = new AU14ExactStackRefundAction(spawn.Prototype, refund);
                else
                    args.Actions.RemoveAt(i--);
            }

            if (remaining > 0)
                ent.Comp.MissingByStack[stackType] = remaining;
            else
                ent.Comp.MissingByStack.Remove(stackType);
        }
    }

    private void OnConstructionChangeEntity(Entity<AU14MaterialShortfallComponent> ent, ref ConstructionChangeEntityEvent args)
    {
        if (ent.Owner != args.Old || args.New == ent.Owner)
            return;

        var replacement = EnsureComp<AU14MaterialShortfallComponent>(args.New);
        foreach (var (stackType, missing) in ent.Comp.MissingByStack)
            replacement.MissingByStack[stackType] = replacement.MissingByStack.GetValueOrDefault(stackType) + missing;
    }

}

/// <summary>A per-transaction replacement for a prototype-defined stack refund.</summary>
[DataDefinition]
public sealed partial class AU14ExactStackRefundAction : IGraphAction
{
    [DataField]
    private string _prototype = string.Empty;

    [DataField]
    private int _amount;

    public AU14ExactStackRefundAction()
    {
    }

    public AU14ExactStackRefundAction(string prototype, int amount)
    {
        _prototype = prototype;
        _amount = amount;
    }

    public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
    {
        var coordinates = entityManager.GetComponent<TransformComponent>(uid).Coordinates;
        var stackUid = entityManager.SpawnEntity(_prototype, coordinates);
        var stack = entityManager.GetComponent<StackComponent>(stackUid);
        entityManager.EntitySysManager.GetEntitySystem<StackSystem>().SetCount(stackUid, _amount, stack);
    }
}
