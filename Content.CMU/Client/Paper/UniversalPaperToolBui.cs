using Content.Shared._CMU14.Paper;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Paper;

[UsedImplicitly]
public sealed class UniversalPaperToolBui : BoundUserInterface
{
    private UniversalPaperToolWindow? _window;

    public UniversalPaperToolBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<UniversalPaperToolWindow>();
        _window.OnPrint += prototype => SendMessage(new UniversalPaperToolPrintMessage(prototype));

        if (State is UniversalPaperToolBuiState state)
            _window.Populate(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is UniversalPaperToolBuiState paperTool)
            _window?.Populate(paperTool);
    }
}
