// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Flora;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Flora;

// A knife makes a hole in the bark; a container held to the hole takes the resin. A branch can be broken off by hand.
// Nothing here ticks: the times are looked at only when somebody does something to the tree.
public sealed class TreeTappingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly SoundSpecifier ChopSound = new SoundPathSpecifier("/Audio/Effects/chop.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TreeTappableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TreeTappableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<TreeTappableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(Entity<TreeTappableComponent> tree, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (_tag.HasTag(args.Used, "Knife"))
        {
            args.Handled = true;
            Tap(tree, args.User);
            return;
        }

        if (_solutions.TryGetRefillableSolution(args.Used, out var soln, out var solution))
        {
            args.Handled = true;
            Collect(tree, args.User, soln.Value, solution);
        }
    }

    private void Tap(Entity<TreeTappableComponent> tree, EntityUid user)
    {
        var now = _timing.CurTime;
        if (tree.Comp.HoleUntil is { } until && now < until)
        {
            _popup.PopupEntity(Loc.GetString("au14-tree-already-tapped"), tree, user);
            return;
        }

        tree.Comp.HoleUntil = now + tree.Comp.HoleLasts;
        _audio.PlayPvs(ChopSound, tree);
        _popup.PopupEntity(Loc.GetString("au14-tree-tapped"), tree, user);
    }

    private void Collect(Entity<TreeTappableComponent> tree, EntityUid user, Entity<Content.Shared.Chemistry.Components.SolutionComponent> container, Content.Shared.Chemistry.Components.Solution solution)
    {
        var now = _timing.CurTime;
        var comp = tree.Comp;

        if (comp.HoleUntil is not { } until || now >= until)
        {
            _popup.PopupEntity(Loc.GetString("au14-tree-no-hole"), tree, user, PopupType.SmallCaution);
            return;
        }

        if (now < comp.ResinReadyAt)
        {
            _popup.PopupEntity(Loc.GetString("au14-tree-dry"), tree, user, PopupType.SmallCaution);
            return;
        }

        var amount = FixedPoint2.New(_random.Next(comp.MinResin, comp.MaxResin + 1));
        var room = solution.AvailableVolume;
        if (room <= FixedPoint2.Zero)
        {
            _popup.PopupEntity(Loc.GetString("au14-tree-no-room"), tree, user, PopupType.SmallCaution);
            return;
        }

        var taken = FixedPoint2.Min(amount, room);
        if (!_solutions.TryAddReagent(container, comp.Resin, taken, out _))
            return;

        // The tree gives all it has at once, whatever fitted; it makes more three minutes from now.
        comp.ResinReadyAt = now + comp.ResinRegen;
        _popup.PopupEntity(Loc.GetString("au14-tree-collected", ("amount", taken.Int())), tree, user);
    }

    private void OnGetVerbs(Entity<TreeTappableComponent> tree, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        var target = tree;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-tree-verb-branch"),
            Priority = 1,
            Act = () => BreakBranch(target, user),
        });
    }

    private void BreakBranch(Entity<TreeTappableComponent> tree, EntityUid user)
    {
        var comp = tree.Comp;
        GrowBranches(comp);

        if (comp.Branches <= 0)
        {
            _popup.PopupEntity(Loc.GetString("au14-tree-no-branch"), tree, user, PopupType.SmallCaution);
            return;
        }

        if (comp.Branches == comp.MaxBranches)
            comp.NextBranchAt = _timing.CurTime + comp.BranchRegen;

        comp.Branches--;

        var branch = Spawn(comp.Branch, Transform(user).Coordinates);
        _hands.TryPickupAnyHand(user, branch);
        _audio.PlayPvs(ChopSound, tree);
        _popup.PopupEntity(Loc.GetString("au14-tree-branch-broken"), tree, user);
    }

    // Branches grow back one at a time; a tree that was never touched has them all.
    private void GrowBranches(TreeTappableComponent comp)
    {
        if (comp.Branches < 0)
        {
            comp.Branches = comp.MaxBranches;
            return;
        }

        var now = _timing.CurTime;
        while (comp.Branches < comp.MaxBranches && now >= comp.NextBranchAt)
        {
            comp.Branches++;
            comp.NextBranchAt += comp.BranchRegen;
        }
    }

    private void OnExamined(Entity<TreeTappableComponent> tree, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var now = _timing.CurTime;
        if (tree.Comp.HoleUntil is { } until && now < until)
        {
            args.PushMarkup(Loc.GetString(now < tree.Comp.ResinReadyAt ? "au14-tree-examine-dry" : "au14-tree-examine-hole"));
        }
    }
}
