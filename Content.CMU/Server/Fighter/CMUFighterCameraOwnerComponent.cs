namespace Content.Server.CMU14.Fighter;

/// <summary>Identifies the seat whose replicated camera reference must be cleared on deletion.</summary>
[RegisterComponent]
public sealed partial class CMUFighterCameraOwnerComponent : Component
{
    public EntityUid Seat;
}
