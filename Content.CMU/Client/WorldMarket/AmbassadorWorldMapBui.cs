// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared.CMU14.WorldMarket;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.WorldMarket;

// Client side of the window of the global map, which the console of the ambassador opens: opens the window, gives it the state and sends the gifts that it asks for.
public sealed class AmbassadorWorldMapBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private GlobalMapWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GlobalMapWindow>();
        _window.OnGift += city => SendMessage(new GiftCityMessage(city));
        _window.OnAction += (city, action) => SendMessage(new CityActionMessage(city, action));
        _window.OnClaimZone += (latitude, longitude) => SendMessage(new ZoneClaimMessage(latitude, longitude));
        _window.OnCommand += (kind, a, b, c, n) => SendMessage(new MapCommandMessage(kind, a, b, c, n));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window != null && state is AmbassadorWorldMapState map)
            _window.SetState(map);
    }
}
