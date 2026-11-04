using Content.Client.Actions;
using Content.Client.Mapping;
using Content.Client.Markers;
using Content.Client.SubFloor;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Shared.Console;

namespace Content.Client.Commands;

internal sealed partial class MappingClientSideSetupCommand : LocalizedEntityCommands
{
    [Dependency] private ILightManager _lightManager = default!;
    [Dependency] private ActionsSystem _actionSystem = default!;
    [Dependency] private MarkerSystem _markerSystem = default!;
    [Dependency] private SubFloorHideSystem _subfloorSystem = default!;
    [Dependency] private IStateManager _stateManager = default!;

    public override string Command => "mappingclientsidesetup";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_lightManager.LockConsoleAccess)
            return;

        _markerSystem.MarkersVisible = true;
        _lightManager.Enabled = false;
        _subfloorSystem.ShowAll = true;
        _actionSystem.LoadActionAssignments("/mapping_actions.yml", false);
        _stateManager.RequestStateChange<MappingState>();
    }
}
