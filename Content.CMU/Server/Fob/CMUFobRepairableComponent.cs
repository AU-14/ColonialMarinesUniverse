using Content.Shared.DoAfter;

namespace Content.Server.CMU14.Fob;

/// <summary>One operator per physical fixture; the attempt serial also rejects delayed cancellation callbacks.</summary>
[RegisterComponent, Access(typeof(CMUFobInteractionSystem))]
public sealed partial class CMUFobRepairableComponent : Component
{
    [DataField] public TimeSpan TrainedDuration = TimeSpan.FromSeconds(30);
    [DataField] public TimeSpan UntrainedDuration = TimeSpan.FromSeconds(45);
    [ViewVariables] public int Attempt;
    [ViewVariables] public EntityUid? User;
    [ViewVariables] public EntityUid? Tool;
    [ViewVariables] public DoAfterId? DoAfter;
}
