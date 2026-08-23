using Content.Shared._AU14.Chemistry.Research;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._AU14.Chemistry.Research;

[UsedImplicitly]
public sealed class AdminChemicalContractConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AdminChemicalContractConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AdminChemicalContractConsoleWindow>();
        _window.SelectAll.OnPressed += _ => SendPredictedMessage(new AdminChemicalContractSetAllBuiMsg(1));
        _window.ClearAll.OnPressed += _ => SendPredictedMessage(new AdminChemicalContractSetAllBuiMsg(0));
        _window.IssueContract.OnPressed += _ => SendPredictedMessage(new AdminChemicalContractIssueBuiMsg());

        if (State is AdminChemicalContractConsoleBuiState state)
            RefreshState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is AdminChemicalContractConsoleBuiState contractState)
            RefreshState(contractState);
    }

    private void RefreshState(AdminChemicalContractConsoleBuiState state)
    {
        if (_window is null)
            return;

        _window.PropertyContainer.RemoveAllChildren();
        foreach (var property in state.Properties)
        {
            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(4, 2),
            };

            var name = new Label
            {
                Text = $"{property.Name} ({property.Id})",
                ToolTip = property.Description,
                HorizontalExpand = true,
                VerticalAlignment = Control.VAlignment.Center,
            };

            var decrease = new Button
            {
                Text = "−",
                Disabled = property.SelectedLevel <= 0,
                ToolTip = Loc.GetString("admin-chemical-contract-decrease"),
            };

            var level = new Label
            {
                Text = property.SelectedLevel == 0
                    ? Loc.GetString("admin-chemical-contract-level-off")
                    : Loc.GetString("admin-chemical-contract-level", ("level", property.SelectedLevel)),
                MinSize = new System.Numerics.Vector2(72, 0),
                HorizontalAlignment = Control.HAlignment.Center,
                VerticalAlignment = Control.VAlignment.Center,
            };

            var increase = new Button
            {
                Text = "+",
                Disabled = property.SelectedLevel >= property.MaxLevel,
                ToolTip = Loc.GetString("admin-chemical-contract-increase", ("max", property.MaxLevel)),
            };

            var id = property.Id;
            var selectedLevel = property.SelectedLevel;
            decrease.OnPressed += _ => SendPredictedMessage(
                new AdminChemicalContractSetPropertyBuiMsg(id, selectedLevel - 1));
            increase.OnPressed += _ => SendPredictedMessage(
                new AdminChemicalContractSetPropertyBuiMsg(id, selectedLevel + 1));

            row.AddChild(name);
            row.AddChild(decrease);
            row.AddChild(level);
            row.AddChild(increase);
            _window.PropertyContainer.AddChild(row);
        }

        _window.Status.Text = string.IsNullOrWhiteSpace(state.Status)
            ? Loc.GetString("admin-chemical-contract-status-ready")
            : state.Status;
    }
}
