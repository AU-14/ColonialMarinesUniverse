using System.Linq;
using Content.Shared._RMC14.Overwatch;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.Ghost.Components;

namespace Content.Server.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUTacticalReconstructionSystem
{
    [Dependency] private SharedOverwatchConsoleSystem _overwatch = default!;

    private bool CanUseCamera(EntityUid source, EntityUid actor, out OverwatchConsoleComponent? console)
    {
        console = null;
        return CanUse(source, actor) && !HasComp<GhostComponent>(actor) &&
            _skills.HasSkill(actor, "RMCSkillOverwatch", 1) &&
            TryComp(source, out console) && _ui.IsUiOpen(source, UiKey(source), actor);
    }

    private void OnCamera(EntityUid source, ref CMUReconCameraMessage args)
    {
        if (!_surveys.TryGetValue((source, args.Actor), out var survey) || survey.Generation != args.Generation ||
            !_ui.IsUiOpen(source, UiKey(source), args.Actor))
            return;
        if (args.Target == null)
        {
            StopCamera(survey);
            return;
        }
        if (!IsCurrentSurvey(source, survey) || !CanUseCamera(source, args.Actor, out var console))
            return;
        // Recheck the current authorized overlay, never trust a client-supplied entity ID.
        var targetNet = args.Target.Value;
        var contact = Contacts(source, args.Actor, survey).Contacts.FirstOrDefault(c => c.CameraTarget == targetNet);
        if (contact.CameraTarget == null || !TryGetEntity(targetNet, out var target) ||
            !_overwatch.TryGetWatchCamera(console!, target.Value, out var camera))
            return;

        _overwatch.ToggleWatchFromConsole((source, console!), args.Actor, target.Value);
        if (!TryComp(args.Actor, out OverwatchWatchingComponent? watching) || watching.Watching != camera.Owner)
        {
            StopCamera(survey);
            return;
        }
        survey.CameraTarget = target;
        survey.Camera = camera;
        _ui.ServerSendUiMessage(source, UiKey(source),
            new CMUReconCameraViewMessage(survey.Generation, GetNetEntity(camera), contact.Name), args.Actor);
    }

    private void ValidateCamera(Survey survey, List<CMUReconContact> contacts)
    {
        if (survey.Camera is not { } camera) return;
        if (survey.CameraTarget is not { } target || TerminatingOrDeleted(target) || TerminatingOrDeleted(camera) ||
            !contacts.Any(c => c.CameraTarget == GetNetEntity(target)) ||
            !TryComp(survey.Actor, out OverwatchWatchingComponent? watching) || watching.Watching != camera ||
            !TryComp(survey.Source, out OverwatchConsoleComponent? console) ||
            !_overwatch.TryGetWatchCamera(console, target, out var current) || current.Owner != camera)
            StopCamera(survey);
    }

    private void StopCamera(Survey survey)
    {
        if (survey.Camera is not { } camera) return;
        survey.Camera = null;
        survey.CameraTarget = null;
        _overwatch.StopWatchingCamera(survey.Actor, camera);
        if (!TerminatingOrDeleted(survey.Source) && _ui.IsUiOpen(survey.Source, UiKey(survey.Source), survey.Actor))
            _ui.ServerSendUiMessage(survey.Source, UiKey(survey.Source),
                new CMUReconCameraViewMessage(survey.Generation, null), survey.Actor);
    }
}
