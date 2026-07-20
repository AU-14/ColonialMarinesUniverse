using Content.Client.Mapping;
using Content.Client.Markers;
using Content.Client.SubFloor;
using Robust.Client.Graphics;
using Robust.Shared.Console;

namespace Content.Client.Commands;

[UsedImplicitly]
internal sealed partial class MappingClientSideSetupCommand : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private ILightManager _lightManager = default!;

    public override string Command => "mappingclientsidesetup";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_lightManager.LockConsoleAccess)
        {
            _entitySystemManager.GetEntitySystem<MarkerSystem>().MarkersVisible = true;
            _lightManager.Enabled = false;
            shell.ExecuteCommand("showsubfloor");
            IoCManager.Resolve<IStateManager>().RequestStateChange<MappingState>();
        }
    }
}

