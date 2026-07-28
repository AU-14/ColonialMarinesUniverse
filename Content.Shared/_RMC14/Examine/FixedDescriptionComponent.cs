using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Examine;

/// <summary>
///     Overrides the entity's examine description for viewers that pass <see cref="Whitelist"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FixedDescriptionComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public LocId Description = string.Empty;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;
}
