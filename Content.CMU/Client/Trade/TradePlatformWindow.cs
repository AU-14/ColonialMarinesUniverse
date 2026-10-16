// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.Machines;
using Content.Shared.CMU14.Trade;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;

namespace Content.Client.CMU14.Trade;

// The window of the trade platform. It is made of panels that open and close: the contracts in it with the standing of each, what lies on the pad and what it would be paid for it
// (less the tax of the colony) with the button to call the drone, and for each contract (each category of goods of each company) a panel of its own with what the platform sells
// of it, those goods that its level has not opened yet shown locked. A panel keeps its state when the window is refreshed.
public sealed class TradePlatformWindow : DefaultWindow
{
    private const string ContractsPanel = "#contracts";
    private const string SellPanel = "#sell";

    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly Label _status = new() { HorizontalAlignment = Control.HAlignment.Center };
    private readonly BoxContainer _contracts = MachineWindowKit.Column(2);
    private readonly Label _parcels = new();
    private readonly Label _value = new();
    private readonly Label _tax = new();
    private readonly Label _payout = new();
    private readonly Button _sell = new() { Text = Loc.GetString("rumc-trade-ui-sell") };
    private readonly Label _balance = new();
    private readonly BoxContainer _offers = MachineWindowKit.Column(4);

    private readonly CollapsiblePanel _contractsPanel;
    private readonly CollapsiblePanel _sellPanel;

    // The panels that the person has opened or closed, by key; a panel that was never touched is closed, except the two of the top.
    private readonly Dictionary<string, bool> _open = new();

    private bool _openSky;
    private bool _busy;
    private int? _money;
    private TradePlatformBuiState? _state;

    public event Action? OnSell;
    public event Action<string>? OnBuy;
    public event Action<NetEntity>? OnEject;

    public TradePlatformWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("rumc-trade-ui-title");
        MinSize = new Vector2(520, 520);

        var root = MachineWindowKit.Column(6);
        root.AddChild(_status);

        _contractsPanel = NewPanel(ContractsPanel, Loc.GetString("rumc-trade-ui-contracts-section"), true, _contracts);
        root.AddChild(_contractsPanel);

        var sell = MachineWindowKit.Column(2);
        sell.AddChild(_parcels);
        sell.AddChild(_value);
        sell.AddChild(_tax);
        sell.AddChild(_payout);
        sell.AddChild(new Label { Text = Loc.GetString("rumc-trade-ui-sell-hint"), FontColorOverride = MachineWindowKit.SectionTitle });
        _sell.OnPressed += _ => OnSell?.Invoke();
        sell.AddChild(_sell);
        _sellPanel = NewPanel(SellPanel, Loc.GetString("rumc-trade-ui-sell-section"), true, sell);
        root.AddChild(_sellPanel);

