using Robust.Shared.Prototypes;

namespace Content.Shared._AU14.Insurgency;

/// <summary>
///     "A Package": the after-spawn loadout delivery. Because the faction is chosen after everyone
///     has spawned, each member is handed one of these; using it unpacks that member's designated
///     role loadout. The contents are resolved and stored at grant time so the package is
///     self-contained once handed out.
///
///     Lives in Shared only so the client also registers it and can load the item prototype without
///     an unknown-component error. The behavior is server-side (APackageSystem).
/// </summary>
[RegisterComponent]
public sealed partial class APackageComponent : Component
{
    /// <summary>
    ///     Entities spawned into the holder's hands (or dropped at their feet) when the package is used.
    /// </summary>
    [DataField]
    public List<EntProtoId> Contents = new();
}
