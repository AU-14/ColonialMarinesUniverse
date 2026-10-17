using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Yautja;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCattleProdComponent : Component
{
    [DataField]
    public TimeSpan StunDuration = TimeSpan.FromSeconds(4);
}
