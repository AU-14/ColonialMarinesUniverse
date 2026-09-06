using System;
using System.Numerics;
using Content.Client._CMU14.Lobby;
using Content.Client._RMC14.Mentor;
using Content.Client.Administration.UI.Bwoink;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Bwoink;
using Content.Client.Voting;
using Content.Client.Voting.UI;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     Opens the small CRT panels named by <see cref="CCVars.CMUPanelPreview"/> once at startup.
/// </summary>
/// <remarks>
///     <para>
///     For the panels that share
///     <see cref="CmuPanelMetrics"/>. Two of them sit behind a lobby button and the third only
///     exists while a vote is running, so a change to a measurement they all read could not be
///     looked at without a person driving the client - which is how three separate no-op changes
///     got through in one session.
///     </para>
///     <para>
///     Preview only, and nothing here is wired to the game: the vote is fabricated, and pressing one
///     of its options would send a cast for a vote id the server has never heard of. It is for
///     looking at.
///     </para>
/// </remarks>
public sealed partial class CmuPanelPreviewSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>
    ///     Wait before opening: the UI root exists well before the lobby has finished building
    ///     itself, and a window opened into that gap gets buried by whatever is added afterwards.
    /// </summary>
    private static readonly TimeSpan OpenDelay = TimeSpan.FromSeconds(3);

    private bool _done;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_done)
            return;

        var spec = _cfg.GetCVar(CCVars.CMUPanelPreview);
        if (string.IsNullOrWhiteSpace(spec))
            return;

        if (_timing.RealTime < OpenDelay)
            return;

        _done = true;

        var opened = 0;

        foreach (var part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "join":
                    Open(new JoinRoundWindow(), opened++);
                    break;
                case "staffhelp":
                    Open(new StaffHelpWindow(), opened++);
                    break;
                // The chat panel itself sits behind the staffhelp card's pick, one more click no
                // preview command can make - these open it directly, the same reasoning as every
                // other case here.
                case "ahelp":
                    Open(CreateBwoinkPreview(), opened++);
                    break;
                case "mentorhelp":
                    Open(CreateMentorHelpPreview(), opened++);
                    break;
                case "vote":
                    Open(CreateVotePreview(), opened++);
                    break;
                case "ready":
                    Open(CreateReadyPreview(), opened++);
                    break;
                case "observe":
                    Open(new ObserveWarningWindow(), opened++);
                    break;
            }
        }
    }

    /// <summary>
    ///     Cascaded rather than centred. Asking for two panels at once is the whole point - you are
    ///     comparing their padding and their row heights - and centring both would stack the second
    ///     exactly on the first.
    /// </summary>
    private static void Open(DefaultWindow window, int index)
    {
        window.OpenCenteredAt(new Vector2(0.42f + index * 0.06f, 0.34f + index * 0.13f));
        window.MoveToFront();
    }

    /// <summary>
    ///     AHelp's chat panel, standalone - bypasses <c>AHelpUIController</c> entirely rather than
    ///     faking a live conversation through it. <c>BwoinkPanel</c> only ever needed the one
    ///     delegate to send a message, so a no-op is enough to look at. The one exception is the
    ///     intro notice: it's pushed through <see cref="AHelpUIController.BuildIntroText"/> so the
    ///     header colour and bullet layout can actually be looked at, since that text only otherwise
    ///     appears the moment a real AHelp window opens.
    /// </summary>
    /// <remarks>
    ///     The theme call mirrors <c>AHelpUIController.EnsureInit</c> exactly
    ///     (<c>includeChat: true</c>, plain typography) - the CRT pass is applied by the controller
    ///     that opens a window, never by the window itself, so a preview that skips this step opens
    ///     looking like base NanoUI regardless of the CRT cvar.
    /// </remarks>
    private static DefaultWindow CreateBwoinkPreview()
    {
        var panel = new BwoinkPanel(_ => { });
        panel.ReceiveLine(new SharedBwoinkSystem.BwoinkTextMessage(
            default, SharedBwoinkSystem.SystemUserId, UserAHelpUIHandler.BuildIntroText()));
        var root = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            StyleClasses = { StyleNano.StyleClassCrtScreenPanel },
        };
        root.AddChild(panel);

        var window = new DefaultWindow
        {
            Title = "AHelp (preview)",
            MinSize = new Vector2(600, 360),
        };
        window.Contents.AddChild(root);
        window.Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetNano;
        CrtLobbyTheme.ApplyWindow(window, includeChat: true, useCrtTypography: false);
        return window;
    }

    /// <summary>
    ///     Same reasoning as <see cref="CreateBwoinkPreview"/>: the real window comes from
    ///     <c>StaffHelpUIController.ApplyCrtChrome</c>, which this replicates so the preview matches
    ///     what opening it through the actual button does.
    /// </summary>
    private static MentorHelpWindow CreateMentorHelpPreview()
    {
        var window = new MentorHelpWindow();
        window.Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetNano;
        CrtLobbyTheme.ApplyWindow(window, useCrtTypography: false);
        return window;
    }

    /// <summary>
    ///     Both states of the lobby ready toggle, side by side.
    /// </summary>
    /// <remarks>
    ///     The real control only exists before a round starts, so on a server that is mid-round -
    ///     which is most of the time while iterating - there is no way to look at it at all. This
    ///     shows the styling only: the same class, the same marks, the same label colours, and the
    ///     two states together so the contrast between them can actually be judged, which is the
    ///     one thing a screenshot of a single state cannot tell you.
    /// </remarks>
    private static DefaultWindow CreateReadyPreview()
    {
        var rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = CmuPanelMetrics.GroupSeparation,
            Margin = CmuPanelMetrics.PanelPadding,
            HorizontalExpand = true,
        };

        rows.AddChild(MakeReadyToggle(false));
        rows.AddChild(MakeReadyToggle(true));

        var window = new DefaultWindow { Title = "Ready toggle (preview)" };
        window.Contents.AddChild(rows);
        window.SetSize = new Vector2(420, 200);
        return window;
    }

    private static Button MakeReadyToggle(bool ready)
    {
        var button = new Button
        {
            ToggleMode = true,
            Pressed = ready,
            HorizontalExpand = true,
            MinHeight = CmuPanelMetrics.RowTall,
        };

        // Everything about the look comes from the shared helper - the text, the classes and the
        // label colour. Nothing here duplicates it, which is the whole point.
        CmuReadyToggle.Apply(button, ready);

        return button;
    }

    /// <summary>
    ///     A vote with votes already cast, in a plain window to hold it.
    /// </summary>
    /// <remarks>
    ///     The counts are uneven and one of them is zero on purpose. Those are the cases the option
    ///     bars exist to show and the ones that are easy to get wrong - a full bar for a lone vote in
    ///     a four-way split, or a sliver of fill drawn for an option nobody picked. <c>OurVote</c> is
    ///     set so the accent edge marking your own choice is visible too.
    /// </remarks>
    private static DefaultWindow CreateVotePreview()
    {
        var vote = new VoteManager.ActiveVote(0)
        {
            Title = "Restart round?",
            Initiator = "Preview",
            DisplayVotes = true,
            OurVote = 1,
            StartTime = TimeSpan.Zero,
            // Short enough to actually watch: opens green, ambers around a minute left (open the
            // preview and wait), reds in the last 20s (CmuCountdownUrgency) - a real vote's whole
            // reason for existing. 30 minutes here would preview only the state nobody has to wait to
            // see.
            EndTime = TimeSpan.FromSeconds(75),
            Entries =
            [
                new VoteManager.VoteEntry("Yes") { Votes = 7 },
                new VoteManager.VoteEntry("No") { Votes = 3 },
                new VoteManager.VoteEntry("Abstain") { Votes = 0 },
            ],
        };

        var popup = new VotePopup(vote);

        // The bars are drawn here, not in the constructor - the real popup is built empty and filled
        // by VoteManager when the counts arrive.
        popup.UpdateData();

        // Explicitly sized, and generously. A window left to size itself to this popup comes out
        // short, and the grid then compresses every option row and every gap by the same fraction -
        // measured 32px rows and 6px gaps where the code asks for 36 and 8. A preview that reports
        // the wrong numbers is worse than no preview, so give it more room than it can use. The
        // height covers the large-UI scale, where the rows are half again as tall.
        var window = new DefaultWindow { Title = "Vote (preview)" };
        window.Contents.AddChild(popup);
        window.SetSize = new Vector2(760, 460);
        return window;
    }
}
