using System.Linq;
using Content.Client.Audio;
using Content.Client._RMC14.LinkAccount;
using Content.Client.GameTicking.Managers;
using Content.Client.LateJoin;
using Content.Client.Lobby.UI;
using Content.Client.Message;
using Content.Client.Playtime;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Voting;
using Content.Shared.CCVar;
using Content.Shared.AU14.Allegiance;
using Content.Shared.Preferences;
using Robust.Client;
using Robust.Client.Console;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Lobby
{
    public sealed partial class LobbyState : Robust.Client.State.State
    {
        [Dependency] private IBaseClient _baseClient = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private IClientConsoleHost _consoleHost = default!;
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IResourceCache _resourceCache = default!;
        [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private LinkAccountManager _linkAccount = default!;
        [Dependency] private IVoteManager _voteManager = default!;
        [Dependency] private ClientsidePlaytimeTrackingManager _playtimeTracking = default!;
        [Dependency] private IPrototypeManager _protoMan = default!;
        [Dependency] private IClientPreferencesManager _preferencesManager = default!;

        public bool IgnoreAllegiance { get; set; }

        private ClientGameTicker _gameTicker = default!;
        private ContentAudioSystem _contentAudioSystem = default!;
        private Button? _joinGovforButton;
        private Button? _joinOpforButton;
        private Button? _joinOtherButton;

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

            _joinGovforButton = Lobby.FindControl<Button>("JoinGovforButton");
            _joinGovforButton.OnPressed += OnJoinGovforPressed;
            _joinGovforButton.AddStyleClass("OpenRight");

            _joinOpforButton = Lobby.FindControl<Button>("JoinOpforButton");
            _joinOpforButton.OnPressed += OnJoinOpforPressed;
            _joinOpforButton.AddStyleClass("OpenRight");

            _joinOtherButton = Lobby.FindControl<Button>("JoinOtherButton");
            _joinOtherButton.OnPressed += OnJoinOtherPressed;
            _joinOtherButton.AddStyleClass("OpenRight");

            UpdateLobbyUi();

            Lobby.CharacterPreview.CharacterSetupButton.OnPressed += OnSetupPressed;
            Lobby.CharacterPreview.PatronPerks.OnPressed += OnPatronPerksPressed;
            Lobby.CharacterPreview.PrevCharacterButton.OnPressed += OnPrevCharPressed;
            Lobby.CharacterPreview.NextCharacterButton.OnPressed += OnNextCharPressed;
            Lobby.CharacterPreview.IgnoreAllegianceToggle.OnToggled += OnIgnoreAllegianceToggled;
            Lobby.CharacterPreview.PatronPerks.Visible = _linkAccount.CanViewPatronPerks();
            Lobby.ReadyButton.OnPressed += OnReadyPressed;
            Lobby.ReadyButton.OnToggled += OnReadyToggled;

            _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated += LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;
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
            _joinGovforButton!.OnPressed -= OnJoinGovforPressed;
            _joinOpforButton!.OnPressed -= OnJoinOpforPressed;
            _joinOtherButton!.OnPressed -= OnJoinOtherPressed;

            Lobby = null;
            _joinGovforButton = null;
            _joinOpforButton = null;
            _joinOtherButton = null;
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

        private void OnPatronPerksPressed(BaseButton.ButtonEventArgs args)
        {
            _userInterfaceManager.GetUIController<LinkAccountUIController>().TogglePatronPerksWindow();
        }

        private void OnPrevCharPressed(BaseButton.ButtonEventArgs args)
        {
            SelectAdjacentCharacter(-1);
        }

        private void OnNextCharPressed(BaseButton.ButtonEventArgs args)
        {
            SelectAdjacentCharacter(1);
        }

        private void SelectAdjacentCharacter(int offset)
        {
            var preferences = _preferencesManager.Preferences;
            if (preferences == null || _preferencesManager.Settings == null)
                return;

            var sortedSlots = preferences.Characters.Keys.OrderBy(slot => slot).ToList();
            if (sortedSlots.Count <= 1)
                return;

            var index = sortedSlots.IndexOf(preferences.SelectedCharacterIndex);
            index = index < 0 ? 0 : (index + offset + sortedSlots.Count) % sortedSlots.Count;
            _preferencesManager.SelectCharacter(sortedSlots[index]);
            _userInterfaceManager.GetUIController<LobbyUIController>().ReloadCharacterSetup();
        }

        private void OnIgnoreAllegianceToggled(BaseButton.ButtonToggledEventArgs args)
        {
            IgnoreAllegiance = args.Pressed;
            var netManager = IoCManager.Resolve<Robust.Shared.Network.IClientNetManager>();
            netManager.ClientSendMessage(new MsgIgnoreAllegiance
            {
                IgnoreAllegiance = args.Pressed,
            });
        }

        private void OnReadyPressed(BaseButton.ButtonEventArgs args)
        {
            if (!_gameTicker.IsGameStarted)
            {
                return;
            }

            new LateJoinGui("colonists").OpenCentered();
        }

        private void OnReadyToggled(BaseButton.ButtonToggledEventArgs args)
        {
            SetReady(args.Pressed);
        }

        public override void FrameUpdate(FrameEventArgs e)
        {
            if (_gameTicker.IsGameStarted)
            {
                Lobby!.StartTime.Text = string.Empty;
                var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-time", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));
                return;
            }

            Lobby!.StationTime.Text = Loc.GetString("lobby-state-player-status-round-not-started");
            string text;

            if (_gameTicker.Paused)
            {
                text = Loc.GetString("lobby-state-paused");
            }
            else if (_gameTicker.StartTime < _gameTiming.CurTime)
            {
                Lobby!.StartTime.Text = Loc.GetString("lobby-state-soon");
                return;
            }
            else
            {
                var difference = _gameTicker.StartTime - _gameTiming.CurTime;
                var seconds = difference.TotalSeconds;
                if (seconds < 0)
                {
                    text = Loc.GetString(seconds < -5 ? "lobby-state-right-now-question" : "lobby-state-right-now-confirmation");
                }
                else if (difference.TotalHours >= 1)
                {
                    text = $"{Math.Floor(difference.TotalHours)}:{difference.Minutes:D2}:{difference.Seconds:D2}";
                }
                else
                {
                    text = $"{difference.Minutes}:{difference.Seconds:D2}";
                }
            }

            Lobby!.StartTime.Text = Loc.GetString("lobby-state-round-start-countdown-text", ("timeLeft", text));
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
            if (_gameTicker.IsGameStarted)
            {
                Lobby!.ReadyButton.Text = Loc.GetString("lobby-state-ready-button-join-state");
                Lobby!.ReadyButton.ToggleMode = false;
                Lobby!.ReadyButton.Pressed = false;
                Lobby!.ObserveButton.Disabled = false;
                Lobby.ReadyButton.AddStyleClass("OpenLeft");
                _joinGovforButton!.Visible = true;
                _joinOpforButton!.Visible = true;
                _joinOtherButton!.Visible = true;
            }
            else
            {
                Lobby!.StartTime.Text = string.Empty;
                Lobby!.ReadyButton.Pressed = _gameTicker.AreWeReady;
                Lobby!.ReadyButton.Text = Loc.GetString(Lobby!.ReadyButton.Pressed ? "lobby-state-player-status-ready": "lobby-state-player-status-not-ready");
                Lobby!.ReadyButton.ToggleMode = true;
                Lobby!.ReadyButton.Disabled = false;
                Lobby!.ObserveButton.Disabled = true;
                Lobby.ReadyButton.RemoveStyleClass("OpenLeft");
                _joinGovforButton!.Visible = false;
                _joinOpforButton!.Visible = false;
                _joinOtherButton!.Visible = false;
            }

            if (_gameTicker.ServerInfoBlob != null)
            {
                Lobby!.ServerInfo.SetInfoBlob(_gameTicker.ServerInfoBlob);
            }

            var minutesToday = _playtimeTracking.PlaytimeMinutesToday;
            if (minutesToday > 60)
            {
                Lobby!.PlaytimeComment.Visible = true;

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
            _consoleHost.RemoteExecuteCommand(null, "ghostroles");
        }
    }
}
