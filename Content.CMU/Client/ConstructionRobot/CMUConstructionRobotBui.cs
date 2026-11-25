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
        _window.OnJobCommand += (robot, job, command) => SendMessage(new CMUConstructionRobotJobCommandMessage(robot, job, command));
        _window.OnRefuelThreshold += (robot, threshold) => SendMessage(new CMUConstructionRobotRefuelThresholdMessage(robot, threshold));
        _window.OnFuelWarning += (robot, threshold) => SendMessage(new CMUConstructionRobotFuelWarningMessage(robot, threshold));
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
    public event Action<NetEntity, int, CMUConstructionRobotJobCommand>? OnJobCommand;
    public event Action<NetEntity, int>? OnRefuelThreshold;
    public event Action<NetEntity, int>? OnFuelWarning;

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
        var refuel = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        refuel.AddChild(new Label
        {
            Text = Loc.GetString("cmu-construction-robot-refuel-threshold", ("percent", state.AutoRefuelThreshold)),
        });
        var lower = new Button { Text = "-5" };
        lower.OnPressed += _ => OnRefuelThreshold?.Invoke(selected, Math.Max(0, state.AutoRefuelThreshold - 5));
        refuel.AddChild(lower);
        var higher = new Button { Text = "+5" };
        higher.OnPressed += _ => OnRefuelThreshold?.Invoke(selected, Math.Min(100, state.AutoRefuelThreshold + 5));
        refuel.AddChild(higher);
        _content.AddChild(refuel);
        var warning = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        warning.AddChild(new Label
        {
            Text = Loc.GetString("cmu-construction-robot-fuel-warning-threshold", ("percent", state.FuelWarningThreshold)),
        });
        var warningLower = new Button { Text = "-5" };
        warningLower.OnPressed += _ => OnFuelWarning?.Invoke(selected, Math.Max(0, state.FuelWarningThreshold - 5));
        warning.AddChild(warningLower);
        var warningHigher = new Button { Text = "+5" };
        warningHigher.OnPressed += _ => OnFuelWarning?.Invoke(selected, Math.Min(100, state.FuelWarningThreshold + 5));
        warning.AddChild(warningHigher);
        _content.AddChild(warning);
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
        AddButton(selected, "cmu-construction-robot-jobs-automatic", CMUConstructionRobotCommand.AutomaticJobOrder);
        if (state.Jobs.Count == 0)
            _content.AddChild(new Label { Text = Loc.GetString("cmu-construction-robot-jobs-empty") });
        foreach (var job in state.Jobs)
        {
            _content.AddChild(new Label { Text = job.Status });
            var controls = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            var priority = new Button { Text = Loc.GetString(job.Priority switch
            {
                CMUConstructionRobotJobPriority.Low => "cmu-construction-robot-priority-low",
                CMUConstructionRobotJobPriority.High => "cmu-construction-robot-priority-high",
                _ => "cmu-construction-robot-priority-normal",
            }) };
            priority.OnPressed += _ => OnJobCommand?.Invoke(selected, job.Id, CMUConstructionRobotJobCommand.Priority);
            controls.AddChild(priority);
            var up = new Button { Text = Loc.GetString("cmu-construction-robot-job-up") };
            up.OnPressed += _ => OnJobCommand?.Invoke(selected, job.Id, CMUConstructionRobotJobCommand.Up);
            controls.AddChild(up);
            var down = new Button { Text = Loc.GetString("cmu-construction-robot-job-down") };
            down.OnPressed += _ => OnJobCommand?.Invoke(selected, job.Id, CMUConstructionRobotJobCommand.Down);
            controls.AddChild(down);
            _content.AddChild(controls);
        }
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
