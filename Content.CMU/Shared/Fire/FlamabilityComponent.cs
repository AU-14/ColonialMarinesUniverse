using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Fire;

/// <summary>
/// Marks an entity as participating in the AU14 fire spread system.
/// This is independent of the existing RMC FlammableComponent and
/// handles entity-to-entity fire propagation with per-entity tuning.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlamabilityComponent : Component
{
    /// <summary>
    /// Probability (0–1) that this entity catches fire when a burning neighbour
    /// is within spread range. Acted on each spread tick per neighbour.
    /// </summary>
    [DataField("chance"), AutoNetworkedField]
    public float Chance = 0.5f;

    /// <summary>
    /// Damage (in HP) dealt to this entity per second while it is on fire.
    /// Damage type is Heat.
    /// </summary>
    [DataField("rate"), AutoNetworkedField]
    public float Rate = 0.00008f;

    /// <summary>
    /// Multiplier on the base spread radius (default 3 tiles).
    /// A value of 2 means fire from this entity can reach 6 tiles away.
    /// </summary>
    [DataField("range"), AutoNetworkedField]
    public float Range = 1f;

    /// <summary>
    /// Multiplier on how quickly this entity tries to spread fire.
    /// Higher values cause more frequent spread attempts.
    /// A value of 2 means spread ticks twice as often.
    /// </summary>
    [DataField("spread"), AutoNetworkedField]
    public float Spread = 1f;

    /// <summary>Minimum time before fire burns out if the entity survives.</summary>
    [DataField]
    public TimeSpan BurnDurationMin = TimeSpan.FromSeconds(50);

    /// <summary>Maximum time before fire burns out if the entity survives.</summary>
    [DataField]
    public TimeSpan BurnDurationMax = TimeSpan.FromSeconds(340);

    /// <summary>
    /// Probability (0–1) that when this entity successfully ignites another entity, scattered
    /// tile fires are spawned in a radius around this entity.
    /// </summary>
    [DataField]
    public float ScatterFireChance = 0.06f;

    /// <summary>Radius in tiles within which scattered tile fires can be spawned.</summary>
    [DataField]
    public float ScatterFireRadius = 6f;

    /// <summary>Minimum number of tile fires to scatter when a scatter roll succeeds.</summary>
    [DataField]
    public int ScatterFireMinCount = 1;

    /// <summary>Maximum number of tile fires to scatter when a scatter roll succeeds.</summary>
    [DataField]
    public int ScatterFireMaxCount = 3;

    /// <summary>
    /// When true, the entity is deleted when the fire burns out by natural duration expiry.
    /// Manual extinguishing (pats, ExtinguishEvent) does not trigger deletion.
    /// </summary>
    [DataField]
    public bool DestroyAnyway;

    /// <summary>Number of melee pats required to extinguish this entity.</summary>
    [DataField]
    public int PatsToExtinguish = 2;

    /// <summary>Runtime: whether this entity is currently on fire.</summary>
    [AutoNetworkedField]
    public bool OnFire;

    /// <summary>Runtime: how many pats have been applied to the current fire.</summary>
    public int CurrentPats;

    /// <summary>Runtime: child entity used for the fire visual overlay (server-side only).</summary>
    public EntityUid? FireVisualEntity;

    /// <summary>Runtime: once burnt out, entity can no longer be ignited.</summary>
    public bool Burnt;

    /// <summary>Runtime: when this fire should burn out (set at ignition).</summary>
    public TimeSpan BurnEndTime;

    /// <summary>Runtime: game time of the next spread attempt.</summary>
    public TimeSpan NextSpreadTime;

    /// <summary>Runtime: game time of the next damage tick.</summary>
    public TimeSpan NextDamageTime;
}
