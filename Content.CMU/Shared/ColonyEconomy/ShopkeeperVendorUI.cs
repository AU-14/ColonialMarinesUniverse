using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
namespace Content.Shared.AU14.ColonyEconomy;
[Serializable, NetSerializable]
public enum AU14ShopkeeperVendorUi { Shop }
[Serializable, NetSerializable]
public sealed class AU14ShopkeeperListingState
{
    public int Index { get; }
    public string DisplayName { get; }
    public int EffectivePrice { get; }
    public int BasePrice { get; }
    public int Count { get; }
    public string? ItemProtoId { get; }
    public AU14ShopkeeperListingState(int index, string displayName, int effectivePrice, int basePrice, int count = 1, string? itemProtoId = null)
    { Index = index; DisplayName = displayName; EffectivePrice = effectivePrice; BasePrice = basePrice; Count = count; ItemProtoId = itemProtoId; }
}
[Serializable, NetSerializable]
public sealed class AU14ShopkeeperVendorShopState : BoundUserInterfaceState
{
    public float InsertedCash { get; }
    public List<AU14ShopkeeperListingState> Listings { get; }
    public float SalesTaxPercent { get; }
    public AU14ShopkeeperVendorShopState(float cash, List<AU14ShopkeeperListingState> listings, float salesTaxPercent = 0f)
    { InsertedCash = cash; Listings = listings; SalesTaxPercent = salesTaxPercent; }
}
[Serializable, NetSerializable]
public sealed class AU14ShopkeeperBuyBuiMsg : BoundUserInterfaceMessage
{ public int Index { get; } public AU14ShopkeeperBuyBuiMsg(int i) { Index = i; } }
[Serializable, NetSerializable]
public sealed class AU14ShopkeeperReturnChangeBuiMsg : BoundUserInterfaceMessage { }
[Serializable, NetSerializable]
public sealed class AU14ShopkeeperEditListingBuiMsg : BoundUserInterfaceMessage
{
    public int Index { get; }
    public string DisplayName { get; }
    public int Price { get; }
    public AU14ShopkeeperEditListingBuiMsg(int index, string displayName, int price)
    { Index = index; DisplayName = displayName; Price = price; }
}
[Serializable, NetSerializable]
public sealed class AU14ShopkeeperRemoveListingBuiMsg : BoundUserInterfaceMessage
{ public int Index { get; } public AU14ShopkeeperRemoveListingBuiMsg(int i) { Index = i; } }
