using Content.Shared.Access.Systems;
using Content.Shared.AU14.ColonyEconomy;
using Robust.Client.Player;
using Robust.Client.UserInterface;
namespace Content.Client.AU14.ColonyEconomy;
public sealed class AU14ShopkeeperVendorBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;

    private AU14ShopkeeperVendorWindow? _window;
    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AU14ShopkeeperVendorWindow>();
        _window.OnBuyPressed += idx => SendPredictedMessage(new AU14ShopkeeperBuyBuiMsg(idx));
        _window.ReturnChangeBtn.OnPressed += _ => SendPredictedMessage(new AU14ShopkeeperReturnChangeBuiMsg());
        _window.OnSaveListing += (idx, name, price) => SendPredictedMessage(new AU14ShopkeeperEditListingBuiMsg(idx, name, price));
        _window.OnRemoveListing += idx => SendPredictedMessage(new AU14ShopkeeperRemoveListingBuiMsg(idx));
    }
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not AU14ShopkeeperVendorShopState s)
            return;
        var isShopkeeper = false;
        if (_player.LocalEntity is { Valid: true } player)
            isShopkeeper = _entMan.System<AccessReaderSystem>().IsAllowed(player, Owner);
        _window.UpdateState(s, isShopkeeper);
    }
}
