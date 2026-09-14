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
    private readonly DefaultWindow _window = new() { Title = Loc.GetString("cmu-economy-title"), MinSize = new(620, 450) };
    private readonly BoxContainer _content = new()
    {
        Orientation = BoxContainer.LayoutOrientation.Vertical,
        Margin = new Thickness(12),
    };

    private Guid _token;
    private bool _pending;

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
            Token = _token,
            Action = action,
            Amount = amount,
            Target = target,
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
        _pending = false;
        _content.DisposeAllChildren();

        Label(data.Character);
        Label(Loc.GetString("cmu-economy-balance", ("balance", data.Balance)));
        Label(Loc.GetString("cmu-economy-round",
            ("stake", data.Stake),
            ("cap", data.Cap),
            ("escrow", data.Escrow),
            ("credited", data.Credited),
            ("remaining", Math.Max(0, data.Cap - data.Credited - data.Escrow))));

        var stake = new CheckBox
        {
            Text = Loc.GetString("cmu-economy-stake-enabled"),
            Pressed = data.StakeEnabled,
        };
        stake.OnToggled += args => Send(CMUEconomyAction.Stake, args.Pressed ? 1 : 0);
        _content.AddChild(stake);

        if (!string.IsNullOrWhiteSpace(data.Status))
            Label(data.Status);

        Button("cmu-economy-refresh", () => Send(CMUEconomyAction.Refresh));

        var id = new LineEdit { Text = data.PlayerId, Editable = false };
        _content.AddChild(id);
        Label(Loc.GetString("cmu-economy-id-hint"));

        if (data.Atm)
        {
            var amount = new LineEdit { PlaceHolder = Loc.GetString("cmu-economy-amount") };
            _content.AddChild(amount);
            Button("cmu-economy-withdraw", () =>
            {
                if (long.TryParse(amount.Text, out var value))
                    Send(CMUEconomyAction.Withdraw, value);
            });
            Button("cmu-economy-deposit", () => Send(CMUEconomyAction.Deposit));
            Button("cmu-economy-withdraw-escrow", () =>
            {
                if (long.TryParse(amount.Text, out var value))
                    Send(CMUEconomyAction.WithdrawEscrow, value);
            });

            var recipient = new LineEdit { PlaceHolder = Loc.GetString("cmu-economy-recipient") };
            _content.AddChild(recipient);
            Button("cmu-economy-transfer", () =>
            {
                if (long.TryParse(amount.Text, out var value))
                    Send(CMUEconomyAction.Transfer, value, recipient.Text);
            });
        }
        else
        {
            Label(Loc.GetString("cmu-economy-atm-required"));
        }

        Label(Loc.GetString("cmu-economy-history"));
        Label(data.History);
    }
}
