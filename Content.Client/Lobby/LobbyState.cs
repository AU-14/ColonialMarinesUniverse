using System;
using System.Linq;
using System.Numerics;
using Content.Client._CMU14.Interface;
using Content.Client._CMU14.Lobby;
using Content.Client._CMU14.UserInterface.Options;
using Content.Client._RMC14.LinkAccount;
using Content.Client.Audio;
using Content.Client.GameTicking.Managers;
using Content.Client.LateJoin;
using Content.Client.Lobby.UI;
using Content.Client.Message;
using Content.Client.Playtime;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Voting;
using Content.Shared.CMU14.Allegiance;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Robust.Client;
using Robust.Client.Console;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Lobby
{
    public sealed partial class LobbyState : State
    {
        [Dependency] private IBaseClient _baseClient = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private IClientConsoleHost _consoleHost = default!;
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IResourceCache _resourceCache = default!;
        [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private IVoteManager _voteManager = default!;
        [Dependency] private ClientsidePlaytimeTrackingManager _playtimeTracking = default!;

        // RMC14
        [Dependency] private LinkAccountManager _linkAccount = default!;
        [Dependency] private IClientPreferencesManager _preferencesManager = default!;

        /// <summary>
        /// Whether the player wants to ignore allegiance for spawning the current character.
        /// </summary>
        public bool IgnoreAllegiance { get; set; }
        [Dependency] private IPrototypeManager _protoMan = default!;

        private ClientGameTicker _gameTicker = default!;
        private ContentAudioSystem _contentAudioSystem = default!;
        // The faction choices, opened from JoinRoundButton. Held so a second press re-focuses the
        // one window rather than stacking another copy on top of it.
        private JoinRoundWindow? _joinRoundWindow;
        private CmuUiSetupWindow? _uiSetupWindow;
        private bool _clockPlaced;

        protected override Type? LinkedScreenType { get; } = typeof(LobbyGui);
        public LobbyGui? Lobby;

        protected override void Startup()
        {
            if (_userInterfaceManager.ActiveScreen == null)
            {
                return;
            }

            Lobby = (LobbyGui) _userInterfaceManager.ActiveScreen;

            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            _gameTicker = _entityManager.System<ClientGameTicker>();
            _contentAudioSystem = _entityManager.System<ContentAudioSystem>();
            _contentAudioSystem.LobbySoundtrackChanged += UpdateLobbySoundtrackInfo;

            chatController.SetMainChat(true);

            _voteManager.SetPopupContainer(Lobby.VoteContainer);
            LayoutContainer.SetAnchorPreset(Lobby, LayoutContainer.LayoutPreset.Wide);

            var lobbyNameCvar = _cfg.GetCVar(CCVars.ServerLobbyName);
            var serverName = _baseClient.GameInfo?.ServerName ?? string.Empty;

            Lobby.ServerName.Text = string.IsNullOrEmpty(lobbyNameCvar)
                ? Loc.GetString("ui-lobby-title", ("serverName", serverName))
                : lobbyNameCvar;

            var width = _cfg.GetCVar(CCVars.ServerLobbyRightPanelWidth);
            Lobby.RightSide.SetWidth = width;

            UpdateLobbyUi();

            Lobby.CharacterPreview.CharacterSetupButton.OnPressed += OnSetupPressed;
            Lobby.CharacterPreview.PatronPerks.OnPressed += OnPatronPerksPressed;
            Lobby.CharacterPreview.PrevCharacterButton.OnPressed += OnPrevCharPressed;
            Lobby.CharacterPreview.NextCharacterButton.OnPressed += OnNextCharPressed;
            Lobby.CharacterPreview.IgnoreAllegianceToggle.OnToggled += OnIgnoreAllegianceToggled;
            Lobby.CharacterPreview.IgnoreAllegianceToggle.Pressed = false;
            SetIgnoreAllegiance(false);
            Lobby.ReadyButton.OnPressed += OnReadyPressed;
            Lobby.ReadyButton.OnToggled += OnReadyToggled;
            Lobby.RoundClock.PositionChanged += OnRoundClockMoved;
            Lobby.ClockMinimizeButton.OnPressed += OnClockMinimizePressed;
            Lobby.DockedClockButton.OnPressed += OnClockRestorePressed;

            _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated += LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;

            // RMC14/CMU: the faction choices used to be three buttons on the lobby panel. They now
            // live in JoinRoundWindow, opened from one button; the handlers below are unchanged.
            Lobby.JoinRoundButton.OnPressed += OnJoinRoundPressed;

            if (CmuUiSetupWindow.NeedsSetup(_cfg))
            {
                _uiSetupWindow = new CmuUiSetupWindow();
                _uiSetupWindow.OpenCentered();
            }
        }

        protected override void Shutdown()
        {
            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            chatController.SetMainChat(false);
            _gameTicker.InfoBlobUpdated -= UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated -= LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated -= LobbyLateJoinStatusUpdated;
            _contentAudioSystem.LobbySoundtrackChanged -= UpdateLobbySoundtrackInfo;

            _voteManager.ClearPopupContainer();

            Lobby!.CharacterPreview.CharacterSetupButton.OnPressed -= OnSetupPressed;
            Lobby.CharacterPreview.PatronPerks.OnPressed -= OnPatronPerksPressed;
            Lobby.CharacterPreview.PrevCharacterButton.OnPressed -= OnPrevCharPressed;
            Lobby.CharacterPreview.NextCharacterButton.OnPressed -= OnNextCharPressed;
            Lobby.CharacterPreview.IgnoreAllegianceToggle.OnToggled -= OnIgnoreAllegianceToggled;
            Lobby!.ReadyButton.OnPressed -= OnReadyPressed;
            Lobby!.ReadyButton.OnToggled -= OnReadyToggled;
            Lobby!.RoundClock.PositionChanged -= OnRoundClockMoved;
            Lobby!.ClockMinimizeButton.OnPressed -= OnClockMinimizePressed;
            Lobby!.DockedClockButton.OnPressed -= OnClockRestorePressed;

            // Unhook RMC14 buttons
            Lobby.JoinRoundButton.OnPressed -= OnJoinRoundPressed;
            _joinRoundWindow?.Close();
            _joinRoundWindow = null;
            _uiSetupWindow?.Close();
            _uiSetupWindow = null;

            Lobby = null;
        }

        public void SwitchState(LobbyGui.LobbyGuiState state)
        {
            // Yeah I hate this but LobbyState contains all the badness for now.
            Lobby?.SwitchState(state);
        }

        private void OnSetupPressed(BaseButton.ButtonEventArgs args)
        {
            SetReady(false);
            Lobby?.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
        }

        private void OnPatronPerksPressed(BaseButton.ButtonEventArgs obj)
        {
            _userInterfaceManager.GetUIController<LinkAccountUIController>().TogglePatronPerksWindow();
        }

        private void OnReadyPressed(BaseButton.ButtonEventArgs args)
        {
            if (!_gameTicker.IsGameStarted)
            {
                return;
            }

            // Second-stage ready action: open colonists-filtered late-join UI
            new LateJoinGui("colonists").OpenCentered();
        }

        private void OnReadyToggled(BaseButton.ButtonToggledEventArgs args)
        {
            SetReady(args.Pressed);

            // Immediately, not on the server's echo. UpdateLobbyUi is what normally repaints this,
            // and it runs on a lobby status update - so without this the mark and the colour lag the
            // click by a round trip, which reads as the button not having worked.
            UpdateReadyAppearance();
        }

        /// <summary>
        ///     Put the ready toggle into the state it is actually in.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///     The box comes from <see cref="StyleNano.StyleClassCrtReadyToggle"/>, which keys off the
        ///     Pressed pseudo-class. The mark and the label colour are set here because neither can
        ///     come from a rule: the text is content, and a per-state font colour would need a rule
        ///     selecting a label by its parent's pseudo-class.
        ///     </para>
        ///     <para>
        ///     Three channels, deliberately - fill, edge colour, and a mark that reads with no colour
        ///     at all. The old version moved one step up the surface ladder and changed nothing else,
        ///     which is why it was easy to miss whether you had readied up.
        ///     </para>
        /// </remarks>
        private void UpdateReadyAppearance()
        {
            // Shared with the cmu.panel_preview=ready harness, so what that shows is what this
            // does. They drifted apart once already and the harness quietly showed the wrong state.
            CmuReadyToggle.Apply(Lobby!.ReadyButton, Lobby!.ReadyButton.Pressed);
        }

        public override void FrameUpdate(FrameEventArgs e)
        {
            // The clock is a caption and a face. The caption says what is being counted so the face
            // never has to carry a sentence, which is what lets it be read at a glance rather than
            // parsed - the whole reason it left the action panel's heading slot.
            if (_gameTicker.IsGameStarted)
            {
                var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                Lobby!.StationTime.Text = Loc.GetString("lobby-state-round-time-short", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));

                // Nothing left to count, and the header already carries elapsed time. Both forms go.
                Lobby!.RoundClock.Visible = false;
                Lobby!.DockedClockButton.Visible = false;
                return;
            }

            var minimized = _cfg.GetCVar(CCVars.CMULobbyClockMinimized);
            Lobby!.RoundClock.Visible = !minimized;
            Lobby!.DockedClockButton.Visible = minimized;
            Lobby!.LobbyStatusLine.Visible = false;
            // Words, not a placeholder duration: the sidebar header no longer carries the round
            // state, so this is the only place it shows.
            Lobby!.StationTime.Text = Loc.GetString("lobby-state-round-not-started-short");

            // Skipped while folded: PlaceRoundClock's default branch centres using the panel's own
            // size, which is zero when it isn't drawn, and would bank a garbage position.
            if (!minimized)
                PlaceRoundClock();

            if (_gameTicker.Paused)
            {
                // Paused is indefinite, not urgent - it must not sit there glowing red.
                Lobby!.ClockCaption.Text = Loc.GetString("cmu-lobby-clock-caption-start");
                SetClockFace(
                    Loc.GetString("cmu-lobby-clock-face-paused"),
                    Loc.GetString("cmu-lobby-clock-docked-paused"));
                SetCountdownUrgency(CmuCountdownUrgency.Level.Normal);
                return;
            }

            Lobby!.ClockCaption.Text = Loc.GetString("cmu-lobby-clock-caption-countdown");

            if (_gameTicker.StartTime < _gameTiming.CurTime)
            {
                SetCountdownUrgency(CmuCountdownUrgency.Level.Imminent);
                SetClockFace(
                    Loc.GetString("cmu-lobby-clock-face-soon"),
                    Loc.GetString("cmu-lobby-clock-docked-soon"));
                return;
            }

            var difference = _gameTicker.StartTime - _gameTiming.CurTime;
            var seconds = difference.TotalSeconds;
            SetCountdownUrgency(CmuCountdownUrgency.Get(seconds));

            if (seconds < 0)
            {
                SetClockFace(
                    Loc.GetString("cmu-lobby-clock-face-now"),
                    Loc.GetString("cmu-lobby-clock-docked-now"));
                return;
            }

            var face = difference.TotalHours >= 1
                ? $"{Math.Floor(difference.TotalHours)}:{difference.Minutes:D2}:{difference.Seconds:D2}"
                : $"{difference.Minutes}:{difference.Seconds:D2}";

            SetClockFace(face, Loc.GetString("cmu-lobby-clock-docked", ("time", face)));
        }

        /// <summary>
        ///     Write the countdown to both forms of the clock. Urgency colour is the floating face
        ///     only; folded, the wording carries it instead.
        /// </summary>
        private void SetClockFace(string face, string docked)
        {
            Lobby!.StartTime.Text = face;

            // Guarded: Label.Text invalidates measure on every set, and this one sits in the action
            // column's stack rather than a LayoutContainer.
            var button = Lobby!.DockedClockButton;
            if (button.Text != docked)
                button.Text = docked;
        }

        private void OnClockMinimizePressed(BaseButton.ButtonEventArgs args)
        {
            _cfg.SetCVar(CCVars.CMULobbyClockMinimized, true);
        }

        private void OnClockRestorePressed(BaseButton.ButtonEventArgs args)
        {
            _cfg.SetCVar(CCVars.CMULobbyClockMinimized, false);
        }

        /// <summary>
        ///     Put the clock where the player left it, or in its default place the first time.
        /// </summary>
        /// <remarks>
        ///     Runs every frame but does its work once: a control has no size until the first layout
        ///     pass, and a fraction of the free space cannot be resolved before then. Retrying until
        ///     it takes is simpler than hooking whichever pass happens to be the one that gives the
        ///     panel a size.
        /// </remarks>
        private void PlaceRoundClock()
        {
            var clock = Lobby!.RoundClock;
            var layer = Lobby!.ClockLayer;

            // The layer has to span the window, and it does not do so on its own: a LayoutContainer
            // positions its children absolutely and therefore reports no desired size, which leaves
            // it zero-sized inside the root Control. Everything downstream then clamps into a
            // zero-by-zero box, which is how the clock ended up pinned to the top-left corner and
            // why it could not be dragged at all. Guarded so this is not a layout invalidation
            // every frame.
            if (layer.Size != Lobby!.Size)
                layer.SetSize = Lobby!.Size;

            var room = layer.Size - clock.Size;

            // Nothing to place into yet. A control has no size until the first layout pass, and the
            // panel behind it is still growing for a few frames after that.
            if (room.X <= 0f || room.Y <= 0f)
                return;

            var savedX = _cfg.GetCVar(CCVars.CMULobbyClockX);
            var savedY = _cfg.GetCVar(CCVars.CMULobbyClockY);

            if (savedX >= 0f && savedY >= 0f)
            {
                // A position the player chose. Apply it once and then leave it alone - re-applying
                // every frame would fight the drag that is setting it.
                if (_clockPlaced)
                    return;

                _clockPlaced = clock.TryPlaceAtFraction(new Vector2(savedX, savedY));
                return;
            }

            // Untouched, so keep it in its default place: horizontally in the gap between the
            // action column and the server-info screen, vertically near the top.
            //
            // Recomputed every frame rather than latched on the first one. The first frame with a
            // non-zero size is not the frame the layout has settled on, and latching there put the
            // clock up beside the action panel instead of in the gap. Recomputing also means the
            // default follows a window resize and the right-hand panel being collapsed.
            var left = CmuPanelMetrics.LobbyActionColumnWidth;

            // The right-hand screen's own edge, so the clock centres in the space actually left
            // rather than in a guess at it. Collapsed, there is no edge and the window is the limit.
            var right = Lobby!.RightSide.Visible
                ? Lobby!.RightSide.GlobalPosition.X
                : layer.Size.X;

            var x = Math.Clamp((left + right - clock.Size.X) / 2f, 0f, room.X);
            var y = Math.Clamp(CmuPanelMetrics.LobbyClockTopMargin, 0f, room.Y);

            LayoutContainer.SetPosition(clock, new Vector2(x, y));
        }

        private void OnRoundClockMoved(Vector2 fraction)
        {
            // Set before the cvars, not after: the moment a saved position exists, PlaceRoundClock
            // would start applying it, and applying a position mid-drag fights the drag.
            _clockPlaced = true;

            _cfg.SetCVar(CCVars.CMULobbyClockX, fraction.X);
            _cfg.SetCVar(CCVars.CMULobbyClockY, fraction.Y);
        }

        private void SetCountdownUrgency(CmuCountdownUrgency.Level urgency)
        {
            var styleClass = urgency switch
            {
                CmuCountdownUrgency.Level.Imminent => StyleNano.StyleClassCrtClockDanger,
                CmuCountdownUrgency.Level.Soon => StyleNano.StyleClassCrtClockWarning,
                _ => StyleNano.StyleClassCrtClock
            };

            var label = Lobby!.StartTime;
            if (label.HasStyleClass(styleClass))
                return;

            // Swap, never stack: all three rules set the same font and font colour, and two matching
            // rules of equal specificity have no defined winner.
            label.RemoveStyleClass(StyleNano.StyleClassCrtClock);
            label.RemoveStyleClass(StyleNano.StyleClassCrtClockWarning);
            label.RemoveStyleClass(StyleNano.StyleClassCrtClockDanger);
            label.AddStyleClass(styleClass);
        }

        private void LobbyStatusUpdated()
        {
            UpdateLobbyBackground();
            UpdateLobbyUi();
        }

        private void LobbyLateJoinStatusUpdated()
        {
            Lobby!.ReadyButton.Disabled = _gameTicker.DisallowedLateJoin;
        }

        private void UpdateLobbyUi()
        {
            Lobby!.CharacterPreview.PatronPerks.Visible = _linkAccount.CanViewPatronPerks();

            if (_gameTicker.IsGameStarted)
            {
                Lobby!.ObserveButton.Disabled = false;

                // RMC14/CMU: readying up is meaningless once the round is running, so the row swaps
                // to the single button that opens the faction choices rather than restyling Ready
                // into a join button.
                Lobby!.ReadyButton.Visible = false;
                Lobby!.JoinRoundButton.Visible = true;
            }
            else
            {
                Lobby!.StartTime.Text = string.Empty;
                Lobby!.ReadyButton.ToggleMode = true;
                Lobby!.ReadyButton.Disabled = false;
                Lobby!.ReadyButton.Pressed = _gameTicker.AreWeReady;

                // After Pressed, never before: the mark and the colour are read off it.
                UpdateReadyAppearance();
                Lobby!.ObserveButton.Disabled = true;

                // RMC14/CMU
                Lobby!.ReadyButton.Visible = true;
                Lobby!.JoinRoundButton.Visible = false;
                _joinRoundWindow?.Close();
            }

            if (_gameTicker.ServerInfoBlob != null)
            {
                Lobby!.ServerInfo.SetInfoBlob(_gameTicker.ServerInfoBlob);
            }

            Lobby!.ServerInfo.SetRoundInfo(_gameTicker.ServerRoundInfo);

            var minutesToday = _playtimeTracking.PlaytimeMinutesToday;
            if (minutesToday > 60)
            {
                Lobby!.PlaytimeComment.Visible = false; // RMC14

                var hoursToday = Math.Round(minutesToday / 60f, 1);

                var chosenString = minutesToday switch
                {
                    < 180 => "lobby-state-playtime-comment-normal",
                    < 360 => "lobby-state-playtime-comment-concerning",
                    < 720 => "lobby-state-playtime-comment-grasstouchless",
                    _ => "lobby-state-playtime-comment-selfdestructive"
                };

                Lobby.PlaytimeComment.SetMarkup(Loc.GetString(chosenString, ("hours", hoursToday)));
            }
            else
                Lobby!.PlaytimeComment.Visible = false;
        }

        private void UpdateLobbySoundtrackInfo(LobbySoundtrackChangedEvent ev)
        {
            if (ev.SoundtrackFilename == null)
            {
                Lobby!.LobbySong.SetMarkup(Loc.GetString("lobby-state-song-no-song-text"));
            }
            else if (
                ev.SoundtrackFilename != null
                && _resourceCache.TryGetResource<AudioResource>(ev.SoundtrackFilename, out var lobbySongResource)
                )
            {
                var lobbyStream = lobbySongResource.AudioStream;

                var title = string.IsNullOrEmpty(lobbyStream.Title)
                    ? Loc.GetString("lobby-state-song-unknown-title")
                    : lobbyStream.Title;

                var artist = string.IsNullOrEmpty(lobbyStream.Artist)
                    ? Loc.GetString("lobby-state-song-unknown-artist")
                    : lobbyStream.Artist;

                var markup = Loc.GetString("lobby-state-song-text",
                    ("songTitle", title),
                    ("songArtist", artist));

                Lobby!.LobbySong.SetMarkup(markup);
            }
        }

        private void UpdateLobbyBackground()
        {
            if (_protoMan.TryIndex(_gameTicker.LobbyBackground, out var proto))
            {
                Lobby!.Background.Texture = _resourceCache.GetResource<TextureResource>(proto.Background);

                var markup = Loc.GetString("lobby-state-background-text",
                    ("backgroundTitle", Loc.GetString(proto.Title)),
                    ("backgroundArtist", Loc.GetString(proto.Artist)));

                Lobby!.LobbyBackground.SetMarkup(markup);
            }
            else
            {
                Lobby!.Background.Texture = null;

                Lobby!.LobbyBackground.SetMarkup(Loc.GetString("lobby-state-background-no-background-text"));
            }
        }

        private void SetReady(bool newReady)
        {
            if (_gameTicker.IsGameStarted)
            {
                return;
            }

            _consoleHost.ExecuteCommand($"toggleready {newReady}");
        }

        private void OnJoinRoundPressed(BaseButton.ButtonEventArgs args)
        {
            if (_joinRoundWindow is { Disposed: false })
            {
                _joinRoundWindow.MoveToFront();
                return;
            }

            var window = new JoinRoundWindow();
            _joinRoundWindow = window;

            // Each choice is terminal - it opens a late-join or ghost-roles window on top - so close
            // this one first rather than leaving it stranded behind whatever the choice opened.
            window.JoinColonistsButton.OnPressed += args2 => { window.Close(); OnReadyPressed(args2); };
            window.JoinGovforButton.OnPressed += args2 => { window.Close(); OnJoinGovforPressed(args2); };
            window.JoinOpforButton.OnPressed += args2 => { window.Close(); OnJoinOpforPressed(args2); };
            window.JoinOtherButton.OnPressed += args2 => { window.Close(); OnJoinOtherPressed(args2); };
            window.OnClose += () =>
            {
                if (_joinRoundWindow == window)
                    _joinRoundWindow = null;
            };

            window.OpenCentered();
        }

        private void OnJoinGovforPressed(BaseButton.ButtonEventArgs args)
        {
            new LateJoinGui("govfor").OpenCentered();
        }

        private void OnJoinOpforPressed(BaseButton.ButtonEventArgs args)
        {
            new LateJoinGui("opfor").OpenCentered();
        }

        private void OnJoinOtherPressed(BaseButton.ButtonEventArgs args)
        {
             // Open the ghost roles UI (server-driven) to display all ghost roles
             _consoleHost.RemoteExecuteCommand(null, "ghostroles");
        }

        private void OnPrevCharPressed(BaseButton.ButtonEventArgs args)
        {
            if (_preferencesManager.Preferences == null || _preferencesManager.Settings == null)
                return;

            var characters = _preferencesManager.Preferences.Characters;
            var currentIndex = _preferencesManager.Preferences.SelectedCharacterIndex;

            // Find the previous occupied slot
            var sortedSlots = characters.Keys.OrderBy(k => k).ToList();
            if (sortedSlots.Count <= 1)
                return;

            var idx = sortedSlots.IndexOf(currentIndex);
            var prevIdx = idx <= 0 ? sortedSlots.Count - 1 : idx - 1;
            _preferencesManager.SelectCharacter(sortedSlots[prevIdx]);
            _userInterfaceManager.GetUIController<LobbyUIController>().ReloadCharacterSetup();
        }

        private void OnNextCharPressed(BaseButton.ButtonEventArgs args)
        {
            if (_preferencesManager.Preferences == null || _preferencesManager.Settings == null)
                return;

            var characters = _preferencesManager.Preferences.Characters;
            var currentIndex = _preferencesManager.Preferences.SelectedCharacterIndex;

            // Find the next occupied slot
            var sortedSlots = characters.Keys.OrderBy(k => k).ToList();
            if (sortedSlots.Count <= 1)
                return;

            var idx = sortedSlots.IndexOf(currentIndex);
            var nextIdx = idx >= sortedSlots.Count - 1 ? 0 : idx + 1;
            _preferencesManager.SelectCharacter(sortedSlots[nextIdx]);
            _userInterfaceManager.GetUIController<LobbyUIController>().ReloadCharacterSetup();
        }

        private void OnIgnoreAllegianceToggled(BaseButton.ButtonToggledEventArgs args)
        {
            SetIgnoreAllegiance(args.Pressed);
        }

        private void SetIgnoreAllegiance(bool ignoreAllegiance)
        {
            IgnoreAllegiance = ignoreAllegiance;
            var netManager = IoCManager.Resolve<Robust.Shared.Network.IClientNetManager>();
            var msg = new MsgIgnoreAllegiance
            {
                IgnoreAllegiance = ignoreAllegiance
            };
            netManager.ClientSendMessage(msg);
        }
    }
}
