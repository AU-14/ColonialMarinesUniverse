using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared._RMC14.Xenonids.Eye;
using Robust.Shared.Map;
using System.Linq;

namespace Content.Server.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUTacticalReconstructionSystem
{
    private Enum UiKey(EntityUid source) => HasComp<CMUTacticalReconstructionComponent>(source) ? Key :
        HasComp<TacticalMapUserComponent>(source) ? TacticalMapUserUi.Key : TacticalMapComputerUi.Key;

    private void RegisterInterface<T>(Enum key) where T : Component
    {
        Subs.BuiEvents<T>(key, subs =>
        {
            subs.Event<CMUReconViewMessage>((Entity<T> e, ref CMUReconViewMessage m) => OnView(e.Owner, ref m));
            subs.Event<CMUReconLayerMessage>((Entity<T> e, ref CMUReconLayerMessage m) => OnLayer(e.Owner, ref m));
            subs.Event<CMUReconCameraMessage>((Entity<T> e, ref CMUReconCameraMessage m) => OnCamera(e.Owner, ref m));
            subs.Event<CMUReconQueenEyeMoveMessage>((Entity<T> e, ref CMUReconQueenEyeMoveMessage m) => OnQueenEyeMove(e.Owner, ref m));
            subs.Event<CMUReconXenoWatchMessage>((Entity<T> e, ref CMUReconXenoWatchMessage m) => OnXenoWatch(e.Owner, ref m));
            subs.Event<CMUReconOrderMessage>((Entity<T> e, ref CMUReconOrderMessage m) => OnOrder(e.Owner, ref m));
            subs.Event<CMUReconRouteMessage>((Entity<T> e, ref CMUReconRouteMessage m) => OnRoute(e.Owner, ref m));
            subs.Event<CMUReconSendMessage>((Entity<T> e, ref CMUReconSendMessage m) => OnSend(e.Owner, ref m));
            subs.Event<CMUReconCancelOrderMessage>((Entity<T> e, ref CMUReconCancelOrderMessage m) => OnCancelOrder(e.Owner, ref m));
            subs.Event<CMUReconClearOrdersMessage>((Entity<T> e, ref CMUReconClearOrdersMessage m) => OnClear(e.Owner, ref m));
        });
    }

    private void OnXenoWatch(EntityUid source, ref CMUReconXenoWatchMessage args)
    {
        var requestedTarget = args.Target;
        if (source != args.Actor || !HasComp<TacticalMapUserComponent>(source) ||
            !_ui.IsUiOpen(source, UiKey(source), args.Actor) || !CanUse(source, args.Actor) ||
            !_surveys.TryGetValue((source, args.Actor), out var survey) || survey.Generation != args.Generation ||
            !IsCurrentSurvey(source, survey) ||
            !Contacts(source, args.Actor, survey).Contacts.Any(c => c.XenoWatchTarget == requestedTarget) ||
            !TryGetEntity(requestedTarget, out var target))
            return;
        _xenoWatch.WatchFromTacticalMap(args.Actor, target.Value);
    }

    private void OnQueenEyeMove(EntityUid source, ref CMUReconQueenEyeMoveMessage args)
    {
        if (source != args.Actor || !HasComp<TacticalMapUserComponent>(source) ||
            !_ui.IsUiOpen(source, UiKey(source), args.Actor) || !CanUse(source, args.Actor) ||
            !_surveys.TryGetValue((source, args.Actor), out var survey) || survey.Generation != args.Generation ||
            !IsCurrentSurvey(source, survey) || !float.IsFinite(args.Position.X) || !float.IsFinite(args.Position.Y))
            return;
        var atlas = survey.Atlas;
        var level = (long) args.Depth - atlas.MinDepth;
        var local = args.Position - (System.Numerics.Vector2) atlas.Origin;
        if (level < 0 || level >= atlas.Maps.Length || atlas.Maps[(int) level] is not { } map || TerminatingOrDeleted(map) ||
            local.X < 0 || local.Y < 0 || local.X >= atlas.Width || local.Y >= atlas.Height)
            return;
        var coords = new MapCoordinates(args.Position, Transform(map).MapID);
        if (!_maps.TryFindGridAt(coords, out var grid, out _)) return;
        EntityManager.System<QueenEyeSystem>().TryTeleport(args.Actor, _transform.ToCoordinates(grid, coords));
    }

    public void CloseSurvey(EntityUid source, EntityUid actor)
    {
        if (_surveys.Remove((source, actor), out var survey))
            StopCamera(survey);
    }

    private static bool HasDrawingFaction(string? faction) => faction is
        SharedTacticalMapSystem.MarinesFaction or SharedTacticalMapSystem.XenosFaction or
        SharedTacticalMapSystem.GovforFaction or SharedTacticalMapSystem.OpforFaction or
        SharedTacticalMapSystem.ClfFaction or SharedTacticalMapSystem.WeYuFaction;

    private bool TrySource(EntityUid source, out EntityUid? map, out string faction)
    {
        if (TryComp<TacticalMapUserComponent>(source, out var user))
        {
            map = user.Map;
            faction = user.Govfor ? SharedTacticalMapSystem.GovforFaction :
                user.Opfor ? SharedTacticalMapSystem.OpforFaction :
                user.Clf ? SharedTacticalMapSystem.ClfFaction :
                user.WeYu ? SharedTacticalMapSystem.WeYuFaction :
                user.Xenos ? SharedTacticalMapSystem.XenosFaction : user.Marines ? SharedTacticalMapSystem.MarinesFaction : "";
            return true;
        }
        if (TryComp<TacticalMapComputerComponent>(source, out var computer))
        {
            map = computer.Map;
            faction = SharedTacticalMapSystem.NormalizeMapFaction(computer.Faction) ?? SharedTacticalMapSystem.MarinesFaction;
            if (!HasDrawingFaction(faction)) faction = "";
            return true;
        }
        map = null;
        faction = "";
        return false;
    }
}
