using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._CMU14.Medical.Presentation.Windows;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Ghost.Controls;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Medical.Treatment.Surgery;

[UsedImplicitly]
public sealed partial class CMUBodyScannerBui : BoundUserInterface
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPlayerManager _players = default!;

    private static readonly SoundPathSpecifier CorrectSound = new("/Audio/Machines/quickbeep.ogg");
    private static readonly SoundPathSpecifier WrongTimingSound = new("/Audio/Machines/warning_buzzer.ogg");
    private static readonly SoundPathSpecifier WrongLayerSound = new("/Audio/Machines/buzz-two.ogg");

    private SharedAudioSystem _audio = default!;
    private CMUBodyScannerWindow? _window;
    private CMUBodyScannerBuiState? _latestState;
    private string? _selectedLayer;
    private TimeSpan? _lastPlayedFeedbackAt;
    private CMUBodyScannerFeedbackKind _lastPlayedFeedbackKind;

    public CMUBodyScannerBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
        _audio = _entities.System<SharedAudioSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUBodyScannerWindow>();
        _window.Title = Loc.GetString("cmu-body-scanner-window-title");
        _window.ResetButton.OnPressed += ResetPressed;
        _window.EjectButton.OnPressed += EjectPressed;

        if (_latestState is { } state)
            Refresh(state);
        else if (State is CMUBodyScannerBuiState legacyState)
            Refresh(legacyState);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is not CMUBodyScannerStateMessage update)
            return;

        _latestState = update.State;
        Refresh(update.State);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CMUBodyScannerBuiState scanner)
        {
            _latestState = scanner;
            Refresh(scanner);
        }
    }

    private void Refresh(CMUBodyScannerBuiState state)
    {
        if (_window is null)
            return;

        var boostText = state.BoostExpiresAt is { } expires
            ? Loc.GetString("cmu-body-scanner-boost-active", ("time", FormatRemaining(expires)))
            : Loc.GetString("cmu-body-scanner-boost-inactive");

        _window.SetPatient(ResolvePatient(state.Patient), state.PatientName, state.Status, boostText, _entities, _players);
        _window.StatusLabel.Text = state.Status;
        _window.BoostLabel.Text = boostText;
        _window.ScanSummaryLabel.Text = state.CanScan
            ? Loc.GetString("cmu-body-scanner-diagnostic-summary", ("count", state.ScanLines.Count))
            : Loc.GetString("cmu-body-scanner-surgery1-required");

        var matched = state.Assignments.Count;
        var required = CountRealTargets(state);
        var locked = CalibrationLocked(state);
        var expired = !locked && CalibrationExpired(state);
        var started = state.CalibrationStartedAt is not null;
        var calibrated = state.BoostExpiresAt is not null;
        var remaining = FormatCalibrationRemaining(state);
        _window.PuzzleSummaryLabel.Text = state.CanScan
            ? calibrated && state.BoostExpiresAt is { } boostExpires
                ? Loc.GetString("cmu-body-scanner-calibrated-summary", ("time", FormatRemaining(boostExpires)))
                : locked && state.CalibrationLockoutExpiresAt is { } lockoutExpires
                ? Loc.GetString("cmu-body-scanner-lockout-summary", ("time", FormatRemaining(lockoutExpires)))
                : required == 0
                ? Loc.GetString("cmu-body-scanner-no-surgical-targets")
                : !started
                    ? Loc.GetString("cmu-body-scanner-match-summary-idle", ("matched", matched), ("required", required))
                    : Loc.GetString("cmu-body-scanner-match-summary", ("matched", matched), ("required", required), ("time", remaining))
            : Loc.GetString("cmu-body-scanner-surgery1-required");
        _window.PuzzleSummaryLabel.FontColorOverride = calibrated || state.PuzzleComplete
            ? CMUMedicalMachineStyle.Cyan
            : locked || expired ? CMUMedicalMachineStyle.Red : matched >= required && required > 0 ? CMUMedicalMachineStyle.Warning : CMUMedicalMachineStyle.Muted;

        _window.CalibrationButtonLabel.Text = started
            ? Loc.GetString("cmu-body-scanner-reset-button")
            : Loc.GetString("cmu-body-scanner-start-button");
        _window.ResetButton.Disabled = !state.CanScan || required == 0 || locked || started || calibrated;
        _window.EjectButton.Disabled = !state.CanScan || state.Patient is null;
        _window.SweepStatusOverlay.Visible = calibrated;
        _window.SweepStatusOverlay.Text = calibrated && state.BoostExpiresAt is { } badgeExpires
            ? Loc.GetString("cmu-body-scanner-calibrated-badge", ("time", FormatRemaining(badgeExpires)))
            : string.Empty;
        EnsureSelectedLayer(state);
        _window.SweepControl.SetState(state, _selectedLayer);
        PlayFeedbackSound(state);
        RefreshCalibrationPrompt(state, expired, locked);

        RefreshScan(state);
        RefreshPuzzle(state);
    }

    private void RefreshCalibrationPrompt(CMUBodyScannerBuiState state, bool expired, bool locked)
    {
        if (_window is null)
            return;

        if (!state.CanScan)
        {
            _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-surgery1-required");
            _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Muted;
            return;
        }

        if (CountRealTargets(state) == 0)
        {
            _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-no-surgical-targets-detail");
            _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Muted;
            return;
        }

        if (locked && state.CalibrationLockoutExpiresAt is { } lockoutExpires)
        {
            _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-lockout-status", ("time", FormatRemaining(lockoutExpires)));
            _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Red;
            return;
        }

        if (state.BoostExpiresAt is not null)
        {
            _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-complete-status");
            _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Cyan;
            return;
        }

        if (state.CalibrationStartedAt is null)
        {
            _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-start-status");
            _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Muted;
            return;
        }

        if (FeedbackActive(state, out var feedback))
        {
            switch (feedback)
            {
                case CMUBodyScannerFeedbackKind.Correct:
                    _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-feedback-correct");
                    _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Cyan;
                    break;
                case CMUBodyScannerFeedbackKind.WrongLayer:
                    _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-feedback-wrong-layer", ("seconds", state.LastPenaltySeconds));
                    _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Purple;
                    break;
                default:
                    _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-feedback-wrong-timing", ("seconds", state.LastPenaltySeconds));
                    _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Red;
                    break;
            }
            return;
        }

        if (PenaltyActive(state))
        {
            _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-penalty-status", ("seconds", state.LastPenaltySeconds));
            _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Red;
            return;
        }

        if (expired)
        {
            _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-expired-status");
            _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Red;
            return;
        }

        if (state.PuzzleComplete)
        {
            _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-complete-status");
            _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Cyan;
            return;
        }

        if (_selectedLayer is { } layer)
        {
            _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-armed-status", ("layer", GetLayerText(state, layer)));
            _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Warning;
            return;
        }

        _window.SweepDetailLabel.Text = Loc.GetString("cmu-body-scanner-ready-status");
        _window.SweepDetailLabel.FontColorOverride = CMUMedicalMachineStyle.Muted;
    }

    private void PlayFeedbackSound(CMUBodyScannerBuiState state)
    {
        if (!FeedbackActive(state, out var feedback) || state.LastFeedbackAt is not { } feedbackAt)
            return;

        if (_lastPlayedFeedbackAt == feedbackAt && _lastPlayedFeedbackKind == feedback)
            return;

        _lastPlayedFeedbackAt = feedbackAt;
        _lastPlayedFeedbackKind = feedback;

        var sound = feedback switch
        {
            CMUBodyScannerFeedbackKind.Correct => CorrectSound,
            CMUBodyScannerFeedbackKind.WrongLayer => WrongLayerSound,
            CMUBodyScannerFeedbackKind.WrongTiming => WrongTimingSound,
            _ => null,
        };

        if (sound is null)
            return;

        _audio.PlayGlobal(sound, Filter.Local(), false, AudioParams.Default.WithVolume(-7f));
    }

    private void EnsureSelectedLayer(CMUBodyScannerBuiState state)
    {
        if (!state.CanScan ||
            state.BoostExpiresAt is not null ||
            state.CalibrationStartedAt is null ||
            CountRealTargets(state) == 0 ||
            state.Terms.Count == 0)
        {
            _selectedLayer = null;
            return;
        }

        if (_selectedLayer is { } selected &&
            HasLayer(state, selected) &&
            (state.PuzzleComplete || CountUnlockedSignalsForLayer(state, selected) > 0))
        {
            return;
        }

        foreach (var layer in state.Terms)
        {
            if (CountUnlockedSignalsForLayer(state, layer.Id) <= 0)
                continue;

            _selectedLayer = layer.Id;
            return;
        }

        _selectedLayer = _selectedLayer is { } fallback && HasLayer(state, fallback)
            ? fallback
            : state.Terms[0].Id;
    }

    private EntityUid? ResolvePatient(NetEntity? patient)
    {
        if (patient is not { } netPatient)
            return null;

        var uid = _entities.GetEntity(netPatient);
        return uid.Valid ? uid : null;
    }

    private void ResetPressed(BaseButton.ButtonEventArgs args)
    {
        _selectedLayer = null;
        SendMessage(new CMUBodyScannerResetPuzzleMessage());
    }

    private void EjectPressed(BaseButton.ButtonEventArgs args)
    {
        SendMessage(new CMUBodyScannerEjectPatientMessage());
    }

    private void RefreshScan(CMUBodyScannerBuiState state)
    {
        if (_window is null)
            return;

        _window.ScanList.DisposeAllChildren();
        if (!state.CanScan)
        {
            _window.ScanList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-body-scanner-surgery1-required")));
            return;
        }

        if (state.ScanLines.Count == 0)
        {
            _window.ScanList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-body-scanner-no-scan-lines")));
            return;
        }

        _window.ScanList.AddChild(BuildScanBanner(state));
        AddScanSection(state, CMUBodyScannerScanCategory.Vitals, Loc.GetString("cmu-body-scanner-section-vitals"));
        AddScanSection(state, CMUBodyScannerScanCategory.Body, Loc.GetString("cmu-body-scanner-section-body"));
        AddScanSection(state, CMUBodyScannerScanCategory.Organs, Loc.GetString("cmu-body-scanner-section-organs"));
    }

    private void AddScanSection(CMUBodyScannerBuiState state, CMUBodyScannerScanCategory category, string title)
    {
        if (_window is null)
            return;

        var addedHeader = false;
        foreach (var line in state.ScanLines)
        {
            if (line.Category != category)
                continue;

            if (!addedHeader)
            {
                _window.ScanList.AddChild(BuildSectionHeader(title, CategoryAccent(category)));
                addedHeader = true;
            }

            _window.ScanList.AddChild(BuildDiagnosticRow(line));
        }
    }

    private Control BuildScanBanner(CMUBodyScannerBuiState state)
    {
        var severity = CMUBodyScannerScanSeverity.Stable;
        var concerns = new List<string>();

        foreach (var line in state.ScanLines)
        {
            var lineSeverity = GetLineSeverity(line);
            if (lineSeverity > severity)
                severity = lineSeverity;

            if (lineSeverity == CMUBodyScannerScanSeverity.Stable)
                continue;

            concerns.Add(line.Title);
        }

        var accent = SeverityAccent(severity);
        var titleText = severity switch
        {
            CMUBodyScannerScanSeverity.Critical => Loc.GetString("cmu-body-scanner-triage-critical"),
            CMUBodyScannerScanSeverity.Warning => Loc.GetString("cmu-body-scanner-triage-serious"),
            _ => Loc.GetString("cmu-body-scanner-triage-stable"),
        };

        var detail = concerns.Count == 0
            ? Loc.GetString("cmu-body-scanner-triage-clear")
            : string.Join(", ", concerns.GetRange(0, Math.Min(3, concerns.Count)));

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(7, 42),
            PanelOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        text.AddChild(new Label
        {
            Text = titleText,
            StyleClasses = { "LabelHeading" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
            ClipText = true,
        });
        text.AddChild(new Label
        {
            Text = detail,
            FontColorOverride = CMUMedicalMachineStyle.Muted,
            ClipText = true,
        });

        return CMUMedicalMachineStyle.Wrap(row, CMUMedicalMachineStyle.DeepCardBg, accent, new Thickness(9, 7), new Thickness(2));
    }

    private Control BuildSectionHeader(string title, Color accent)
    {
        return new Label
        {
            Text = title,
            StyleClasses = { "LabelHeading" },
            FontColorOverride = accent,
            Margin = new Thickness(2, 7, 2, 1),
            ClipText = true,
            HorizontalExpand = true,
        };
    }

    private Control BuildDiagnosticRow(CMUBodyScannerScanLine line)
    {
        return line.Category switch
        {
            CMUBodyScannerScanCategory.Body => BuildBodyPartCard(line),
            CMUBodyScannerScanCategory.Organs => BuildDiagnosticCard(line.Title, line.Detail, GetScanLineAccent(line)),
            _ => CMUMedicalMachineStyle.Metric(line.Title, line.Detail, GetScanLineAccent(line)),
        };
    }

    private Control BuildBodyPartCard(CMUBodyScannerScanLine line)
    {
        var severity = line.Severity;
        var accent = SeverityAccent(severity);

        var stack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            HorizontalExpand = true,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        stack.AddChild(row);

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 32),
            PanelOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        });

        row.AddChild(new Label
        {
            Text = line.Title,
            MinWidth = 90,
            HorizontalExpand = true,
            FontColorOverride = CMUMedicalMachineStyle.Text,
            ClipText = true,
            VerticalAlignment = Control.VAlignment.Center,
        });

        if (line.HasRange)
        {
            row.AddChild(new Label
            {
                Text = SeverityText(severity),
                MinWidth = 66,
                FontColorOverride = accent,
                ClipText = true,
                VerticalAlignment = Control.VAlignment.Center,
            });

            var hpRow = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 8,
                Margin = new Thickness(13, 0, 0, 0),
                HorizontalExpand = true,
            };
            stack.AddChild(hpRow);

            hpRow.AddChild(new Label
            {
                Text = Loc.GetString(
                    "cmu-body-scanner-part-health",
                    ("current", line.Current),
                    ("max", line.Maximum)),
                MinWidth = 150,
                HorizontalExpand = true,
                ClipText = true,
                VerticalAlignment = Control.VAlignment.Center,
            });
            hpRow.AddChild(BuildHpBar(line.Current, line.Maximum, accent));
        }
        else
        {
            row.AddChild(new Label
            {
                Text = line.Detail,
                HorizontalExpand = true,
                ClipText = true,
                VerticalAlignment = Control.VAlignment.Center,
            });
        }

        var chips = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            Margin = new Thickness(13, 2, 0, 0),
            HorizontalExpand = true,
        };

        foreach (var piece in line.Details)
            chips.AddChild(BuildScanChip(piece, accent));

        if (chips.ChildCount > 0)
            stack.AddChild(chips);

        return CMUMedicalMachineStyle.Wrap(stack, CMUMedicalMachineStyle.DeepCardBg, CMUMedicalMachineStyle.MutedBorder, new Thickness(7, 5));
    }

    private Control BuildDiagnosticCard(string title, string detail, Color accent)
    {
        var stack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            HorizontalExpand = true,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        stack.AddChild(row);

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 30),
            PanelOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        });
        row.AddChild(new Label
        {
            Text = title,
            MinWidth = 112,
            FontColorOverride = CMUMedicalMachineStyle.Text,
            ClipText = true,
            VerticalAlignment = Control.VAlignment.Center,
        });
        row.AddChild(new Label
        {
            Text = detail,
            HorizontalExpand = true,
            ClipText = true,
            VerticalAlignment = Control.VAlignment.Center,
        });

        return CMUMedicalMachineStyle.Wrap(stack, CMUMedicalMachineStyle.DeepCardBg, CMUMedicalMachineStyle.MutedBorder, new Thickness(7, 5));
    }

    private static Control BuildHpBar(float current, float max, Color accent)
    {
        const int trackWidth = 72;
        const int barHeight = 10;
        var pct = max <= 0f ? 0f : Math.Clamp(current / max, 0f, 1f);
        var track = new PanelContainer
        {
            MinSize = new Vector2(trackWidth, barHeight),
            VerticalAlignment = Control.VAlignment.Center,
            PanelOverride = CMUMedicalMachineStyle.Flat(Color.FromHex("#223039"), Color.FromHex("#314B55")),
        };

        var fillWidth = (int)Math.Round(trackWidth * pct);
        if (fillWidth <= 0)
            return track;

        var fill = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        fill.AddChild(new PanelContainer
        {
            MinSize = new Vector2(fillWidth, barHeight),
            PanelOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        });
        track.AddChild(fill);
        return track;
    }

    private static Control BuildScanChip(string text, Color accent)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = CMUMedicalMachineStyle.Flat(Color.FromHex("#1B242A"), accent),
        };
        panel.AddChild(new Label
        {
            Text = text,
            ClipText = true,
            HorizontalExpand = true,
            Margin = new Thickness(6, 2),
            FontColorOverride = CMUMedicalMachineStyle.Text,
        });
        return panel;
    }

    private void RefreshPuzzle(CMUBodyScannerBuiState state)
    {
        if (_window is null)
            return;

        _window.TermList.DisposeAllChildren();
        _window.TargetList.DisposeAllChildren();

        if (!state.CanScan)
        {
            _window.TermList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-body-scanner-surgery1-required")));
            _window.TargetList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-body-scanner-surgery1-required")));
            return;
        }

        if (CountRealTargets(state) == 0)
        {
            _window.TermList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-body-scanner-no-surgical-targets")));
            _window.TargetList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-body-scanner-no-surgical-targets-detail")));
            return;
        }

        var lockedOut = CalibrationLocked(state);
        var expired = !lockedOut && CalibrationExpired(state);
        var started = state.CalibrationStartedAt is not null;
        var calibrated = state.BoostExpiresAt is not null;
        foreach (var layer in state.Terms)
        {
            var selected = _selectedLayer == layer.Id;
            var total = CountSignalsForLayer(state, layer.Id);
            var layerLocked = CountLockedSignalsForLayer(state, layer.Id);
            var button = BuildLayerButton(
                layer.Text,
                total == 0
                    ? Loc.GetString("cmu-body-scanner-layer-empty")
                    : selected
                        ? Loc.GetString("cmu-body-scanner-layer-selected", ("locked", layerLocked), ("total", total))
                        : Loc.GetString("cmu-body-scanner-layer-ready", ("locked", layerLocked), ("total", total)),
                layerLocked >= total && total > 0 ? CMUMedicalMachineStyle.Cyan : selected ? CMUMedicalMachineStyle.Warning : CMUMedicalMachineStyle.Blue,
                selected,
                !started || expired || lockedOut || calibrated);
            var captured = layer.Id;
            button.OnPressed += _ =>
            {
                if (!started || lockedOut || calibrated)
                    return;

                _selectedLayer = captured;
                RefreshPuzzle(state);
            };
            _window.TermList.AddChild(button);
        }

        if (calibrated)
        {
            _window.TargetList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-body-scanner-complete-status")));
            return;
        }

        if (!started && !lockedOut)
        {
            _window.TargetList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-body-scanner-start-status")));
            return;
        }

        _window.TargetList.AddChild(BuildActiveSliceTimer(state, expired, lockedOut));
        if (lockedOut)
            return;

        if (_selectedLayer is not { } selectedLayer)
        {
            _window.TargetList.AddChild(CMUMedicalMachineStyle.Empty(Loc.GetString("cmu-body-scanner-ready-status")));
            return;
        }

        var addedSignal = false;
        foreach (var signal in state.Targets)
        {
            if (signal.LayerId != selectedLayer || signal.IsDecoy)
                continue;

            addedSignal = true;
            var assigned = TryGetAssignmentForSignal(state, signal.Id, out _);
            var button = BuildSignalButton(
                signal.Text,
                assigned
                    ? Loc.GetString("cmu-body-scanner-signal-locked")
                    : Loc.GetString("cmu-body-scanner-signal-ready", ("detail", signal.Detail)),
                assigned ? CMUMedicalMachineStyle.Cyan : CMUMedicalMachineStyle.Purple,
                assigned,
                assigned || expired);
            var signalCaptured = signal.Id;
            button.OnPressed += _ =>
            {
                if (assigned || expired)
                    return;

                SendMessage(new CMUBodyScannerConfirmPuzzleMessage(selectedLayer, signalCaptured, GetPulsePhase(state)));
            };
            _window.TargetList.AddChild(button);
        }

        var addedDecoy = 0;
        foreach (var signal in state.Targets)
        {
            if (signal.LayerId != selectedLayer || !signal.IsDecoy || addedDecoy >= 2)
                continue;

            addedSignal = true;
            addedDecoy++;
            var button = BuildSignalButton(
                signal.Text,
                Loc.GetString("cmu-body-scanner-decoy-ready", ("detail", signal.Detail)),
                CMUMedicalMachineStyle.Purple,
                false,
                expired);
            var signalCaptured = signal.Id;
            button.OnPressed += _ =>
            {
                if (expired)
                    return;

                SendMessage(new CMUBodyScannerConfirmPuzzleMessage(selectedLayer, signalCaptured, GetPulsePhase(state)));
            };
            _window.TargetList.AddChild(button);
        }

        var interferenceAdded = 0;
        foreach (var signal in state.Targets)
        {
            if (signal.IsDecoy ||
                signal.LayerId == selectedLayer ||
                TryGetAssignmentForSignal(state, signal.Id, out _) ||
                interferenceAdded >= 2)
            {
                continue;
            }

            addedSignal = true;
            interferenceAdded++;
            var button = BuildSignalButton(
                Loc.GetString("cmu-body-scanner-interference-title"),
                Loc.GetString("cmu-body-scanner-interference-detail", ("layer", GetLayerText(state, selectedLayer))),
                CMUMedicalMachineStyle.Purple,
                false,
                expired);
            var signalCaptured = signal.Id;
            button.OnPressed += _ =>
            {
                if (expired)
                    return;

                SendMessage(new CMUBodyScannerConfirmPuzzleMessage(selectedLayer, signalCaptured, GetPulsePhase(state)));
            };
            _window.TargetList.AddChild(button);
        }

        if (!addedSignal)
            _window.TargetList.AddChild(CMUMedicalMachineStyle.Empty(
                Loc.GetString("cmu-body-scanner-no-layer-signals", ("layer", GetLayerText(state, selectedLayer)))));
    }

    private Button BuildLayerButton(string title, string subtitle, Color accent, bool active, bool disabled = false)
    {
        var button = new Button
        {
            HorizontalExpand = true,
            MinHeight = 42,
            ModulateSelfOverride = active ? Color.White : Color.FromHex("#CDD6DE"),
            Disabled = disabled,
        };

        var panel = CMUMedicalMachineStyle.Panel(
            active ? Color.FromHex("#1B2228") : CMUMedicalMachineStyle.DeepCardBg,
            accent,
            active ? new Thickness(2) : new Thickness(1));

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 7,
            Margin = new Thickness(7, 4),
            HorizontalExpand = true,
        };
        panel.AddChild(row);

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(10, 10),
            VerticalAlignment = Control.VAlignment.Center,
            PanelOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        text.AddChild(new Label
        {
            Text = title,
            ClipText = true,
            HorizontalExpand = true,
            FontColorOverride = CMUMedicalMachineStyle.Text,
        });
        text.AddChild(new Label
        {
            Text = subtitle,
            ClipText = true,
            HorizontalExpand = true,
            StyleClasses = { "LabelSubText" },
            FontColorOverride = CMUMedicalMachineStyle.Muted,
        });

        button.AddChild(panel);
        return button;
    }

    private Button BuildSignalButton(string title, string subtitle, Color accent, bool active, bool disabled = false)
    {
        var button = new Button
        {
            HorizontalExpand = true,
            MinHeight = 58,
            ModulateSelfOverride = active ? Color.White : Color.FromHex("#CDD6DE"),
            Disabled = disabled,
        };

        var panel = CMUMedicalMachineStyle.Panel(
            active ? Color.FromHex("#15262A") : Color.FromHex("#0E171D"),
            accent,
            active ? new Thickness(2) : new Thickness(1));

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(8, 6),
            HorizontalExpand = true,
        };
        panel.AddChild(row);

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 42),
            PanelOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        text.AddChild(new Label
        {
            Text = title,
            ClipText = true,
            HorizontalExpand = true,
            FontColorOverride = CMUMedicalMachineStyle.Text,
        });
        text.AddChild(new Label
        {
            Text = subtitle,
            ClipText = true,
            HorizontalExpand = true,
            StyleClasses = { "LabelSubText" },
            FontColorOverride = active ? accent : CMUMedicalMachineStyle.Muted,
        });

        button.AddChild(panel);
        return button;
    }

    private Control BuildActiveSliceTimer(CMUBodyScannerBuiState state, bool expired, bool locked)
    {
        var accent = locked || expired ? CMUMedicalMachineStyle.Red : state.PuzzleComplete ? CMUMedicalMachineStyle.Cyan : CMUMedicalMachineStyle.Warning;
        var heading = locked
            ? Loc.GetString("cmu-body-scanner-timer-locked")
            : expired
                ? Loc.GetString("cmu-body-scanner-timer-expired")
                : Loc.GetString("cmu-body-scanner-timer-active");
        var value = locked && state.CalibrationLockoutExpiresAt is { } lockoutExpires
            ? FormatRemaining(lockoutExpires)
            : expired
                ? "0:00"
                : FormatCalibrationRemaining(state);
        var detail = locked
            ? Loc.GetString("cmu-body-scanner-lockout-detail")
            : state.PuzzleComplete
                ? Loc.GetString("cmu-body-scanner-complete-status")
                : Loc.GetString("cmu-body-scanner-timer-detail");

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 9,
            HorizontalExpand = true,
        };

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(6, 48),
            PanelOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        text.AddChild(new Label
        {
            Text = heading,
            StyleClasses = { "LabelSubText" },
            FontColorOverride = accent,
            ClipText = true,
            HorizontalExpand = true,
        });
        text.AddChild(new Label
        {
            Text = value,
            StyleClasses = { "LabelKeyText" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
            ClipText = true,
            HorizontalExpand = true,
        });
        text.AddChild(new Label
        {
            Text = detail,
            StyleClasses = { "LabelSubText" },
            FontColorOverride = CMUMedicalMachineStyle.Muted,
            ClipText = true,
            HorizontalExpand = true,
        });

        return CMUMedicalMachineStyle.Wrap(row, Color.FromHex("#121D23"), accent, new Thickness(8, 6), new Thickness(2));
    }

    private static bool TryGetAssignmentForSignal(CMUBodyScannerBuiState state, string signalId, out string layerId)
    {
        foreach (var assignment in state.Assignments)
        {
            if (assignment.SignalId != signalId)
                continue;

            layerId = assignment.LayerId;
            return true;
        }

        layerId = string.Empty;
        return false;
    }

    private static int CountSignalsForLayer(CMUBodyScannerBuiState state, string layerId)
    {
        var count = 0;
        foreach (var signal in state.Targets)
        {
            if (signal.LayerId == layerId && !signal.IsDecoy)
                count++;
        }

        return count;
    }

    private static int CountRealTargets(CMUBodyScannerBuiState state)
    {
        var count = 0;
        foreach (var signal in state.Targets)
        {
            if (!signal.IsDecoy)
                count++;
        }

        return count;
    }

    private static int CountLockedSignalsForLayer(CMUBodyScannerBuiState state, string layerId)
    {
        var count = 0;
        foreach (var assignment in state.Assignments)
        {
            if (assignment.LayerId == layerId)
                count++;
        }

        return count;
    }

    private static int CountUnlockedSignalsForLayer(CMUBodyScannerBuiState state, string layerId)
    {
        var count = 0;
        foreach (var signal in state.Targets)
        {
            if (signal.IsDecoy || signal.LayerId != layerId || TryGetAssignmentForSignal(state, signal.Id, out _))
                continue;

            count++;
        }

        return count;
    }

    private static bool HasLayer(CMUBodyScannerBuiState state, string layerId)
    {
        foreach (var layer in state.Terms)
        {
            if (layer.Id == layerId)
                return true;
        }

        return false;
    }

    private static string GetLayerText(CMUBodyScannerBuiState state, string layerId)
    {
        foreach (var term in state.Terms)
        {
            if (term.Id == layerId)
                return term.Text;
        }

        return layerId;
    }

    private static CMUBodyScannerScanSeverity GetLineSeverity(CMUBodyScannerScanLine line)
    {
        return line.Severity;
    }

    private static Color SeverityAccent(CMUBodyScannerScanSeverity severity)
    {
        return severity switch
        {
            CMUBodyScannerScanSeverity.Critical => CMUMedicalMachineStyle.Red,
            CMUBodyScannerScanSeverity.Warning => CMUMedicalMachineStyle.Warning,
            _ => CMUMedicalMachineStyle.Cyan,
        };
    }

    private static string SeverityText(CMUBodyScannerScanSeverity severity)
    {
        return severity switch
        {
            CMUBodyScannerScanSeverity.Critical => Loc.GetString("cmu-body-scanner-health-critical"),
            CMUBodyScannerScanSeverity.Warning => Loc.GetString("cmu-body-scanner-health-damaged"),
            _ => Loc.GetString("cmu-body-scanner-health-stable"),
        };
    }

    private static Color GetScanLineAccent(CMUBodyScannerScanLine line)
    {
        return line.Severity switch
        {
            CMUBodyScannerScanSeverity.Critical => CMUMedicalMachineStyle.Red,
            CMUBodyScannerScanSeverity.Warning => CMUMedicalMachineStyle.Warning,
            _ => line.Kind switch
            {
                CMUBodyScannerScanKind.Organ or CMUBodyScannerScanKind.MissingOrgan => CMUMedicalMachineStyle.Purple,
                CMUBodyScannerScanKind.Heart or CMUBodyScannerScanKind.Blood => CMUMedicalMachineStyle.Red,
                _ => CMUMedicalMachineStyle.Cyan,
            },
        };
    }

    private static Color CategoryAccent(CMUBodyScannerScanCategory category)
    {
        return category switch
        {
            CMUBodyScannerScanCategory.Body => CMUMedicalMachineStyle.Warning,
            CMUBodyScannerScanCategory.Organs => CMUMedicalMachineStyle.Purple,
            _ => CMUMedicalMachineStyle.Cyan,
        };
    }

    private static bool CalibrationExpired(CMUBodyScannerBuiState state)
    {
        if (state.CalibrationEndsAt is not { } endsAt)
            return false;

        var timing = IoCManager.Resolve<IGameTiming>();
        return timing.CurTime >= endsAt;
    }

    private static bool CalibrationLocked(CMUBodyScannerBuiState state)
    {
        if (state.CalibrationLockoutExpiresAt is not { } expiresAt)
            return false;

        var timing = IoCManager.Resolve<IGameTiming>();
        return timing.CurTime < expiresAt;
    }

    private static bool PenaltyActive(CMUBodyScannerBuiState state)
    {
        if (state.LastPenaltyAt is not { } lastPenalty || state.LastPenaltySeconds <= 0f)
            return false;

        var timing = IoCManager.Resolve<IGameTiming>();
        return (timing.CurTime - lastPenalty).TotalSeconds < 1.4f;
    }

    private static bool FeedbackActive(CMUBodyScannerBuiState state, out CMUBodyScannerFeedbackKind feedback)
    {
        feedback = state.LastFeedbackKind;
        if (feedback == CMUBodyScannerFeedbackKind.None || state.LastFeedbackAt is not { } lastFeedback)
            return false;

        var timing = IoCManager.Resolve<IGameTiming>();
        return lastFeedback != TimeSpan.Zero && (timing.CurTime - lastFeedback).TotalSeconds < 0.85f;
    }

    private static string FormatCalibrationRemaining(CMUBodyScannerBuiState state)
    {
        if (state.CalibrationEndsAt is not { } endsAt)
            return Loc.GetString("cmu-body-scanner-calibration-ready");

        return FormatRemaining(endsAt);
    }

    private static float GetPulsePhase(CMUBodyScannerBuiState state)
    {
        if (state.PulseStartedAt is not { } startedAt)
            return 0f;

        var period = MathF.Max(0.1f, state.PulsePeriod);
        var timing = IoCManager.Resolve<IGameTiming>();
        var elapsed = (timing.CurTime - startedAt).TotalSeconds;
        var phase = (float)(elapsed / period);
        phase -= MathF.Floor(phase);
        return phase;
    }

    private static string FormatRemaining(TimeSpan expiresAt)
    {
        var timing = IoCManager.Resolve<IGameTiming>();
        var remaining = expiresAt - timing.CurTime;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        var totalSeconds = (int) Math.Ceiling(remaining.TotalSeconds);
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:00}";
    }
}

