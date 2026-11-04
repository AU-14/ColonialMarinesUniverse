using Robust.Shared.Map;

namespace Content.Shared._RMC14.Areas;

public sealed partial class AreaSystem
{
    private void OnAreaMapInit(Entity<AreaComponent> ent, ref MapInitEvent args)
    {
        // Keep authored flags intact on uninitialized mapping grids. Runtime area entities
        // spawn in nullspace, which is already map-initialized, so their restrictions still apply.
        ApplyCeilingTier(ent.Comp);
        Dirty(ent);
    }

    /// <summary>
    /// Legacy area prototypes describe roofs with permission flags. The strongest restriction
    /// determines the tier; a thicker roof also blocks everything stopped by thinner roofs.
    /// </summary>
    internal static void ApplyCeilingTier(AreaComponent area)
    {
        var tier = !area.OB ? 4
            : !area.CAS ? 3
            : !area.Fulton || !area.SupplyDrop || !area.MortarFire ? 2
            : !area.MortarPlacement || !area.Lasing || !area.Medevac || !area.Paradropping ? 1
            : 0;

        area.OB = tier < 4;
        area.CAS = tier < 3;
        area.Fulton = area.SupplyDrop = area.MortarFire = tier < 2;
        area.MortarPlacement = area.Lasing = area.Medevac = area.Paradropping = tier < 1;
    }

    public bool SetCeilingLevelZero(EntityCoordinates coordinates)
    {
        if (!TryGetArea(coordinates, out var area, out _))
            return false;

        var comp = area.Value.Comp;
        if (comp.OB && comp.CAS && comp.Fulton && comp.SupplyDrop && comp.MortarFire &&
            comp.MortarPlacement && comp.Lasing && comp.Medevac && comp.Paradropping)
        {
            return false;
        }

        comp.OB = comp.CAS = comp.Fulton = comp.SupplyDrop = comp.MortarFire = true;
        comp.MortarPlacement = comp.Lasing = comp.Medevac = comp.Paradropping = true;
        Dirty(area.Value);
        return true;
    }
}
