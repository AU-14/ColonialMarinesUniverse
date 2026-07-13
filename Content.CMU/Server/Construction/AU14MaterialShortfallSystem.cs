// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Numerics;
using Content.Shared._AU14.Construction;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._AU14.Construction;

/// <summary>
/// Enforces "deconstruction output = materials actually invested" for skill-discounted builds. When a
/// structure carrying <see cref="AU14MaterialShortfallComponent"/> is deleted (deconstruction deletes the
/// entity and spawns its refund sheets in the same tick), the freshly spawned refund stacks of the recorded
/// material at that spot are reduced by the shortfall. Only stacks CREATED in that same tick are touched, so
/// a coincidental pile of sheets lying on the tile is never eaten.
/// </summary>
public sealed class AU14MaterialShortfallSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // 🔧 TUNABLE: how far around the deconstructed structure refund stacks are matched.
    private const float RefundSearchRange = 0.75f;

    private readonly List<(MapCoordinates Where, string StackType, int Missing, GameTick Tick)> _pending = new();
    private readonly HashSet<Entity<StackComponent>> _stacksBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AU14MaterialShortfallComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnTerminating(Entity<AU14MaterialShortfallComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Missing <= 0 || string.IsNullOrEmpty(ent.Comp.StackTypeId))
            return;

        // The refund spawns during the same graph action that deletes us; process at the end of the tick
        // (next Update) so the stacks exist by the time we look for them.
        _pending.Add((_transform.GetMapCoordinates(ent.Owner), ent.Comp.StackTypeId, ent.Comp.Missing, _timing.CurTick));
    }

    public override void Update(float frameTime)
    {
        if (_pending.Count == 0)
            return;

        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var (where, stackType, missing, tick) = _pending[i];

            _stacksBuffer.Clear();
            _lookup.GetEntitiesInRange(where, RefundSearchRange, _stacksBuffer);

            var remaining = missing;
            foreach (var stackEnt in _stacksBuffer)
            {
                if (remaining <= 0)
                    break;

                if (stackEnt.Comp.StackTypeId != stackType)
                    continue;

                // Only stacks created since the deconstruction started - never a pre-existing pile.
                if (MetaData(stackEnt).CreationTick < tick)
                    continue;

                var take = Math.Min(remaining, stackEnt.Comp.Count);
                _stack.SetCount(stackEnt.Owner, stackEnt.Comp.Count - take, stackEnt.Comp);
                remaining -= take;
            }

            // Done, or the deconstruction tick (plus one grace tick) has passed - a structure that was merely
            // destroyed spawns no refund, so there is nothing to reduce and the entry just expires.
            if (remaining <= 0 || _timing.CurTick.Value > tick.Value + 1)
                _pending.RemoveAt(i);
            else
                _pending[i] = (where, stackType, remaining, tick);
        }
    }
}
