using Robust.Shared.GameStates;

namespace Content.Shared.Access.Components;

public sealed partial class IdCardComponent
{
    [DataField, AutoNetworkedField]
    public EntityUid? OriginalOwner;
}