public sealed partial class CMUUprightSpriteView : SpriteView
{
    protected override void Draw(IRenderHandle renderHandle)
    {
        if (Sprite is not { } sprite)
        {
            base.Draw(renderHandle);
            return;
        }

        var oldRotation = sprite.Rotation;
        if (oldRotation == Angle.Zero)
        {
            base.Draw(renderHandle);
            return;
        }

#pragma warning disable CS0618
        sprite.Rotation = Angle.Zero;
        try
        {
            base.Draw(renderHandle);
        }
        finally
        {
            sprite.Rotation = oldRotation;
        }
#pragma warning restore CS0618
    }
}

public sealed partial class CMUScannerSweepControl : Control
{
    private const float SpriteScanWidthRatio = 0.58f;
    private const float SpriteScanHeightRatio = 0.70f;

    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan? _pulseStartedAt;
    private TimeSpan? _calibrationEndsAt;
    private TimeSpan? _lastPenaltyAt;
    private TimeSpan? _lastFeedbackAt;
    private CMUBodyScannerFeedbackKind _lastFeedbackKind;
    private string? _selectedLayer;
    private float _pulsePeriod = 2.4f;
    private float _targetPhase = 0.25f;
    private float _windowSize = 0.26f;
    private float _graceSize = 0.12f;
    private float _lastPenaltySeconds;
    private int _locked;
    private int _total;

