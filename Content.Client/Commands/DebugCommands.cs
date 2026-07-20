using Content.Client.Markers;
using Content.Client.Popups;
using Content.Client.SubFloor;
using Robust.Shared.Console;

namespace Content.Client.Commands;

internal sealed partial class ShowMarkersCommand : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;

    public override string Command => "showmarkers";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _markerSystem.MarkersVisible ^= true;
    }
}

internal sealed partial class ShowSubFloor : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;

    public override string Command => "showsubfloor";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _subfloorSystem.ShowAll ^= true;
    }
}

internal sealed partial class NotifyCommand : LocalizedCommands
{
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;

    public override string Command => "notify";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _popupSystem.PopupCursor(args[0]);
    }
}
