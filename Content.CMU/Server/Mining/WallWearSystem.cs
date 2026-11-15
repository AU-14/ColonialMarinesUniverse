// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Forge;
using Content.Shared.CMU14.Forge;
using Content.Shared.CMU14.Mining;
using Content.Shared.CMU14.ToolDamage;
using Content.Shared.Destructible;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server.CMU14.Mining;

// The wooden and the stone digging tools give out with the blocks they dig: a clay wall or a stone wall that falls to a blow of such a tool costs the tool one use (a pit dug with it
// costs one too, see PitDiggingToolSystem). The wall remembers the tool of the last blow, so the block is paid for by the tool that finished it.
public sealed class WallWearSystem : EntitySystem
{
    [Dependency] private readonly ToolWearSystem _wear = default!;

    public override void Initialize()
    {
        base.Initialize();

        // On the walls that prefer a tool (the clay, the stone), whose blows other systems listen to by other components.
        SubscribeLocalEvent<PreferredToolComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<WallHitByToolComponent, DestructionEventArgs>(OnDestroyed);
    }

    private void OnAttacked(Entity<PreferredToolComponent> wall, ref AttackedEvent args)
    {
        if (!HasComp<BreaksAfterUsesComponent>(args.Used))
        {
            RemComp<WallHitByToolComponent>(wall);
            return;
        }

        var note = EnsureComp<WallHitByToolComponent>(wall);
        note.Tool = args.Used;
        note.User = args.User;
    }

    private void OnDestroyed(Entity<WallHitByToolComponent> wall, ref DestructionEventArgs args)
    {
        _wear.Use(wall.Comp.Tool, wall.Comp.User);
    }
}
