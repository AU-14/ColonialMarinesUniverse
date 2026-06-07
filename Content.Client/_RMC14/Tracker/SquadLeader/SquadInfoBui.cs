using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Tracker.SquadLeader;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Content.Client._RMC14.Overwatch;

namespace Content.Client._RMC14.Tracker.SquadLeader;

[UsedImplicitly]
public sealed partial class SquadInfoBui : BoundUserInterface
{
    [Dependency] private IPrototypeManager _prototype = default!;

    private SquadInfoWindow? _window;

    private readonly SpriteSystem _sprite;

    public SquadInfoBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _sprite = EntMan.System<SpriteSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<SquadInfoWindow>();
        Refresh();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        Refresh();
    }

    public void Refresh()
    {
        if (_window is not { IsOpen: true })
            return;

        if (!EntMan.TryGetComponent(Owner, out SquadLeaderTrackerComponent? tracker))
            return;

        Texture? background = null;
        Color backgroundColor = default;
        if (EntMan.TryGetComponent(Owner, out SquadMemberComponent? ownerMember))
        {
            background = _sprite.Frame0(ownerMember.Background);
            backgroundColor = ownerMember.BackgroundColor;
        }

        var isSquadLeader = EntMan.HasComponent<SquadLeaderComponent>(PlayerManager.LocalEntity);
        var localNetEntity = EntMan.GetNetEntity(PlayerManager.LocalEntity);
        var isOverwatchOperator = localNetEntity != null && tracker.TemporaryOverwatchEditors.Contains(localNetEntity.Value);
        var canManageFireteams = isSquadLeader || isOverwatchOperator;

        var squadLeader = tracker.Fireteams.SquadLeader == null
            ? Loc.GetString("rmc-squad-info-squad-leader-none")
            : Loc.GetString("rmc-squad-info-squad-leader-name", ("leader", tracker.Fireteams.SquadLeader));
        _window.SquadLeaderLabel.Text = squadLeader;

        // Changing the client-side tracker is allowed for every client that has the component
        _window.ChangeTrackerButton.Visible = true;
        _window.ChangeTrackerButton.OnPressed += _ => SendPredictedMessage(new SquadLeaderTrackerChangeTrackedMsg());

        // Get squad objectives from state
        Dictionary<SquadObjectiveType, string> objectives = new();
        if (State is SquadLeaderTrackerBoundUserInterfaceState state)
        {
            objectives = new Dictionary<SquadObjectiveType, string>(state.Objectives);
        }

        // Display objectives
        _window.ObjectivesContainer.DisposeAllChildren();
        foreach (SquadObjectiveType objectiveType in Enum.GetValues<SquadObjectiveType>())
        {
            var objectiveText = objectives.GetValueOrDefault(objectiveType, string.Empty);
            var objectiveLabel = new RichTextLabel
            {
                HorizontalExpand = true
            };

            var objectiveName = objectiveType switch
            {
                SquadObjectiveType.Primary => Loc.GetString("rmc-overwatch-console-objective-primary"),
                SquadObjectiveType.Secondary => Loc.GetString("rmc-overwatch-console-objective-secondary"),
                _ => objectiveType.ToString()
            };

            var displayText = string.IsNullOrWhiteSpace(objectiveText)
                ? $"{objectiveName}: {Loc.GetString("rmc-squad-info-none")}"
                : $"{objectiveName}: {FormattedMessage.EscapeText(objectiveText)}";

            objectiveLabel.Text = displayText;
            _window.ObjectivesContainer.AddChild(objectiveLabel);
        }

        _window.FireteamsContainer.DisposeAllChildren();
        for (var i = 0; i < tracker.Fireteams.Fireteams.Length; i++)
        {
            var fireteam = tracker.Fireteams.Fireteams[i];
            if (fireteam?.Members is not { Count: > 0 })
                continue;

            var container = new SquadFireteamContainer();
            // Show edit nickname button; server will validate permissions when message is sent.
            container.EditNicknameButton.Visible = canManageFireteams;
            var fireteamIndex = i;
            container.EditNicknameButton.OnPressed += _ =>
            {
                var window = new OverwatchTextInputWindow { Title = "Edit Fireteam Nickname" };
                window.MessageBox.Text = fireteam.Nickname ?? string.Empty;

                void SendNickname()
                {
                    SendPredictedMessage(new SquadLeaderTrackerSetFireteamNicknameMsg(fireteamIndex, window.MessageBox.Text));
                    window.Close();
                }

                window.MessageBox.OnTextEntered += _ => SendNickname();
                window.OkButton.OnPressed += _ => SendNickname();
                window.CancelButton.OnPressed += _ => window.Close();
                window.OpenCentered();
            };

            var teamLeader = fireteam.Leader == null
                ? Loc.GetString("rmc-squad-info-team-leader-none")
                : Loc.GetString("rmc-squad-info-team-leader-name", ("leader", fireteam.Leader.Value.Name));
            container.LeaderLabel.Text = teamLeader;

            var fireteamIdx = i;
            container.RemoveLeaderButton.OnPressed +=
                _ => SendPredictedMessage(new SquadLeaderTrackerDemoteFireteamLeaderMsg(fireteamIdx));
            // Hide tracker-mode clientside-change for non-leaders, serverside verifies if OW or SL.
            container.RemoveLeaderButton.Visible = fireteam.Leader != null && canManageFireteams;
            var fireteamLabel = Loc.GetString("rmc-squad-info-fireteam", ("fireteam", i + 1));
            if (!string.IsNullOrWhiteSpace(fireteam.Nickname))
                fireteamLabel += $": {fireteam.Nickname}";
            container.FireteamLabel.Text = fireteamLabel;

            foreach (var (_, member) in fireteam.Members)
            {
                if (member.Id == fireteam.Leader?.Id)
                    continue;

                var row = CreateRow(member, background, backgroundColor);
                container.MembersContainer.AddChild(row);

                var promoteButton = new Button
                {
                    MaxWidth = 25,
                    MaxHeight = 25,
                    VerticalAlignment = Control.VAlignment.Center,
                    StyleClasses = { "OpenBoth" },
                    Text = "^",
                    TextAlign = Label.AlignMode.Center,
                    ToolTip = Loc.GetString("rmc-squad-info-promote-team-leader"),
                    Margin = new Thickness(0, 0, 2, 0)
                };

                // Allow Overwatch actors to promote as well; server will validate permission.
                promoteButton.Visible = canManageFireteams;
                promoteButton.OnPressed += _ =>
                    SendPredictedMessage(new SquadLeaderTrackerPromoteFireteamLeaderMsg(member.Id));

                row.ActionsContainer.AddChild(promoteButton);

                var unassignButton = new Button
                {
                    MaxWidth = 25,
                    MaxHeight = 25,
                    VerticalAlignment = Control.VAlignment.Center,
                    StyleClasses = { "OpenBoth" },
                    Text = "x",
                    TextAlign = Label.AlignMode.Center,
                    ToolTip = Loc.GetString("rmc-squad-info-unassign-fireteam"),
                    Margin = new Thickness(0, 0, 2, 0)
                };

                // Allow Overwatch actors to unassign members too; server will validate permission.
                unassignButton.Visible = canManageFireteams;
                unassignButton.OnPressed += _ =>
                    SendPredictedMessage(new SquadLeaderTrackerUnassignFireteamMsg(member.Id));

                row.ActionsContainer.AddChild(unassignButton);
            }

            _window.FireteamsContainer.AddChild(container);
        }

        var unassignedContainer = new SquadFireteamContainer();
        unassignedContainer.LeaderContainer.Visible = false;
        unassignedContainer.RemoveLeaderButton.Visible = false;
        unassignedContainer.FireteamLabel.Text = Loc.GetString("rmc-squad-info-unassigned");
        unassignedContainer.ActionsLabel.Text = Loc.GetString("rmc-squad-info-actions");
        foreach (var (_, unassigned) in tracker.Fireteams.Unassigned)
        {
            if (tracker.Fireteams.SquadLeaderId == unassigned.Id)
                continue;

            var row = CreateRow(unassigned, background, backgroundColor);
            unassignedContainer.MembersContainer.AddChild(row);

            for (var i = 0; i < tracker.Fireteams.Fireteams.Length; i++)
            {
                var button = new Button
                {
                    MaxWidth = 25,
                    MaxHeight = 25,
                    VerticalAlignment = Control.VAlignment.Top,
                    StyleClasses = { "OpenBoth" },
                    Text = $"{i + 1}",
                };

                var fireteamIndex = i;
                // Allow Overwatch actors to assign members; server validates permissions.
                button.Visible = true;
                button.OnPressed += _ =>
                    SendPredictedMessage(new SquadLeaderTrackerAssignFireteamMsg(unassigned.Id, fireteamIndex));

                row.ActionsContainer.AddChild(button);
            }
        }

        _window.FireteamsContainer.AddChild(unassignedContainer);
    }

    private SquadInfoRow CreateRow(SquadLeaderTrackerMarine member, Texture? background, Color backgroundColor)
    {
        var row = new SquadInfoRow();
        if (member.Role is { } role &&
            _prototype.TryIndex(role, out var job))
        {
            if (member.IconOverride != null)
                row.RoleIcon.Texture = _sprite.Frame0(member.IconOverride);
            else if (_prototype.TryIndex(job.Icon, out var icon))
                row.RoleIcon.Texture = _sprite.Frame0(icon.Icon);

            row.RoleBackground.Texture = background;
            row.RoleBackground.ModulateSelfOverride = backgroundColor;
        }

        row.NameLabel.Text = $"[bold]{FormattedMessage.EscapeText(member.Name)}[/bold]";
        return row;
    }
}
