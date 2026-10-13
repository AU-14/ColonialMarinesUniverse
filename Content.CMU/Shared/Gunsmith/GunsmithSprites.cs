// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
namespace Content.Shared.CMU14.Gunsmith;

/// <summary>
/// Which parts of which donor weapons make up the picture of a weapon: the rear (the stock), the middle (the action) and the front (the barrel).
/// The parts are cut from the sprites of existing weapons (see parts.rsi); the same rule is used by the bench and by the preview in its window.
/// </summary>
public static class GunsmithSprites
{
    public const string Rsi = "/Textures/CMU14/Objects/Weapons/Gunsmith/parts.rsi";

    public static string Rear(FirearmDesign design, GunsmithActionPrototype action)
    {
        if (design.Stock == StockKind.None)
            return "pistol_rear";

        if (design.Stock == StockKind.Folding)
            return "smg_rear";

        return action.MidSprite + "_rear";
    }

    public static string Mid(GunsmithActionPrototype action) => action.MidSprite + "_mid";

    public static string Front(FirearmDesign design)
    {
        if (design.Caliber.BoreMm >= 15)
            return "breaker_front";

        var length = design.BarrelLengthMm;
        if (length < 150)
            return "pistol_front";
        if (length < 260)
            return "smg_front";
        if (length < 420)
            return "mp_front";
        if (length < 620)
            return "carbine_front";
        if (length < 900)
            return "hunting_front";

        return "sniper_front";
    }
}

/// <summary>The capacities a magazine can be made with (each has a prototype of its own).</summary>
public static class GunsmithCapacities
{
    public static readonly int[] All = { 5, 6, 8, 10, 12, 15, 18, 20, 25, 30, 40, 50, 60, 75, 100 };
}

/// <summary>Numbers of the game that the formulas use and the handbook prints.</summary>
public static class GunsmithConstants
{
    /// <summary>The time it takes the barrel to lose half of its excess heat, seconds.</summary>
    public const double CoolingHalfLifeSeconds = 25.0;
}
