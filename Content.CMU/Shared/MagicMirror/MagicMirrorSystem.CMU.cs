// ReSharper disable CheckNamespace

using System.Linq;
using Content.Shared.Body;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Popups;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.MagicMirror;

/// <summary>
///     Scissors that can only give regulation haircuts: the regulation hair and facial hair styles, in the
///     regulation hair colours.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMURegulationMirrorComponent : Component;

// CMU14: regulation-only scissors.
public sealed partial class MagicMirrorSystem
{
    /// <summary>
    ///     False, with a popup, if regulation-only scissors were asked for a style that isn't regulation.
    /// </summary>
    private bool CMUAllowedCut(EntityUid mirror,
        EntityUid user,
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings)
    {
        if (!HasComp<CMURegulationMirrorComponent>(mirror))
            return true;

        foreach (var layers in markings.Values)
        {
            foreach (var (layer, list) in layers)
            {
                var allowed = layer switch
                {
                    HumanoidVisualLayers.Hair => HairStyles.RegulationHairStyles,
                    HumanoidVisualLayers.FacialHair => HairStyles.RegulationFacialHairStyles,
                    _ => null,
                };

                if (allowed == null)
                    continue;

                foreach (var marking in list)
                {
                    if (allowed.Contains(marking.MarkingId))
                        continue;

                    _popup.PopupClient(Loc.GetString("cmu-regulation-scissors-not-regulation"), user, user, PopupType.SmallCaution);
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Regulation-only scissors leave hair in the nearest regulation colour.
    /// </summary>
    private void CMUSnapToRegulationColors(EntityUid mirror,
        Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> markings)
    {
        if (!HasComp<CMURegulationMirrorComponent>(mirror))
            return;

        foreach (var layers in markings.Values)
        {
            foreach (var (layer, list) in layers)
            {
                if (layer is not (HumanoidVisualLayers.Hair or HumanoidVisualLayers.FacialHair))
                    continue;

                for (var i = 0; i < list.Count; i++)
                {
                    var marking = list[i];
                    for (var c = 0; c < marking.MarkingColors.Count; c++)
                    {
                        marking = marking.WithColorAt(c, NearestRegulationColor(marking.MarkingColors[c]));
                    }

                    list[i] = marking;
                }
            }
        }
    }

    private static Color NearestRegulationColor(Color color)
    {
        var best = HairStyles.RegulationHairColors[0].Color;
        var bestDistance = float.MaxValue;
        foreach (var (_, candidate) in HairStyles.RegulationHairColors)
        {
            var dr = candidate.R - color.R;
            var dg = candidate.G - color.G;
            var db = candidate.B - color.B;
            var distance = dr * dr + dg * dg + db * db;
            if (distance >= bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }
}
