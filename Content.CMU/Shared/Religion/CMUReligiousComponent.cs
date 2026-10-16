using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Religion;

/// <summary>
///     On a character who follows a religion, letting them pray to the admins.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUReligiousComponent : Component
{
    [DataField]
    public ProtoId<CMUReligionPrototype> Religion = CMUReligionPrototype.Atheist;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     When they can next pray.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextPrayer;

    [DataField]
    public int MaxLength = 400;
}
