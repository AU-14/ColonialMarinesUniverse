using System.Globalization;
using System.Numerics;
using Content.Client._RMC14.Medical.HUD;
using Content.Client.Atmos.Rotting;
using Content.Client.Message;
using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared._RMC14.Medical.HUD.Systems;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Systems;
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
public sealed class HealthScannerBui : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    [ViewVariables]
    private HealthScannerWindow? _window;
    private NetEntity _lastTarget;

    private readonly ShowHolocardIconsSystem _holocardIcons;
    private readonly SkillsSystem _skills;
    private readonly SharedWoundsSystem _wounds;
    private readonly RMCUnrevivableSystem _unrevivable;
    private readonly MobStateSystem _mob;
    private readonly RottingSystem _rot;

    private Dictionary<EntProtoId<SkillDefinitionComponent>, int> BloodPackSkill = new() { ["RMCSkillSurgery"] = 1 };
    private Dictionary<EntProtoId<SkillDefinitionComponent>, int> DefibSkill = new() { ["RMCSkillMedical"] = 2 };
    private Dictionary<EntProtoId<SkillDefinitionComponent>, int> LarvaSurgerySkill = new() { ["RMCSkillSurgery"] = 2 };

    public HealthScannerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _holocardIcons = _entities.System<ShowHolocardIconsSystem>();
        _skills = _entities.System<SkillsSystem>();
        _wounds = _entities.System<SharedWoundsSystem>();
        _unrevivable = _entities.System<RMCUnrevivableSystem>();
        _mob = _entities.System<MobStateSystem>();
        _rot = _entities.System<RottingSystem>();
    }

    protected override void Open()
    {
        base.Open();
        if (State is HealthScannerBuiState state)
            UpdateState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is HealthScannerBuiState uiState)
            UpdateState(uiState);
    }

    private void UpdateState(HealthScannerBuiState uiState)
    {
        if (_window == null)
        {
            _window = this.CreateWindow<HealthScannerWindow>();
            _window.Title = Loc.GetString("rmc-health-analyzer-title");
        }

        if (_entities.GetEntity(uiState.Target) is not { Valid: true } target)
            return;

        _lastTarget = uiState.Target;

        _window.PatientLabel.Text = Loc.GetString("rmc-health-analyzer-patient", ("name", Identity.Name(target, _entities, _player.LocalEntity)));

        var thresholdsSystem = _entities.System<MobThresholdSystem>();
        if (!_entities.TryGetComponent(target, out DamageableComponent? damageable))
        {
            if (!_window.IsOpen)
                _window.OpenCentered();

            return;
        }

        var ent = new Entity<DamageableComponent>(target, damageable);
        AddGroup(ent, _window.BruteLabel, Color.FromHex("#DF3E3E"), "Brute", Loc.GetString("rmc-health-analyzer-brute"));
        AddGroup(ent, _window.BurnLabel, Color.FromHex("#FFB833"), "Burn", Loc.GetString("rmc-health-analyzer-burn"));
        AddGroup(ent, _window.ToxinLabel, Color.FromHex("#25CA4C"), "Toxin", Loc.GetString("rmc-health-analyzer-toxin"));
        AddGroup(ent, _window.OxygenLabel, Color.FromHex("#2E93DE"), "Airloss", Loc.GetString("rmc-health-analyzer-oxygen"));
        if (damageable.DamagePerGroup["Genetic"] > 0)
        {
            _window.CloneBox.Visible = true;
            AddGroup(ent, _window.CloneLabel, Color.FromHex("#02c9c0"), "Genetic", Loc.GetString("rmc-health-analyzer-clone"));
        }
        else
            _window.CloneBox.Visible = false;

        bool isPermaDead = false;

        if (thresholdsSystem.TryGetIncapThreshold(target, out var threshold))
        {
            var damage = threshold.Value - damageable.TotalDamage;
            _window.HealthBar.MinValue = 0;
            _window.HealthBar.MaxValue = 100;

            if (_mob.IsDead(target) && (_entities.HasComponent<VictimBurstComponent>(target) ||
                _rot.IsRotten(target) || _unrevivable.IsUnrevivable(target) ||
                _entities.HasComponent<RMCDefibrillatorBlockedComponent>(target)))
            {
                isPermaDead = true;
                _window.HealthBar.Value = 100;
                _window.HealthBar.ModulateSelfOverride = Color.Red;
                _window.HealthBarText.Text = Loc.GetString("rmc-health-analyzer-permadead");
            }
            else
            {
                _window.HealthBar.ModulateSelfOverride = null;
                //Scale negative values with how close to death we are - if we have a different crit and dead state
                if (damage < 0 && thresholdsSystem.TryGetDeadThreshold(target, out var deadThreshold) &&
                    deadThreshold != threshold)
                    threshold = deadThreshold - threshold;

                var healthValue = damage.Float() / threshold.Value.Float() * 100f;
                _window.HealthBar.Value = healthValue;

                var healthString = MathHelper.CloseTo(healthValue, 100) ? "100%" : $"{healthValue:F2}%";

                _window.HealthBarText.Text = Loc.GetString("rmc-health-analyzer-healthy", ("percent", healthString));
            }
        }

        _window.ChangeHolocardButton.Text = Loc.GetString("ui-health-scanner-holocard-change");
        _window.ChangeHolocardButton.OnPressed += OpenChangeHolocardUI;
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

        _window.ChemicalsContainer.DisposeAllChildren();

        var anyChemicals = false;
        var anyUnknown = false;
        if (uiState.Chemicals != null)
        {
            foreach (var reagent in uiState.Chemicals.Contents)
            {
                if (!_prototype.TryIndexReagent(reagent.Reagent.Prototype, out ReagentPrototype? prototype))
                    continue;

                if (prototype.Unknown)
                {
                    // TODO RMC14 these shouldn't be setting sent to the client
                    anyUnknown = true;
                    continue;
                }

                var text = $"{reagent.Quantity.Float():F1} {prototype.LocalizedName}";
                if (prototype.Overdose != null && reagent.Quantity > prototype.Overdose)
                    text = $"[bold][color=red]{FormattedMessage.EscapeText(text)} OD[/color][/bold]";

                var label = new RichTextLabel();
                label.SetMarkupPermissive(text);
                _window.ChemicalsContainer.AddChild(label);
                anyChemicals = true;
            }
        }

        _window.UnknownReagentsLabel.SetMarkupPermissive(Loc.GetString("rmc-health-analyzer-unknown-reagents"));
        _window.UnknownChemicalsPanel.Visible = anyUnknown;
        _window.ChemicalContentsLabel.Visible = anyChemicals;
        _window.ChemicalContentsSeparator.Visible = anyChemicals;
        _window.ChemicalsContainer.Visible = anyChemicals;
        _window.ChemicalContentsCard.Visible = anyChemicals || anyUnknown;

        _window.BloodTypeLabel.Text = "Blood:";
        var bloodMsg = new FormattedMessage();
        bloodMsg.PushColor(Color.FromHex("#25B732"));

        var percentage = uiState.MaxBlood == 0 ? 100 : uiState.Blood.Float() / uiState.MaxBlood.Float() * 100f;
        var percentageString = MathHelper.CloseTo(percentage, 100) ? "100%" : $"{percentage:F1}%";
        bloodMsg.AddText($"{percentageString}, {uiState.Blood}cl");
        bloodMsg.Pop();
        _window.BloodAmountLabel.SetMessage(bloodMsg);

        if (uiState.CMUExternalBleeding)
            _window.Bleeding.SetMarkup(" [bold][color=#DF3E3E]\\[Bleeding\\][/color][/bold]");
        else if (uiState.Bleeding)
            _window.Bleeding.SetMarkup(" [bold][color=#DF3E3E]\\[Bleeding\\][/color][/bold]");
        else
            _window.Bleeding.SetMessage(string.Empty);

        var temperatureMsg = new FormattedMessage();
        if (uiState.Temperature is { } temperatureKelvin)
        {
            var celsius = TemperatureHelpers.KelvinToCelsius(temperatureKelvin);
            var fahrenheit = TemperatureHelpers.KelvinToFahrenheit(temperatureKelvin);
            temperatureMsg.AddText($"{celsius:F1}ºC ({fahrenheit:F1}ºF)");
        }
        else
        {
            temperatureMsg.AddText("None");
        }

        _window.BodyTemperatureLabel.SetMessage(temperatureMsg);

        _window.AdviceContainer.DisposeAllChildren();
        //Medication Advice
        if (!isPermaDead)
        {
            _window.MedicalAdviceLabel.Visible = true;
            _window.MedicalAdviceSeparator.Visible = true;
            MedicalAdvice(ent, uiState, _window);
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

        UpdateBigStatRow(uiState, damageable, thresholdsSystem, target, isPermaDead);

        UpdateCMUBodyMap(uiState);

        if (!_window.IsOpen)
        {
            _window.OpenCentered();
        }
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
        DamageableComponent? damageable,
        MobThresholdSystem thresholdsSystem,
        EntityUid target,
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
    }

    private void UpdateCMUBodyMap(HealthScannerBuiState uiState)
    {
        if (_window == null)
            return;

        var section = _window.CMUBodyMapSection;
        if (uiState.CMUParts == null)
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
        var present = new HashSet<(BodyPartType, BodyPartSymmetry)>();
        foreach (var (type, sym) in CmuPartLayout)
        {
            var part = TryFindPart(uiState, type, sym);
            if (part is null)
                continue;
            present.Add((type, sym));
            _window!.CMUBodyChartContainer.AddChild(BuildBodyRow(uiState, part.Value));
        }
        foreach (var (type, sym) in CmuPartLayout)
        {
            if (present.Contains((type, sym)))
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
        var sev = SeverityFromHpFraction(pct);
        var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(0, 1) };

        row.AddChild(new Label
        {
            Text = PartDisplayName(part.Type, part.Symmetry),
            MinWidth = 90,
        });

        row.AddChild(new Label
        {
            Text = $"{(int)Math.Round(pct * 100f)}%",
            MinWidth = 44,
            FontColorOverride = SeverityTextColor(sev),
        });

        row.AddChild(BuildHpBar(pct, sev));

        row.AddChild(new Label
        {
            Text = SeverityWord(sev),
            MinWidth = 78,
            Margin = new Thickness(8, 0, 0, 0),
            FontColorOverride = SeverityTextColor(sev),
        });

        var chipStrip = new BoxContainer { Orientation = LayoutOrientation.Horizontal };
        AppendFractureChip(chipStrip, uiState, part);
        AppendBleedChip(chipStrip, uiState, part);
        AppendWoundChip(chipStrip, part);
        if (part.Eschar)
            chipStrip.AddChild(BuildChip(Loc.GetString("cmu-medical-scanner-eschar"), Color.FromHex("#7A5540")));
        if (part.Splinted)
            chipStrip.AddChild(BuildChip(Loc.GetString("cmu-medical-scanner-chip-splint"), Color.FromHex("#5B88B0")));
        if (part.Cast)
            chipStrip.AddChild(BuildChip(Loc.GetString("cmu-medical-scanner-chip-cast"), Color.FromHex("#5B88B0")));
        if (part.Tourniquet)
            chipStrip.AddChild(BuildChip(Loc.GetString("cmu-medical-scanner-chip-tourniquet"), Color.FromHex("#A02020")));
        row.AddChild(chipStrip);

        return row;
    }

    private Control BuildSeveredRow(BodyPartType type, BodyPartSymmetry sym)
    {
        var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal, Margin = new Thickness(0, 1) };
        row.AddChild(new Label
        {
            Text = PartDisplayName(type, sym),
            MinWidth = 90,
        });
        // Em-dash instead of "0%" so a missing limb reads visually
        // distinct from a 0-HP attached one.
        row.AddChild(new Label
        {
            Text = "—",
            MinWidth = 44,
            FontColorOverride = SeverityTextColor(PartSeverity.Severed),
        });
        row.AddChild(BuildHpBar(0f, PartSeverity.Severed));
        row.AddChild(new Label
        {
            Text = SeverityWord(PartSeverity.Severed),
            MinWidth = 78,
            Margin = new Thickness(8, 0, 0, 0),
            FontColorOverride = SeverityTextColor(PartSeverity.Severed),
        });
        return row;
    }

    private static Control BuildHpBar(float pct, PartSeverity sev)
    {
        const int trackWidth = 100;
        const int barHeight = 10;
        var track = new PanelContainer
        {
            MinSize = new Vector2(trackWidth, barHeight),
            VerticalAlignment = Control.VAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#2A2F3A") },
        };
        // For severed parts force the bar to render as a solid dark-red
        // strip so the medic sees the "limb gone" cue at a glance, even
        // though pct is 0.
        var fillWidth = sev == PartSeverity.Severed ? trackWidth : (int)Math.Round(trackWidth * Math.Clamp(pct, 0f, 1f));
        if (fillWidth > 0)
        {
            var fillRow = new BoxContainer { Orientation = LayoutOrientation.Horizontal };
            fillRow.AddChild(new PanelContainer
            {
                MinSize = new Vector2(fillWidth, barHeight),
                PanelOverride = new StyleBoxFlat { BackgroundColor = SeverityFillColor(sev) },
            });
            track.AddChild(fillRow);
        }
        return track;
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
            Loc.GetString(WoundDescriptorLocaleKey(descriptor)),
            WoundDescriptorColor(descriptor)));
    }

    private static string WoundDescriptorLocaleKey(WoundSize descriptor) => descriptor switch
    {
        WoundSize.Small => "cmu-medical-scanner-wound-small",
        WoundSize.Deep => "cmu-medical-scanner-wound-deep",
        WoundSize.Gaping => "cmu-medical-scanner-wound-gaping",
        WoundSize.Massive => "cmu-medical-scanner-wound-massive",
        _ => "cmu-medical-scanner-wound-deep",
    };

    private static Color WoundDescriptorColor(WoundSize descriptor) => descriptor switch
    {
        WoundSize.Small => Color.FromHex("#7A4040"),
        WoundSize.Deep => Color.FromHex("#8A3030"),
        WoundSize.Gaping => Color.FromHex("#A02020"),
        WoundSize.Massive => Color.FromHex("#B01818"),
        _ => Color.FromHex("#8A3030"),
    };

    private static Control BuildChip(string text, Color background)
    {
        var panel = new PanelContainer
        {
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = Control.VAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = background },
        };
        panel.AddChild(new Label
        {
            Text = text,
            FontColorOverride = Color.White,
            Margin = new Thickness(6, 1),
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
            PartSeverity.Severed => (Loc.GetString("cmu-medical-scanner-status-critical"), Color.FromHex("#A02020")),
            PartSeverity.Critical => (Loc.GetString("cmu-medical-scanner-status-critical"), Color.FromHex("#A02020")),
            PartSeverity.Damaged => (Loc.GetString("cmu-medical-scanner-status-serious"), Color.FromHex("#C07020")),
            PartSeverity.Bruised => (Loc.GetString("cmu-medical-scanner-status-stable"), Color.FromHex("#3FB44A")),
            _ => (Loc.GetString("cmu-medical-scanner-status-stable"), Color.FromHex("#3FB44A")),
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
        if (uiState.CMUOrgans is not { Count: > 0 } organs)
            return;

        foreach (var organ in organs)
        {
            var sev = organ.Removed ? PartSeverity.Severed : SeverityFromOrganStage(organ.Stage);
            var row = new BoxContainer { Orientation = LayoutOrientation.Horizontal };
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
            _window!.CMUOrgansContainer.AddChild(row);
        }
    }

    private static readonly (BodyPartType Type, BodyPartSymmetry Sym)[] CmuPartLayout =
    {
        (BodyPartType.Head,  BodyPartSymmetry.None),
        (BodyPartType.Torso, BodyPartSymmetry.None),
        (BodyPartType.Arm,   BodyPartSymmetry.Left),
        (BodyPartType.Arm,   BodyPartSymmetry.Right),
        (BodyPartType.Leg,   BodyPartSymmetry.Left),
        (BodyPartType.Leg,   BodyPartSymmetry.Right),
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

    private static PartSeverity SeverityFromFracture(Content.Shared._CMU14.Medical.Bones.FractureSeverity severity)
        => severity switch
        {
            Content.Shared._CMU14.Medical.Bones.FractureSeverity.Hairline => PartSeverity.Bruised,
            Content.Shared._CMU14.Medical.Bones.FractureSeverity.Simple => PartSeverity.Damaged,
            Content.Shared._CMU14.Medical.Bones.FractureSeverity.Compound => PartSeverity.Critical,
            Content.Shared._CMU14.Medical.Bones.FractureSeverity.Comminuted => PartSeverity.Critical,
            _ => PartSeverity.Bruised,
        };

    private static PartSeverity SeverityFromOrganStage(Content.Shared._CMU14.Medical.Organs.OrganDamageStage stage)
        => stage switch
        {
            Content.Shared._CMU14.Medical.Organs.OrganDamageStage.Healthy => PartSeverity.Healthy,
            Content.Shared._CMU14.Medical.Organs.OrganDamageStage.Bruised => PartSeverity.Bruised,
            Content.Shared._CMU14.Medical.Organs.OrganDamageStage.Damaged => PartSeverity.Damaged,
            Content.Shared._CMU14.Medical.Organs.OrganDamageStage.Failing => PartSeverity.Critical,
            Content.Shared._CMU14.Medical.Organs.OrganDamageStage.Dead => PartSeverity.Severed,
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
        "CMUOrganHumanEars" or "ears" => Loc.GetString("cmu-medical-scanner-organ-ears"),
        _ => idOrSlot.StartsWith("CMUOrganHuman") ? idOrSlot.Substring("CMUOrganHuman".Length) : idOrSlot,
    };

    private static Color PainTierColor(PainTier? tier) => tier switch
    {
        PainTier.Mild => Color.FromHex("#9CCC42"),
        PainTier.Moderate => Color.FromHex("#FFAA00"),
        PainTier.Severe => Color.FromHex("#FF6060"),
        PainTier.Shock => Color.FromHex("#FF3030"),
        _ => Color.FromHex("#5B88B0"),
    };

    private static string FormatPainTier(PainTier? tier) => tier switch
    {
        null => Loc.GetString("cmu-medical-scanner-pain-unknown"),
        PainTier.None => Loc.GetString("cmu-medical-scanner-pain-none"),
        PainTier.Mild => Loc.GetString("cmu-medical-scanner-pain-mild"),
        PainTier.Moderate => Loc.GetString("cmu-medical-scanner-pain-moderate"),
        PainTier.Severe => Loc.GetString("cmu-medical-scanner-pain-severe"),
        PainTier.Shock => Loc.GetString("cmu-medical-scanner-pain-shock"),
        _ => Loc.GetString("cmu-medical-scanner-pain-unknown"),
    };

    // Vitals stat-block variant: returns just the tier word so "Pain"
    // can be the row label and the tier word the value (avoiding the
    // "Pain    Pain: Severe" double-up).
    private static string FormatPainTierValue(PainTier? tier) => tier switch
    {
        null => "?",
        PainTier.None => "None",
        PainTier.Mild => "Mild",
        PainTier.Moderate => "Moderate",
        PainTier.Severe => "Severe",
        PainTier.Shock => "Shock",
        _ => "?",
    };

    private void OpenChangeHolocardUI(BaseButton.ButtonEventArgs obj)
    {
        if (_player.LocalEntity is { } viewer)
            SendMessage(new OpenChangeHolocardUIEvent(_entities.GetNetEntity(viewer), _lastTarget));
    }

    private void AddGroup(Entity<DamageableComponent> damageable, RichTextLabel label, Color color, ProtoId<DamageGroupPrototype> group, string labelStr)
    {
        var msg = new FormattedMessage();
        msg.AddText($"{labelStr}: ");
        msg.PushColor(color);

        var damage = damageable.Comp.DamagePerGroup.GetValueOrDefault(group)
            .Int()
            .ToString(CultureInfo.InvariantCulture);
        if (_wounds.HasUntreated(damageable.Owner, group))
            msg.AddText($"{{{damage}}}");
        else
            msg.AddText($"{damage}");

        msg.Pop();
        label.SetMessage(msg);
    }

    private void MedicalAdvice(Entity<DamageableComponent> target, HealthScannerBuiState uiState, HealthScannerWindow window)
    {
        WoundedComponent? wounds = null;
        _entities.TryGetComponent(target, out wounds);
        bool hasBruteWounds = false;
        bool hasBurnWounds = false;

        if (wounds != null && _wounds.HasUntreated((target, wounds), wounds.BruteWoundGroup))
            hasBruteWounds = true;

        if (wounds != null && _wounds.HasUntreated((target, wounds), wounds.BurnWoundGroup))
            hasBurnWounds = true;

        if (_player.LocalEntity is not { } viewer)
            return;

        //Defibrilation related
        if (_mob.IsDead(target))
        {
            var thresholdsSystem = _entities.System<MobThresholdSystem>();

            if (thresholdsSystem.TryGetDeadThreshold(target, out var deadThreshold))
            {
                if (deadThreshold + 30 < target.Comp.Damage.GetTotal() && uiState.Chemicals != null
                    && !uiState.Chemicals.ContainsReagent("CMEpinephrine", null))
                {
                    AddAdvice(Loc.GetString("rmc-health-analyzer-advice-epinedrine"), window);
                }
                else
                {
                    string defib = String.Empty;
                    if (deadThreshold - 20 <= target.Comp.Damage.GetTotal() &&
                        wounds != null && !hasBruteWounds && !hasBurnWounds)
                        defib = Loc.GetString("rmc-health-analyzer-advice-defib-repeated");
                    else if (deadThreshold > target.Comp.Damage.GetTotal())
                        defib = Loc.GetString("rmc-health-analyzer-advice-defib");

                    if (defib != String.Empty && !_skills.HasAllSkills(viewer, DefibSkill))
                        defib = $"[color=#858585]{defib}[/color]";

                    if (defib != String.Empty)
                        AddAdvice(defib, window);
                }
            }

            AddAdvice(Loc.GetString("rmc-health-analyzer-advice-cpr"), window);
        }

        //Surgery related
        if (_entities.TryGetComponent(target, out HolocardStateComponent? holocardComponent) &&
            holocardComponent.HolocardStatus == HolocardStatus.Xeno)
        {
            string larvaSurgery = Loc.GetString("rmc-health-analyzer-advice-larva-surgery");
            if (!_skills.HasAllSkills(viewer, LarvaSurgerySkill))
                larvaSurgery = $"[color=#858585]{larvaSurgery}[/color]";
            AddAdvice(Loc.GetString("rmc-health-analyzer-advice-larva-surgery"), window);
        }

        //TODO RMC14 more surgery advice

        //Wound related
        if (hasBruteWounds)
            AddAdvice(Loc.GetString("rmc-health-analyzer-advice-brute-wounds"), window);

        if (hasBurnWounds)
            AddAdvice(Loc.GetString("rmc-health-analyzer-advice-burn-wounds"), window);

        //Blood related
        if (uiState.Blood < uiState.MaxBlood)
        {
            var bloodPercent = uiState.Blood / uiState.MaxBlood;

            if (bloodPercent < 0.85)
            {
                string bloodpack = Loc.GetString("rmc-health-analyzer-advice-blood-pack");
                if (!_skills.HasAllSkills(viewer, BloodPackSkill))
                    bloodpack = $"[color=#858585]{bloodpack}[/color]";
                AddAdvice(bloodpack, window);
            }

            if (bloodPercent < 0.9 && uiState.Chemicals != null && !uiState.Chemicals.ContainsReagent("Nutriment", null))
                AddAdvice(Loc.GetString("rmc-health-analyzer-advice-food"), window);
        }

        //TODO RMC14 Pain related medical advice

        //Damage related
        var airloss = target.Comp.DamagePerGroup.GetValueOrDefault("Airloss");
        var brute = target.Comp.DamagePerGroup.GetValueOrDefault("Brute");
        var burn = target.Comp.DamagePerGroup.GetValueOrDefault("Burn");
        var toxin = target.Comp.DamagePerGroup.GetValueOrDefault("Toxin");
        var genetic = target.Comp.DamagePerGroup.GetValueOrDefault("Genetic");

        if (airloss > 0 && !_mob.IsDead(target))
        {
            if (airloss > 10 && _mob.IsCritical(target))
                AddAdvice(Loc.GetString("rmc-health-analyzer-advice-cpr-crit"), window);

            if (airloss > 30 && uiState.Chemicals != null &&
                !uiState.Chemicals.ContainsReagent("CMDexalin", null))
                AddAdvice(Loc.GetString("rmc-health-analyzer-advice-dexalin"), window);
        }

        if (brute > 30 && uiState.Chemicals != null &&
            !uiState.Chemicals.ContainsReagent("CMBicaridine", null) &&
            !_mob.IsDead(target))
            AddAdvice(Loc.GetString("rmc-health-analyzer-advice-bicaridine"), window);

        if (burn > 30 && uiState.Chemicals != null &&
            !uiState.Chemicals.ContainsReagent("CMKelotane", null) &&
            !_mob.IsDead(target))
            AddAdvice(Loc.GetString("rmc-health-analyzer-advice-kelotane"), window);

        if (toxin > 10 && uiState.Chemicals != null &&
            !uiState.Chemicals.ContainsReagent("CMDylovene", null) && !uiState.Chemicals.ContainsReagent("Inaprovaline", null) &&
            !_mob.IsDead(target))
            AddAdvice(Loc.GetString("rmc-health-analyzer-advice-dylovene"), window);

        //TODO RMC14 Clone damage advice
    }

    private void AddAdvice(string text, HealthScannerWindow window)
    {
        var label = new RichTextLabel();
        label.SetMarkupPermissive(text);
        window.AdviceContainer.AddChild(label);
    }
}
