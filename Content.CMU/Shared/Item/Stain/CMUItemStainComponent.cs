using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Item.Stain;

/// <summary>
/// Stores the single visual stain currently applied to an item.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CMUItemStainSystem))]
public sealed partial class CMUItemStainComponent : Component
{
    /// <summary>
    /// Whether this item accepts stains. Prototype opt-out equivalent to CMSS13's NOBLOODY flag.
    /// </summary>
    [DataField]
    public bool CanStain = true;

    /// <summary>
    /// Optional stain mask overrides keyed by inventory slot name.
    /// </summary>
    [DataField]
    [Access(typeof(CMUItemStainSystem), Other = AccessPermissions.ReadExecute)]
    public Dictionary<string, string> WornStates = new();

    [DataField, AutoNetworkedField]
    public CMUItemStainKind Kind = CMUItemStainKind.Blood;

    /// <summary>
    /// Null when the item is clean.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Color? Color;
}

[Serializable, NetSerializable]
public enum CMUItemStainKind : byte
{
    Blood,
    Oil,
}
