using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.CMU14.Input;
using Content.Shared.CMU14.VoiceChat;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.VoiceChat;

/// <summary>
/// Opens the headset's voice radio window when the player shift-clicks a headset they are wearing or holding,
/// instead of examining it. Right-click → Examine still examines.
/// </summary>
public sealed partial class CMUVoiceRadioWindowSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private CMUVoiceChatManager _voice = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private CMUVoiceRadioWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUVoiceRadioComponent, AfterAutoHandleStateEvent>((_, _, _) => Refresh());
        Subs.CVar(_cfg, CCVars.CMUVoiceChatRadioMuted, _ => Refresh());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }

    /// <summary>
    /// Opens the window for <paramref name="item"/> if it is a headset on the local player and voice radio is on.
    /// </summary>
    /// <returns>True if the window opened, so the click shouldn't also examine.</returns>
    public bool TryOpen(EntityUid item)
    {
        if (!_cfg.GetCVar(CCVars.CMUVoiceChatEnabled) ||
            !_cfg.GetCVar(CCVars.CMUVoiceChatRadioEnabled) ||
            _player.LocalEntity is not { } player ||
            !HasComp<HeadsetComponent>(item) ||
            !HasComp<EncryptionKeyHolderComponent>(item) ||
            Transform(item).ParentUid != player ||
            !_container.IsEntityInContainer(item))
        {
            return false;
        }

        if (_window == null)
        {
            _window = new CMUVoiceRadioWindow();
            _window.TalkPressed += channel => RaiseNetworkEvent(new CMUVoiceRadioSelectEvent(channel.ID));
            _window.HearToggled += (channel, heard) => _voice.SetChannelMuted(channel.ID, !heard);
            _window.OnClose += () => _window = null;
        }

        Populate(item);
        _window.OpenCentered();
        _window.MoveToFront();
        return true;
    }

    private void Refresh()
    {
        if (_window is { IsOpen: true } && Exists(_window.Headset))
            Populate(_window.Headset);
    }

    private void Populate(EntityUid headset)
    {
        if (_window == null || !TryComp<EncryptionKeyHolderComponent>(headset, out var keys))
            return;

        // Only the worn headset transmits; a held one can still set which channels you hear.
        var player = _player.LocalEntity;
        var worn = player != null &&
                   TryComp<WearingHeadsetComponent>(player, out var wearing) &&
                   wearing.Headset == headset;
        var radioCommon = _cfg.GetCVar(CCVars.CMUVoiceChatRadioCommon);
        var muted = _voice.GetMutedChannels();

        bool CanTalk(ProtoId<RadioChannelPrototype> id)
        {
            return worn &&
                   !keys.ReadOnlyChannels.Contains(id) &&
                   (radioCommon || id != SharedChatSystem.CommonChannel);
        }

        // Same choice as the server: the picked channel if usable, else the headset default, else Common.
        ProtoId<RadioChannelPrototype>? talking = null;
        if (worn && player != null)
        {
            if (TryComp<CMUVoiceRadioComponent>(player, out var radio) &&
                radio.Channel is { } picked &&
                keys.Channels.Contains(picked) &&
                CanTalk(picked))
            {
                talking = picked;
            }
            else
            {
                var ev = new GetDefaultRadioChannelEvent();
                RaiseLocalEvent(player.Value, ev);
                ProtoId<RadioChannelPrototype> fallback = ev.Channel ?? SharedChatSystem.CommonChannel.Id;
                if (keys.Channels.Contains(fallback) && CanTalk(fallback))
                    talking = fallback;
            }
        }

        var rows = new List<CMUVoiceRadioWindow.ChannelRow>();
        foreach (var id in keys.Channels)
        {
            if (!_prototype.TryIndex(id, out var channel))
                continue;

            rows.Add(new CMUVoiceRadioWindow.ChannelRow(channel, CanTalk(id), id == talking, !muted.Contains(id)));
        }

        rows.Sort((a, b) => string.Compare(a.Channel.LocalizedName, b.Channel.LocalizedName, StringComparison.CurrentCultureIgnoreCase));
        _window.Populate(headset, Name(headset), rows, Key(CMUKeyFunctions.CMUVoiceRadioPushToTalk), Key(CMUKeyFunctions.CMUVoiceRadioCycleChannel));
    }

    private string Key(BoundKeyFunction function)
    {
        return _input.TryGetKeyBinding(function, out _)
            ? _input.GetKeyFunctionButtonString(function)
            : Loc.GetString("cmu-ui-options-voice-chat-key-unbound");
    }
}
