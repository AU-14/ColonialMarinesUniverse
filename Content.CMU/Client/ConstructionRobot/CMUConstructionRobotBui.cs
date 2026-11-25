using System.Numerics;
using Content.Shared.CMU14.ConstructionRobot;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.ConstructionRobot;

public sealed class CMUConstructionRobotBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUConstructionRobotWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUConstructionRobotWindow>();
        _window.OnCommand += (robot, command) => SendMessage(new CMUConstructionRobotCommandMessage(robot, command));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CMUConstructionRobotUiState status)
            _window?.UpdateState(status);
    }
}

public sealed class CMUConstructionRobotWindow : DefaultWindow
{
    public event Action<NetEntity, CMUConstructionRobotCommand>? OnCommand;

    private readonly BoxContainer _content;

    public CMUConstructionRobotWindow()
    {
        Title = Loc.GetString("cmu-construction-robot-status-title");
        SetSize = MinSize = new Vector2(420, 480);
        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 6,
        };
        scroll.AddChild(_content);
        Contents.AddChild(scroll);
    }

    public void UpdateState(CMUConstructionRobotUiState state)
    {
        _content.RemoveAllChildren();
        if (state.Robots.Count == 0)
        {
            _content.AddChild(new Label { Text = Loc.GetString("cmu-construction-robot-status-no-robots") });
            return;
        }

        foreach (var robot in state.Robots)
        {
            var button = new Button { Text = robot.Name, ToggleMode = true, Pressed = state.Selected == robot.Robot };
            button.OnPressed += _ => OnCommand?.Invoke(robot.Robot, CMUConstructionRobotCommand.Select);
            _content.AddChild(button);
        }

        if (state.Selected is not { } selected)
            return;

        _content.AddChild(new Label
        {
            Text = Loc.GetString("cmu-construction-robot-status-fuel", ("fuel", state.Fuel), ("capacity", state.Capacity)),
        });
        if (!state.RepairAreaAssigned)
        {
            _content.AddChild(new Label
            {
                Text = Loc.GetString(state.AvoidingHazard ? "cmu-construction-robot-status-hazard" :
                    state.Following ? "cmu-construction-robot-status-follow" : "cmu-construction-robot-status-hold"),
            });
            AddButton(selected, state.Following ? "cmu-construction-robot-hold-here" : "cmu-construction-robot-follow",
                state.Following ? CMUConstructionRobotCommand.Hold : CMUConstructionRobotCommand.Follow);
        }

        _content.AddChild(new Label
        {
            Text = Loc.GetString(state.RepairAreaAssigned
                ? "cmu-construction-robot-status-repair-area-assigned"
                : "cmu-construction-robot-status-repair-area-unassigned"),
        });
        if (state.RepairAreaAssigned)
            AddButton(selected, "cmu-construction-robot-repair-area-clear", CMUConstructionRobotCommand.ClearRepairArea);
        AddButton(selected, state.Viewing ? "cmu-construction-robot-camera-stop" : "cmu-construction-robot-camera-start",
            CMUConstructionRobotCommand.Camera);
        AddToggle(selected, "cmu-construction-robot-avoid", state.AvoidHazards, CMUConstructionRobotCommand.AvoidHazards);
        AddToggle(selected, "cmu-construction-robot-avoid-fire", state.AvoidFire, CMUConstructionRobotCommand.AvoidFire);
        AddToggle(selected, "cmu-construction-robot-avoid-hostiles", state.AvoidHostiles, CMUConstructionRobotCommand.AvoidHostiles);
        AddToggle(selected, "cmu-construction-robot-avoid-explosives", state.AvoidExplosives, CMUConstructionRobotCommand.AvoidExplosives);
        _content.AddChild(new Label { Text = Loc.GetString("cmu-construction-robot-jobs") });
        if (state.Jobs.Count == 0)
            _content.AddChild(new Label { Text = Loc.GetString("cmu-construction-robot-jobs-empty") });
        foreach (var job in state.Jobs)
            _content.AddChild(new Label { Text = job });
    }

    private void AddButton(NetEntity robot, string text, CMUConstructionRobotCommand command)
    {
        var button = new Button { Text = Loc.GetString(text) };
        button.OnPressed += _ => OnCommand?.Invoke(robot, command);
        _content.AddChild(button);
    }

    private void AddToggle(NetEntity robot, string text, bool enabled, CMUConstructionRobotCommand command)
    {
        var button = new Button { Text = Loc.GetString(text), ToggleMode = true, Pressed = enabled };
        button.OnPressed += _ => OnCommand?.Invoke(robot, command);
        _content.AddChild(button);
    }
}
