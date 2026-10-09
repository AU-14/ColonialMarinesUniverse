using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Lab;

/// <summary>
/// A lab-made Wendigo created with MH-33, compelled to listen to the scientist who injected it.
/// Obedience is role-play; this only records and displays the bond.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUWendigoTamedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Master;

    /// <summary>
    /// Stored separately because the master can be deleted or change bodies.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string MasterName = string.Empty;
}
