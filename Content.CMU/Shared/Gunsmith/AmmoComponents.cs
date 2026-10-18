// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Gunsmith;

/// <summary>
/// The ammunition bench: it makes cartridges for the calibers of the weapons of the gunsmith's bench out of the materials of the parts of a bullet,
/// the powder and, if the player wants a bullet that does more than hit, a liquid that fills its core: the fuel of a flamethrower, an acid, a poison.
/// Only someone who has the sapper's skill can use it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AmmoBenchComponent : Component
{
    /// <summary>The slot a weapon, a magazine or a cartridge lies in: the caliber is read from it.</summary>
    [DataField]
    public string ItemSlot = "ammo_item";

    /// <summary>What the bench holds, items of each type of stack.</summary>
    [DataField]
    public Dictionary<string, int> Stock = new();

    [DataField]
    public string PowderStack = "RUMCSaltpeterPowder";

    [DataField]
    public int StockLimit = 400;

    /// <summary>The name of the solution the liquids go into.</summary>
    [DataField]
    public string TankSolution = "ammo_tank";

    /// <summary>How much liquid one pouring takes, at the most.</summary>
    [DataField]
    public double PourUnits = 100;

    [DataField]
    public double TimeScale = 1.0;

    [DataField]
    public string CartridgePrototype = "RUMCCraftedCartridge";
}

// The do-after of making cartridges at the ammunition bench; carries the design and how many.
[Serializable, NetSerializable]
public sealed partial class AmmoBuildDoAfterEvent : DoAfterEvent
{
    [DataField]
    public AmmoDesign Design = new();

    [DataField]
    public int Count;

    private AmmoBuildDoAfterEvent()
    {
    }

    public AmmoBuildDoAfterEvent(AmmoDesign design, int count)
    {
        Design = design;
        Count = count;
    }

    public override DoAfterEvent Clone() => this;
}
