using System.Globalization;
using System.Numerics;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Body.Part;
using Content.Shared.Temperature;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._RMC14.Medical.Scanner;

public sealed partial class HealthScannerBui
{
    private static bool BodyPartHasScannerDamage(HealthScannerBuiState uiState, CMUBodyPartReadout part)
    {
        if (part.Current < part.Max)
            return true;
        if (part.WoundDescriptor != null || part.ShrapnelFragments > 0 || part.Eschar)
            return true;
        if (PartHasFractureReadout(uiState, part.Type, part.Symmetry))
            return true;
        if (PartHasInternalBleedReadout(uiState, part.Type, part.Symmetry))
            return true;

        return false;
    }

    private static bool PartHasFractureReadout(
        HealthScannerBuiState uiState,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        if (uiState.CMUFractures is not { Count: > 0 } fractures)
            return false;

        foreach (var fracture in fractures)
        {
            if (fracture.Part == type && fracture.Symmetry == symmetry)
                return true;
        }

        return false;
    }

    private static bool PartHasInternalBleedReadout(
        HealthScannerBuiState uiState,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        if (uiState.CMUInternalBleeds is not { Count: > 0 } bleeds)
            return false;

        foreach (var bleed in bleeds)
        {
            if (bleed.ExactLocationKnown)
            {
                if (bleed.Part == type && bleed.Symmetry == symmetry)
                    return true;

                continue;
            }

            if (type == BodyPartType.Torso)
                return true;
        }

        return false;
    }

    private static float? LineGraftRecoverableFraction(CMUBodyPartReadout part)
    {
        var max = Math.Max(1f, part.Max.Float());
        var current = Math.Clamp(part.Current.Float(), 0f, max);
        if (current >= max)
            return null;

        var cap = current + (max - current) * 0.5f;
        if (part.WoundDescriptor is { } wound)
            cap = Math.Min(cap, max * LargestWoundTreatmentCap(wound, part.WoundDamage.Float()));

        cap = Math.Clamp(cap, current, max);
        return cap > current
            ? cap / max
            : null;
    }

    private static float LargestWoundTreatmentCap(WoundSize size, float damage)
    {
        return Math.Clamp(1f - WoundSizeProfile.FieldTreatmentPenalty(size, damage), 0.35f, 1f);
    }

    private void AppendFractureChip(BoxContainer strip, HealthScannerBuiState uiState, CMUBodyPartReadout part)
    {
        if (uiState.CMUFractures is not { Count: > 0 } fractures)
            return;
        foreach (var frac in fractures)
        {
            if (frac.Part != part.Type || frac.Symmetry != part.Symmetry)
                continue;
            var label = frac.ExactSeverity ? frac.Severity.ToString()
                : Loc.GetString("cmu-medical-scanner-chip-fracture-vague");
            if (frac.Suppressed)
                label += Loc.GetString("cmu-medical-scanner-chip-suppressed-suffix");
            strip.AddChild(BuildChip(label, SeverityFillColor(SeverityFromFracture(frac.Severity))));
            return;
        }
    }

    private void AppendBleedChip(BoxContainer strip, HealthScannerBuiState uiState, CMUBodyPartReadout part)
    {
        if (uiState.CMUInternalBleeds is not { Count: > 0 } bleeds)
            return;
        foreach (var bleed in bleeds)
        {
            // Exact bleeds attach to their declared part; vague (Med-1)
            // bleeds attach to the Torso row as a catch-all anchor for
            // "internal bleed somewhere".
            if (bleed.ExactLocationKnown
                ? (bleed.Part != part.Type || bleed.Symmetry != part.Symmetry)
                : (part.Type != BodyPartType.Torso))
            {
                continue;
            }
            strip.AddChild(BuildChip(Loc.GetString("cmu-medical-scanner-chip-bleed"),
                Color.FromHex("#A02020")));
            return;
        }
    }

