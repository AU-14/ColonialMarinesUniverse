namespace Content.Server.CMU14.Fighter;

/// <summary>Tracks the cockpit that retains references to a part even while it is parented to the ground airframe.</summary>
[RegisterComponent]
public sealed partial class CMUFighterPartOwnerComponent : Component
{
    public EntityUid Aircraft;
}
