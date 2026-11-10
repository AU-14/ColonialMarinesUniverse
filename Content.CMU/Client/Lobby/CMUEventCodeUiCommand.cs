using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client.CMU14.Lobby;

[AnyCommand]
public sealed partial class CMUEventCodeUiCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;

    public override string Command => "eventcodeui";

    public override void Execute(IConsoleShell shell, string argStr, string[] args) =>
        _entities.System<CMUEventAccessSystem>().OpenCodeWindow();
}
