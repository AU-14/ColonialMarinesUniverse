using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.BodyPart;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedHitLocationSystem))]
public sealed partial class HitLocationComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool UsePositionalRouting;

    /// <summary>
    ///     Aim-mode override consumed by the next incoming hit. Cleared after consumption.
    /// </summary>
    [DataField, AutoNetworkedField]
    public BodyPartType? NextHitOverride;
}
