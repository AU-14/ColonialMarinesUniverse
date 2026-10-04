using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Toolshed;

namespace Content.Server._RMC14.Toolshed.MappingCommands;

[ToolshedCommand, AdminCommand(AdminFlags.Query)]
internal sealed class MapRenameCommand : ToolshedCommand
{
    private MetaDataSystem? _metaData;

    [CommandImplementation]
    public void RenameEntities([PipedArgument] IEnumerable<EntityUid> input, string name)
    {
        _metaData ??= Sys<MetaDataSystem>();
        foreach (var entity in input)
        {
            _metaData.SetEntityName(entity, name);
        }
    }
}
