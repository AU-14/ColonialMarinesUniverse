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
    private void UpdateCmuState(HealthScannerBuiState uiState)
    {
        if (_window == null)
            return;

        var isPermaDead = _window.HealthBar.ModulateSelfOverride == Color.Red;
        UpdateBigStatRow(uiState, isPermaDead);
        UpdateCMUBodyMap(uiState);
        _window.ApplyUniformScale(true);
    }

    private enum PartSeverity : byte
    {
        Healthy = 0,
        Bruised = 1,
        Damaged = 2,
        Critical = 3,
        Severed = 4,
    }

    private void UpdateBigStatRow(
        HealthScannerBuiState uiState,
        bool isPermaDead)
    {
        if (_window == null)
            return;

        var healthValue = isPermaDead ? 0f : _window.HealthBar.Value;
        _window.CMUBigHealthValue.Text = isPermaDead
            ? Loc.GetString("cmu-medical-scanner-stat-deceased-short")
            : $"{healthValue:F0}%";
        _window.CMUBigHealthValue.FontColorOverride = isPermaDead
            ? Color.FromHex("#A02020")
            : SeverityTextColor(SeverityFromHpFraction(healthValue / 100f));

        if (uiState.CMUHeartStopped == true)
        {
            _window.CMUBigPulseValue.Text = Loc.GetString("cmu-medical-scanner-stat-pulse-stopped");
            _window.CMUBigPulseValue.FontColorOverride = Color.FromHex("#FF6060");
        }
        else if (uiState.CMUHeartBpm is { } bpm)
        {
            _window.CMUBigPulseValue.Text = bpm.ToString(CultureInfo.InvariantCulture);
            _window.CMUBigPulseValue.FontColorOverride = Color.White;
        }
        else
        {
            _window.CMUBigPulseValue.Text = "--";
            _window.CMUBigPulseValue.FontColorOverride = Color.FromHex("#5B88B0");
        }

        if (uiState.MaxBlood > 0)
        {
            var bloodPct = uiState.Blood.Float() / uiState.MaxBlood.Float() * 100f;
            _window.CMUBigBloodValue.Text = $"{bloodPct:F0}%";
            _window.CMUBigBloodValue.FontColorOverride = bloodPct < 60f
                ? Color.FromHex("#FF6060")
                : bloodPct < 85f ? Color.FromHex("#FFAA00") : Color.White;
        }
        else
        {
            _window.CMUBigBloodValue.Text = "--";
            _window.CMUBigBloodValue.FontColorOverride = Color.FromHex("#5B88B0");
        }

        if (uiState.Temperature is { } kelvin)
        {
            var celsius = TemperatureHelpers.KelvinToCelsius(kelvin);
            _window.CMUBigTempValue.Text = $"{celsius:F1}";
            _window.CMUBigTempValue.FontColorOverride = (celsius < 35f || celsius > 39f)
                ? Color.FromHex("#FFAA00")
                : Color.White;
        }
        else
        {
            _window.CMUBigTempValue.Text = "--";
            _window.CMUBigTempValue.FontColorOverride = Color.FromHex("#5B88B0");
        }

        _window.CMUBigShockRiskValue.Text = FormatPainShockRiskValue(
            uiState.CMUPainShockRisk,
            uiState.CMUPainShockSuppressed);
        _window.CMUBigShockRiskValue.FontColorOverride = PainShockRiskColor(uiState.CMUPainShockRisk);
    }

    private void UpdateCMUBodyMap(HealthScannerBuiState uiState)
    {
        if (_window == null)
            return;

        var section = _window.CMUBodyMapSection;
        if (uiState.CMUParts is not { Count: > 0 })
        {
            section.Visible = false;
            _window.CMUStatusBanner.Visible = false;
            return;
        }

        section.Visible = true;
        _window.CMUBodyChartContainer.DisposeAllChildren();
        _window.CMUOrgansContainer.DisposeAllChildren();

        BuildBodyChart(uiState);
        BuildOrgans(uiState);
        BuildStatusBanner(uiState);
    }

    private void BuildBodyChart(HealthScannerBuiState uiState)
    {
        var attached = new HashSet<(BodyPartType, BodyPartSymmetry)>();
        foreach (var (type, sym) in CmuPartLayout)
        {
            var part = TryFindPart(uiState, type, sym);
            if (part is null)
                continue;

            attached.Add((type, sym));
            if (!BodyPartHasScannerDamage(uiState, part.Value))
                continue;

            _window!.CMUBodyChartContainer.AddChild(BuildBodyRow(uiState, part.Value));
        }
        foreach (var (type, sym) in CmuPartLayout)
        {
            if (attached.Contains((type, sym)))
                continue;
            _window!.CMUBodyChartContainer.AddChild(BuildSeveredRow(type, sym));
        }

        // Skill hints — fractures + bleeds are gated at Med-1 in the
        // server-side populator. When both are null the examiner is
        // sub-Med-1 and we render a hint row so the medic understands
        // *why* the body chart looks bare instead of assuming the
        // patient is fine. Med-1+ examiners see fracture/bleed chips
        // inline on the part rows, so the hint hides at that point.
        if (uiState.CMUFractures is null && uiState.CMUInternalBleeds is null)
            _window!.CMUBodyChartContainer.AddChild(BuildSkillHint(
                "cmu-medical-scanner-skill-hint-fractures"));
    }

    private static Control BuildSkillHint(string locKey)
    {
        return new Label
        {
            Text = Loc.GetString(locKey),
            FontColorOverride = Color.FromHex("#5B6B7B"),
            Margin = new Thickness(0, 6, 0, 0),
        };
    }

    private Control BuildBodyRow(HealthScannerBuiState uiState, CMUBodyPartReadout part)
    {
        var pct = part.Current.Float() / Math.Max(1f, part.Max.Float());
        var recoverablePct = LineGraftRecoverableFraction(part);
        var sev = SeverityFromHpFraction(pct);
        var card = new PanelContainer
        {
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#10191E"),
                BorderColor = Color.FromHex("#263A42"),
                BorderThickness = new Thickness(1),
            },
        };

        var stack = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(8, 6),
            HorizontalExpand = true,
        };
        card.AddChild(stack);

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        stack.AddChild(row);

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 28),
            Margin = new Thickness(0, 0, 8, 0),
            PanelOverride = new StyleBoxFlat { BackgroundColor = SeverityFillColor(sev) },
        });

        row.AddChild(new Label
        {
            Text = PartDisplayName(part.Type, part.Symmetry),
            MinWidth = 112,
            VerticalAlignment = Control.VAlignment.Center,
            ClipText = true,
        });

        row.AddChild(new Label
        {
            Text = $"{(int)Math.Round(pct * 100f)}%",
            MinWidth = 48,
            VerticalAlignment = Control.VAlignment.Center,
            FontColorOverride = SeverityTextColor(sev),
        });

        row.AddChild(BuildHpBar(pct, sev, recoverablePct));

        row.AddChild(new Label
        {
            Text = SeverityWord(sev),
            MinWidth = 82,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = Control.VAlignment.Center,
            FontColorOverride = SeverityTextColor(sev),
        });

        var chipStrip = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        AppendFractureChip(chipStrip, uiState, part);
        AppendBleedChip(chipStrip, uiState, part);
        AppendWoundChip(chipStrip, part);
        AppendShrapnelChip(chipStrip, part);
        if (part.Eschar)
            chipStrip.AddChild(BuildChip(Loc.GetString("cmu-medical-scanner-eschar"), Color.FromHex("#7A5540")));
        if (part.Splinted)
            chipStrip.AddChild(BuildChip(Loc.GetString("cmu-medical-scanner-chip-splint"), Color.FromHex("#5B88B0")));
        if (part.Cast)
            chipStrip.AddChild(BuildChip(Loc.GetString("cmu-medical-scanner-chip-cast"), Color.FromHex("#5B88B0")));
        if (part.Tourniquet)
            chipStrip.AddChild(BuildChip(Loc.GetString("cmu-medical-scanner-chip-tourniquet"), Color.FromHex("#A02020")));
        if (chipStrip.ChildCount > 0)
        {
            chipStrip.Margin = new Thickness(13, 5, 0, 0);
            stack.AddChild(chipStrip);
        }

        return card;
    }

    private Control BuildSeveredRow(BodyPartType type, BodyPartSymmetry sym)
    {
        var card = new PanelContainer
        {
            Margin = new Thickness(0, 0, 0, 6),
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#10191E"),
                BorderColor = Color.FromHex("#3B2226"),
                BorderThickness = new Thickness(1),
            },
        };

        var stack = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(8, 6),
            HorizontalExpand = true,
        };
        card.AddChild(stack);

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        stack.AddChild(row);

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 28),
            Margin = new Thickness(0, 0, 8, 0),
            PanelOverride = new StyleBoxFlat { BackgroundColor = SeverityFillColor(PartSeverity.Severed) },
        });
        row.AddChild(new Label
        {
            Text = PartDisplayName(type, sym),
            MinWidth = 112,
            VerticalAlignment = Control.VAlignment.Center,
            ClipText = true,
        });
        // Em-dash instead of "0%" so a missing limb reads visually
        // distinct from a 0-HP attached one.
        row.AddChild(new Label
        {
            Text = "—",
            MinWidth = 48,
            VerticalAlignment = Control.VAlignment.Center,
            FontColorOverride = SeverityTextColor(PartSeverity.Severed),
        });
        row.AddChild(BuildHpBar(0f, PartSeverity.Severed));

        row.AddChild(new Label
        {
            Text = SeverityWord(PartSeverity.Severed),
            MinWidth = 82,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = Control.VAlignment.Center,
            FontColorOverride = SeverityTextColor(PartSeverity.Severed),
        });
        return card;
    }

    private static Control BuildHpBar(float pct, PartSeverity sev, float? recoverablePct = null)
    {
        const int trackWidth = 140;
        const int barHeight = 10;
        var track = new PanelContainer
        {
            MinSize = new Vector2(trackWidth, barHeight),
            VerticalAlignment = Control.VAlignment.Center,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#223039"),
                BorderColor = Color.FromHex("#314B55"),
                BorderThickness = new Thickness(1),
            },
        };
        var currentPct = Math.Clamp(pct, 0f, 1f);
        var recoverPct = recoverablePct is { } target
            ? Math.Clamp(target, currentPct, 1f)
            : currentPct;

        // For severed parts force the bar to render as a solid dark-red
        // strip so the medic sees the "limb gone" cue at a glance, even
        // though pct is 0.
        var currentWidth = sev == PartSeverity.Severed
            ? trackWidth
            : (int)Math.Round(trackWidth * currentPct);
        var recoverableWidth = sev == PartSeverity.Severed
            ? 0
            : (int)Math.Round(trackWidth * recoverPct) - currentWidth;

        if (currentWidth > 0 || recoverableWidth > 0)
        {
            var fillRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal };
            if (currentWidth > 0)
            {
                fillRow.AddChild(new PanelContainer
                {
                    MinSize = new Vector2(currentWidth, barHeight),
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = sev == PartSeverity.Severed
                            ? SeverityFillColor(sev)
                            : Color.FromHex("#3FB44A"),
                    },
                });
            }
            if (recoverableWidth > 0)
            {
                fillRow.AddChild(new PanelContainer
                {
                    MinSize = new Vector2(recoverableWidth, barHeight),
                    PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#D9B43A") },
                });
            }
            track.AddChild(fillRow);
        }
        return track;
    }

}
