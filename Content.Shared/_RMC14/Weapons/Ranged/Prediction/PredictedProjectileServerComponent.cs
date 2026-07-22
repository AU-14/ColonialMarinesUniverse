using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Projectiles;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Weapons.Ranged.Prediction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedGunPredictionSystem), typeof(SharedGunSystem), typeof(SharedProjectileSystem))]
public sealed partial class PredictedProjectileServerComponent : Component
{
    public ICommonSession? Shooter;

    [DataField, AutoNetworkedField]
    public int ClientId;

    [DataField, AutoNetworkedField]
    public EntityUid? ClientEnt;

    [DataField]
    public bool Hit;

    /// <summary>
    /// Prevents a stale or malicious client from making the server resend the
    /// same authority-takeover notification every tick. Once set, the correlation
    /// is retired and the authoritative projectile resumes normal collision.
    /// </summary>
    [DataField]
    public bool RejectionSent;

    /// <summary>
    /// Targets already processed by authoritative physics while a penetrating
    /// client copy was still approaching them. Late matching reports are consumed
    /// instead of being mistaken for rejected predictions.
    /// </summary>
    public HashSet<NetEntity> AuthoritativeHits = new();
}
