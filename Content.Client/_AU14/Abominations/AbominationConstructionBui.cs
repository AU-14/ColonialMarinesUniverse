using Content.Shared._AU14.Abominations.Abilities;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.Abominations;

[UsedImplicitly]
public sealed class AbominationConstructionBui : BoundUserInterface
{
    [ViewVariables]
    private AbominationConstructionWindow? _window;

    public AbominationConstructionBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<AbominationConstructionWindow>();
        _window.OnStructurePicked += id => SendPredictedMessage(new AbominationConstructionChooseMessage(new EntProtoId(id)));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is AbominationConstructionBuiState s)
            _window?.Populate(s.Options, s.Selected);
    }
}