    public CMUScannerSweepControl()
    {
        IoCManager.InjectDependencies(this);
    }

    public void SetState(CMUBodyScannerBuiState state, string? selectedLayer)
    {
        _pulseStartedAt = state.PulseStartedAt;
        _calibrationEndsAt = state.CalibrationEndsAt;
        _pulsePeriod = state.PulsePeriod;
        _targetPhase = state.PulseTargetPhase;
        _windowSize = state.PulseWindowSize;
        _graceSize = state.PulseGraceSize;
        _lastPenaltyAt = state.LastPenaltyAt;
        _lastPenaltySeconds = state.LastPenaltySeconds;
        _lastFeedbackAt = state.LastFeedbackAt;
        _lastFeedbackKind = state.LastFeedbackKind;
        _selectedLayer = selectedLayer;
        _locked = state.Assignments.Count;
        _total = 0;
        foreach (var target in state.Targets)
        {
            if (!target.IsDecoy)
                _total++;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var previewScale = GetPreviewScale(size);
        if (previewScale <= 0f)
            return;

        var active = _total > 0 && _calibrationEndsAt is { } endsAt && _timing.CurTime < endsAt;
        var scanRect = GetSpriteScanRect(size);
        var feedbackActive = _lastFeedbackAt is { } feedbackAt &&
                             feedbackAt != TimeSpan.Zero &&
                             (_timing.CurTime - feedbackAt).TotalSeconds < 0.85f;
        var penaltyActive = _lastPenaltyAt is { } penaltyAt &&
                            _lastPenaltySeconds > 0f &&
                            (_timing.CurTime - penaltyAt).TotalSeconds < 1.4f;

        DrawScannerFrame(handle, scanRect, active, _selectedLayer, previewScale);

        if (_total <= 0 || _pulseStartedAt is null)
        {
            return;
        }

        if (active)
        {
            DrawPhaseBand(handle,
                scanRect,
                _targetPhase,
                _windowSize,
                CMUMedicalMachineStyle.Cyan.WithAlpha(0.9f),
                previewScale);

            var phase = CurrentPhase();
            var y = scanRect.Top + scanRect.Height * phase;
            var lineOverhang = 3f * previewScale;
            var markerWidth = 3f * previewScale;
            var markerHalfHeight = 2f * previewScale;
            handle.DrawLine(
                new Vector2(scanRect.Left - lineOverhang, y),
                new Vector2(scanRect.Right + lineOverhang, y),
                CMUMedicalMachineStyle.Text);
            handle.DrawRect(
                new UIBox2(
                    scanRect.Left - lineOverhang - markerWidth,
                    y - markerHalfHeight,
                    scanRect.Left - lineOverhang,
                    y + markerHalfHeight),
                CMUMedicalMachineStyle.Text);
            handle.DrawRect(
                new UIBox2(
                    scanRect.Right + lineOverhang,
                    y - markerHalfHeight,
                    scanRect.Right + lineOverhang + markerWidth,
                    y + markerHalfHeight),
                CMUMedicalMachineStyle.Text);
        }

        if (feedbackActive)
        {
            switch (_lastFeedbackKind)
            {
                case CMUBodyScannerFeedbackKind.Correct:
                    handle.DrawRect(Enlarge(scanRect, 4f * previewScale), CMUMedicalMachineStyle.Cyan.WithAlpha(0.42f), false);
                    break;
                case CMUBodyScannerFeedbackKind.WrongLayer:
                    DrawStatic(handle, scanRect, CMUMedicalMachineStyle.Purple, previewScale);
                    handle.DrawRect(Enlarge(scanRect, 4f * previewScale), CMUMedicalMachineStyle.Purple.WithAlpha(0.5f), false);
                    break;
                case CMUBodyScannerFeedbackKind.WrongTiming:
                    handle.DrawRect(Enlarge(scanRect, 4f * previewScale), CMUMedicalMachineStyle.Red.WithAlpha(0.6f), false);
                    break;
            }
        }
        else if (penaltyActive)
        {
            handle.DrawRect(Enlarge(scanRect, 4f * previewScale), CMUMedicalMachineStyle.Red.WithAlpha(0.4f), false);
        }
    }

    private float CurrentPhase()
    {
        if (_pulseStartedAt is not { } startedAt)
            return 0f;

        var period = MathF.Max(0.1f, _pulsePeriod);
        var phase = (float)((_timing.CurTime - startedAt).TotalSeconds / period);
        phase -= MathF.Floor(phase);
        return phase;
    }

    private static UIBox2 GetSpriteScanRect(Vector2 size)
    {
        // SpriteView draws its entity at the center of its pixel box. Center
        // the scanner on that same box instead of carrying resolution-specific
        // offsets that can drift on different UI scales.
        var center = size / 2f;
        var halfWidth = size.X * SpriteScanWidthRatio / 2f;
        var halfHeight = size.Y * SpriteScanHeightRatio / 2f;
        return new UIBox2(center.X - halfWidth, center.Y - halfHeight, center.X + halfWidth, center.Y + halfHeight);
    }

    private static float GetPreviewScale(Vector2 size)
    {
        if (size.X <= 0f || size.Y <= 0f)
            return 0f;

        return MathF.Min(size.X / 96f, size.Y / 110f);
    }

    private static void DrawScannerFrame(DrawingHandleScreen handle, UIBox2 scanRect, bool active, string? selectedLayer, float scale)
    {
        var color = selectedLayer switch
        {
            "vitals" => CMUMedicalMachineStyle.Cyan,
            "skeleton" => CMUMedicalMachineStyle.Warning,
            "organs" => CMUMedicalMachineStyle.Purple,
            "tissue" => CMUMedicalMachineStyle.Blue,
            _ => CMUMedicalMachineStyle.Muted,
        };

        var frame = color.WithAlpha(active ? 0.46f : 0.22f);
        var railOffset = 4f * scale;
        DrawBracket(handle, scanRect, frame, 7f * scale);
        handle.DrawLine(
            new Vector2(scanRect.Left - railOffset, scanRect.Top),
            new Vector2(scanRect.Left - railOffset, scanRect.Bottom),
            frame.WithAlpha(frame.A * 0.55f));
        handle.DrawLine(
            new Vector2(scanRect.Right + railOffset, scanRect.Top),
            new Vector2(scanRect.Right + railOffset, scanRect.Bottom),
            frame.WithAlpha(frame.A * 0.55f));
    }

    private static void DrawPhaseBand(
        DrawingHandleScreen handle,
        UIBox2 scanRect,
        float centerPhase,
        float size,
        Color color,
        float scale)
    {
        var half = Math.Clamp(size, 0f, 1f) / 2f;
        centerPhase = Math.Clamp(centerPhase, half, 1f - half);
        DrawNormalizedBand(handle, scanRect, centerPhase - half, centerPhase + half, color, scale);
    }

    private static void DrawNormalizedBand(DrawingHandleScreen handle, UIBox2 scanRect, float start, float end, Color color, float scale)
    {
        start = Math.Clamp(start, 0f, 1f);
        end = Math.Clamp(end, 0f, 1f);
        if (end <= start)
            return;

        var top = scanRect.Top + scanRect.Height * start;
        var bottom = scanRect.Top + scanRect.Height * end;
        var overhang = 4f * scale;
        var rect = new UIBox2(scanRect.Left - overhang, top, scanRect.Right + overhang, bottom);
        handle.DrawRect(rect, color.WithAlpha(0.12f));
        handle.DrawLine(new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top), color);
        handle.DrawLine(new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Right, rect.Bottom), color);
    }

    private void DrawStatic(DrawingHandleScreen handle, UIBox2 scanRect, Color color, float scale)
    {
        var spacing = 18f * scale;
        var offset = (float)((_timing.CurTime.TotalSeconds * 42d * scale) % spacing);
        var overhang = 10f * scale;
        for (var i = -4; i < 13; i++)
        {
            var y = scanRect.Top + i * spacing + offset;
            handle.DrawLine(
                new Vector2(scanRect.Left - overhang, y),
                new Vector2(scanRect.Right + overhang, y + 9f * scale),
                color.WithAlpha(0.28f));
        }
    }

    private static UIBox2 Enlarge(UIBox2 rect, float amount)
    {
        return new UIBox2(rect.Left - amount, rect.Top - amount, rect.Right + amount, rect.Bottom + amount);
    }

    private static void DrawBracket(DrawingHandleScreen handle, UIBox2 rect, Color color, float length)
    {
        handle.DrawLine(new Vector2(rect.Left, rect.Top), new Vector2(rect.Left + length, rect.Top), color);
        handle.DrawLine(new Vector2(rect.Left, rect.Top), new Vector2(rect.Left, rect.Top + length), color);
        handle.DrawLine(new Vector2(rect.Right, rect.Top), new Vector2(rect.Right - length, rect.Top), color);
        handle.DrawLine(new Vector2(rect.Right, rect.Top), new Vector2(rect.Right, rect.Top + length), color);
        handle.DrawLine(new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Left + length, rect.Bottom), color);
        handle.DrawLine(new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Left, rect.Bottom - length), color);
        handle.DrawLine(new Vector2(rect.Right, rect.Bottom), new Vector2(rect.Right - length, rect.Bottom), color);
        handle.DrawLine(new Vector2(rect.Right, rect.Bottom), new Vector2(rect.Right, rect.Bottom - length), color);
    }
}

