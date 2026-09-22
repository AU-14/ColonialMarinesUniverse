using Content.Shared.CMU14.Threats.Mobs.Biomorph.Abilities;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Threats.Mobs.Biomorphs;

[UsedImplicitly]
public sealed class BiomorphConstructionBui : BoundUserInterface
{
    [ViewVariables]
    private BiomorphConstructionWindow? _window;

    public BiomorphConstructionBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<BiomorphConstructionWindow>();
        _window.OnStructurePicked += id => SendPredictedMessage(new BiomorphConstructionChooseMessage(new(id)));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is BiomorphConstructionBuiState s)
            _window?.Populate(s.Options, s.Selected);
    }
}
