using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Wounds;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUTourniquetItemComponent : Component
{
    [DataField]
    public TimeSpan ApplyDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan RemoveDelay = TimeSpan.FromSeconds(1.5);

    [DataField]
    public SoundSpecifier? ApplySound;

    [DataField]
    public SoundSpecifier? RemoveSound;

    [DataField]
    public bool ConsumedOnApply = true;

    [DataField]
    public EntProtoId? RefundOnRemove;
}
