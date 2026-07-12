// ReSharper disable CheckNamespace

using Content.Shared._CMU14.Item.Stain;

namespace Content.Shared.Chemistry.Reagent;

public partial class ReagentPrototype
{
    /// <summary>
    /// Visual item stain applied when this reagent touches an item or equipped mob.
    /// </summary>
    [DataField]
    public CMUItemStainKind? ItemStain;

    /// <summary>
    /// Optional stain tint. Defaults to <see cref="SubstanceColor"/>.
    /// </summary>
    [DataField]
    public Color? ItemStainColor;

    /// <summary>
    /// Whether touch reactions with this reagent remove visual item stains.
    /// </summary>
    [DataField]
    public bool CleansItemStains;
}
