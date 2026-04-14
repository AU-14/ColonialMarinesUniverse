
using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Radio;

/// <summary>
///     Placed on an entity that has been granted radio access by an <see cref="AccessoryHeadsetComponent"/>.
///     Provides a default channel for the :h radio prefix.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AccessoryRadioWearerComponent : Component
{
    /// <summary>
    ///     The default radio channel for the :h prefix.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? DefaultChannel;
}

