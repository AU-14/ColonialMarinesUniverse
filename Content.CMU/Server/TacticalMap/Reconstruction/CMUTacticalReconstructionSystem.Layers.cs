using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.Ghost.Components;

namespace Content.Server.CMU14.TacticalMap.Reconstruction;

public sealed partial class CMUTacticalReconstructionSystem
{
    private CMUReconLayer AvailableLayers(Survey survey)
    {
        var layers = CMUReconLayer.Combined;
        if (!TryComp<TacticalMapUserComponent>(survey.Source, out var user)) return layers;
        if (HasComp<GhostComponent>(survey.Actor))
        {
            if (user.Marines) layers |= CMUReconLayer.Marines;
            if (user.Govfor) layers |= CMUReconLayer.Govfor;
            if (user.Opfor) layers |= CMUReconLayer.Opfor;
            if (user.Xenos) layers |= CMUReconLayer.Xenos;
            if (user.Clf) layers |= CMUReconLayer.Clf;
            if (user.WeYu) layers |= CMUReconLayer.WeYu;
            if (user.Abomination) layers |= CMUReconLayer.Abomination;
            if (user.Yautja) layers |= CMUReconLayer.Yautja;
        }
        else if (!CanOrder(survey.Source, survey.Actor) &&
                 EntityManager.System<TacticalMapSystem>().ReconstructionViewerSquad(survey.Source) != null)
            layers |= CMUReconLayer.Platoon | CMUReconLayer.Squad;
        return layers;
    }

    private static bool AllowedLayer(CMUReconLayer available, CMUReconLayer selected)
    {
        var value = (int) selected;
        return value > 0 && (value & (value - 1)) == 0 && (available & selected) != 0;
    }

    private CMUReconLayer RefreshLayer(Survey survey)
    {
        var available = AvailableLayers(survey);
        if (!AllowedLayer(available, survey.Layer)) survey.Layer = CMUReconLayer.Combined;
        return available;
    }

    private static bool IncludesLayer(Survey survey, CMUReconLayer layer) =>
        survey.Layer == CMUReconLayer.Combined || survey.Layer == layer ||
        survey.Layer == CMUReconLayer.Platoon && layer != CMUReconLayer.Squad;

    private void OnLayer(EntityUid source, ref CMUReconLayerMessage args)
    {
        if (!CanUse(source, args.Actor) || !_ui.IsUiOpen(source, UiKey(source), args.Actor) ||
            !_surveys.TryGetValue((source, args.Actor), out var survey) || survey.Generation != args.Generation ||
            !IsCurrentSurvey(source, survey) || !AllowedLayer(AvailableLayers(survey), args.Layer)) return;
        // Only the overlay subscription changes. Keep the atlas, camera and chunk transfer intact.
        survey.Layer = args.Layer;
    }
}
