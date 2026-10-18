// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Gunsmith;

// Key of the ammunition bench window.
[Serializable, NetSerializable]
public enum AmmoBenchUiKey : byte
{
    Key,
}

/// <summary>What the ammunition bench tells its window: what it holds, the liquids in its tank and the caliber of what lies on it.</summary>
[Serializable, NetSerializable]
public sealed class AmmoBenchBuiState : BoundUserInterfaceState
{
    public readonly Dictionary<string, int> Stock;

    /// <summary>The liquids in the tank: the reagent and the units.</summary>
    public readonly Dictionary<string, double> Tank;

    public readonly double TankCapacity;

    /// <summary>The caliber of the weapon, the magazine or the cartridge on the bench, if there is one.</summary>
    public readonly CaliberSpec? ItemCaliber;

    public AmmoBenchBuiState(Dictionary<string, int> stock, Dictionary<string, double> tank, double tankCapacity, CaliberSpec? itemCaliber)
    {
        Stock = stock;
        Tank = tank;
        TankCapacity = tankCapacity;
        ItemCaliber = itemCaliber;
    }
}

// From the window: make this many cartridges of this design.
[Serializable, NetSerializable]
public sealed class AmmoBuildMessage : BoundUserInterfaceMessage
{
    public readonly AmmoDesign Design;
    public readonly int Count;

    public AmmoBuildMessage(AmmoDesign design, int count)
    {
        Design = design;
        Count = count;
    }
}

// From the window: take this stack type out of the bench.
[Serializable, NetSerializable]
public sealed class AmmoEjectStockMessage : BoundUserInterfaceMessage
{
    public readonly string StackType;

    public AmmoEjectStockMessage(string stackType)
    {
        StackType = stackType;
    }
}

// From the window: drain the liquid tank of the bench.
[Serializable, NetSerializable]
public sealed class AmmoDrainTankMessage : BoundUserInterfaceMessage
{
}

// From the window: take the item that lies on the bench.
[Serializable, NetSerializable]
public sealed class AmmoTakeItemMessage : BoundUserInterfaceMessage
{
}
