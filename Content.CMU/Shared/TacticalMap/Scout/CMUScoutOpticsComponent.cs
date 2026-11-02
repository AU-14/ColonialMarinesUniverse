namespace Content.Shared.CMU14.TacticalMap.Scout;

[RegisterComponent]
public sealed partial class CMUScoutOpticsComponent : Component
{
    [DataField]
    public bool RequiresScoutTraining;

    [ViewVariables]
    public EntityUid? InfantryAction;

    [ViewVariables]
    public EntityUid? VehicleAction;

    [ViewVariables]
    public EntityUid? EmplacementAction;

    [ViewVariables]
    public EntityUid? UnknownAction;
}
