using System.Linq;
using Content.Client.Eui;
using Content.Shared.CMU14.PersistentEconomy;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.PersistentEconomy;

[UsedImplicitly]
public sealed class CMUEconomyEui : BaseEui
{
    private readonly DefaultWindow _window = new() { Title = Loc.GetString("cmu-economy-title"), MinSize = new(700, 650) };
    private readonly BoxContainer _content = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private Guid _token;
    private int _profileId;
    private bool _pending;
    private readonly List<(string Id, CheckBox Box)> _selected = new();

    public CMUEconomyEui()
    {
        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true };
        scroll.AddChild(_content);
        _window.AddChild(scroll);
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
    }

    public override void Opened() => _window.OpenCentered();
    public override void Closed() => _window.Close();

    private void Send(CMUEconomyAction action, long amount = 0, string target = "")
    {
        if (_pending)
            return;
        _pending = true;
        SendMessage(new CMUEconomyMessage
        {
            Token = _token, ProfileId = _profileId, Action = action, Amount = amount, Target = target,
            Items = action == CMUEconomyAction.Save ? _selected.Where(i => i.Box.Pressed).Select(i => i.Id).ToList() : new(),
        });
    }

    private void Label(string text) => _content.AddChild(new Label { Text = text });
    private Button Button(string key, Action action)
    {
        var button = new Button { Text = Loc.GetString(key) };
        button.OnPressed += _ => action();
        _content.AddChild(button);
        return button;
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not CMUEconomyState data)
            return;
        _token = data.Token;
        _profileId = data.ProfileId;
        _pending = false;
        _content.DisposeAllChildren();
        _selected.Clear();
        Label(data.Character);
        Label(Loc.GetString("cmu-economy-balance", ("balance", data.Balance)));
        Label(Loc.GetString("cmu-economy-round", ("stake", data.Stake), ("cap", data.Cap), ("credited", data.Credited), ("remaining", data.Cap - data.Credited)));
        Label(data.Status);
        Button("cmu-economy-refresh", () => Send(CMUEconomyAction.Refresh));
        var id = new LineEdit { Text = data.PlayerId, Editable = false };
        _content.AddChild(id);
        Label(Loc.GetString("cmu-economy-id-hint"));

        if (data.Atm)
        {
            var amount = new LineEdit { PlaceHolder = Loc.GetString("cmu-economy-amount") };
            _content.AddChild(amount);
            Button("cmu-economy-withdraw", () => { if (long.TryParse(amount.Text, out var value)) Send(CMUEconomyAction.Withdraw, value); });
            Button("cmu-economy-deposit", () => Send(CMUEconomyAction.Deposit));
            var recipient = new LineEdit { PlaceHolder = Loc.GetString("cmu-economy-recipient") };
            _content.AddChild(recipient);
            Button("cmu-economy-transfer", () => { if (long.TryParse(amount.Text, out var value)) Send(CMUEconomyAction.Transfer, value, recipient.Text); });
        }
        else
            Label(Loc.GetString("cmu-economy-atm-required"));

        Label(Loc.GetString("cmu-economy-loadout"));
        Label(Loc.GetString("cmu-economy-projection", ("cost", data.Cost), ("balance", Math.Max(0, data.Balance - data.Cost)),
            ("stake", data.ProjectedStake), ("cap", data.ProjectedCap)));
        Label(Loc.GetString("cmu-economy-loadout-hint"));
        var stake = new CheckBox { Text = Loc.GetString("cmu-economy-stake-enabled"), Pressed = data.StakeEnabled };
        stake.OnToggled += args => Send(CMUEconomyAction.Stake, args.Pressed ? 1 : 0);
        _content.AddChild(stake);
        foreach (var item in data.Items)
        {
            var price = item.Owned ? Loc.GetString("cmu-economy-owned") : $"${item.Price}";
            var box = new CheckBox { Text = $"{item.Name} — {price}", Pressed = item.Selected, Disabled = item.Permanent && !item.Owned };
            _content.AddChild(box);
            _selected.Add((item.Id, box));
            Label(item.Jobs);
            if (item.Permanent && !item.Owned)
                Button("cmu-economy-buy", () => Send(CMUEconomyAction.Buy, target: item.Id));
        }
        Button("cmu-economy-save", () => Send(CMUEconomyAction.Save));
        Label(Loc.GetString("cmu-economy-history"));
        Label(data.History);
    }
}