        root.AddChild(new Label { Text = Loc.GetString("rumc-trade-ui-buy-section"), FontColorOverride = MachineWindowKit.SectionTitle });
        root.AddChild(_balance);
        root.AddChild(new ScrollContainer
        {
            MinSize = new Vector2(480, 220),
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _offers },
        });

        Contents.AddChild(root);
    }

    private CollapsiblePanel NewPanel(string key, string title, bool openByDefault, Control body)
    {
        var open = _open.TryGetValue(key, out var known) ? known : openByDefault;
        var panel = new CollapsiblePanel(title, open, body);
        panel.OnToggled += state => _open[key] = state;
        return panel;
    }

    public void SetState(TradePlatformBuiState state)
    {
        _state = state;
        _openSky = state.OpenSky;
        _busy = state.Busy;

        _contractsPanel.SetTitle(Loc.GetString("rumc-trade-ui-contracts-count", ("count", state.Contracts.Count), ("max", state.MaxContracts)));
        RebuildContracts(state);

        _parcels.Text = Loc.GetString("rumc-trade-ui-parcels", ("count", state.Parcels));
        _value.Text = Loc.GetString("rumc-trade-ui-value", ("value", state.ParcelValue));
        _tax.Text = Loc.GetString("rumc-trade-ui-tax", ("percent", state.TaxPercent), ("tax", state.ParcelValue - state.Payout));
        _payout.Text = Loc.GetString("rumc-trade-ui-payout", ("payout", state.Payout));

        RebuildOffers(state);
        Refresh();
    }

    public void SetBalance(int? balance)
    {
        _money = balance;
        Refresh();
    }

    private void Refresh()
    {
        if (_state == null)
            return;

        _status.Text = _busy
            ? Loc.GetString("rumc-trade-ui-busy")
            : _openSky
                ? Loc.GetString("rumc-trade-ui-sky-open")
                : Loc.GetString("rumc-trade-ui-sky-closed");

        _status.FontColorOverride = _busy || !_openSky ? Color.OrangeRed : Color.LightGreen;
        _sell.Disabled = _busy || !_openSky || _state.Parcels == 0 || _state.ParcelValue <= 0;

        _balance.Text = _money is { } money
            ? Loc.GetString("rumc-trade-ui-balance", ("money", money))
            : Loc.GetString("rumc-trade-ui-no-card");

        foreach (var panel in _offers.Children)
        {
            if (panel is not CollapsiblePanel group)
                continue;

            foreach (var child in group.Body.Children)
            {
                if (child is OfferRow row)
                    row.Buy.Disabled = _busy || !_openSky || row.Locked || _money is not { } have || have < row.Price;
            }
        }

        foreach (var child in _contracts.Children)
        {
            if (child is ContractRow row)
                row.Eject.Disabled = _busy;
        }
    }

    private void RebuildContracts(TradePlatformBuiState state)
    {
        _contracts.RemoveAllChildren();

        _contracts.AddChild(new Label { Text = Loc.GetString("rumc-trade-ui-contracts-hint"), FontColorOverride = MachineWindowKit.SectionTitle });

        if (state.Contracts.Count == 0)
        {
            _contracts.AddChild(new Label { Text = Loc.GetString("rumc-trade-ui-no-contracts"), FontColorOverride = MachineWindowKit.SectionTitle });
            return;
        }

        foreach (var contract in state.Contracts)
        {
            var row = new ContractRow(contract);
            var id = contract.Contract;
            row.Eject.OnPressed += _ => OnEject?.Invoke(id);
            _contracts.AddChild(row);
        }
    }

    // A panel for each contract: the goods of one category of one company, with their prices.
    private void RebuildOffers(TradePlatformBuiState state)
    {
        _offers.RemoveAllChildren();

        if (state.Offers.Count == 0)
        {
            _offers.AddChild(new Label { Text = Loc.GetString("rumc-trade-ui-no-goods"), FontColorOverride = MachineWindowKit.SectionTitle });
            return;
        }

        string? company = null;
        BoxContainer? body = null;
        var count = 0;
        var groups = new List<(string Company, BoxContainer Body, int Count)>();

        foreach (var offer in state.Offers)
        {
            if (offer.Company != company || body == null)
            {
                if (body != null && company != null)
                    groups.Add((company, body, count));

                company = offer.Company;
                body = MachineWindowKit.Column(3);
                count = 0;
            }

            var row = new OfferRow(_entMan, offer);
            var id = offer.Id;
            row.Buy.OnPressed += _ => OnBuy?.Invoke(id);
            body.AddChild(row);
            count++;
        }

        if (body != null && company != null)
            groups.Add((company, body, count));

        foreach (var (name, panelBody, size) in groups)
            _offers.AddChild(NewPanel(name, Loc.GetString("rumc-trade-ui-category", ("name", name), ("count", size)), false, panelBody));
    }

    // A header that opens and closes what is under it.
    private sealed class CollapsiblePanel : BoxContainer
    {
        private readonly Button _header = new() { HorizontalExpand = true, ToggleMode = false };
        private string _title;

        public readonly BoxContainer Body;

        public event Action<bool>? OnToggled;

        public bool IsOpen => Body.Visible;

        public CollapsiblePanel(string title, bool open, Control content)
        {
            Orientation = LayoutOrientation.Vertical;
            SeparationOverride = 2;

            _title = title;
            Body = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(12, 2, 0, 4),
                Visible = open,
            };
            Body.AddChild(content);

            _header.OnPressed += _ =>
            {
                Body.Visible = !Body.Visible;
                UpdateHeader();
                OnToggled?.Invoke(Body.Visible);
            };

            AddChild(_header);
            AddChild(Body);
            UpdateHeader();
        }

        public void SetTitle(string title)
        {
            _title = title;
            UpdateHeader();
        }

        private void UpdateHeader()
        {
            _header.Text = (Body.Visible ? "[-] " : "[+] ") + _title;
            _header.HorizontalAlignment = HAlignment.Stretch;
        }
    }

    private sealed class ContractRow : BoxContainer
    {
        public readonly Button Eject;

        public ContractRow(TradeContractEntry contract)
        {
            Orientation = LayoutOrientation.Horizontal;
            SeparationOverride = 6;

            var standing = contract.NextPoints is { } next
                ? Loc.GetString("rumc-trade-ui-contract-standing", ("level", contract.Level), ("points", contract.Points), ("next", next))
                : Loc.GetString("rumc-trade-ui-contract-standing-max", ("level", contract.Level), ("points", contract.Points));

            AddChild(new Label { Text = contract.Company, HorizontalExpand = true, ClipText = true, VerticalAlignment = VAlignment.Center });
            AddChild(new Label { Text = standing, VerticalAlignment = VAlignment.Center });

            Eject = new Button { Text = Loc.GetString("rumc-trade-ui-eject") };
            AddChild(Eject);
        }
    }

    private sealed class OfferRow : BoxContainer
    {
        public readonly Button Buy;
        public readonly int Price;
        public readonly bool Locked;

        public OfferRow(IEntityManager entMan, TradeOfferEntry offer)
        {
            Orientation = LayoutOrientation.Horizontal;
            SeparationOverride = 6;
            Price = offer.Price;
            Locked = offer.Locked;

            AddChild(new EntityPrototypeView(offer.Product, entMan) { MinSize = new Vector2(36, 36) });

            var name = offer.Count > 1 ? $"{offer.Name} x{offer.Count}" : offer.Name;
            var label = new Label { Text = name, HorizontalExpand = true, ClipText = true, VerticalAlignment = VAlignment.Center };
            if (offer.Locked)
                label.FontColorOverride = Color.Gray;

            AddChild(label);

            var price = offer.Locked
                ? Loc.GetString("rumc-trade-ui-needs-level", ("level", offer.Level))
                : Loc.GetString("rumc-trade-ui-price", ("price", offer.Price));
            var priceLabel = new Label { Text = price, VerticalAlignment = VAlignment.Center };
            if (offer.Locked)
                priceLabel.FontColorOverride = Color.Gray;

            AddChild(priceLabel);

            Buy = new Button { Text = Loc.GetString("rumc-trade-ui-buy") };
            AddChild(Buy);
        }
    }
}
