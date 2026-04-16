using Robust.Shared.GameStates;

namespace Content.Shared.AU14.ColonyEconomy;

/// <summary>
///     One item listed for sale in a player-managed shopkeeper vendor.
///     Server-side only; the client receives <see cref="AU14ShopkeeperListingState"/> via BUI state.
/// </summary>
public sealed class AU14ShopkeeperListing
{
    /// <summary>The actual entity held in the stock container.</summary>
    public NetEntity ItemNet;

    /// <summary>Display name shown to customers (set by the shopkeep).</summary>
    public string DisplayName = string.Empty;

    /// <summary>Base price before sales tax.</summary>
    public int Price = 10;

    /// <summary>Prototype ID of the stored entity, used for sprite display and stacking.</summary>
    public string? ProtoId;
}

/// <summary>
///     Marks an entity as a player-operated cash vendor.
///     Authorized staff (shopkeep / food service worker) use items on the machine to add stock,
///     then set names and prices via the Manage UI.
///     Regular players insert cash and buy listed items through the Shop UI.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AU14ShopkeeperVendorComponent : Component
{
    /// <summary>Active listings — runtime only, not serialized.</summary>
    public List<AU14ShopkeeperListing> Listings = new();

    /// <summary>Cash the current customer has inserted but not yet spent.</summary>
    public float InsertedCash = 0f;

    public const string StockContainerName = "shopkeeper_stock";
    public const string CashSlotName = "shopkeeper_cash";
}

