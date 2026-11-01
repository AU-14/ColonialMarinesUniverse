namespace Content.Shared._RMC14.Dropship.Weapon;

/// <summary>
/// Marks an entity as a holder of loose dropship ammo (shells, trays, etc.)
/// kept in <see cref="Content.Shared.Containers.ItemSlots.ItemSlotsComponent"/>.
/// Used for hand-loaded ammo holders such as the 40mm BOFORS tray and the
/// 105mm howitzer shell holder, which are dragged or moved by powerloader and
/// have their rounds individually inserted/removed by hand.
/// </summary>
[RegisterComponent]
public sealed partial class AmmoHolderComponent : Component
{
}
