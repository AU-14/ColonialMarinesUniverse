using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.GameObjects;
using Robust.Shared.Toolshed;

namespace Content.Server.Toolshed.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Debug)]
public sealed class PrototypeContainsCommand : ToolshedCommand
{
    [CommandImplementation]
    public IEnumerable<EntityUid> Prototyped(
        [PipedArgument] IEnumerable<EntityUid> input,
        [CommandArgument] string prototype,
        [CommandInverted] bool inverted
    )
        => input.Where(x => !Deleted(x) && ((MetaData(x).EntityPrototype?.ID.Contains(prototype) ?? false) ^ inverted));
}
