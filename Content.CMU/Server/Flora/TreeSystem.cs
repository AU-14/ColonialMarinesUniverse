// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.CMU14.Forge;
using Content.Shared.CMU14.Flora;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Flora;

// The trees: an axe knocks firewood off the tree it hits and bites into the trunk; the same axe splits a log into firewood (a saw makes planks of it).
public sealed class TreeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ToolWearSystem _wear = default!;

    private static readonly SoundSpecifier SplitSound = new SoundPathSpecifier("/Audio/Effects/chop.ogg");

    public override void Initialize()
    {
        base.Initialize();

        // Broadcast: the melee weapon already has its subscription of the game for this event, and one component takes one handler of a system only.
        SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<LogSplittableComponent, InteractUsingEvent>(OnLogInteractUsing);
    }

    private void OnMeleeHit(MeleeHitEvent args)
    {
        var weapon = args.Weapon;
        foreach (var hit in args.HitEntities)
        {
            if (!TryComp<TreeFellableComponent>(hit, out var tree) || !IsAxe(weapon, tree.Axes))
                continue;

            if (tree.BlowDamage > 0f)
            {
                var bite = new DamageSpecifier();
                bite.DamageDict["Slash"] = FixedPoint2.New(tree.BlowDamage);
                _damage.TryChangeDamage(hit, bite, ignoreResistances: true, origin: args.User, tool: weapon);
            }

            if (TerminatingOrDeleted(hit))
                continue;

            if (tree.FirewoodLeft < 0)
                tree.FirewoodLeft = tree.MaxFirewood;

            var pieces = Math.Min(tree.FirewoodLeft, _random.Next(1, tree.FirewoodPerBlow + 1));
            tree.FirewoodLeft -= pieces;
            var coords = Transform(hit).Coordinates;
            for (var i = 0; i < pieces; i++)
            {
                Spawn(tree.Firewood, coords.Offset(_random.NextVector2(0.35f)));
            }

            // An axe of wood or of stone wears with the blows.
            _wear.Use(weapon, args.User);
            if (TerminatingOrDeleted(weapon))
                return;
        }
    }

    // An axe held to a log splits it into firewood. A saw does something else with it (planks), and that is left to the tool of the game.
    private void OnLogInteractUsing(Entity<LogSplittableComponent> log, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsAxe(args.Used, log.Comp.Axes))
            return;

        args.Handled = true;
        var coords = Transform(log).Coordinates;
        var firewood = Spawn(log.Comp.Firewood, coords);
        _stack.SetCount(firewood, log.Comp.Pieces);

        _audio.PlayPvs(SplitSound, coords);
        _popup.PopupEntity(Loc.GetString("au14-log-split", ("log", log.Owner), ("tool", args.Used)), log, args.User);
        QueueDel(log);
        _wear.Use(args.Used, args.User);
    }

    private bool IsAxe(EntityUid weapon, List<Robust.Shared.Prototypes.ProtoId<TagPrototype>> axes)
    {
        foreach (var tag in axes)
        {
            if (_tag.HasTag(weapon, tag))
                return true;
        }

        return false;
    }
}
