using Content.Shared._CMU14.Threats.Mobs.ZombieSummoner;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._CMU14.Threats.Mobs.ZombieSummoner;

public sealed class ZombieSummonerWindow : DefaultWindow
{
    private readonly Label _pointsLabel;
    private readonly Label _controlledLabel;
    private readonly Label _civilianAvailableLabel;
    private readonly Label _militaryAvailableLabel;
    private readonly SpinBox _amount;
    private readonly Button _summonCivilianButton;
    private readonly Button _summonMilitaryButton;

    private int _civilianMaxSummonable;
    private int _militaryMaxSummonable;
    private int _maxSummonable;

    public event Action<int, ZombieSummonerSpawnType>? OnSummon;

    public ZombieSummonerWindow()
    {
        Title = Loc.GetString("cmu-zombie-summoner-title");
        SetSize = MinSize = new(500, 235);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 7,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true
        };
        Contents.AddChild(root);

        _pointsLabel = new Label { HorizontalExpand = true };
        root.AddChild(_pointsLabel);

        _controlledLabel = new Label { HorizontalExpand = true };
        root.AddChild(_controlledLabel);

        _civilianAvailableLabel = new Label { HorizontalExpand = true };
        root.AddChild(_civilianAvailableLabel);

        _militaryAvailableLabel = new Label { HorizontalExpand = true };
        root.AddChild(_militaryAvailableLabel);

        var amountRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true
        };
        root.AddChild(amountRow);

        amountRow.AddChild(new Label
        {
            Text = Loc.GetString("cmu-zombie-summoner-amount"),
            VerticalAlignment = VAlignment.Center
        });

        _amount = new SpinBox
        {
            Value = 1,
            HorizontalExpand = true,
            IsValid = IsValidAmount
        };
        amountRow.AddChild(_amount);

        var buttonRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true
        };
        root.AddChild(buttonRow);

        _summonCivilianButton = new Button
        {
            Text = Loc.GetString("cmu-zombie-summoner-submit-civilian"),
            TextAlign = Label.AlignMode.Center,
            HorizontalExpand = true
        };
        _summonCivilianButton.OnPressed += _ => TrySummon(ZombieSummonerSpawnType.Civilian);
        buttonRow.AddChild(_summonCivilianButton);

        _summonMilitaryButton = new Button
        {
            Text = Loc.GetString("cmu-zombie-summoner-submit-military"),
            TextAlign = Label.AlignMode.Center,
            HorizontalExpand = true
        };
        _summonMilitaryButton.OnPressed += _ => TrySummon(ZombieSummonerSpawnType.Military);
        buttonRow.AddChild(_summonMilitaryButton);
    }

    public void UpdateState(ZombieSummonerBuiState state)
    {
        var zombieCost = Math.Max(1, state.ZombieCost);
        var militaryZombieCost = Math.Max(1, state.MilitaryZombieCost);
        var openSlots = Math.Max(0, state.MaxControlledZombies - state.ControlledZombies);
        _civilianMaxSummonable = Math.Min(state.Points / zombieCost, openSlots);
        _militaryMaxSummonable = Math.Min(state.Points / militaryZombieCost, openSlots);
        _maxSummonable = Math.Max(_civilianMaxSummonable, _militaryMaxSummonable);

        _pointsLabel.Text = Loc.GetString(
            "cmu-zombie-summoner-points",
            ("points", state.Points),
            ("max", state.MaxPoints));
        _controlledLabel.Text = Loc.GetString(
            "cmu-zombie-summoner-controlled",
            ("count", state.ControlledZombies),
            ("max", state.MaxControlledZombies));
        _civilianAvailableLabel.Text = Loc.GetString(
            "cmu-zombie-summoner-available-civilian",
            ("count", _civilianMaxSummonable),
            ("cost", zombieCost));
        _militaryAvailableLabel.Text = Loc.GetString(
            "cmu-zombie-summoner-available-military",
            ("count", _militaryMaxSummonable),
            ("cost", militaryZombieCost));

        _summonCivilianButton.Disabled = _civilianMaxSummonable <= 0;
        _summonMilitaryButton.Disabled = _militaryMaxSummonable <= 0;
        _amount.IsValid = IsValidAmount;

        if (_maxSummonable <= 0)
        {
            _amount.OverrideValue(1);
            return;
        }

        if (_amount.Value < 1)
            _amount.OverrideValue(1);
        else if (_amount.Value > _maxSummonable)
            _amount.OverrideValue(_maxSummonable);
    }

    private bool IsValidAmount(int value)
    {
        return value >= 1 && value <= Math.Max(1, _maxSummonable);
    }

    private void TrySummon(ZombieSummonerSpawnType type)
    {
        var maxSummonable = type == ZombieSummonerSpawnType.Military
            ? _militaryMaxSummonable
            : _civilianMaxSummonable;

        if (maxSummonable <= 0)
            return;

        OnSummon?.Invoke(Math.Clamp(_amount.Value, 1, maxSummonable), type);
    }
}
