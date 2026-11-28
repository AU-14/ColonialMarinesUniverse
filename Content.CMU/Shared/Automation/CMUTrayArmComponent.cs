using Content.Shared.CMU14.Botany;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Automation;

/// <summary>
/// CMU14 (ARCH-03): an arm that works the 3x3 block of hydroponics trays on its left or right (ECS-03).
/// The reach fields (ECS-04) let the client animate the arm reaching the tray it worked; the work itself is instant. A harvester harvests the ripe plant (clearing a dead
/// one) and keeps the produce in its produce slots for an inserter to take; a planter plants the first seed packet from its seed
/// slots that passes its gene filter. Trays that farm themselves (the automatic tray) are left alone. Server-driven.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUTrayArmComponent : Component
{
    [DataField]
    public CMUTrayArmMode Mode = CMUTrayArmMode.Harvest;

    [DataField]
    public bool Enabled = true;

    /// <summary>Time between two acts. An act harvests or plants at most once.</summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextAct;

    /// <summary>The genes a planter accepts (COUPLING-02: the arm owns the gene-aware filter, not AutoPorts).</summary>
    [DataField]
    public CMUSeedFilter Filter = new();

    /// <summary>The item slots a planter takes seed packets from, in order.</summary>
    [DataField]
    public List<string> SeedSlots = new();

    /// <summary>The item slots a harvester keeps its produce in, for an inserter (or a hand) to take.</summary>
    [DataField]
    public List<string> ProduceSlots = new();

    /// <summary>ECS-04: the tile the arm last worked, relative to its own tile in grid space.</summary>
    [DataField, AutoNetworkedField]
    public Vector2i ReachTarget;

    /// <summary>ECS-04: when the arm last reached for a tray; the client plays the reach from here.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ReachStartedAt;

    /// <summary>ECS-04: how long the reach out and back takes; kept below <see cref="Interval"/>.</summary>
    [DataField, AutoNetworkedField]
    public float ReachDuration = 1.6f;

    /// <summary>ECS-04: what the claw shows on the way: the seed packet going out, or the produce coming back.</summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? ReachCarries;

    /// <summary>ECS-04: true when the claw carries <see cref="ReachCarries"/> out to the tray (planting), false when it brings it back.</summary>
    [DataField, AutoNetworkedField]
    public bool ReachCarriesOut;
}

public enum CMUTrayArmMode : byte
{
    Harvest,
    Plant,
}
