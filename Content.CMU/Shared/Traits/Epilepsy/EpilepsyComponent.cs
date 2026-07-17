using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Traits.Epilepsy;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EpilepsyComponent : Component
{
    [DataField]
    public TimeSpan IntervalMin = TimeSpan.FromMinutes(25);

    [DataField]
    public TimeSpan IntervalMax = TimeSpan.FromMinutes(30);

    [DataField]
    public float DurationMin = 30f;

    [DataField]
    public float DurationMax = 70f;

    /// <summary>
    ///     Visible radius (in tiles) while convulsing. Lower is darker.
    /// </summary>
    [DataField]
    public float VisionDimRadius = 2f;

    [DataField, AutoNetworkedField]
    public TimeSpan NextSeizure;
}