public sealed partial class CMUBodyScannerWindow : FancyWindow
{
    private const string RememberedSizeKey = "cmu-body-scanner";
    private static readonly Vector2 PreferredWindowSize = new(1040f, 680f);
    private static readonly Vector2 MinimumWindowSize = new(680f, 440f);
    private static readonly Vector2 PatientPreviewSize = new(88f, 88f);
    private static readonly Vector2 PatientPreviewScale = new(1.55f, 1.55f);
    private static readonly Vector2 ScanPreviewFrameSize = new(96f, 110f);
    private static readonly Vector2 ScanPatientPreviewSize = new(92f, 106f);
    private static readonly Vector2 ScanPatientPreviewScale = new(2.15f, 2.15f);

    [Dependency] private IResourceCache _resourceCache = default!;

    private readonly CMUMedicalUniformScaler _uniformScaler = new();
    private readonly PanelContainer _scaleRoot;
    private readonly SpriteView _patientPreview;
    private readonly Label _previewFallbackLabel;
    private readonly SpriteView _scanPatientPreview;
    private readonly Label _scanPreviewFallbackLabel;

    public readonly Label PatientLabel;
    public readonly Label StatusLabel;
    public readonly Label BoostLabel;
    public readonly Label ScanSummaryLabel;
    public readonly Label PuzzleSummaryLabel;
    public readonly Label SweepDetailLabel;
    public readonly Label SweepStatusOverlay;
    public readonly CMUScannerSweepControl SweepControl;
    public readonly BoxContainer ScanList;
    public readonly BoxContainer TermList;
    public readonly BoxContainer TargetList;
    public readonly Button ResetButton;
    public readonly Label CalibrationButtonLabel;
    public readonly Button EjectButton;

