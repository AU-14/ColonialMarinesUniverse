// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.CMU14.WorldMarket;
using Content.Shared.CMU14.Government;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using static Content.Client.CMU14.WorldMarket.TermKit;

namespace Content.Client.CMU14.Government;

// The window of the market stall. The owner sees his goods, sets the price of each, takes back what he will not sell, and lets the stall go when it is empty; anyone else sees what is for sale and buys.
public sealed class MarketStallWindow : BaseWindow
{
    private const float WindowWidth = 520f;

    private readonly BoxContainer _page = Column(6);
    private readonly ScrollContainer _scroll;
    private readonly Dictionary<NetEntity, string> _priceDrafts = new();

    private StallState? _state;
    private string _signature = string.Empty;

    public event Action<NetEntity, int>? OnSetPrice;
    public event Action<NetEntity>? OnTake;
    public event Action<NetEntity>? OnBuy;
    public event Action? OnRelease;

    public MarketStallWindow()
    {
        Resizable = false;
        MinSize = new Vector2(WindowWidth, 0);
        MouseFilter = MouseFilterMode.Stop;

        var close = new TermButton { Text = "✕", MinWidth = 28 };
        close.OnPressed += _ => Close();

        var header = Row(10);
        header.AddChild(Gold(Loc.GetString("rumc-stall-title"), expand: true));
        header.AddChild(close);

        _scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, MinSize = new Vector2(0, 360), HorizontalExpand = true };
        _scroll.AddChild(_page);

        var root = Column(6);
        root.Margin = new Thickness(8, 6, 8, 8);
        root.AddChild(header);
        root.AddChild(Rule());
        root.AddChild(_scroll);

        AddChild(new TermPanel(scanlines: true) { MouseFilter = MouseFilterMode.Pass, Children = { root } });
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => relativeMousePos.Y < 34 ? DragMode.Move : DragMode.None;

    private string MyName()
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        return IoCManager.Resolve<IPlayerManager>().LocalEntity is { } local ? entMan.GetComponent<MetaDataComponent>(local).EntityName : string.Empty;
    }

    public void SetState(StallState state)
    {
        _state = state;
        var signature = state.Owner + "|" + string.Join(";", state.Items.Select(i => $"{i.Item}:{i.Price}"));
        if (signature == _signature)
            return;

        _signature = signature;
        _page.RemoveAllChildren();

        var mine = state.Owner.Length > 0 && state.Owner == MyName();

        var head = Column(4);
        head.AddChild(Text(state.Owner.Length == 0 ? Loc.GetString("rumc-stall-free") : Loc.GetString("rumc-stall-owner", ("owner", state.Owner)), TermPalette.Gold));
        head.AddChild(Muted(Loc.GetString("rumc-stall-tax", ("value", state.Tax))));
        if (mine)
            head.AddChild(Wrapped(Loc.GetString("rumc-stall-owner-hint", ("max", state.MaxItems)), TermPalette.Muted));
        else if (state.Owner.Length == 0)
            head.AddChild(Wrapped(Loc.GetString("rumc-stall-free-hint"), TermPalette.Muted));

        _page.AddChild(Card(head));

        if (state.Items.Count == 0)
        {
            _page.AddChild(Card(Muted(Loc.GetString("rumc-stall-empty"))));
        }

        foreach (var item in state.Items)
        {
            var id = item.Item;
            var column = Column(4);
            column.AddChild(Text(item.Name, TermPalette.Text));

            if (mine)
            {
                column.AddChild(Muted(item.Price > 0 ? Loc.GetString("rumc-stall-price", ("sum", Money(item.Price))) : Loc.GetString("rumc-stall-not-for-sale")));

                var draft = _priceDrafts.GetValueOrDefault(id, item.Price > 0 ? item.Price.ToString() : string.Empty);
                var row = Row(8);
                var edit = new LineEdit { HorizontalExpand = true, PlaceHolder = Loc.GetString("rumc-stall-price-placeholder"), Text = draft, IsValid = value => value.Length <= 6 && value.All(char.IsDigit) };
                edit.OnTextChanged += args => _priceDrafts[id] = args.Text;
                row.AddChild(edit);
                row.AddChild(Action(Loc.GetString("rumc-stall-set-price"), () => OnSetPrice?.Invoke(id, int.TryParse(_priceDrafts.GetValueOrDefault(id, draft), out var price) ? price : 0), true, 150));
                column.AddChild(row);
                column.AddChild(Danger(Loc.GetString("rumc-stall-take"), () => OnTake?.Invoke(id), true, 190));
            }
            else if (item.Price > 0)
            {
                column.AddChild(Action(Loc.GetString("rumc-stall-buy", ("sum", Money(item.Price))), () => OnBuy?.Invoke(id), state.Owner.Length > 0, 260));
            }
            else
            {
                column.AddChild(Muted(Loc.GetString("rumc-stall-not-for-sale")));
            }

            _page.AddChild(Card(column));
        }

        if (mine && state.Items.Count == 0)
            _page.AddChild(Card(Danger(Loc.GetString("rumc-stall-release"), () => OnRelease?.Invoke(), true, 220)));
    }

    private static Control Wrapped(string text, Color color)
    {
        var label = new RichTextLabel { MaxWidth = WindowWidth - 70f, HorizontalExpand = true };
        label.SetMessage(Robust.Shared.Utility.FormattedMessage.FromUnformatted(text), null, color);
        return label;
    }
}
