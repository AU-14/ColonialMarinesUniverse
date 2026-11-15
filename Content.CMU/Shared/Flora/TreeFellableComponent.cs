// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Flora;

// A tree that is felled with an axe. Every blow of an axe knocks off a piece of firewood (up to a limit for the tree), and the blow bites into the
// trunk harder than the plain weapon damage. The logs come out of the tree when it falls (that is the ore of the tree, see OreVein).
[RegisterComponent]
public sealed partial class TreeFellableComponent : Component
{
    // What counts as an axe.
    [DataField]
    public List<ProtoId<TagPrototype>> Axes = new() { "FireAxe", "BotanyHatchet", "RUMCAxe" };

    [DataField]
    public EntProtoId Firewood = "RUMCFirewood1";

    // How many pieces of firewood the tree gives in all, and how many a blow can knock off at most.
    [DataField]
    public int MaxFirewood = 6;

    [DataField]
    public int FirewoodPerBlow = 2;

    // The firewood the tree still has; filled in by the first blow.
    [ViewVariables]
    public int FirewoodLeft = -1;

    // The damage of the trunk that an axe adds to its own, whatever the armour of the wood.
    [DataField]
    public float BlowDamage = 40f;
}
