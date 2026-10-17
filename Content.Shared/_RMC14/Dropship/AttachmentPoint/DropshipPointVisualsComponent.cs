using Content.Shared._RMC14.Dropship.Utility.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility; // CMU14

namespace Content.Shared._RMC14.Dropship.AttachmentPoint;

// CMU14 class: replicate mount-specific equipment artwork.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDropshipSystem), typeof(DropshipUtilitySystem))]
public sealed partial class DropshipPointVisualsComponent : Component
{
    /// <summary>Mount-specific versions of installed equipment artwork.</summary>
    [DataField, AutoNetworkedField, Access(Other = AccessPermissions.ReadExecute)]
    public Dictionary<string, SpriteSpecifier.Rsi> SpriteOverrides = new();
}

[Serializable, NetSerializable]
public enum DropshipPointVisualsLayers
{
    AttachmentBase,
    AttachedUtility,
}
