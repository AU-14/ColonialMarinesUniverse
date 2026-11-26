using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Weapons.Melee;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSynthBatonComponent : Component
{
    /// <summary>A second shock during the visible jitter knocks the synthetic down.</summary>
    [DataField]
    public TimeSpan JitterDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(3);
}
