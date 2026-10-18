// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Armorsmith;

// Key of the armorsmith bench window.
[Serializable, NetSerializable]
public enum ArmorsmithUiKey : byte
{
    Key,
}

/// <summary>What the bench tells the window: what it holds, and the design of the armor on it (if there is one). Never what a design works out to.</summary>
[Serializable, NetSerializable]
public sealed class ArmorsmithBuiState : BoundUserInterfaceState
{
    public readonly Dictionary<string, int> Stock;

    /// <summary>The design of the piece of armor lying on the bench, if it is one of ours.</summary>
    public readonly ArmorDesign? Item;

    public ArmorsmithBuiState(Dictionary<string, int> stock, ArmorDesign? item)
    {
        Stock = stock;
        Item = item;
    }
}

// From the window: build this armor design.
[Serializable, NetSerializable]
public sealed class ArmorsmithBuildMessage : BoundUserInterfaceMessage
{
    public readonly ArmorDesign Design;

    public ArmorsmithBuildMessage(ArmorDesign design)
    {
        Design = design;
    }
}

// From the window: take this stack type out of the bench.
[Serializable, NetSerializable]
public sealed class ArmorsmithEjectStockMessage : BoundUserInterfaceMessage
{
    public readonly string StackType;

    public ArmorsmithEjectStockMessage(string stackType)
    {
        StackType = stackType;
    }
}

// From the window: take the item that lies on the bench.
[Serializable, NetSerializable]
public sealed class ArmorsmithTakeItemMessage : BoundUserInterfaceMessage
{
}

/// <summary>Paints the armor that lies on the bench anew.</summary>
[Serializable, NetSerializable]
public sealed class ArmorsmithRecolorMessage : BoundUserInterfaceMessage
{
    public readonly Color Color;

    public ArmorsmithRecolorMessage(Color color)
    {
        Color = color;
    }
}
