// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Gunsmith;

[Serializable, NetSerializable]
public enum GunsmithUiKey : byte
{
    Key,
}

/// <summary>What the bench tells the window: what it holds, and the caliber of the weapon on it (if there is one). Never what a design works out to.</summary>
[Serializable, NetSerializable]
public sealed class GunsmithBuiState : BoundUserInterfaceState
{
    public readonly Dictionary<string, int> Stock;

    /// <summary>The design of the weapon lying on the bench, if it is one of ours.</summary>
    public readonly FirearmDesign? Weapon;

    /// <summary>The caliber of what lies on the bench, of ours or of the game (a weapon, a magazine, a cartridge), if the bench can tell it.</summary>
    public readonly CaliberSpec? ItemCaliber;

    public GunsmithBuiState(Dictionary<string, int> stock, FirearmDesign? weapon, CaliberSpec? itemCaliber = null)
    {
        Stock = stock;
        Weapon = weapon;
        ItemCaliber = itemCaliber;
    }
}

[Serializable, NetSerializable]
public sealed class GunsmithBuildFirearmMessage : BoundUserInterfaceMessage
{
    public readonly FirearmDesign Design;

    public GunsmithBuildFirearmMessage(FirearmDesign design)
    {
        Design = design;
    }
}

[Serializable, NetSerializable]
public sealed class GunsmithBuildMagazineMessage : BoundUserInterfaceMessage
{
    public readonly MagazineDesign Design;

    public GunsmithBuildMagazineMessage(MagazineDesign design)
    {
        Design = design;
    }
}

[Serializable, NetSerializable]
public sealed class GunsmithEjectStockMessage : BoundUserInterfaceMessage
{
    public readonly string StackType;

    public GunsmithEjectStockMessage(string stackType)
    {
        StackType = stackType;
    }
}

[Serializable, NetSerializable]
public sealed class GunsmithTakeWeaponMessage : BoundUserInterfaceMessage
{
}
