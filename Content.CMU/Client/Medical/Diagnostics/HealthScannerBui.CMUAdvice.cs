using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Mobs;
using Content.Shared.Temperature;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Medical.Scanner;

/// <summary>
///     CMU14 medical advice: what to do about the readings the CMU medical scan adds (breathing, bleeding,
///     fractures, stumps, shrapnel, pain, temperature and overdoses).
/// </summary>
public sealed partial class HealthScannerBui
{
    private const float CMUHypothermiaCelsius = 35f;
    private const float CMUHyperthermiaCelsius = 39f;

    private void CMUMedicalAdvice(HealthScannerBuiState uiState, List<AdviceRecommendation> recommendations)
    {
        if (uiState.MobState == MobState.Dead)
            return;

        if (uiState.CMUAllergicReaction is { } allergen)
        {
            AddAdviceCandidate(recommendations, 94, allergen.Length > 0
                ? Loc.GetString("cmu-health-analyzer-advice-allergy", ("reagent", FormattedMessage.EscapeText(allergen)))
                : Loc.GetString("cmu-health-analyzer-advice-allergy-unknown"));
        }

        if (uiState.Advice.CMUShowInternals)
            AddAdviceCandidate(recommendations, 91, Loc.GetString("cmu-health-analyzer-advice-internals"));

        if (uiState.CMUStumps is { } stumps)
        {
            foreach (var stump in stumps)
            {
                if (stump.Clamped)
                    continue;

                AddAdviceCandidate(recommendations, 93, Loc.GetString("cmu-health-analyzer-advice-stump"));
                break;
            }
        }

        var worstBleed = ExternalBleedTier.None;
        var unsplinted = false;
        var shrapnel = false;
        if (uiState.CMUParts is { } parts)
        {
            foreach (var part in parts.Values)
            {
                if (!part.Tourniquet && part.ExternalBleeding > worstBleed)
                    worstBleed = part.ExternalBleeding;

                if (part.ShrapnelFragments > 0)
                    shrapnel = true;
            }

            if (uiState.CMUFractures is { } fractures)
            {
                foreach (var fracture in fractures)
                {
                    if (fracture.Suppressed ||
                        fracture.Part is not (BodyPartType.Arm or BodyPartType.Leg or BodyPartType.Hand or BodyPartType.Foot))
                    {
                        continue;
                    }

                    var held = false;
                    foreach (var part in parts.Values)
                    {
                        if (part.Type == fracture.Part && part.Symmetry == fracture.Symmetry)
                        {
                            held = part.Splinted || part.Cast;
                            break;
                        }
                    }

                    if (held)
                        continue;

                    unsplinted = true;
                    break;
                }
            }
        }

        if (worstBleed == ExternalBleedTier.Arterial)
            AddAdviceCandidate(recommendations, 89, Loc.GetString("cmu-health-analyzer-advice-arterial-bleed"));
        else if (worstBleed >= ExternalBleedTier.Moderate)
            AddAdviceCandidate(recommendations, 74, Loc.GetString("cmu-health-analyzer-advice-external-bleed"));

        if (uiState.CMUInternalBleeds is { Count: > 0 })
            AddAdviceCandidate(recommendations, 80, Loc.GetString("cmu-health-analyzer-advice-internal-bleed"));

        if (CMUOverdose(uiState) is { } overdose)
            AddAdviceCandidate(recommendations, 79, Loc.GetString("cmu-health-analyzer-advice-overdose", ("reagent", overdose)));

        if (!uiState.CMUPainShockSuppressed)
        {
            if (uiState.CMUPainShockRisk is CMUPainShockRisk.Imminent or CMUPainShockRisk.Active)
                AddAdviceCandidate(recommendations, 85, Loc.GetString("cmu-health-analyzer-advice-painkillers"));
            else if (uiState.CMUPainShockRisk == CMUPainShockRisk.High)
                AddAdviceCandidate(recommendations, 62, Loc.GetString("cmu-health-analyzer-advice-painkillers"));
        }

        if (uiState.Temperature is { } kelvin)
        {
            var celsius = TemperatureHelpers.KelvinToCelsius(kelvin);
            if (celsius < CMUHypothermiaCelsius)
                AddAdviceCandidate(recommendations, 70, Loc.GetString("cmu-health-analyzer-advice-hypothermia"));
            else if (celsius > CMUHyperthermiaCelsius)
                AddAdviceCandidate(recommendations, 70, Loc.GetString("cmu-health-analyzer-advice-hyperthermia"));
        }

        if (unsplinted)
            AddAdviceCandidate(recommendations, 65, Loc.GetString("cmu-health-analyzer-advice-splint"));

        if (shrapnel)
            AddAdviceCandidate(recommendations, 55, Loc.GetString("cmu-health-analyzer-advice-shrapnel"));
    }

    private string? CMUOverdose(HealthScannerBuiState uiState)
    {
        foreach (var chemical in uiState.KnownChemicals)
        {
            if (chemical.Overdose && _reagent.TryIndex(chemical.Prototype, out var reagent))
                return FormattedMessage.EscapeText(reagent.LocalizedName);
        }

        return null;
    }
}
