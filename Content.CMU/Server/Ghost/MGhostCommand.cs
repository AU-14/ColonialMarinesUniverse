using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared.Administration;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._CMU14.Ghost;

[AdminCommand(AdminFlags.MentorHelp)]
public sealed partial class MGhostCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entities = default!;

    public override string Command => "mghost";
    public override string Description => "Makes you a Mentor Ghost.";
    public override string Help => "mghost";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 0)
        {
            shell.WriteError(LocalizationManager.GetString("shell-wrong-arguments-number"));
            return;
        }

        ICommonSession? player = shell.Player;
        if (player == null)
        {
            shell.WriteError(LocalizationManager.GetString("ghost-command-no-session"));
            return;
        }

        var gameTicker = _entities.System<GameTicker>();
        if (!gameTicker.PlayerGameStatuses.TryGetValue(player.UserId, out PlayerGameStatus playerStatus)
            || playerStatus is not PlayerGameStatus.JoinedGame)
        {
            shell.WriteError(LocalizationManager.GetString("ghost-command-error-lobby"));
            return;
        }

        if (player.AttachedEntity is { Valid: true } frozen
            && _entities.HasComponent<AdminFrozenComponent>(frozen))
        {
            string deniedMessage = LocalizationManager.GetString("ghost-command-denied");
            shell.WriteError(deniedMessage);
            _entities.System<PopupSystem>()
                .PopupEntity(deniedMessage, frozen, frozen);
            return;
        }

        var mindSystem = _entities.System<SharedMindSystem>();
        var metaDataSystem = _entities.System<MetaDataSystem>();
        var ghostSystem = _entities.System<SharedGhostSystem>();
        var transformSystem = _entities.System<TransformSystem>();

        if (!mindSystem.TryGetMind(player, out EntityUid mindId, out MindComponent? mind))
        {
            shell.WriteError(LocalizationManager.GetString("aghost-no-mind-self"));
            return;
        }

        if (mind.VisitingEntity != default(EntityUid?))
        {
            mindSystem.UnVisit(mindId, mind);
            return;
        }

        bool canReturn = mind.CurrentEntity != null
            && !_entities.HasComponent<GhostComponent>(mind.CurrentEntity);
        EntityCoordinates coordinates = player.AttachedEntity != null
            ? _entities.GetComponent<TransformComponent>(player.AttachedEntity.Value).Coordinates
            : gameTicker.GetObserverSpawnPoint();

        EntityUid ghost = _entities.SpawnEntity(GameTicker.MentorObserverPrototypeName, coordinates);
        transformSystem.AttachToGridOrMap(ghost, _entities.GetComponent<TransformComponent>(ghost));

        if (canReturn)
        {
            if (!string.IsNullOrWhiteSpace(mind.CharacterName))
                metaDataSystem.SetEntityName(ghost, mind.CharacterName);
            else if (!string.IsNullOrWhiteSpace(player.Name))
                metaDataSystem.SetEntityName(ghost, player.Name);

            mindSystem.Visit(mindId, ghost, mind);
        }
        else
        {
            metaDataSystem.SetEntityName(ghost, player.Name);
            mindSystem.TransferTo(mindId, ghost, mind: mind);
        }

        var comp = _entities.GetComponent<GhostComponent>(ghost);
        ghostSystem.SetCanReturnToBody((ghost, comp), canReturn);
    }
}
