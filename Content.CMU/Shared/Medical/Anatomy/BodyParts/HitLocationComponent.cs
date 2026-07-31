using Content.Shared.Body.Part;
using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Medical.Anatomy.BodyParts;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedHitLocationSystem))]
public sealed partial class HitLocationComponent : Component
{
    /// <summary>
    ///     Aim-mode override consumed by the next incoming hit. Cleared after consumption.
    /// </summary>
    [DataField, AutoNetworkedField]
    public BodyPartType? NextHitOverride;
}
