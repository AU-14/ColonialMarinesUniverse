using System.Numerics;
using RmcDrawDepth = Content.Shared.DrawDepth.DrawDepth;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Charge.ChargerJockey;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoChargerJockeyComponent : Component
{
    /// <summary>
    /// Max small xenonids that can ride at once.
    /// </summary>
    [DataField, AutoNetworkedField] public int MaxRiders = 4;

    /// <summary>
    /// Fallback local position riders are snapped to when mounting.
    /// </summary>
    [DataField, AutoNetworkedField] public Vector2 RiderLocalPosition = Vector2.Zero;

    /// <summary>
    /// Local rider slots. If empty, every rider uses <see cref="RiderLocalPosition"/>.
    /// </summary>
    [DataField] public List<Vector2> RiderLocalPositions = new();

    /// <summary>
    /// Draw depth used for riders so they render on top of the crusher.
    /// </summary>
    [DataField, AutoNetworkedField] public int RiderDrawDepth = (int) RmcDrawDepth.OverMobs + 2;

    /// <summary>
    /// Render order used for riders so they sort above their crusher parent.
    /// </summary>
    [DataField, AutoNetworkedField] public int RiderRenderOrder = 100;

    /// <summary>
    /// Tracks current riders.
    /// </summary>
    [DataField, AutoNetworkedField] public HashSet<EntityUid> Riders = new();

    [DataField] public TimeSpan MountDoAfter = TimeSpan.FromSeconds(1.5f);
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class XenoChargerRidingComponent : Component
{
    [DataField, AutoNetworkedField] public EntityUid Charger;

    [DataField, AutoNetworkedField] public Vector2 LocalPosition;

    [DataField, AutoNetworkedField] public int RiderSlot = -1;

    [DataField, AutoNetworkedField] public int DrawDepth = (int) RmcDrawDepth.OverMobs + 2;
}
