// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.CMU14.Mining;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Mining;

// Gathers the stone, the black stone and the clay that lie near each other on the ground into one stack. A stack that lies still for
// a moment is looked at once: the stacks of the same kind within its range go into the fullest of them, and what does not fit stays as
// the next stack. A stack that is carried, or lies in a container, is left alone; one that moves, or whose count changes, is looked at again.
public sealed class GroundStackSystem : EntitySystem
{
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // How often the stacks are looked over, in seconds.
    private const float Interval = 0.5f;

    private float _accumulator;
    private readonly HashSet<Entity<GroundStackComponent>> _near = new();
    private readonly List<Entity<StackComponent>> _gather = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GroundStackComponent, StackCountChangedEvent>(OnCountChanged);
    }

    private void OnCountChanged(Entity<GroundStackComponent> ent, ref StackCountChangedEvent args)
    {
        ent.Comp.Settled = false;
        ent.Comp.LyingSince = _timing.CurTime;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < Interval)
            return;

        _accumulator = 0f;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<GroundStackComponent, StackComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ground, out var stack, out var xform))
        {
            if (_container.IsEntityInContainer(uid))
            {
                ground.Settled = false;
                ground.LyingSince = now;
                continue;
            }

            // Moved since the last look: it is on its way, and is looked at once it lies still.
            if (xform.ParentUid != ground.LastParent || xform.LocalPosition != ground.LastPosition)
            {
                ground.LastParent = xform.ParentUid;
                ground.LastPosition = xform.LocalPosition;
                ground.LyingSince = now;
                ground.Settled = false;
                continue;
            }

            if (ground.Settled || now - ground.LyingSince < TimeSpan.FromSeconds(ground.Delay))
                continue;

            ground.Settled = true;
            Gather((uid, ground), (uid, stack), xform);
        }
    }

    // The stacks of the same kind around this one go into the fullest of them.
    private void Gather(Entity<GroundStackComponent> start, Entity<StackComponent> stack, TransformComponent xform)
    {
        _near.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, start.Comp.Range, _near);

        _gather.Clear();
        foreach (var other in _near)
        {
            if (TerminatingOrDeleted(other) || _container.IsEntityInContainer(other))
                continue;

            if (!TryComp<StackComponent>(other, out var otherStack) || otherStack.StackTypeId != stack.Comp.StackTypeId)
                continue;

            _gather.Add((other, otherStack));
        }

        if (_gather.Count < 2)
            return;

        // The fullest first: they take the ones that are left, the emptiest first.
        _gather.Sort((a, b) => _stack.GetCount(b.AsNullable()).CompareTo(_stack.GetCount(a.AsNullable())));

        var target = 0;
        var donor = _gather.Count - 1;
        while (target < donor)
        {
            var into = _gather[target];
            var from = _gather[donor];

            if (_stack.GetAvailableSpace(into.Comp) <= 0)
            {
                target++;
                continue;
            }

            // A stack that cannot be merged (covered in acid, say) is left where it is.
            if (!_stack.TryMergeStacks((from, from.Comp), (into, into.Comp), out _))
            {
                donor--;
                continue;
            }

            if (TerminatingOrDeleted(from) || EntityManager.IsQueuedForDeletion(from))
                donor--;
        }
    }
}
