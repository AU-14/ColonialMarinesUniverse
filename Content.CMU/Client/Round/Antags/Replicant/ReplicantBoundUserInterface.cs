using Content.Shared.CMU14.Round.Antags.Replicant;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Round.Antags.Replicant;

public sealed class ReplicantBoundUserInterface : BoundUserInterface
{
    private ReplicantPickerWindow? _window;

    public ReplicantBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        CreateWindow();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || !_window.IsOpen)
            CreateWindow();

        if (state is ReplicantPickerState s)
            _window?.Populate(s.Targets);
    }

    private void CreateWindow()
    {
        CloseWindow();
        _window = new ReplicantPickerWindow();
        _window.OnTargetPicked += OnPicked;
        _window.OpenCentered();
    }

    private void OnPicked(NetEntity target)
    {
        SendMessage(new ReplicantPickedMessage(target));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        CloseWindow();
    }

    private void CloseWindow()
    {
        if (_window == null)
            return;

        _window.OnTargetPicked -= OnPicked;
        _window.Close();
        _window = null;
    }
}
