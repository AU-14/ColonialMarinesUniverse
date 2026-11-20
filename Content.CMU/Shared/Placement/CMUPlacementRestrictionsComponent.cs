using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Placement;

/// <summary>
///     Keeps this entity from being deployed on or near anchored entities matching any of
///     <see cref="Restrictions"/>. Checked by <see cref="CMUPlacementSystem.CanPlacePopup"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUPlacementSystem))]
public sealed partial class CMUPlacementRestrictionsComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<CMUPlacementRestriction> Restrictions = new();
}
