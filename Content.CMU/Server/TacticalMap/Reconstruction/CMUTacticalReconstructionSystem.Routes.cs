using System.Linq;
using Content.Shared.CMU14.TacticalMap.Reconstruction;

namespace Content.Server.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUTacticalReconstructionSystem
{
    private void OnSend(EntityUid ent, ref CMUReconSendMessage args)
    {
        var accepted = false;
        CMUReconOrder[] result = [];
        if (TryOrderSurvey(ent, args.Actor, args.Generation, out var survey) && args.RequestId > 0 &&
            args.Additions is { Length: <= CMUReconRoutes.MaxAnnotations } &&
            args.Removals != null && args.Removals.Length <= Math.Max(CMUReconRoutes.MaxAnnotations,
                EntityManager.System<Content.Server._RMC14.TacticalMap.TacticalMapSystem>().LineLimit + 256))
        {
            var atlas = survey.Atlas;
            var valid = true;
            foreach (var annotation in args.Additions)
                valid &= CMUReconRoutes.ValidAnnotation(annotation, atlas.Origin, atlas.Width, atlas.Height, atlas.MinDepth, atlas.Maps.Length);
            if (valid)
            {
                var orders = Orders(survey);
                foreach (var id in args.Removals) orders.RemoveAll(o => o.Id == id);
                foreach (var annotation in args.Additions)
                {
                    if (orders.Count >= CMUReconRoutes.MaxAnnotations) orders.RemoveAt(0);
                    var point = annotation.Points[^1];
                    orders.Add(new CMUReconOrder(++_orderId, new Vector2i((int) MathF.Floor(point.X), (int) MathF.Floor(point.Y)),
                        annotation.Depth, annotation.Text == null ? CMUReconOrderKind.Route : CMUReconOrderKind.Text,
                        annotation.Points.ToArray(), annotation.Ink, annotation.Width, annotation.Text?.Trim()));
                }
                survey.NextOrder = _timing.CurTime + TimeSpan.FromSeconds(0.5);
                PublishCanvas(survey);
                accepted = true;
                result = orders.ToArray();
            }
        }
        if (_ui.IsUiOpen(ent, UiKey(ent), args.Actor))
            _ui.ServerSendUiMessage(ent, UiKey(ent), new CMUReconSentMessage(args.RequestId, accepted, result), args.Actor);
    }

    private bool TryOrderSurvey(EntityUid console, EntityUid actor, int generation, out Survey survey)
    {
        survey = default!;
        return _ui.IsUiOpen(console, UiKey(console), actor) && CanOrder(console, actor) &&
            _surveys.TryGetValue((console, actor), out survey!) && IsCurrentSurvey(console, survey) &&
            survey.Generation == generation && _timing.CurTime >= survey.NextOrder;
    }

    private void OnRoute(EntityUid ent, ref CMUReconRouteMessage args)
    {
        if (!TryOrderSurvey(ent, args.Actor, args.Generation, out var survey))
        {
            Feedback(ent, args.Actor, "cmu-recon-order-invalid");
            return;
        }
        var level = (long) args.Depth - survey.Atlas.MinDepth;
        if (level < 0 || level >= survey.Atlas.Maps.Length ||
            !CMUReconRoutes.ValidStroke(args.Waypoints, survey.Atlas.Origin, survey.Atlas.Width, survey.Atlas.Height, args.Ink))
        {
            Feedback(ent, args.Actor, "cmu-recon-route-invalid");
            return;
        }
        var orders = Orders(survey);
        if (orders.Count >= 32) orders.RemoveAt(0);
        var end = args.Waypoints[^1];
        orders.Add(new CMUReconOrder(++_orderId, new Vector2i((int) MathF.Floor(end.X), (int) MathF.Floor(end.Y)),
            args.Depth, CMUReconOrderKind.Route, args.Waypoints.ToArray(), args.Ink));
        PublishCanvas(survey);
        Feedback(ent, args.Actor, "cmu-recon-order-sent");
    }

    private void OnCancelOrder(EntityUid ent, ref CMUReconCancelOrderMessage args)
    {
        if (!TryOrderSurvey(ent, args.Actor, args.Generation, out var survey))
        {
            Feedback(ent, args.Actor, "cmu-recon-order-invalid");
            return;
        }
        var id = args.Id;
        if (Orders(survey).RemoveAll(o => o.Id == id) > 0)
        {
            PublishCanvas(survey);
            Feedback(ent, args.Actor, "cmu-recon-order-cancelled");
        }
    }
}
