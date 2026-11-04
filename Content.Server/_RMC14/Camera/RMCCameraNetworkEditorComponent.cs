namespace Content.Server._RMC14.Camera;

[RegisterComponent, Access(typeof(RMCCameraSystem))]
public sealed partial class RMCCameraNetworkEditorComponent : Component
{
    public readonly HashSet<EntityUid> SeededNetworks = [];
    public readonly Dictionary<EntityUid, string> OwnedNetworks = [];
    public readonly Dictionary<EntityUid, string> Aliases = [];
    public readonly HashSet<EntityUid> HiddenSeededNetworks = [];
    public uint Revision;
}
