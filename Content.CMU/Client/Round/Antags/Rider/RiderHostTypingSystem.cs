using Content.Client.UserInterface.Systems.Chat;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.CMU14.Round.Antags.Rider;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Round.Antags.Rider;

// mirrors the rider's typing onto the host, but only for channels that actually come out of the host's mouth
public sealed class RiderHostTypingSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    // same timeout stock typing uses before it drops back to idle
    private static readonly TimeSpan TypingTimeout = TimeSpan.FromSeconds(2);

    private const ChatSelectChannel HostChannels =
        ChatSelectChannel.Local | ChatSelectChannel.Radio | ChatSelectChannel.Emotes;

    private TypingIndicatorState _sent = TypingIndicatorState.None;
    private string _lastText = string.Empty;
    private TimeSpan _lastChange;

    public override void FrameUpdate(float frameTime)
    {
        var state = ComputeState();
        if (state == _sent)
            return;

        _sent = state;
        RaiseNetworkEvent(new RiderHostTypingEvent(state));
    }

    private TypingIndicatorState ComputeState()
    {
        if (_player.LocalEntity is not { } local || !IsRiding(local))
            return TypingIndicatorState.None;

        if (!_cfg.GetCVar(CCVars.ChatShowTypingIndicator))
            return TypingIndicatorState.None;

        // walk up from the focused control, the centred-input mode moves the box out of the ChatBox tree
        ChatInputBox? box = null;
        for (var control = _ui.KeyboardFocused; control != null; control = control.Parent)
        {
            if (control is ChatInputBox found)
            {
                box = found;
                break;
            }
        }

        if (box == null || !box.Input.HasKeyboardFocus())
            return TypingIndicatorState.None;

        var text = box.Input.Text;
        if (text != _lastText)
        {
            _lastText = text;
            _lastChange = _timing.RealTime;
        }

        // a typed prefix wins over the selector, same as when the message is actually sent
        var (prefixed, _, _) = _ui.GetUIController<ChatUIController>().SplitInputContents(text.ToLower());
        var channel = prefixed == ChatSelectChannel.None
            ? box.ChannelSelector.SelectedChannel
            : prefixed;

        // whispers to the host stay private, so no tell for those
        if ((channel & HostChannels) == 0)
            return TypingIndicatorState.None;

        return text.Length > 0 && _timing.RealTime - _lastChange <= TypingTimeout
            ? TypingIndicatorState.Typing
            : TypingIndicatorState.Idle;
    }

    // client doesn't get RiderComponent.Host, but a hatchling tucked in a container or the phantom is close enough,
    // the server checks the real host anyway
    private bool IsRiding(EntityUid local)
    {
        if (HasComp<RiderManifestComponent>(local))
            return true;

        return HasComp<RiderComponent>(local) && _container.IsEntityInContainer(local);
    }
}
