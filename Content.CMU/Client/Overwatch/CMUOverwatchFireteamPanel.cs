using System.Linq;
using System.Text;
using Content.Client.CMU14.Interface;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared._RMC14.Tracker.SquadLeader;
using Content.Shared.CMU14.Preferences;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.Overwatch;

/// <summary>
/// The overwatch console's Fireteams tab: every fireteam and the unassigned, with buttons to move
/// marines between fireteams, choose who leads each one and name them. Works whether or not the
/// squad has a squad leader, unlike the squad leader's tracker window it replaces here.
/// </summary>
public sealed class CMUOverwatchFireteamPanel : BoxContainer
{
    private const float SmallButtonWidth = 34;

    [Dependency] private IPrototypeManager _prototypes = default!;

    public event Action<NetEntity, int?>? OnAssign;
    public event Action<NetEntity, bool>? OnLead;
    public event Action<int, string>? OnNickname;

    private string _signature = string.Empty;

    public CMUOverwatchFireteamPanel()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 8;
        Margin = new Thickness(6);
    }

    /// <summary>
    /// Rebuilds the panel when the fireteams changed. Skipped otherwise, so a click or a nickname
    /// being typed is not lost to the console's twice-a-second refresh.
    /// </summary>
    public void Update(FireteamData? data)
    {
        data ??= new FireteamData();
        var signature = Signature(data);
        if (signature == _signature)
            return;

        _signature = signature;
        RemoveAllChildren();

        AddChild(new Label
        {
            Text = Loc.GetString("cmu-overwatch-fireteams-help"),
            StyleClasses = { StyleNano.StyleClassCrtDimText },
        });

        AddChild(new Label
        {
            Text = data.SquadLeader is { } leader
                ? Loc.GetString("cmu-overwatch-fireteams-squad-leader", ("name", CMUCharacterName.StripNickname(leader)))
                : Loc.GetString("cmu-overwatch-fireteams-no-squad-leader"),
        });

        for (var i = 0; i < data.Fireteams.Length; i++)
        {
            AddChild(FireteamCard(i, data.Fireteams[i]));
        }

        AddChild(UnassignedCard(data));
        CrtLobbyTheme.Apply(this, useCrtTypography: true);
    }

    private Control FireteamCard(int index, SquadLeaderTrackerFireteam? fireteam)
    {
        var body = Card(out var card);

        var nickname = new LineEdit
        {
            Text = fireteam?.Nickname ?? string.Empty,
            PlaceHolder = Loc.GetString("cmu-overwatch-fireteams-nickname"),
            HorizontalExpand = true,
        };
        nickname.OnTextEntered += args => OnNickname?.Invoke(index, args.Text);

        var setName = new Button { Text = Loc.GetString("cmu-overwatch-fireteams-set-nickname") };
        setName.OnPressed += _ => OnNickname?.Invoke(index, nickname.Text);

        body.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Children =
            {
                new Label
                {
                    Text = Loc.GetString("cmu-overwatch-roster-group-fireteam", ("number", index + 1)),
                    StyleClasses = { StyleNano.StyleClassCrtHeading },
                    VerticalAlignment = VAlignment.Center,
                },
                nickname,
                setName,
            },
        });

        var members = fireteam?.Members?.Values
            .OrderBy(m => m.Id == fireteam.Leader?.Id ? 0 : 1)
            .ThenBy(m => m.Name)
            .ToList() ?? new List<SquadLeaderTrackerMarine>();

        if (members.Count == 0)
            body.AddChild(Empty());

        foreach (var member in members)
        {
            body.AddChild(MemberRow(member, index, member.Id == fireteam?.Leader?.Id));
        }

        return card;
    }

    private Control UnassignedCard(FireteamData data)
    {
        var body = Card(out var card);
        body.AddChild(new Label
        {
            Text = Loc.GetString("cmu-overwatch-roster-group-unassigned"),
            StyleClasses = { StyleNano.StyleClassCrtHeading },
        });

        var members = data.Unassigned.Values
            .Where(m => m.Id != data.SquadLeaderId)
            .OrderBy(m => m.Name)
            .ToList();

        if (members.Count == 0)
            body.AddChild(Empty());

        foreach (var member in members)
        {
            body.AddChild(MemberRow(member, null, false));
        }

        return card;
    }

    private Control MemberRow(SquadLeaderTrackerMarine member, int? fireteam, bool isLeader)
    {
        var plainName = CMUCharacterName.StripNickname(member.Name);
        var name = isLeader
            ? Loc.GetString("cmu-overwatch-roster-ftl", ("name", plainName))
            : plainName;

        var role = member.Role is { } roleId && _prototypes.TryIndex(roleId, out JobPrototype? job)
            ? job.LocalizedName
            : string.Empty;

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Children =
            {
                new Label
                {
                    Text = name,
                    ToolTip = name,
                    HorizontalExpand = true,
                    SizeFlagsStretchRatio = 1.6f,
                    ClipText = true,
                    VerticalAlignment = VAlignment.Center,
                },
                new Label
                {
                    Text = role,
                    ToolTip = role,
                    HorizontalExpand = true,
                    ClipText = true,
                    VerticalAlignment = VAlignment.Center,
                    FontColorOverride = CrtTerminalPalette.TextDim,
                },
            },
        };

        if (fireteam != null)
        {
            var lead = new Button
            {
                Text = Loc.GetString("cmu-overwatch-fireteams-lead"),
                ToolTip = Loc.GetString(isLeader ? "cmu-overwatch-fireteams-lead-off-tooltip" : "cmu-overwatch-fireteams-lead-tooltip"),
            };
            lead.OnPressed += _ => OnLead?.Invoke(member.Id, !isLeader);
            CMUOverwatchCrt.SetLit(lead, isLeader);
            row.AddChild(lead);
        }

        for (var i = 0; i < 3; i++)
        {
            var target = i;
            var move = new Button
            {
                Text = $"{i + 1}",
                MinWidth = SmallButtonWidth,
                ToolTip = Loc.GetString("cmu-overwatch-fireteams-move-tooltip", ("number", i + 1)),
                Disabled = fireteam == i,
            };
            move.OnPressed += _ => OnAssign?.Invoke(member.Id, target);
            CMUOverwatchCrt.SetLit(move, fireteam == i);
            row.AddChild(move);
        }

        if (fireteam != null)
        {
            var remove = new Button
            {
                Text = Loc.GetString("cmu-overwatch-fireteams-remove"),
                ToolTip = Loc.GetString("cmu-overwatch-fireteams-remove-tooltip"),
            };
            remove.OnPressed += _ => OnAssign?.Invoke(member.Id, null);
            row.AddChild(remove);
        }

        return row;
    }

    private static BoxContainer Card(out PanelContainer card)
    {
        var body = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };

        card = new PanelContainer
        {
            StyleClasses = { StyleNano.StyleClassCrtInsetPanel },
            HorizontalExpand = true,
            Children = { body },
        };

        return body;
    }

    private static Label Empty()
    {
        return new Label
        {
            Text = Loc.GetString("cmu-overwatch-fireteams-empty"),
            FontColorOverride = CrtTerminalPalette.TextDim,
        };
    }

    private static string Signature(FireteamData data)
    {
        var builder = new StringBuilder();
        builder.Append(data.SquadLeaderId).Append('|').Append(data.SquadLeader).Append('|');
        foreach (var fireteam in data.Fireteams)
        {
            builder.Append(fireteam?.Nickname).Append(':').Append(fireteam?.Leader?.Id).Append(':');
            if (fireteam?.Members != null)
            {
                foreach (var member in fireteam.Members.Values.OrderBy(m => m.Id))
                {
                    builder.Append(member.Id).Append(member.Name).Append(member.Role).Append(',');
                }
            }

            builder.Append(';');
        }

        foreach (var member in data.Unassigned.Values.OrderBy(m => m.Id))
        {
            builder.Append(member.Id).Append(member.Name).Append(member.Role).Append(',');
        }

        return builder.ToString();
    }
}
