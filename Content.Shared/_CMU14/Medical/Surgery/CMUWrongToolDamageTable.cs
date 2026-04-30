using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Items;
using Content.Shared._RMC14.Medical.Surgery.Tools;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Medical.Surgery;

/// <summary>
///     Entries are ordered by descending damage so the lookup picks the
///     most-severe match when a tool carries multiple surgery-tool
///     components.
/// </summary>
public static class CMUWrongToolDamageTable
{
    public static readonly IReadOnlyList<(Type ComponentType, string DamageType, float Amount)> Entries =
        new (Type, string, float)[]
        {
            (typeof(CMBoneSawComponent),       "Slash",    15f),
            (typeof(CMSurgicalDrillComponent), "Blunt",    12f),
            (typeof(CMCauteryComponent),       "Heat",     10f),
            (typeof(CMScalpelComponent),       "Slash",    8f),
            (typeof(CMBoneSetterComponent),    "Blunt",    8f),
            (typeof(CMRetractorComponent),     "Piercing", 6f),
            (typeof(CMUCastItemComponent),     "Blunt",    6f),
            (typeof(CMUOrganClampComponent),   "Blunt",    5f),
            (typeof(CMHemostatComponent),      "Slash",    4f),
            (typeof(CMUSplintItemComponent),   "Blunt",    4f),
            (typeof(CMUBoneGraftComponent),    "Blunt",    3f),
            (typeof(CMBoneGelComponent),       "Blunt",    2f),
        };

    public static DamageSpecifier MakeSpec(string damageType, float amount)
    {
        return new DamageSpecifier { DamageDict = { [damageType] = (FixedPoint2)amount } };
    }
}
