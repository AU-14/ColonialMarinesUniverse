using System.Linq;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Lobby;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Lobby;

public sealed partial class LobbyLineupPanel
{
    private LobbyPartyShowControl? _partyShow;
    private LobbyPartyShowEvent? _pendingShow;
    private float _showCooldown;
    private float _nextShowcaseShow = 20;
    private LobbyPartyShow _nextShowcaseKind;

    private void InitializeShows()
    {
        Flyby.OnPressed += _ => PerformShow(LobbyPartyShow.Flyby);
        Parade.OnPressed += _ => PerformShow(LobbyPartyShow.Parade);
        StopShow.OnPressed += _ =>
        {
            ReleaseShow();
            UpdateActions();
        };
        Ambient.OnToggled += _ =>
        {
            if (!Ambient.Pressed)
                ReleaseShow();
            UpdateActions();
        };
        PartySounds.OnToggled += _ =>
        {
            if (_partyShow == null)
                return;
            _partyShow.SoundEnabled = PartySounds.Pressed;
            if (!PartySounds.Pressed)
                _partyShow.StopSounds();
        };
    }

    private void OnShowSettingsChanged(bool enabled)
    {
        if (_partyShow is { } active && !LobbyPartySettings.IsShowEnabled(_configuration, active.Show) ||
            _pendingShow is { } pending && !LobbyPartySettings.IsShowEnabled(_configuration, pending.Show))
            ReleaseShow();
        Refresh();
        UpdateActions();
    }

    public void PerformShow(LobbyPartyShow show)
    {
        if (!LobbyPartySettings.IsShowEnabled(_configuration, show) || _selected == null || _showCooldown > 0 || _partyShow != null ||
            _stageMotion != null || _arrivalPending || _departing)
            return;
        if (_showcase == null)
            _social?.RequestShow(show);
        else
            OnShow(new LobbyPartyShowEvent(show, _random.Next(), _entries.Select(entry => entry.UserId).ToList(), false));
    }

    private void OnShow(LobbyPartyShowEvent ev)
    {
        if (!VisibleInTree || _departing || !LobbyPartySettings.IsShowEnabled(_configuration, ev.Show))
            return;
        _showCooldown = LobbyPartyShowEvent.Cooldown;
        UpdateShowActions();
        if (ev.Automatic && !Ambient.Pressed)
            return;
        // A single pending routine waits for arrival. Never create competing owners of a preview sprite.
        _pendingShow = ev;
        UpdateActions();
    }

    private void AdvanceShowCooldown(float delta)
    {
        var previous = (int) Math.Ceiling(_showCooldown);
        _showCooldown = Math.Max(0, _showCooldown - delta);
        if (previous != (int) Math.Ceiling(_showCooldown))
            UpdateShowActions();
    }

    private void AdvanceShow(float delta)
    {
        // Card bounds may not exist on the first frame. Keep the show queued until arrival has
        // both started and finished, otherwise its cleanup unhides portraits borrowed by the show.
        if (_arrivalPending || _stageMotion != null || _departing)
            return;
        if (_partyShow == null && _pendingShow is { } pending)
        {
            _pendingShow = null;
            var cast = pending.Participants.Where(_cards.ContainsKey).Select(id => _cards[id]).ToArray();
            if (cast.Length > 0)
            {
                _partyShow = new LobbyPartyShowControl(pending, cast,
                    _configuration.GetCVar(CCVars.ReducedMotion), PartySounds.Pressed);
                var show = _partyShow;
                UserInterfaceManager.DeferAction(() =>
                {
                    if (_partyShow != show)
                        return;
                    var root = UserInterfaceManager.WindowRoot;
                    LayoutContainer.SetAnchorPreset(show, LayoutContainer.LayoutPreset.Wide);
                    root.AddChild(show);
                    show.Measure(root.Size);
                    show.Arrange(new UIBox2(Vector2.Zero, root.Size));
                    show.Visible = VisibleInTree;
                    show.Advance(0);
                });
                UpdateActions();
            }
        }
        if (_partyShow != null)
        {
            _partyShow.Advance(delta);
            if (_partyShow.Finished)
            {
                ReleaseShow();
                UpdateActions();
            }
        }
        if (_showcase == null || !Ambient.Pressed || _partyShow != null || _showCooldown > 0)
            return;
        _nextShowcaseShow -= delta;
        if (_nextShowcaseShow > 0 || _entries.Count == 0)
            return;
        if (!LobbyPartySettings.TryNextShow(_configuration, _nextShowcaseKind, out var next))
            return;
        OnShow(new LobbyPartyShowEvent(next, _random.Next(), _entries.Select(entry => entry.UserId).ToList(), true));
        _nextShowcaseKind = next == LobbyPartyShow.Flyby ? LobbyPartyShow.Parade : LobbyPartyShow.Flyby;
        _nextShowcaseShow = LobbyPartyShowEvent.AutomaticInterval;
    }

    private void UpdateShowActions()
    {
        var busy = _partyShow != null || _pendingShow != null || _stageMotion != null || _arrivalPending || _departing;
        var unavailable = busy || _showCooldown > 0 || !_entries.Any(entry => entry.UserId == _selected);
        Flyby.Visible = LobbyPartySettings.IsShowEnabled(_configuration, LobbyPartyShow.Flyby);
        Parade.Visible = LobbyPartySettings.IsShowEnabled(_configuration, LobbyPartyShow.Parade);
        Flyby.Disabled = unavailable || !Flyby.Visible;
        Parade.Disabled = unavailable || !Parade.Visible;
        StopShow.Visible = _partyShow != null || _pendingShow != null;
        var status = _showCooldown > 0
            ? Loc.GetString("cmu-lobby-party-cooldown", ("seconds", (int) Math.Ceiling(_showCooldown)))
            : Loc.GetString("cmu-lobby-party-ready");
        Flyby.ToolTip = Loc.GetString("cmu-lobby-party-flyby-hint") + "\n" + status;
        Parade.ToolTip = Loc.GetString("cmu-lobby-party-parade-hint") + "\n" + status;
    }

    private void ReleaseShow()
    {
        _pendingShow = null;
        _partyShow?.Release();
        _partyShow = null;
    }
}
