using System.Globalization;
using System.Numerics;
using Content.Client.CMU14.Medical.Presentation.Windows;
using Content.Client.CMU14.Temperature; // CMU14
using Content.Client.CMU14.UI;
using Content.Client._RMC14.Medical.HUD;
using Content.Client.Message;
using Content.Shared.CMU14.Medical.Blood; // CMU14
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Medical.HUD.Systems;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs;
using Content.Shared.Temperature;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._RMC14.Medical.Scanner;

[UsedImplicitly]
public sealed partial class HealthScannerBui : BoundUserInterface
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _player = default!;

    [ViewVariables]
    private HealthScannerWindow? _window;
    private HealthScannerBuiState? _latestState;
    private NetEntity _lastTarget;
    private bool _hasLastTarget;

    private readonly ShowHolocardIconsSystem _holocardIcons;
    private readonly RMCReagentSystem _reagent;
    private readonly SkillsSystem _skills;

    private Dictionary<EntProtoId<SkillDefinitionComponent>, int> BloodPackSkill = new() { ["RMCSkillSurgery"] = 1 };
    private Dictionary<EntProtoId<SkillDefinitionComponent>, int> DefibSkill = new() { ["RMCSkillMedical"] = 2 };
    private Dictionary<EntProtoId<SkillDefinitionComponent>, int> LarvaSurgerySkill = new() { ["RMCSkillSurgery"] = 2 };

    private const int MaxAdviceRecommendations = 4;

    public HealthScannerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _holocardIcons = _entities.System<ShowHolocardIconsSystem>();
        _reagent = _entities.System<RMCReagentSystem>();
        _skills = _entities.System<SkillsSystem>();
    }

    protected override void Open()
    {
        base.Open();

        var window = EnsureWindow();
        window.ShowServerLoadingPulse();
        if (!window.IsOpen)
            window.OpenCentered();

        if (_latestState is { } latestState)
            UpdateState(latestState);
        else if (State is HealthScannerBuiState state)
            UpdateState(state);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is not HealthScannerStateMessage update)
            return;

        _latestState = update.State;
        UpdateState(update.State);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is HealthScannerBuiState uiState)
            UpdateState(uiState);
    }

    private void UpdateState(HealthScannerBuiState uiState)
    {
        _latestState = uiState;
        _window = EnsureWindow();

        if (_entities.GetEntity(uiState.Target) is not { Valid: true } target)
            return;

        if (_hasLastTarget && _lastTarget != uiState.Target)
            _window.ShowServerLoadingPulse();

        _lastTarget = uiState.Target;
        _hasLastTarget = true;

        _window.Title = Loc.GetString("rmc-health-analyzer-patient", ("name", Identity.Name(target, _entities, _player.LocalEntity)));

        var damage = uiState.Damage;
        _window.DamageRow.RemoveAllChildren();
        _window.DamageRow.AddChild(BuildDamageChip(Loc.GetString("rmc-health-analyzer-brute"), BruteColor, damage.Brute, damage.UntreatedBruteWounds));
        _window.DamageRow.AddChild(BuildDamageChip(Loc.GetString("rmc-health-analyzer-burn"), BurnColor, damage.Burn, damage.UntreatedBurnWounds));
        _window.DamageRow.AddChild(BuildDamageChip(Loc.GetString("rmc-health-analyzer-toxin"), Color.FromHex("#25CA4C"), damage.Toxin, false));
        _window.DamageRow.AddChild(BuildDamageChip(Loc.GetString("rmc-health-analyzer-oxygen"), Color.FromHex("#2E93DE"), damage.Airloss, false));
        if (damage.Genetic > 0)
            _window.DamageRow.AddChild(BuildDamageChip(Loc.GetString("rmc-health-analyzer-clone"), Color.FromHex("#02c9c0"), damage.Genetic, false));

        var isPermaDead = uiState.PermaDead;

        _window.HealthBar.MinValue = 0;
        _window.HealthBar.MaxValue = 100;
        if (uiState.HasIncapThreshold)
        {
            var threshold = uiState.IncapThreshold;
            var remaining = threshold - damage.Total;

            if (isPermaDead)
            {
                _window.HealthBar.Value = 100;
                _window.HealthBar.ModulateSelfOverride = Color.Red;
                _window.HealthBarText.Text = Loc.GetString("rmc-health-analyzer-permadead");
            }
            else
            {
                _window.HealthBar.ModulateSelfOverride = null;
                //Scale negative values with how close to death we are - if we have a different crit and dead state
                if (remaining < 0 && uiState.HasDeadThreshold && uiState.DeadThreshold != threshold)
                    threshold = uiState.DeadThreshold - threshold;

                var healthValue = threshold.Float() == 0 ? 0 : remaining.Float() / threshold.Float() * 100f;
                _window.HealthBar.Value = healthValue;

                var healthString = MathHelper.CloseTo(healthValue, 100) ? "100%" : $"{healthValue:F2}%";

                _window.HealthBarText.Text = Loc.GetString("rmc-health-analyzer-healthy", ("percent", healthString));
            }
        }
        else if (isPermaDead)
        {
            _window.HealthBar.Value = 100;
            _window.HealthBar.ModulateSelfOverride = Color.Red;
            _window.HealthBarText.Text = Loc.GetString("rmc-health-analyzer-permadead");
        }
        else
        {
            _window.HealthBar.Value = 100;
            _window.HealthBar.ModulateSelfOverride = null;
            _window.HealthBarText.Text = Loc.GetString("rmc-health-analyzer-healthy", ("percent", "100%"));
        }

        // cmu edit start
        if (!isPermaDead && uiState.CMUTimeUntilUnrevivable is { } untilUnrevivable)
        {
            var minutes = Math.Max(1, (int) Math.Ceiling(untilUnrevivable.TotalMinutes));
            _window.HealthBarText.Text += " " + Loc.GetString("cmu-health-analyzer-time-until-unrevivable", ("minutes", minutes));
        }
        // cmu edit end

        _window.ChangeHolocardButton.Text = Loc.GetString("ui-health-scanner-holocard-change");
        if (_player.LocalEntity is { } viewer &&
            _skills.HasSkill(viewer, HolocardSystem.SkillType, HolocardSystem.MinimumRequiredSkill))
        {
            _window.ChangeHolocardButton.Disabled = false;
            _window.ChangeHolocardButton.ToolTip = "";
        }
        else
        {
            _window.ChangeHolocardButton.Disabled = true;
            _window.ChangeHolocardButton.ToolTip = Loc.GetString("ui-holocard-change-insufficient-skill");
        }

        if (_entities.TryGetComponent(target, out HolocardStateComponent? holocardComponent) &&
            _holocardIcons.TryGetDescription((target, holocardComponent), out var description) &&
            _holocardIcons.TryGetHolocardColor((target, holocardComponent), out var color))
        {
            _window.HolocardDescription.Text = description;
            if (_window.HolocardPanel.PanelOverride is StyleBoxFlat panel)
                panel.BackgroundColor = color.Value;
        }
        else
        {
            _window.HolocardDescription.Text = Loc.GetString("hc-none-description");
            _window.HolocardPanel.ModulateSelfOverride = null;
            if (_window.HolocardPanel.PanelOverride is StyleBoxFlat panel)
                panel.BackgroundColor = Color.Transparent;
        }

        _window.ChemicalsContainer.ReleaseChildren();

        var anyChemicals = false;
        foreach (var reagent in uiState.KnownChemicals)
        {
            if (!_reagent.TryIndex(reagent.Prototype, out var prototype))
                continue;

            var text = $"{reagent.Quantity.Float():F1}u {prototype.LocalizedName}";
            if (reagent.Overdose)
                text += " OD";

            _window.ChemicalsContainer.AddChild(BuildChemicalRow(text, prototype.SubstanceColor, reagent.Overdose));
            anyChemicals = true;
        }

        if (uiState.OmittedKnownChemicals > 0)
        {
            _window.ChemicalsContainer.AddChild(new Label
            {
                Text = FormattedMessage.RemoveMarkupPermissive(
                    Loc.GetString("rmc-health-analyzer-additional-reagents", ("count", uiState.OmittedKnownChemicals))),
                FontColorOverride = Color.FromHex("#8E9AA6"),
            });
            anyChemicals = true;
        }

        SetSizedMarkup(_window.UnknownReagentsLabel, Loc.GetString("rmc-health-analyzer-unknown-reagents"));
        _window.UnknownChemicalsPanel.Visible = uiState.UnknownChemicals;
        _window.ChemicalContentsLabel.Visible = anyChemicals;
        _window.ChemicalContentsSeparator.Visible = anyChemicals;
        _window.ChemicalsContainer.Visible = anyChemicals;
        _window.ChemicalContentsCard.Visible = anyChemicals || uiState.UnknownChemicals;

        // cmu edit start: blood type and saline share
        _window.BloodTypeLabel.Text = uiState.CMUBloodType is { } bloodType
            ? Loc.GetString("cmu-health-analyzer-blood-type", ("type", bloodType))
            : "Blood:";
        // cmu edit end
        var percentage = uiState.MaxBlood == 0 ? 100 : uiState.Blood.Float() / uiState.MaxBlood.Float() * 100f;
        var percentageString = MathHelper.CloseTo(percentage, 100) ? "100%" : $"{percentage:F1}%";
        _window.BloodAmountLabel.Text = $"{percentageString}, {uiState.Blood}cl";
        _window.BloodAmountLabel.FontColorOverride = percentage < 60f
            ? Color.FromHex("#FF6060")
            : percentage < 85f ? Color.FromHex("#FFAA00") : Color.FromHex("#25B732");

        // cmu edit start
        _window.SalineLabel.Visible = uiState.CMUSalinePercent >= 1f;
        _window.SalineLabel.Text = $"({Loc.GetString("cmu-health-analyzer-saline", ("percent", (int) uiState.CMUSalinePercent))})";
        _window.SalineLabel.FontColorOverride = Color.FromHex(uiState.CMUSalinePercent > 25f ? "#DF8A3E" : "#5B88B0");

        string? bleeding = null;
        if (uiState.CMUExternalBleeding && uiState.CMUExternalBleedTier != ExternalBleedTier.None)
            bleeding = Loc.GetString("cmu-medical-scanner-bleeding-tier", ("tier", BleedTierName(uiState.CMUExternalBleedTier)));
        else if (uiState.CMUExternalBleeding || uiState.Bleeding)
            bleeding = Loc.GetString("cmu-medical-scanner-chip-bleeding");
        // cmu edit end
        _window.Bleeding.Visible = bleeding != null;
        _window.Bleeding.Text = $"[{bleeding}]";

        _window.AdviceContainer.ReleaseChildren();
        //Medication Advice
        if (!isPermaDead)
        {
            _window.MedicalAdviceLabel.Visible = true;
            _window.MedicalAdviceSeparator.Visible = true;
            MedicalAdvice(uiState, _window);
            if (_window.AdviceContainer.ChildCount == 0)
            {
                _window.MedicalAdviceLabel.Visible = false;
                _window.MedicalAdviceSeparator.Visible = false;
            }
        }
        else
        {
            _window.MedicalAdviceLabel.Visible = false;
            _window.MedicalAdviceSeparator.Visible = false;
        }
        _window.MedicalAdviceCard.Visible = _window.AdviceContainer.ChildCount > 0;

        UpdateVitals(uiState);

        UpdateCMUBodyMap(uiState);

        _window.CMURiderBanner.Visible = uiState.CMURiderReading is not null;
        if (uiState.CMURiderReading is { } riderReading)
            _window.CMURiderLabel.Text = riderReading;

        // CMU14: lab Wendigo procedure progress
        _window.CMUWendigoBanner.Visible = uiState.CMUWendigoReading is not null;
        if (uiState.CMUWendigoReading is { } wendigoReading)
            _window.CMUWendigoLabel.Text = wendigoReading;

        if (!_window.IsOpen)
        {
            _window.OpenCentered();
        }

        _window.ApplyUniformScale(true);
    }

    private HealthScannerWindow EnsureWindow()
    {
        if (_window != null)
            return _window;

        _window = this.CreateWindow<HealthScannerWindow>();
        _window.Title = Loc.GetString("rmc-health-analyzer-title");
        _window.ChangeHolocardButton.OnPressed += OpenChangeHolocardUI;
        return _window;
    }

    private enum PartSeverity : byte
    {
        Healthy = 0,
        Bruised = 1,
        Damaged = 2,
        Critical = 3,
        Severed = 4,
    }

    private static readonly Color BruteColor = Color.FromHex("#DF3E3E");
    private static readonly Color BurnColor = Color.FromHex("#FFB833");
    private static readonly Color MutedColor = Color.FromHex("#8E9AA6");

    private void UpdateVitals(HealthScannerBuiState uiState)
    {
        if (_window == null)
            return;

        if (uiState.Temperature is { } kelvin)
        {
            var celsius = TemperatureHelpers.KelvinToCelsius(kelvin);
            var fahrenheit = TemperatureHelpers.KelvinToFahrenheit(kelvin);
            SetLabel(_window.BodyTemperatureLabel,
                TemperatureDisplay.Fahrenheit ? $"{fahrenheit:F1}ºF" : $"{celsius:F1}ºC ({fahrenheit:F1}ºF)",
                celsius < 35f || celsius > 39f ? Color.FromHex("#FFAA00") : Color.White);
        }
        else
        {
            SetLabel(_window.BodyTemperatureLabel, "None", Color.White);
        }

        if (uiState.CMUHeartStopped == true)
            SetLabel(_window.PulseLabel, FormattedMessage.RemoveMarkupPermissive(Loc.GetString("cmu-medical-scanner-pulse-stopped")), Color.FromHex("#FF6060"));
        else if (uiState.CMUHeartBpm is { } bpm)
            SetLabel(_window.PulseLabel, Loc.GetString("cmu-medical-scanner-pulse-bpm", ("bpm", bpm)), Color.White);
        else
            SetLabel(_window.PulseLabel, "--", Color.FromHex("#5B88B0"));

        SetLabel(_window.ShockRiskLabel,
            FormatPainShockRiskValue(uiState.CMUPainShockRisk, uiState.CMUPainShockSuppressed),
            PainShockRiskColor(uiState.CMUPainShockRisk));
    }

    private static void SetLabel(Label label, string text, Color color)
    {
        label.Text = text;
        label.FontColorOverride = color;
    }

    private const int RichTextSize = 10;

    private static void SetSizedMessage(RichTextLabel label, FormattedMessage message, int size = RichTextSize)
    {
        var sized = new FormattedMessage();
        sized.PushTag(new MarkupNode("font", null, new Dictionary<string, MarkupParameter>
        {
            ["size"] = new MarkupParameter((long) size),
        }));
        sized.AddMessage(message);
        sized.Pop();
        label.SetMessage(sized);
    }

    private static void SetSizedMarkup(RichTextLabel label, string markup, int size = RichTextSize)
    {
        SetSizedMessage(label, FormattedMessage.FromMarkupPermissive(markup), size);
    }

    private static Control BuildDamageChip(string name, Color color, FixedPoint2 damage, bool untreated)
    {
        var chip = new PanelContainer
        {
            Margin = new Thickness(0, 0, 4, 0),
            VerticalAlignment = Control.VAlignment.Center,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#16181B"),
                BorderColor = Color.FromHex("#4972A1"),
                BorderThickness = new Thickness(1),
            },
        };
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(4, 0),
        };
        row.AddChild(new Label { Text = $"{name}: " });
        row.AddChild(new Label { Text = FormatDamage(damage, untreated), FontColorOverride = color });
        chip.AddChild(row);
        return chip;
    }

    private static string FormatDamage(FixedPoint2 damage, bool untreated)
    {
        var text = damage.Int().ToString(CultureInfo.InvariantCulture);
        return untreated ? $"{{{text}}}" : text;
    }

    private static Control BuildChemicalRow(string text, Color reagentColor, bool overdose)
    {
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };
        row.AddChild(new TextureRect
        {
            TexturePath = CMUIcons.Flask,
            ModulateSelfOverride = reagentColor.WithAlpha(1f),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(11, 11),
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = Control.VAlignment.Center,
        });

        var label = new Label { Text = text };
        if (overdose)
        {
            label.FontColorOverride = Color.FromHex("#FF6060");
            label.AddStyleClass(CMUMedicalUniformScaler.BoldStyleClass);
        }
        row.AddChild(label);
        return row;
    }

    private void UpdateCMUBodyMap(HealthScannerBuiState uiState)
    {
        if (_window == null)
            return;

        if (uiState.CMUParts is not { Count: > 0 })
        {
            _window.CMUBodyMapSection.Visible = false;
            _window.CMUOrgansCard.Visible = false;
            _window.CMUStatusBanner.Visible = false;
            return;
        }

        _window.CMUBodyChartContainer.ReleaseChildren();
        _window.CMUOrgansContainer.ReleaseChildren();

        BuildBodyChart(uiState);
        BuildOrgans(uiState);
        BuildStatusBanner(uiState);

        // only display when something present (e.g. dmg)
        _window.CMUBodyMapSection.Visible = _window.CMUBodyChartContainer.ChildCount > 0;
        _window.CMUOrgansCard.Visible = _window.CMUOrgansContainer.ChildCount > 0;
    }

    private void BuildBodyChart(HealthScannerBuiState uiState)
    {
        // only display limbs with dmg
        var rows = 0;
        foreach (var (type, sym) in CmuPartLayout)
        {
            var row = TryFindPart(uiState, type, sym) is { } part
                ? BuildBodyRow(uiState, part, rows)
                : BuildSeveredRow(uiState, type, sym, rows);
            if (row == null)
                continue;

            _window!.CMUBodyChartContainer.AddChild(row);
            rows++;
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
        var label = new Label
        {
            Text = Loc.GetString(locKey),
            FontColorOverride = Color.FromHex("#6F7B86"),
            Margin = new Thickness(3, 2, 0, 0),
        };
        label.AddStyleClass("LabelSmall");
        return label;
    }

    private Control? BuildBodyRow(HealthScannerBuiState uiState, CMUBodyPartReadout part, int index)
    {
        var tags = new List<(string Text, Color Color)>();
        AppendFractureTag(tags, uiState, part);
        AppendBleedTag(tags, uiState, part);
        // cmu edit start
        AppendExternalBleedTag(tags, part);
        // cmu edit end
        AppendWoundTag(tags, part);
        AppendShrapnelTag(tags, part);
        if (part.Eschar)
            tags.Add((Loc.GetString("cmu-medical-scanner-eschar"), Color.FromHex("#C49A7A")));
        if (part.Splinted)
            tags.Add((Loc.GetString("cmu-medical-scanner-chip-splint"), Color.FromHex("#7FA7D0")));
        if (part.Cast)
            tags.Add((Loc.GetString("cmu-medical-scanner-chip-cast"), Color.FromHex("#7FA7D0")));
        if (part.Tourniquet)
            tags.Add((Loc.GetString("cmu-medical-scanner-chip-tourniquet"), Color.FromHex("#FF6060")));

        var hurt = part.Brute > 0 || part.Burn > 0 || part.Current < part.Max;
        if (!hurt && tags.Count == 0)
            return null;

        return BuildLimbRow(
            index,
            PartDisplayName(part.Type, part.Symmetry),
            FormatDamage(part.Brute, part.UntreatedBrute),
            part.Brute > 0 ? BruteColor : Color.White,
            FormatDamage(part.Burn, part.UntreatedBurn),
            part.Burn > 0 ? BurnColor : Color.White,
            tags);
    }

    private Control BuildSeveredRow(HealthScannerBuiState uiState, BodyPartType type, BodyPartSymmetry sym, int index)
    {
        var tags = new List<(string Text, Color Color)>
        {
            (SeverityWord(PartSeverity.Severed), SeverityTextColor(PartSeverity.Severed)),
        };

        // cmu edit start
        if (uiState.CMUStumps is { } stumps)
        {
            foreach (var stump in stumps)
            {
                if (stump.Type != type || stump.Symmetry != sym)
                    continue;

                tags.Add(stump.Clamped
                    ? (Loc.GetString("cmu-medical-scanner-chip-stump-clamped"), Color.FromHex("#FF6060"))
                    : (Loc.GetString("cmu-medical-scanner-chip-stump-bleeding",
                        ("tier", BleedTierName(ExternalBleedTier.Arterial))), Color.FromHex("#FF3B3B")));
                break;
            }
        }
        // cmu edit end

        return BuildLimbRow(index, PartDisplayName(type, sym), "—", MutedColor, "—", MutedColor, tags);
    }

    private static Control BuildLimbRow(
        int index,
        string name,
        string brute,
        Color bruteColor,
        string burn,
        Color burnColor,
        List<(string Text, Color Color)> tags)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = index % 2 == 0 ? Color.FromHex("#272B30") : Color.Transparent,
            },
        };
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(3, 1),
            HorizontalExpand = true,
        };
        panel.AddChild(row);

        var nameLabel = new Label { Text = name, MinWidth = 80, ClipText = true };
        nameLabel.AddStyleClass(CMUMedicalUniformScaler.BoldStyleClass);
        row.AddChild(nameLabel);
        row.AddChild(new Label { Text = brute, MinWidth = 40, FontColorOverride = bruteColor });
        row.AddChild(new Label { Text = burn, MinWidth = 40, FontColorOverride = burnColor });

        var tagRow = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            RectClipContent = true,
            SeparationOverride = 5,
        };
        foreach (var (text, color) in tags)
            tagRow.AddChild(new Label { Text = $"[{text}]", FontColorOverride = color });
        row.AddChild(tagRow);

        return panel;
    }

    private void AppendFractureTag(List<(string Text, Color Color)> tags, HealthScannerBuiState uiState, CMUBodyPartReadout part)
    {
        if (uiState.CMUFractures is not { Count: > 0 } fractures)
            return;
        foreach (var frac in fractures)
        {
            if (frac.Part != part.Type || frac.Symmetry != part.Symmetry)
                continue;
            var label = frac.ExactSeverity
                ? Loc.GetString("cmu-medical-scanner-fracture", ("severity", frac.Severity.ToString()))
                : Loc.GetString("cmu-medical-scanner-fracture-vague");
            if (frac.Suppressed)
                label += Loc.GetString("cmu-medical-scanner-chip-suppressed-suffix");
            tags.Add((label, SeverityTextColor(SeverityFromFracture(frac.Severity))));
            return;
        }
    }

    private void AppendBleedTag(List<(string Text, Color Color)> tags, HealthScannerBuiState uiState, CMUBodyPartReadout part)
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
            tags.Add((Loc.GetString("cmu-medical-scanner-bleed-internal"), Color.FromHex("#FF6060")));
            return;
        }
    }

    // cmu edit start
    private static void AppendExternalBleedTag(List<(string Text, Color Color)> tags, CMUBodyPartReadout part)
    {
        if (part.ExternalBleeding == ExternalBleedTier.None)
            return;

        var color = part.ExternalBleeding switch
        {
            ExternalBleedTier.Minor => Color.FromHex("#D9A05B"),
            ExternalBleedTier.Moderate => Color.FromHex("#E08A4A"),
            ExternalBleedTier.Severe => Color.FromHex("#FF6060"),
            _ => Color.FromHex("#FF3B3B"),
        };
        tags.Add((Loc.GetString("cmu-medical-scanner-bleeding-tier", ("tier", BleedTierName(part.ExternalBleeding))), color));
    }

    private static string BleedTierName(ExternalBleedTier tier) => Loc.GetString(tier switch
    {
        ExternalBleedTier.Minor => "cmu-medical-scanner-bleed-tier-minor",
        ExternalBleedTier.Moderate => "cmu-medical-scanner-bleed-tier-moderate",
        ExternalBleedTier.Severe => "cmu-medical-scanner-bleed-tier-severe",
        _ => "cmu-medical-scanner-bleed-tier-arterial",
    });
    // cmu edit end

    private static void AppendWoundTag(List<(string Text, Color Color)> tags, CMUBodyPartReadout part)
    {
        if (part.WoundDescriptor is not { } descriptor)
            return;

        var damage = part.WoundDamage.Float();
        tags.Add((WoundSizeProfile.StageName(descriptor, damage), WoundDescriptorColor(descriptor, damage)));
    }

    private static void AppendShrapnelTag(List<(string Text, Color Color)> tags, CMUBodyPartReadout part)
    {
        if (part.ShrapnelFragments <= 0)
            return;

        var color = part.ShrapnelSeverity >= 30f
            ? Color.FromHex("#D9A05B")
            : Color.FromHex("#B8AE98");
        tags.Add((Loc.GetString("cmu-medical-scanner-chip-shrapnel", ("count", part.ShrapnelFragments)), color));
    }

    private static Color WoundDescriptorColor(WoundSize descriptor, float damage) => WoundSizeProfile.SeverityRank(descriptor, damage) switch
    {
        0 => Color.FromHex("#D08A8A"),
        1 => Color.FromHex("#E07070"),
        2 => Color.FromHex("#FF6060"),
        3 => Color.FromHex("#FF4040"),
        _ => Color.FromHex("#E07070"),
    };

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
        // the patient has no attached organs. Distinguish the two so the
        // medic knows whether they need to study harder or whether the
        // patient genuinely has nothing in there.
        if (uiState.CMUOrgans is null)
        {
            _window!.CMUBodyChartContainer.AddChild(BuildSkillHint(
                "cmu-medical-scanner-skill-hint-organs"));
            return;
        }

        if (uiState.CMUSyntheticPhysiology)
        {
            _window!.CMUBodyChartContainer.AddChild(BuildSkillHint(
                "cmu-medical-scanner-synthetic-physiology"));
        }

        if (uiState.CMUOrgans is not { Count: > 0 } organs)
            return;

        // only dmged / removed organs display
        var rows = 0;
        foreach (var organ in organs)
        {
            var sev = organ.Removed ? PartSeverity.Severed : SeverityFromOrganStage(organ.Stage);
            if (sev == PartSeverity.Healthy)
                continue;

            var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal };
            row.AddChild(new Label
            {
                Text = $"{OrganDisplayName(organ.OrganName)}:",
                MinWidth = 90,
                FontColorOverride = MutedColor,
            });
            var status = new Label
            {
                Text = organ.Removed
                    ? Loc.GetString("cmu-medical-scanner-organ-removed-short")
                    : Loc.GetString("cmu-medical-scanner-organ-damage",
                        ("stage", organ.Stage.ToString()),
                        ("damage", (organ.Max - organ.Current).Int())),
                FontColorOverride = SeverityTextColor(sev),
            };
            status.AddStyleClass(CMUMedicalUniformScaler.BoldStyleClass);
            row.AddChild(status);
            _window!.CMUOrgansContainer.AddChild(row);
            rows++;
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

    private static PartSeverity SeverityFromFracture(Content.Shared.CMU14.Medical.Anatomy.Bones.FractureSeverity severity)
        => severity switch
        {
            Content.Shared.CMU14.Medical.Anatomy.Bones.FractureSeverity.Hairline => PartSeverity.Bruised,
            Content.Shared.CMU14.Medical.Anatomy.Bones.FractureSeverity.Simple => PartSeverity.Damaged,
            Content.Shared.CMU14.Medical.Anatomy.Bones.FractureSeverity.Compound => PartSeverity.Critical,
            Content.Shared.CMU14.Medical.Anatomy.Bones.FractureSeverity.Shattered => PartSeverity.Critical,
            _ => PartSeverity.Bruised,
        };

    private static PartSeverity SeverityFromOrganStage(Content.Shared.CMU14.Medical.Anatomy.Organs.OrganDamageStage stage)
        => stage switch
        {
            Content.Shared.CMU14.Medical.Anatomy.Organs.OrganDamageStage.Healthy => PartSeverity.Healthy,
            Content.Shared.CMU14.Medical.Anatomy.Organs.OrganDamageStage.Bruised => PartSeverity.Bruised,
            Content.Shared.CMU14.Medical.Anatomy.Organs.OrganDamageStage.Damaged => PartSeverity.Damaged,
            Content.Shared.CMU14.Medical.Anatomy.Organs.OrganDamageStage.Failing => PartSeverity.Critical,
            Content.Shared.CMU14.Medical.Anatomy.Organs.OrganDamageStage.Dead => PartSeverity.Severed,
            _ => PartSeverity.Healthy,
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

    private void OpenChangeHolocardUI(BaseButton.ButtonEventArgs obj)
    {
        if (_player.LocalEntity is { } viewer)
            SendMessage(new OpenChangeHolocardUIEvent(_entities.GetNetEntity(viewer), _lastTarget));
    }

    private void MedicalAdvice(HealthScannerBuiState uiState, HealthScannerWindow window)
    {
        if (_player.LocalEntity is not { } viewer)
            return;

        var recommendations = new List<AdviceRecommendation>();

        //Defibrilation related
        if (uiState.MobState == MobState.Dead)
        {
            if (uiState.Advice.NeedsEpinephrine)
            {
                AddAdviceCandidate(recommendations, 100, Loc.GetString("rmc-health-analyzer-advice-epinedrine"));
            }
            else
            {
                var defib = string.Empty;
                var priority = 0;
                if (uiState.Advice.ShowRepeatedDefibWarning)
                {
                    defib = Loc.GetString("rmc-health-analyzer-advice-defib-repeated");
                    priority = 96;
                }
                else if (uiState.Advice.ShowDefib)
                {
                    defib = Loc.GetString("rmc-health-analyzer-advice-defib");
                    priority = 95;
                }

                if (defib != string.Empty && !_skills.HasAllSkills(viewer, DefibSkill))
                    defib = $"[color=#858585]{defib}[/color]";

                if (defib != string.Empty)
                    AddAdviceCandidate(recommendations, priority, defib);
            }

            if (uiState.Advice.ShowCpr)
                AddAdviceCandidate(recommendations, 92, Loc.GetString("rmc-health-analyzer-advice-cpr"));
        }

        //Surgery related
        if (uiState.Advice.ShowLarvaBursted)
        {
            AddAdviceCandidate(recommendations, 90, Loc.GetString("rmc-health-analyzer-advice-larva-bursted"));
        }
        else if (uiState.Advice.ShowLarvaSurgery)
        {
            string larvaSurgery = Loc.GetString("rmc-health-analyzer-advice-larva-surgery");
            if (!_skills.HasAllSkills(viewer, LarvaSurgerySkill))
                larvaSurgery = $"[color=#858585]{larvaSurgery}[/color]";
            AddAdviceCandidate(recommendations, 88, larvaSurgery);
        }

        //TODO RMC14 more surgery advice

        //Wound related
        if (uiState.Advice.ShowBruteWounds)
            AddAdviceCandidate(recommendations, 72, Loc.GetString("rmc-health-analyzer-advice-brute-wounds"));

        if (uiState.Advice.ShowBurnWounds)
            AddAdviceCandidate(recommendations, 71, Loc.GetString("rmc-health-analyzer-advice-burn-wounds"));

        //Blood related
        if (uiState.Advice.ShowBloodPack)
        {
            string bloodpack = Loc.GetString("rmc-health-analyzer-advice-blood-pack");
            if (!_skills.HasAllSkills(viewer, BloodPackSkill))
                bloodpack = $"[color=#858585]{bloodpack}[/color]";
            AddAdviceCandidate(recommendations, 78, bloodpack);

            // CMU14: which blood and plasma the patient can take
            if (CMUBloodTypes.TryParseName(uiState.CMUBloodType, out var patientType))
            {
                var types = Loc.GetString("cmu-health-analyzer-advice-blood-types",
                    ("blood", CMUBloodTypes.CompatibleBloodNames(patientType)),
                    ("plasma", CMUBloodTypes.CompatiblePlasmaGroups(patientType)));
                if (!_skills.HasAllSkills(viewer, BloodPackSkill))
                    types = $"[color=#858585]{types}[/color]";
                AddAdviceCandidate(recommendations, 77, types);
            }
        }

        if (uiState.Advice.ShowFood)
            AddAdviceCandidate(recommendations, 25, Loc.GetString("rmc-health-analyzer-advice-food"));

        //Damage related
        if (uiState.Advice.ShowCprCrit)
            AddAdviceCandidate(recommendations, 84, Loc.GetString("rmc-health-analyzer-advice-cpr-crit"));

        if (uiState.Advice.ShowDexalin)
            AddAdviceCandidate(recommendations, 82, Loc.GetString("rmc-health-analyzer-advice-dexalin"));

        if (uiState.Advice.ShowBicaridine)
            AddAdviceCandidate(recommendations, 58, Loc.GetString("rmc-health-analyzer-advice-bicaridine"));

        if (uiState.Advice.ShowKelotane)
            AddAdviceCandidate(recommendations, 57, Loc.GetString("rmc-health-analyzer-advice-kelotane"));

        if (uiState.Advice.ShowDylovene)
            AddAdviceCandidate(recommendations, 60, Loc.GetString("rmc-health-analyzer-advice-dylovene"));

        //TODO RMC14 Clone damage advice

        // cmu edit start
        CMUMedicalAdvice(uiState, recommendations);
        // cmu edit end

        recommendations.Sort((a, b) =>
        {
            var priority = b.Priority.CompareTo(a.Priority);
            return priority != 0 ? priority : a.Sequence.CompareTo(b.Sequence);
        });

        var count = Math.Min(MaxAdviceRecommendations, recommendations.Count);
        for (var i = 0; i < count; i++)
            AddAdvice(recommendations[i], window);
    }

    private static void AddAdviceCandidate(List<AdviceRecommendation> recommendations, int priority, string text)
    {
        recommendations.Add(new AdviceRecommendation(priority, recommendations.Count, text));
    }

    private static void AddAdvice(AdviceRecommendation recommendation, HealthScannerWindow window)
    {
        var accent = AdviceAccentColor(recommendation.Priority).ToHex();
        var label = new CMUScaledRichTextLabel { HorizontalExpand = true };
        SetSizedMarkup(label, $"[color={accent}]\u25B8[/color] {recommendation.Text}");
        window.AdviceContainer.AddChild(label);
    }

    private static Color AdviceAccentColor(int priority)
    {
        if (priority >= 90)
            return Color.FromHex("#B64646");
        if (priority >= 70)
            return Color.FromHex("#D2A95D");
        return Color.FromHex("#5B88B0");
    }

    private readonly record struct AdviceRecommendation(int Priority, int Sequence, string Text);
}
