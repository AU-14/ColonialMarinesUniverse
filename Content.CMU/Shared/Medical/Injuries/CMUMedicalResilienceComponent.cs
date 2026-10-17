using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Medical.Injuries;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUMedicalResilienceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float PainAccumulationMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public FractureSeverity MinimumPenalizingFractureSeverity = FractureSeverity.Hairline;

    [DataField, AutoNetworkedField]
    public float MovementPenaltyFloor = 0.2f;

    [DataField, AutoNetworkedField]
    public float AimPenaltyCeiling = 2.5f;

    [DataField, AutoNetworkedField]
    public float ActionSpeedPenaltyCeiling = 3f;
}
