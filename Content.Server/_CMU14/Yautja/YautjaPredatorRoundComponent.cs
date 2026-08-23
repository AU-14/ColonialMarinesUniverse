using Content.Server.Maps;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Yautja;

[RegisterComponent, Access(typeof(YautjaPredatorRoundSystem))]
public sealed partial class YautjaPredatorRoundComponent : Component
{
    [DataField]
    public ProtoId<JobPrototype> PredatorJob = "CMUYautjaHunter";

    [DataField]
    public ProtoId<GameMapPrototype> HunterShipMap = "CMUYautjaHunterShip";

    [DataField]
    public int MinSlots = 2;

    [DataField]
    public int MaxSlots = 4;

    [DataField]
    public bool ModePredator = true;

    [DataField]
    public bool LoadHunterShip = true;

    [ViewVariables]
    public int Slots;

    /// <summary>
    /// Remaining round-start assignments reserved for Leader and Ancient ranks.
    /// These do not change the configured ordinary Hunter cap.
    /// </summary>
    [ViewVariables]
    public int RankBypassSlotsRemaining;

    /// <summary>
    /// Remaining expanded Hunter slots while round-start job assignment is in progress.
    /// </summary>
    [ViewVariables]
    public int RoundStartHunterSlotsRemaining;

    /// <summary>
    /// Round-start senior reservations still waiting for their assigned players to spawn.
    /// </summary>
    [ViewVariables]
    public int RoundStartBypassSlotsRemaining;

    [ViewVariables]
    public bool HunterShipLoaded;

    [ViewVariables]
    public HashSet<EntityUid> Youngbloods = new();
}
