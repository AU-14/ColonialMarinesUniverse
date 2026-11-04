using Content.Shared._RMC14.Announce;
using Content.Shared._RMC14.CCVar;
using Robust.Client.GameStates;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using System.Collections.Generic;

namespace Content.Client._RMC14.Announce;

public sealed partial class GeneralAnnounceSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClientGameStateManager _gameStates = default!;
    [Dependency] private IUserInterfaceManager _uiManager = default!;

    private AnnouncementDisplayPreference _preference;
    private Dictionary<string, AnnouncementDisplayPreference> _overrides = new();
    private bool _preferenceUpdatePending;

    public override void Initialize()
    {
        base.Initialize();

        _cfg.OnValueChanged(RMCCVars.RMCAnnouncementStyle, OnPreferenceChanged, true);
        _cfg.OnValueChanged(RMCCVars.RMCAnnouncementStyleOverrides, OnOverridesChanged, true);
        _gameStates.GameStateApplied += OnGameStateApplied;
        SubscribeNetworkEvent<AnnouncementNetMessage>(OnAnnouncementMessage);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _gameStates.GameStateApplied -= OnGameStateApplied;
    }

    private void OnAnnouncementMessage(AnnouncementNetMessage msg, EntitySessionEventArgs args)
    {
        if (_preference == AnnouncementDisplayPreference.Disabled)
            return;

        if (_uiManager.GetUIController<GeneralAnnounceUIController>() is { } controller)
        {
            controller.ShowAnnouncement(msg.Data);
        }
    }

    private void OnPreferenceChanged(AnnouncementDisplayPreference preference)
    {
        _preference = preference;
        _preferenceUpdatePending = true;
    }

    private void OnOverridesChanged(string serializedOverrides)
    {
        _overrides = AnnouncementPreferenceOverrides.Parse(serializedOverrides);
        _preferenceUpdatePending = true;
    }

    private void OnGameStateApplied(GameStateAppliedArgs args)
    {
        if (!_preferenceUpdatePending)
            return;

        _preferenceUpdatePending = false;
        RaiseNetworkEvent(new AnnouncementPreferenceNetMessage(_preference, new Dictionary<string, AnnouncementDisplayPreference>(_overrides)));
    }
}