    private static void AppendWoundChip(BoxContainer strip, CMUBodyPartReadout part)
    {
        if (part.WoundDescriptor is not { } descriptor)
            return;

        strip.AddChild(BuildChip(
            WoundChipText(descriptor, part.WoundDamage.Float()),
            WoundDescriptorColor(descriptor, part.WoundDamage.Float())));
    }

    private static void AppendShrapnelChip(BoxContainer strip, CMUBodyPartReadout part)
    {
        if (part.ShrapnelFragments <= 0)
            return;

        var color = part.ShrapnelSeverity >= 30f
            ? Color.FromHex("#9A6A22")
            : Color.FromHex("#756C5A");
        strip.AddChild(BuildChip(
            Loc.GetString("cmu-medical-scanner-chip-shrapnel", ("count", part.ShrapnelFragments)),
            color));
    }

    private static string WoundChipText(WoundSize size, float damage)
    {
        return WoundSizeProfile.StageName(size, damage);
    }

    private static Color WoundDescriptorColor(WoundSize descriptor, float damage) => WoundSizeProfile.SeverityRank(descriptor, damage) switch
    {
        0 => Color.FromHex("#7A4040"),
        1 => Color.FromHex("#8A3030"),
        2 => Color.FromHex("#A02020"),
        3 => Color.FromHex("#B01818"),
        _ => Color.FromHex("#8A3030"),
    };

