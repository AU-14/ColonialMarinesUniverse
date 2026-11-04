using System.Numerics;

namespace Content.Client.CMU14.DroneOperator;

[RegisterComponent]
public sealed partial class CMUDroneTransferShakeComponent : Component
{
    public Vector2 OriginalOffset;
}
