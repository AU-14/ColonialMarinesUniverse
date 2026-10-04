using Content.Shared._CMU14.Language;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Language;

public sealed class FactionLanguageBoundUserInterface : BoundUserInterface
{
    private FactionLanguagePickerWindow? _window;

    public FactionLanguageBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        Logger.Debug("[FactionLanguage] BUI Open() called");
        CreateWindow();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || !_window.IsOpen)
            CreateWindow();

        if (state is FactionLanguagePickerState s)
            _window?.Populate(s.Languages, s.FactionTag);
    }

    private void CreateWindow()
    {
        _window?.Dispose();
        _window = new FactionLanguagePickerWindow();
        _window.OnLanguagePicked += OnPicked;
        _window.OpenCentered();
    }

    private void OnPicked(string language)
    {
        SendMessage(new FactionLanguagePickedMessage(language));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Dispose();
    }
}