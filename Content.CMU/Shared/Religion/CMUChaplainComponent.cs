using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Religion;

/// <summary>
///     On a chaplain. Once their religion is known at spawn they get its holy book, in their chaplain kit if they're
///     holding one.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUChaplainComponent : Component
{
    [DataField]
    public EntProtoId Kit = "CMUKitAuxiliaryStaffChaplain";
}
