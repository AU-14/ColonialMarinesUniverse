using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.DroneOperator;

/// <summary>Pilot flames and firing effects attached to the original Thwompbot claw tips.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUFlamerDroneComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool PilotLit;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan FlameUntil;

    [DataField]
    public TimeSpan FlameDuration = TimeSpan.FromSeconds(0.6);

    [DataField]
    public EntProtoId ClawEffect = "CMUFlamerDroneClawEffect";

    /// <summary>Sprite-space offsets in metres, ordered south, north, east, west.</summary>
    [DataField]
    public List<Vector2> FirstClawOffsets = new()
    {
        new(-0.03125f, -0.28125f), new(-0.03125f, 0.34375f), new(0.125f, 0.375f), new(-0.15625f, 0.375f),
    };

    [DataField]
    public List<Vector2> SecondClawOffsets = new()
    {
        new(0.03125f, -0.28125f), new(0.03125f, 0.34375f), new(0.125f, 0.15625f), new(-0.15625f, 0.15625f),
    };
}
