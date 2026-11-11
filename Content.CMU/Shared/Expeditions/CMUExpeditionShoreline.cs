namespace Content.Shared.CMU14.Expeditions;

public static partial class CMUExpeditionGenerator
{
    /// <summary>
    /// Match the four corners to the neighbouring banks. The RMC art faces south at zero rotation:
    /// edge = south bank, corner = southeast bank, inner corner = deep water to the northwest.
    /// Opposing banks in a narrow channel use full shallows instead of overlapping water entities.
    /// </summary>
    public static CMUExpeditionWaterTile GetWaterTile(CMUExpeditionPlan plan, int x, int y)
    {
        if (plan.Terrain[plan.Index(x, y)] != CMUExpeditionTerrain.Water)
            return new(CMUExpeditionWaterKind.None);

        // A bridge spans the original water; it must not paint a shoreline across the river.
        bool Bank(int dx, int dy)
        {
            var nx = x + dx;
            var ny = y + dy;
            return nx >= 0 && ny >= 0 && nx < plan.Size && ny < plan.Size &&
                   plan.BaseTerrain[plan.Index(nx, ny)] != CMUExpeditionTerrain.Water;
        }

        var south = Bank(0, -1);
        var east = Bank(1, 0);
        var north = Bank(0, 1);
        var west = Bank(-1, 0);
        var mask = (south || west || Bank(-1, -1) ? 1 : 0) |
                   (south || east || Bank(1, -1) ? 2 : 0) |
                   (north || east || Bank(1, 1) ? 4 : 0) |
                   (north || west || Bank(-1, 1) ? 8 : 0);
        return mask switch
        {
            0 => new(CMUExpeditionWaterKind.Deep),
            3 => new(CMUExpeditionWaterKind.Edge),
            6 => new(CMUExpeditionWaterKind.Edge, 1),
            12 => new(CMUExpeditionWaterKind.Edge, 2),
            9 => new(CMUExpeditionWaterKind.Edge, 3),
            2 => new(CMUExpeditionWaterKind.Corner),
            4 => new(CMUExpeditionWaterKind.Corner, 1),
            8 => new(CMUExpeditionWaterKind.Corner, 2),
            1 => new(CMUExpeditionWaterKind.Corner, 3),
            7 => new(CMUExpeditionWaterKind.InnerCorner),
            14 => new(CMUExpeditionWaterKind.InnerCorner, 1),
            13 => new(CMUExpeditionWaterKind.InnerCorner, 2),
            11 => new(CMUExpeditionWaterKind.InnerCorner, 3),
            _ => new(CMUExpeditionWaterKind.Shallow),
        };
    }
}