    private float _layoutScale = 1f;

    public CMUBodyScannerWindow()
    {
        IoCManager.InjectDependencies(this);
        AllowDraggingOutsideParentBounds = true;

        SetSize = CMUMedicalWindowSizing.GetInitialSize(RememberedSizeKey, PreferredWindowSize);
        MinSize = MinimumWindowSize;
        SetCloseButtonAppearance(CMUMedicalMachineStyle.Text, new Vector2(18f, 18f));

        _scaleRoot = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = CMUMedicalMachineStyle.Flat(CMUMedicalMachineStyle.Surface, CMUMedicalMachineStyle.Border),
        };
        AddChild(_scaleRoot);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(12),
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _scaleRoot.AddChild(root);

        var titleBar = CMUMedicalMachineStyle.WindowHeader(Loc.GetString("cmu-body-scanner-window-title"), out var closeButton);
        closeButton.OnPressed += _ => Close();
        root.AddChild(titleBar);

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
            MinHeight = 146,
        };
        root.AddChild(header);

        var patientCard = CMUMedicalMachineStyle.Panel(Color.FromHex("#101922"), Color.FromHex("#345064"), new Thickness(2));
        patientCard.MinWidth = 420;
        patientCard.HorizontalExpand = true;
        header.AddChild(patientCard);

        var patientRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(10),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        patientCard.AddChild(patientRow);

        var previewFrame = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.WindowBg, CMUMedicalMachineStyle.Cyan);
        previewFrame.MinSize = new Vector2(92, 92);
        previewFrame.HorizontalExpand = false;
        patientRow.AddChild(previewFrame);

        _patientPreview = new CMUUprightSpriteView
        {
            SetSize = PatientPreviewSize,
            OverrideDirection = Direction.South,
            WorldRotation = Angle.Zero,
            EyeRotation = Angle.Zero,
            Stretch = SpriteView.StretchMode.Fit,
            Scale = PatientPreviewScale,
        };
        previewFrame.AddChild(_patientPreview);

        _previewFallbackLabel = new Label
        {
            Text = Loc.GetString("cmu-body-scanner-no-patient"),
            Align = Label.AlignMode.Center,
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalAlignment = Control.HAlignment.Center,
            FontColorOverride = CMUMedicalMachineStyle.Dim,
            Visible = false,
        };
        previewFrame.AddChild(_previewFallbackLabel);

        var patientText = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        patientRow.AddChild(patientText);

        patientText.AddChild(new Label
        {
            Text = Loc.GetString("cmu-medical-surgery-section-patient"),
            StyleClasses = { "LabelSubText" },
            FontColorOverride = CMUMedicalMachineStyle.Muted,
        });

        PatientLabel = new Label
        {
            StyleClasses = { "LabelHeading" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
            ClipText = true,
            HorizontalExpand = true,
        };
        patientText.AddChild(PatientLabel);

        StatusLabel = new Label
        {
            FontColorOverride = CMUMedicalMachineStyle.Cyan,
            ClipText = true,
            HorizontalExpand = true,
        };
        patientText.AddChild(StatusLabel);

        BoostLabel = new Label
        {
            FontColorOverride = CMUMedicalMachineStyle.Warning,
            ClipText = true,
            HorizontalExpand = true,
        };
        patientText.AddChild(BoostLabel);

        var statsCard = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.CardBg, CMUMedicalMachineStyle.Border);
        statsCard.MinWidth = 310;
        header.AddChild(statsCard);

        var stats = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        statsCard.AddChild(stats);

        ScanSummaryLabel = MakeMetricLabel(stats, Loc.GetString("cmu-body-scanner-scan-heading"), CMUMedicalMachineStyle.Cyan);
        PuzzleSummaryLabel = MakeMetricLabel(stats, Loc.GetString("cmu-body-scanner-targets-heading"), CMUMedicalMachineStyle.Warning);

        var controlsCard = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.CardBg, CMUMedicalMachineStyle.Border);
        controlsCard.MinWidth = 210;
        header.AddChild(controlsCard);

        var controls = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(8),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        controlsCard.AddChild(controls);

        controls.AddChild(new Label
        {
            Text = Loc.GetString("cmu-medical-surgery-actions-heading"),
            StyleClasses = { "LabelHeading" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
        });

        ResetButton = ActionButton(Loc.GetString("cmu-body-scanner-start-button"), CMUMedicalMachineStyle.Warning, out CalibrationButtonLabel);
        EjectButton = CMUMedicalMachineStyle.ActionButton(Loc.GetString("cmu-body-scanner-eject-button"), CMUMedicalMachineStyle.Red);
        controls.AddChild(ResetButton);
        controls.AddChild(EjectButton);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        root.AddChild(body);

        ScanList = CMUMedicalMachineStyle.MakeTitledList(body, Loc.GetString("cmu-body-scanner-scan-heading"), 430, true);

        var puzzleRoot = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        body.AddChild(puzzleRoot);

        var puzzleHeader = CMUMedicalMachineStyle.Panel(Color.FromHex("#211F2A"), CMUMedicalMachineStyle.Purple);
        puzzleRoot.AddChild(puzzleHeader);
        puzzleHeader.AddChild(new Label
        {
            Text = Loc.GetString("cmu-body-scanner-calibration-heading"),
            StyleClasses = { "LabelHeading" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
            Margin = new Thickness(8, 6),
            ClipText = true,
        });

        var sweepPanel = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.DeepCardBg, CMUMedicalMachineStyle.MutedBorder);
        puzzleRoot.AddChild(sweepPanel);

        var sweepRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(8, 6),
            HorizontalExpand = true,
        };
        sweepPanel.AddChild(sweepRow);

        var scanFrame = CMUMedicalMachineStyle.Panel(Color.FromHex("#081117"), CMUMedicalMachineStyle.Cyan);
        scanFrame.MinSize = ScanPreviewFrameSize;
        scanFrame.SetSize = ScanPreviewFrameSize;
        scanFrame.HorizontalExpand = false;
        sweepRow.AddChild(scanFrame);

        _scanPatientPreview = new CMUUprightSpriteView
        {
            SetSize = ScanPatientPreviewSize,
            OverrideDirection = Direction.South,
            WorldRotation = Angle.Zero,
            EyeRotation = Angle.Zero,
            Stretch = SpriteView.StretchMode.Fit,
            Scale = ScanPatientPreviewScale,
            ModulateSelfOverride = Color.White,
        };
        scanFrame.AddChild(_scanPatientPreview);

        _scanPreviewFallbackLabel = new Label
        {
            Text = Loc.GetString("cmu-body-scanner-no-patient"),
            Align = Label.AlignMode.Center,
            VerticalAlignment = Control.VAlignment.Center,
            HorizontalAlignment = Control.HAlignment.Center,
            SetSize = ScanPreviewFrameSize,
            MinSize = ScanPreviewFrameSize,
            FontColorOverride = CMUMedicalMachineStyle.Dim,
            ClipText = true,
            Visible = false,
        };
        scanFrame.AddChild(_scanPreviewFallbackLabel);

        SweepControl = new CMUScannerSweepControl
        {
            SetSize = ScanPreviewFrameSize,
            MinSize = ScanPreviewFrameSize,
        };
        scanFrame.AddChild(SweepControl);

        SweepStatusOverlay = new Label
        {
            Text = string.Empty,
            Align = Label.AlignMode.Center,
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Bottom,
            Margin = new Thickness(4, 0, 4, 7),
            StyleClasses = { "LabelSubText" },
            FontColorOverride = CMUMedicalMachineStyle.Cyan,
            ClipText = true,
            Visible = false,
        };
        scanFrame.AddChild(SweepStatusOverlay);

        var sweepText = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        sweepRow.AddChild(sweepText);
        sweepText.AddChild(new Label
        {
            Text = Loc.GetString("cmu-body-scanner-sweep-title"),
            StyleClasses = { "LabelHeading" },
            FontColorOverride = CMUMedicalMachineStyle.Text,
            ClipText = true,
            HorizontalExpand = true,
        });
        SweepDetailLabel = new Label
        {
            Text = Loc.GetString("cmu-body-scanner-sweep-detail"),
            FontColorOverride = CMUMedicalMachineStyle.Muted,
            ClipText = false,
            HorizontalExpand = true,
        };
        sweepText.AddChild(SweepDetailLabel);

        var puzzleColumns = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        puzzleRoot.AddChild(puzzleColumns);

        TermList = CMUMedicalMachineStyle.MakeTitledList(puzzleColumns, Loc.GetString("cmu-body-scanner-terms-heading"), 230, true);
        TargetList = CMUMedicalMachineStyle.MakeTitledList(puzzleColumns, Loc.GetString("cmu-body-scanner-targets-heading"), 340, true);

        CMUMedicalWindowSizing.FitToScreen(this, PreferredWindowSize, MinimumWindowSize, clampPosition: false);
        ApplyUniformScale(true);
    }

    private static Button ActionButton(string text, Color accent, out Label label)
    {
        var button = new Button
        {
            HorizontalExpand = true,
            MinHeight = 34,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(7, 5),
            HorizontalExpand = true,
        };

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 24),
            PanelOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        });

        label = new Label
        {
            Text = text,
            FontColorOverride = CMUMedicalMachineStyle.Text,
            ClipText = true,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(label);

        var panel = CMUMedicalMachineStyle.Panel(CMUMedicalMachineStyle.DeepCardBg, accent);
        panel.AddChild(row);
        button.AddChild(panel);
        return button;
    }

    public void SetPatient(
        EntityUid? patient,
        string patientName,
        string status,
        string boost,
        IEntityManager entities,
        IPlayerManager players)
    {
        PatientLabel.Text = patientName;
        StatusLabel.Text = status;
        BoostLabel.Text = boost;

        var showPreview = patient is { } uid &&
                          uid.Valid &&
                          entities.HasComponent<SpriteComponent>(uid);

        _patientPreview.Visible = showPreview;
        _previewFallbackLabel.Visible = !showPreview;
        _scanPatientPreview.Visible = showPreview;
        _scanPreviewFallbackLabel.Visible = !showPreview;

        if (!showPreview || patient is not { } preview)
            return;

        _patientPreview.WorldRotation = Angle.Zero;
        _patientPreview.EyeRotation = Angle.Zero;
        _patientPreview.SetEntity(preview);
        _scanPatientPreview.WorldRotation = Angle.Zero;
        _scanPatientPreview.EyeRotation = Angle.Zero;
        _scanPatientPreview.SetEntity(preview);

        var liveSprite = GhostPreviewHelper.CanUseLiveSprite(entities, players, preview);
        _patientPreview.ModulateSelfOverride = liveSprite
            ? Color.White
            : Color.FromHex("#9AA3AD");
        _scanPatientPreview.ModulateSelfOverride = liveSprite
            ? Color.White
            : Color.FromHex("#73808B");
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        CMUMedicalWindowSizing.FitToScreen(this, PreferredWindowSize, MinimumWindowSize, clampPosition: false);
        ApplyUniformScale();
        CMUMedicalWindowSizing.RememberSize(RememberedSizeKey, this);
    }

    private void ApplyUniformScale(bool force = false)
    {
        var size = Size.X > 0f && Size.Y > 0f ? Size : SetSize;
        var scale = Math.Clamp(
            Math.Min(size.X / PreferredWindowSize.X, size.Y / PreferredWindowSize.Y),
            CMUMedicalUniformScaler.MinimumScale,
            1f);

        if (!force && Math.Abs(_layoutScale - scale) < 0.001f)
            return;

        _layoutScale = scale;
        _uniformScaler.Apply(_scaleRoot, _layoutScale, _resourceCache);
        _patientPreview.Scale = PatientPreviewScale * _layoutScale;
        _scanPatientPreview.Scale = ScanPatientPreviewScale * _layoutScale;
    }

    private static Label MakeMetricLabel(BoxContainer parent, string title, Color accent)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 7,
            HorizontalExpand = true,
        };

        row.AddChild(new PanelContainer
        {
            MinSize = new Vector2(5, 34),
            PanelOverride = CMUMedicalMachineStyle.Flat(accent, accent),
        });

        var text = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalAlignment = Control.VAlignment.Center,
        };
        row.AddChild(text);

        text.AddChild(new Label
        {
            Text = title,
            StyleClasses = { "LabelSubText" },
            FontColorOverride = CMUMedicalMachineStyle.Muted,
            ClipText = true,
        });

        var value = new Label
        {
            FontColorOverride = accent,
            ClipText = true,
            HorizontalExpand = true,
        };
        text.AddChild(value);

        parent.AddChild(CMUMedicalMachineStyle.Wrap(row, CMUMedicalMachineStyle.DeepCardBg, CMUMedicalMachineStyle.MutedBorder));
        return value;
    }
}
