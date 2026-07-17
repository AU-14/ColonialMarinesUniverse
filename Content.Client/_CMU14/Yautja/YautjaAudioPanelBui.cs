using Content.Shared._CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Yautja;

[UsedImplicitly]
public sealed class YautjaAudioPanelBui : BoundUserInterface
{
    private YautjaAudioPanelWindow? _window;

    public YautjaAudioPanelBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<YautjaAudioPanelWindow>();
        _window.OnEmote += emote => SendMessage(new YautjaAudioPanelEmoteMsg(emote));

        if (State is YautjaAudioPanelState state)
            _window.UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is YautjaAudioPanelState audioState)
            _window?.UpdateState(audioState);
    }
}
