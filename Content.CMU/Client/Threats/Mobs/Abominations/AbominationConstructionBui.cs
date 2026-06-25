using Content.Shared._CMU14.Threats.Mobs.Abomination.Abilities;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CMU14.Threats.Mobs.Abominations;

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
        _window.OnStructurePicked += id => SendPredictedMessage(new AbominationConstructionChooseMessage(new(id)));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is AbominationConstructionBuiState s)
            _window?.Populate(s.Options, s.Selected);
    }
}
