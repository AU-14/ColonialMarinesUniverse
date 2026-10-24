using Content.Client._RMC14.Attachable.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Player;
using Robust.Shared.Console;

namespace Content.Client.CMU14.Attachable;

/// <summary>
///     Opens the attachment offset tool for the weapon in your active hand, or for an entity by id.
///     Client-side only: it changes nothing on the server.
/// </summary>
public sealed class CMUAttachmentOffsetCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;

    public string Command => "attachmentoffsets";
    public string Description => Loc.GetString("cmu-cmd-attachment-offsets-desc");
    public string Help => Loc.GetString("cmu-cmd-attachment-offsets-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        EntityUid target;
        if (args.Length >= 1)
        {
            if (!NetEntity.TryParse(args[0], out var net) || !_entities.TryGetEntity(net, out var parsed))
            {
                shell.WriteError(Loc.GetString("cmu-cmd-attachment-offsets-bad-entity", ("entity", args[0])));
                return;
            }

            target = parsed.Value;
        }
        else if (_player.LocalEntity is { } user &&
                 _entities.System<SharedHandsSystem>().GetActiveItem(user) is { } held)
        {
            target = held;
        }
        else
        {
            shell.WriteError(Loc.GetString("cmu-cmd-attachment-offsets-nothing-held"));
            return;
        }

        if (!_entities.HasComponent<AttachableHolderVisualsComponent>(target))
        {
            shell.WriteError(Loc.GetString("cmu-cmd-attachment-offsets-no-visuals"));
            return;
        }

        new CMUAttachmentOffsetWindow(target).OpenCentered();
    }
}
