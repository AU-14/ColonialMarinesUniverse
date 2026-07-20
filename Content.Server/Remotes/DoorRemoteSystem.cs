using Content.Shared.Remotes.EntitySystems;

namespace Content.Shared.Remotes
{
    public sealed partial class DoorRemoteSystem : SharedDoorRemoteSystem
    {
        [Dependency] private IAdminLogManager _adminLogger = default!;
        [Dependency] private AirlockSystem _airlock = default!;
        [Dependency] private DoorSystem _doorSystem = default!;
        [Dependency] private ExamineSystemShared _examine = default!;

public sealed class DoorRemoteSystem : SharedDoorRemoteSystem;
