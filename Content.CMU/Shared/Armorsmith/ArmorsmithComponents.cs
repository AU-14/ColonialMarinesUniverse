// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Armorsmith;

/// <summary>
/// The armorer's bench. Ingots, sheets and cloth are fed into it; the armor and the helmets are made from what it holds, and an armor that lies on it can be
/// painted anew. Only someone who has the sapper's skill can use it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ArmorsmithBenchComponent : Component
{
    /// <summary>The slot a piece of armor lies in to be painted anew.</summary>
    [DataField]
    public string ItemSlot = "armorsmith_item";

    /// <summary>What the bench holds, items of each type of stack.</summary>
    [DataField]
    public Dictionary<string, int> Stock = new();

    /// <summary>How many items of one stack the bench takes in at the most, so that it is not a warehouse.</summary>
    [DataField]
    public int StockLimit = 400;

    /// <summary>How fast the bench works: the time of the work is multiplied by this (the tests make it small).</summary>
    [DataField]
    public double TimeScale = 1.0;
}

/// <summary>A piece of armor made at the bench. Carries its design.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CraftedArmorComponent : Component
{
    [DataField]
    public ArmorDesign Design = new();

    [DataField]
    public double MassKg;
}

// The do-after of building an armor at the armorsmith bench; carries the design that is being built.
[Serializable, NetSerializable]
public sealed partial class ArmorsmithBuildDoAfterEvent : DoAfterEvent
{
    [DataField]
    public ArmorDesign Design = new();

    private ArmorsmithBuildDoAfterEvent()
    {
    }

    public ArmorsmithBuildDoAfterEvent(ArmorDesign design)
    {
        Design = design;
    }

    public override DoAfterEvent Clone() => this;
}
