using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Threats.Mobs.WorkingJoe.Hijack;

/// <summary>
/// A Working Joe (or other synthetic) hijacked for the Grand Machine by a Working Joes shipment. It still looks and
/// reads like a colony Joe. The component is the overlay record: exactly what the hijack added, so a reset key can
/// take back that and nothing else. Spawned shipment bodies list their kit here in YAML; a conversion fills it in.
/// Written only by CMUHijackedJoeSystem (server; the attribute cannot name a server type from Shared). Not
/// networked, so clients cannot tell a hijacked Joe from a colony Joe.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUHijackedJoeComponent : Component
{
    /// <summary>Component names the hijack added and the cure removes.</summary>
    [DataField]
    public HashSet<string> AddedComponents = new();

    /// <summary>Actions granted when the kit is applied (map init for spawned bodies, conversion otherwise).</summary>
    [DataField]
    public List<EntProtoId> GrantActions = new();

    /// <summary>The action entities granted from <see cref="GrantActions"/>; the cure removes them.</summary>
    [DataField]
    public List<EntityUid> AddedActions = new();

    /// <summary>Whether the hijack granted the Grand Machine radio channel, and so must revoke it.</summary>
    [DataField]
    public bool RadioGranted;

    /// <summary>Whether the actions and radio were applied already; the kit is applied once.</summary>
    [DataField]
    public bool KitGranted;

    /// <summary>When the body was converted; zero for spawned shipment bodies.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan ConvertedAt;

    /// <summary>
    /// A reset key used within this time of the conversion does not cure it, so one zap cannot both land in the
    /// same moment as the revival and undo it.
    /// </summary>
    [DataField]
    public TimeSpan ReconversionCooldown = TimeSpan.FromSeconds(2);
}
