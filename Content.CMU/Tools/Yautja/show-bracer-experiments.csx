// Paste this file's CONTENTS into scsi, then Ctrl+Enter. A file path is not a command.
// Do NOT use #load: this engine build crashes while highlighting loaded scripts.
// Close scsi and the console during the countdown.
using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.CMU14.Yautja;
using Content.Shared.Chat;
using Content.Shared.CMU14.Yautja;
{
    var viewerName = "CMU_client1"; // Username, without the localhost@ prefix. Use CMU_client2 for the second client.
    var delaySeconds = 15;
    var secondsPerEffect = 10;
    var repeat = false;
    var firstEffect = 1; // 1..20, useful when replaying a particular section.

    var key = "CMU.BracerShowcase." + viewerName;
    var session = res<IPlayerManager>().Sessions.SingleOrDefault(s =>
        s.Name.Equals(viewerName, StringComparison.OrdinalIgnoreCase) ||
        s.Name.EndsWith("@" + viewerName, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("No connected player named " + viewerName + ". Change viewerName at the top.");
    if (firstEffect < 1 || firstEffect > 20 || secondsPerEffect < CMUYautjaExperimentPrototype.EffectDuration + 1 || delaySeconds < 0)
        throw new InvalidOperationException("Use firstEffect 1..20, secondsPerEffect >= 9 and a nonnegative delay.");

    (AppDomain.CurrentDomain.GetData(key) as Action)?.Invoke();
    var previousBody = session.AttachedEntity;
    var prototypes = res<IPrototypeManager>();
    var recipes = prototypes.EnumeratePrototypes<CMUYautjaExperimentPrototype>().OrderBy(p => p.Visual).ToArray();
    var maps = ressys<SharedMapSystem>();
    var map = maps.CreateMap(out var mapId, runMapInit: true);
    maps.SetAmbientLight(mapId, Color.FromHex("#AEBCC5"));
    var grid = maps.CreateGridEntity(mapId);
    var floor = new Tile(res<ITileDefinitionManager>()["RMCFloorVehicleInteriorDarkSterile"].TileId);
    for (var x = -8; x <= 8; x++)
    for (var y = -6; y <= 6; y++)
        maps.SetTile(grid.Owner, grid.Comp, new Vector2i(x, y), floor);

    var viewer = ent.SpawnEntity("MobObserver", new EntityCoordinates(grid.Owner, 0, -1.5f));
    var bracer = ent.SpawnEntity("CMUYautjaBracer", new EntityCoordinates(grid.Owner, 0, 0));
    ressys<MindSystem>().ControlMob(session.UserId, viewer);
    if (previousBody == null)
        ressys<GameTicker>().PlayerJoinGame(session);
    ressys<SharedEyeSystem>().SetZoom(viewer, new Vector2(.8f));
    var experiments = ressys<CMUYautjaExperimentSystem>();
    var chat = ressys<ChatSystem>();
    var cancellation = new CancellationTokenSource();
    var stopped = false;
    Action stop = () =>
    {
        if (stopped)
            return;
        stopped = true;
        cancellation.Cancel();
        if (session.AttachedEntity == viewer)
        {
            if (previousBody is { } body && !ent.Deleted(body))
                ressys<MindSystem>().ControlMob(session.UserId, body);
            else
                ressys<GameTicker>().Respawn(session);
        }
        if (!ent.Deleted(map))
            ent.QueueDeleteEntity(map);
        AppDomain.CurrentDomain.SetData(key, null);
    };
    AppDomain.CurrentDomain.SetData(key, stop);

    void Say(string text) => chat.TrySendInGameICMessage(bracer, text, InGameICChatType.Speak,
        false, ignoreActionBlocker: true);
    var index = firstEffect - 1;
    Action next = null;
    next = () =>
    {
        if (stopped || ent.Deleted(bracer) || session.AttachedEntity != viewer)
        {
            stop();
            return;
        }
        if (index >= recipes.Length)
        {
            if (!repeat)
            {
                Say("All 20 experiments complete. Run the stop script to return, or rerun the showcase to replay.");
                return;
            }
            index = 0;
        }
        var recipe = recipes[index++];
        Say($"[{index:00}/{recipes.Length}] {Loc.GetString(recipe.Name)}. {Loc.GetString(recipe.Response)}");
        experiments.PlayExperiment(bracer, recipe.ID);
        Robust.Shared.Timing.Timer.Spawn(secondsPerEffect * 1000, next, cancellation.Token);
    };
    Robust.Shared.Timing.Timer.Spawn(delaySeconds * 1000, next, cancellation.Token);
    write($"Bracer showcase starts in {delaySeconds}s: {recipes.Length} effects, {secondsPerEffect}s each. Close scsi and the console. This uses the game's actual effect renderer, sounds and replies. Research rewards and unlocks are not awarded by the preview.");
}
