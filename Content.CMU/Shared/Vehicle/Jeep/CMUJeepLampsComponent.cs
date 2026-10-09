using System.Numerics;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// A jeep's lamps, each fitted on its own in an item slot this component adds, with a clickable part
/// over it: the headlights, the turn signals on the front fenders, and the tail lights, which carry
/// the brake lights and the rear turn signals. A screwdriver takes one off; one is put back by
/// clicking its place with it in hand.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUJeepLampsComponent : Component
{
    [DataField(required: true)]
    public List<CMUJeepLampData> Lamps = new();

    /// <summary>
    /// Whether the jeep comes with its lamps in; a kit chassis comes without.
    /// </summary>
    [DataField]
    public bool Fitted = true;

    /// <summary>
    /// Lamps too damaged to light, by part id.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<string> Broken = new();
}

[DataDefinition]
public sealed partial class CMUJeepLampData
{
    /// <summary>
    /// Part id, matching its sprite layer, click_* mask and *_outline states: headlight_left,
    /// turn_signal_right, taillight_left... Left is the driver's side.
    /// </summary>
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// The lamp's slot, added to the jeep as <see cref="SlotId"/>.
    /// </summary>
    [DataField(required: true)]
    public ItemSlot Slot = new();

    /// <summary>
    /// Where the lamp's click mask sits, in vehicle space facing south, so reach is measured to it.
    /// </summary>
    [DataField]
    public Vector2 Offset;

    /// <summary>
    /// Chance per hit on the jeep that this lamp takes some of it, and the share it takes.
    /// </summary>
    [DataField]
    public float HitChance = 0.3f;

    [DataField]
    public float DamageShare = 0.5f;

    public string SlotId => $"jeep-{Id.Replace('_', '-')}";
}
