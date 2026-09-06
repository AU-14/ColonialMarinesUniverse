using System;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

public struct GunshipSpatialQueryBudget
{
    private readonly int _limit;

    public int Used { get; private set; }

    public GunshipSpatialQueryBudget(int limit)
    {
        _limit = Math.Max(0, limit);
        Used = 0;
    }

    public bool TryConsume()
    {
        if (Used >= _limit)
            return false;

        Used++;
        return true;
    }
}
