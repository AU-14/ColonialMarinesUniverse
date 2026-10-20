using Content.Client._RMC14.Overwatch;
using Content.Shared._RMC14.Overwatch;
using Content.Shared.CMU14.Overwatch;
using Robust.Client.Player;

namespace Content.Client.CMU14.Overwatch;

/// <summary>
/// Opens the overwatch camera feeds window while the local player has feeds up, and closes them
/// all when that window is closed.
/// </summary>
public sealed class CMUOverwatchFeedsSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private CMUOverwatchFeedsWindow? _window;
    private EntityUid? _console;
    private bool _closingFromState;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUOverwatchFeedsComponent, AfterAutoHandleStateEvent>(OnFeedsState);
        SubscribeLocalEvent<CMUOverwatchFeedsComponent, ComponentShutdown>(OnFeedsShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CloseWindow();
    }

    /// <summary>
    /// The targets with a feed up, for lighting the roster's feed buttons.
    /// </summary>
    public IReadOnlyCollection<NetEntity> GetFeedTargets()
    {
        if (_player.LocalEntity is not { } player ||
            !TryComp(player, out CMUOverwatchFeedsComponent? feeds))
        {
            return Array.Empty<NetEntity>();
        }

        var targets = new List<NetEntity>(feeds.Feeds.Count);
        foreach (var feed in feeds.Feeds)
        {
            targets.Add(feed.Target);
        }

        return targets;
    }

    private void OnFeedsState(Entity<CMUOverwatchFeedsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Owner != _player.LocalEntity)
            return;

        _console = ent.Comp.Console;
        RefreshConsoleUi();

        if (ent.Comp.Feeds.Count == 0)
        {
            CloseWindow();
            return;
        }

        if (_window == null)
        {
            _window = new CMUOverwatchFeedsWindow();
            _window.OnCloseFeed += CloseFeed;
            _window.OnClose += OnWindowClosed;
            _window.OpenCentered();
        }

        _window.SetFeeds(ent.Comp.Feeds);
    }

    private void OnFeedsShutdown(Entity<CMUOverwatchFeedsComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner != _player.LocalEntity)
            return;

        CloseWindow();
        RefreshConsoleUi();
    }

    private void CloseFeed(NetEntity target)
    {
        if (_console is { } console)
            _ui.ClientSendUiMessage(console, OverwatchConsoleUI.Key, new CMUOverwatchFeedToggleBuiMsg(target));
    }

    private void OnWindowClosed()
    {
        _window = null;
        if (_closingFromState || _console is not { } console)
            return;

        _ui.ClientSendUiMessage(console, OverwatchConsoleUI.Key, new CMUOverwatchFeedClearBuiMsg());
    }

    private void CloseWindow()
    {
        if (_window == null)
            return;

        _closingFromState = true;
        _window.ClearEyes();
        _window.Close();
        _window = null;
        _closingFromState = false;
    }

    private void RefreshConsoleUi()
    {
        if (_console is { } console &&
            _ui.TryGetOpenUi(console, OverwatchConsoleUI.Key, out var bui) &&
            bui is OverwatchConsoleBui overwatch)
        {
            overwatch.Refresh();
        }
    }
}
