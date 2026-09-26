using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Dropship;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDropshipSystem))]
public sealed partial class DropshipTerminalComponent : Component
{
    /// <summary>
    ///     The faction this terminal belongs to. If set, only users of the same faction can use it.
    ///     On ship grids, this is auto-inherited from ShipFactionComponent on MapInit.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? Faction;

    // CMU14: Shipboard recall terminals may sit on a different deck from their home landing zones.
    [DataField, AutoNetworkedField]
    public bool UseShipDestinations;

    // CMU14 Begin: summon pacing
    /// <summary>Minimum time between remote summons from this terminal.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SummonCooldown = TimeSpan.FromSeconds(90);

    /// <summary>CurTime of the last successful summon; null when never used.</summary>
    public TimeSpan? LastSummonAt;
    // CMU14 End
}