    private static Control BuildChip(string text, Color background)
    {
        var panel = new PanelContainer
        {
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = Control.VAlignment.Center,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = background,
                BorderColor = Color.FromHex("#D8E2E4"),
                BorderThickness = new Thickness(1),
            },
        };
        panel.AddChild(new Label
        {
            Text = text,
            FontColorOverride = Color.White,
            Margin = new Thickness(7, 2),
        });
        return panel;
    }

    private void BuildStatusBanner(HealthScannerBuiState uiState)
    {
        var worst = PartSeverity.Healthy;
        var concerns = new List<string>();

        foreach (var part in uiState.CMUParts!.Values)
        {
            var pct = part.Current.Float() / Math.Max(1f, part.Max.Float());
            var sev = SeverityFromHpFraction(pct);
            if (sev > worst) worst = sev;
            if (sev >= PartSeverity.Damaged)
                concerns.Add(PartDisplayName(part.Type, part.Symmetry));
        }
        var present = new HashSet<(BodyPartType, BodyPartSymmetry)>();
        foreach (var p in uiState.CMUParts!.Values)
            present.Add((p.Type, p.Symmetry));
        foreach (var (type, sym) in CmuPartLayout)
        {
            if (present.Contains((type, sym)))
                continue;
            worst = PartSeverity.Severed;
            concerns.Insert(0, PartDisplayName(type, sym));
        }
        if (uiState.CMUOrgans is { } organs)
        {
            foreach (var organ in organs)
            {
                var sev = organ.Removed ? PartSeverity.Severed : SeverityFromOrganStage(organ.Stage);
                if (sev > worst) worst = sev;
                if (sev >= PartSeverity.Damaged)
                    concerns.Add(OrganDisplayName(organ.OrganName));
            }
        }
        if (uiState.CMUFractures is { Count: > 0 } fractures)
        {
            foreach (var frac in fractures)
            {
                var sev = SeverityFromFracture(frac.Severity);
                if (sev > worst) worst = sev;
            }
        }
        if (uiState.CMUInternalBleeds is { Count: > 0 })
            if (worst < PartSeverity.Critical) worst = PartSeverity.Critical;

        var (word, bgColor) = worst switch
        {
            PartSeverity.Severed => (Loc.GetString("cmu-medical-scanner-status-critical"), Color.FromHex("#8B2F35")),
            PartSeverity.Critical => (Loc.GetString("cmu-medical-scanner-status-critical"), Color.FromHex("#8B2F35")),
            PartSeverity.Damaged => (Loc.GetString("cmu-medical-scanner-status-serious"), Color.FromHex("#8B6334")),
            PartSeverity.Bruised => (Loc.GetString("cmu-medical-scanner-status-stable"), Color.FromHex("#2C6E55")),
            _ => (Loc.GetString("cmu-medical-scanner-status-stable"), Color.FromHex("#2C6E55")),
        };
        _window!.CMUStatusBanner.Visible = true;
        _window.CMUStatusBannerLabel.Text = word;
        if (_window.CMUStatusBanner.PanelOverride is StyleBoxFlat banner)
            banner.BackgroundColor = bgColor;
        _window.CMUStatusBannerDetail.Text = concerns.Count > 0
            ? string.Join(" · ", concerns.GetRange(0, Math.Min(3, concerns.Count)))
            : string.Empty;
        _window.CMUStatusBannerDetail.Visible = concerns.Count > 0;
    }

    private void BuildOrgans(HealthScannerBuiState uiState)
    {
        // null = sub-Med-2 examiner (FillOrgans is gated at skill ≥ 2 in
        // the server-side populator). Empty list = Med-2+ examiner but
        // patient has no organs (corpse / synth). Distinguish the two
        // so the medic knows whether they need to study harder or
        // whether the patient genuinely has nothing in there.
        if (uiState.CMUOrgans is null)
        {
            _window!.CMUOrgansContainer.AddChild(BuildSkillHint(
                "cmu-medical-scanner-skill-hint-organs"));
            return;
        }

        if (uiState.CMUSyntheticPhysiology)
        {
            _window!.CMUOrgansContainer.AddChild(BuildSkillHint(
                "cmu-medical-scanner-synthetic-physiology"));
            return;
        }

        if (uiState.CMUOrgans is not { Count: > 0 } organs)
            return;

        foreach (var organ in organs)
        {
            if (!OrganHasScannerDamage(organ))
                continue;

            var sev = organ.Removed ? PartSeverity.Severed : SeverityFromOrganStage(organ.Stage);
            var card = new PanelContainer
            {
                Margin = new Thickness(0, 0, 0, 5),
                HorizontalExpand = true,
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#10191E"),
                    BorderColor = Color.FromHex("#263A42"),
                    BorderThickness = new Thickness(1),
                },
            };
            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(7, 5),
                HorizontalExpand = true,
            };
            card.AddChild(row);
            row.AddChild(new PanelContainer
            {
                MinSize = new Vector2(10, 10),
                Margin = new Thickness(0, 4, 6, 0),
                VerticalAlignment = Control.VAlignment.Center,
                PanelOverride = new StyleBoxFlat { BackgroundColor = SeverityFillColor(sev) },
            });
            row.AddChild(new Label
            {
                Text = OrganDisplayName(organ.OrganName),
                MinWidth = 80,
            });
            row.AddChild(new Label
            {
                Text = organ.Removed
                    ? Loc.GetString("cmu-medical-scanner-organ-removed-short")
                    : organ.Stage.ToString(),
                MinWidth = 70,
                FontColorOverride = SeverityTextColor(sev),
            });
            // Hide Current/Max HP on Removed organs — the entity isn't on the
            // body, so Current is undefined.
            if (!organ.Removed)
            {
                row.AddChild(new Label
                {
                    Text = $"{organ.Current.Int()}/{organ.Max.Int()}",
                    MinWidth = 64,
                    FontColorOverride = Color.FromHex("#5B88B0"),
                });
            }
            _window!.CMUOrgansContainer.AddChild(card);
        }
    }

    private static readonly (BodyPartType Type, BodyPartSymmetry Sym)[] CmuPartLayout =
    {
        (BodyPartType.Head,  BodyPartSymmetry.None),
        (BodyPartType.Torso, BodyPartSymmetry.None),
        (BodyPartType.Arm,   BodyPartSymmetry.Left),
        (BodyPartType.Hand,  BodyPartSymmetry.Left),
        (BodyPartType.Arm,   BodyPartSymmetry.Right),
        (BodyPartType.Hand,  BodyPartSymmetry.Right),
        (BodyPartType.Leg,   BodyPartSymmetry.Left),
        (BodyPartType.Foot,  BodyPartSymmetry.Left),
        (BodyPartType.Leg,   BodyPartSymmetry.Right),
        (BodyPartType.Foot,  BodyPartSymmetry.Right),
    };

    private static bool OrganHasScannerDamage(CMUOrganReadout organ)
    {
        if (organ.Removed)
            return true;
        if (organ.Stage != Content.Shared._CMU14.Medical.Anatomy.Organs.OrganDamageStage.Healthy)
            return true;

        return organ.Current < organ.Max;
    }

    private static CMUBodyPartReadout? TryFindPart(
        HealthScannerBuiState uiState, BodyPartType type, BodyPartSymmetry symmetry)
    {
        // CMUParts dict key encodes PartType | Symmetry << 8 to keep
        // left/right pairs distinct on the wire. Readout records carry the
        // real Type / Symmetry, so iterate Values rather than keying.
        foreach (var p in uiState.CMUParts!.Values)
        {
            if (p.Type == type && p.Symmetry == symmetry)
                return p;
        }
        return null;
    }

    private static PartSeverity SeverityFromHpFraction(float pct)
    {
        // Do NOT collapse pct <= 0 to Severed here. Severed is reserved for
        // parts the body graph no longer enumerates (handled by
        // BuildSeveredRow). An attached part at 0% HP sits between the
        // severance HP boundary and SeveranceThreshold (Current ∈
        // [-SeveranceThreshold, 0]) — still attached, just hurts a lot.
        if (pct <= 0.25f) return PartSeverity.Critical;
        if (pct < 0.50f) return PartSeverity.Damaged;
        if (pct < 0.75f) return PartSeverity.Bruised;
        return PartSeverity.Healthy;
    }

    private static PartSeverity SeverityFromFracture(Content.Shared._CMU14.Medical.Anatomy.Bones.FractureSeverity severity)
        => severity switch
        {
            Content.Shared._CMU14.Medical.Anatomy.Bones.FractureSeverity.Hairline => PartSeverity.Bruised,
            Content.Shared._CMU14.Medical.Anatomy.Bones.FractureSeverity.Simple => PartSeverity.Damaged,
            Content.Shared._CMU14.Medical.Anatomy.Bones.FractureSeverity.Compound => PartSeverity.Critical,
            Content.Shared._CMU14.Medical.Anatomy.Bones.FractureSeverity.Shattered => PartSeverity.Critical,
            _ => PartSeverity.Bruised,
        };

    private static PartSeverity SeverityFromOrganStage(Content.Shared._CMU14.Medical.Anatomy.Organs.OrganDamageStage stage)
        => stage switch
        {
            Content.Shared._CMU14.Medical.Anatomy.Organs.OrganDamageStage.Healthy => PartSeverity.Healthy,
            Content.Shared._CMU14.Medical.Anatomy.Organs.OrganDamageStage.Bruised => PartSeverity.Bruised,
            Content.Shared._CMU14.Medical.Anatomy.Organs.OrganDamageStage.Damaged => PartSeverity.Damaged,
            Content.Shared._CMU14.Medical.Anatomy.Organs.OrganDamageStage.Failing => PartSeverity.Critical,
            Content.Shared._CMU14.Medical.Anatomy.Organs.OrganDamageStage.Dead => PartSeverity.Severed,
            _ => PartSeverity.Healthy,
        };

    private static Color SeverityFillColor(PartSeverity sev) => sev switch
    {
        PartSeverity.Healthy => Color.FromHex("#3FB44A"),
        PartSeverity.Bruised => Color.FromHex("#9CCC42"),
        PartSeverity.Damaged => Color.FromHex("#FFAA00"),
        PartSeverity.Critical => Color.FromHex("#E04040"),
        PartSeverity.Severed => Color.FromHex("#600000"),
        _ => Color.Gray,
    };

    private static Color SeverityTextColor(PartSeverity sev) => sev switch
    {
        PartSeverity.Healthy => Color.FromHex("#3FB44A"),
        PartSeverity.Bruised => Color.FromHex("#CFE070"),
        PartSeverity.Damaged => Color.FromHex("#FFAA00"),
        PartSeverity.Critical => Color.FromHex("#FF6060"),
        PartSeverity.Severed => Color.FromHex("#FF6060"),
        _ => Color.White,
    };

    private static string SeverityWord(PartSeverity sev) => sev switch
    {
        PartSeverity.Healthy => Loc.GetString("cmu-medical-scanner-severity-healthy"),
        PartSeverity.Bruised => Loc.GetString("cmu-medical-scanner-severity-bruised"),
        PartSeverity.Damaged => Loc.GetString("cmu-medical-scanner-severity-damaged"),
        PartSeverity.Critical => Loc.GetString("cmu-medical-scanner-severity-critical"),
        PartSeverity.Severed => Loc.GetString("cmu-medical-scanner-severity-severed"),
        _ => string.Empty,
    };

    private static string PartDisplayName(BodyPartType type, BodyPartSymmetry sym)
    {
        if (sym == BodyPartSymmetry.None)
            return type.ToString();
        return $"{sym} {type}";
    }

    // Small switch from CMU organ prototype id (attached organ path) OR
    // body-graph slot id (removed-organ path) → friendly display name.
    // Removed organs come through with their slot id ("heart", "lungs", …)
    // since there's no proto entity to read; attached organs come through
    // with their proto id ("CMUOrganHumanHeart"). Both routes land on the
    // same locale keys so the UI label stays consistent across states.
    // Fallback strips the "CMUOrganHuman" prefix so unknown prototypes
    // (V2.5 cybernetic / bespoke organs) still render readably.
    private static string OrganDisplayName(string idOrSlot) => idOrSlot switch
    {
        "CMUOrganHumanHeart" or "heart" => Loc.GetString("cmu-medical-scanner-organ-heart"),
        "CMUOrganHumanLungs" or "lungs" => Loc.GetString("cmu-medical-scanner-organ-lungs"),
        "CMUOrganHumanLiver" or "liver" => Loc.GetString("cmu-medical-scanner-organ-liver"),
        "CMUOrganHumanBrain" or "brain" => Loc.GetString("cmu-medical-scanner-organ-brain"),
        "CMUOrganHumanKidneys" or "kidneys" => Loc.GetString("cmu-medical-scanner-organ-kidneys"),
        "CMUOrganHumanStomach" or "stomach" => Loc.GetString("cmu-medical-scanner-organ-stomach"),
        "CMUOrganHumanEyes" or "eyes" => Loc.GetString("cmu-medical-scanner-organ-eyes"),
        _ => idOrSlot.StartsWith("CMUOrganHuman") ? idOrSlot.Substring("CMUOrganHuman".Length) : idOrSlot,
    };

    private static Color PainShockRiskColor(CMUPainShockRisk? risk) => risk switch
    {
        CMUPainShockRisk.Elevated => Color.FromHex("#CFE070"),
        CMUPainShockRisk.High => Color.FromHex("#FFAA00"),
        CMUPainShockRisk.Imminent => Color.FromHex("#FF6060"),
        CMUPainShockRisk.Active => Color.FromHex("#FF3030"),
        CMUPainShockRisk.Low => Color.White,
        _ => Color.FromHex("#5B88B0"),
    };

    private static string FormatPainShockRiskValue(CMUPainShockRisk? risk, bool suppressed)
    {
        if (risk is null)
            return "--";

        var key = risk.Value switch
        {
            CMUPainShockRisk.Low => "cmu-medical-scanner-pain-risk-low",
            CMUPainShockRisk.Elevated => "cmu-medical-scanner-pain-risk-elevated",
            CMUPainShockRisk.High => "cmu-medical-scanner-pain-risk-high",
            CMUPainShockRisk.Imminent => "cmu-medical-scanner-pain-risk-imminent",
            CMUPainShockRisk.Active => "cmu-medical-scanner-pain-risk-active",
            _ => "cmu-medical-scanner-pain-risk-unknown",
        };

        var value = Loc.GetString(key);
        if (suppressed)
            value += Loc.GetString("cmu-medical-scanner-pain-risk-suppressed-suffix");
        return value;
    }
}
