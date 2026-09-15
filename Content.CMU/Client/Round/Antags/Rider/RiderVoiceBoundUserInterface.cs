using Content.Shared.CMU14.Round.Antags.Rider;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Round.Antags.Rider;

public sealed class RiderVoiceBoundUserInterface : BoundUserInterface
{
    private RiderVoiceWindow? _window;

    public RiderVoiceBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        _window = new RiderVoiceWindow();
        _window.OnVoiceSent += (text, speakThrough) =>
            SendMessage(new RiderVoiceMessage(text, speakThrough));
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
