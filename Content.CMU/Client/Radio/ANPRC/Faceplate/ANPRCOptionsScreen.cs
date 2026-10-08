using System.Linq;
using Content.Shared.CMU14.Radio;
using Content.Shared.Radio;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     OPT: how the set is being worked rather than what it is tuned to. Every line here is one
///     radio setting, and ENT on a line steps it - which is how a set with no arrow keys is
///     configured. The TECHNIQUES block is what the guided panel never offers: the set runs on
///     AUTO there, and these are the levers a trained operator reaches for.
/// </summary>
public sealed class ANPRCOptionsScreen : ANPRCScreen
{
    public override string Title => Loc.GetString("anprc-fp-opt-title");

    public override string Status(ANPRCPanelContext context)
        => Loc.GetString(context.Deployed ? "anprc-fp-deployed" : "anprc-fp-stowed");

    public static RadioMode NextMode(RadioMode mode) => mode switch
    {
        RadioMode.FrequencyHopping => RadioMode.SingleChannel,
        RadioMode.SingleChannel => RadioMode.CipherText,
        RadioMode.CipherText => RadioMode.PlainText,
        _ => RadioMode.FrequencyHopping,
    };

    public static RadioTxPower NextPower(RadioTxPower power) => power switch
    {
        RadioTxPower.Low => RadioTxPower.Medium,
        RadioTxPower.Medium => RadioTxPower.High,
        _ => RadioTxPower.Low,
    };

    private static string OnOff(bool on) => Loc.GetString(on ? "anprc-fp-on" : "anprc-fp-off");

    public override void Build(ANPRCPanelContext context, List<ANPRCScreenRow> rows)
    {
        var state = context.State;
        var expert = state.Expert;

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-mode"),
            Value = ANPRCPanelContext.ModeName(state.Mode),
            Activate = () =>
            {
                var mode = NextMode(state.Mode);
                Radio.SetMode(mode);
                Host.Acknowledge(Loc.GetString("anprc-fp-ack-mode", ("mode", ANPRCPanelContext.ModeName(mode))));
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-monitor"),
            Value = OnOff(state.MonitorEnabled),
            Lit = state.MonitorEnabled,
            Activate = () =>
            {
                Radio.ToggleMonitor();
                Host.Acknowledge(Loc.GetString("anprc-fp-ack-monitor", ("state", OnOff(!state.MonitorEnabled))));
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-scan"),
            Value = OnOff(state.ScanEnabled),
            Lit = state.ScanEnabled,
            Activate = () =>
            {
                Radio.SetScan(!state.ScanEnabled);
                Host.Acknowledge(Loc.GetString("anprc-fp-ack-scan", ("state", OnOff(!state.ScanEnabled))));
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-squelch"),
            Value = state.SquelchLevel.ToString(),
            Activate = () =>
            {
                var level = state.SquelchLevel >= ANPRCRadioComponent.MaxSquelchLevel
                    ? 0
                    : state.SquelchLevel + 1;

                Radio.SetSquelch(level);
                Host.Acknowledge(Loc.GetString("anprc-fp-ack-squelch", ("level", level)));
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-output"),
            Value = ANPRCPanelContext.PowerShort(state.TxPower),
            Activate = () =>
            {
                var power = NextPower(state.TxPower);
                Radio.SetTxPower(power);
                Host.Acknowledge(Loc.GetString("anprc-fp-ack-output", ("power", ANPRCPanelContext.PowerShort(power))));
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-backlight"),
            Value = Loc.GetString("anprc-fp-opt-backlight-value"),
            Activate = () => Radio.CycleBacklight(),
        });

        // ----- techniques: faceplate only ---------------------------------------------------------

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-opt-techniques")));

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-burst"),
            Value = OnOff(expert.Burst),
            Lit = expert.Burst,
            Activate = () =>
            {
                Radio.SetBurst(!expert.Burst);
                Host.Acknowledge(Loc.GetString("anprc-fp-ack-burst", ("state", OnOff(!expert.Burst))));
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-power-save"),
            Value = OnOff(expert.PowerSave),
            Lit = expert.PowerSave,
            Activate = () =>
            {
                Radio.SetPowerSave(!expert.PowerSave);
                Host.Acknowledge(Loc.GetString("anprc-fp-ack-power-save", ("state", OnOff(!expert.PowerSave))));
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-priority"),
            Value = expert.PriorityWatchSlot >= 0
                ? context.SlotLabel(expert.PriorityWatchSlot)
                : Loc.GetString("anprc-fp-off"),
            Lit = expert.PriorityWatchSlot >= 0,
            Activate = () =>
            {
                var next = NextWatchSlot(state, expert.PriorityWatchSlot);
                Radio.SetPriorityWatch(next);
                Host.Acknowledge(next >= 0
                    ? Loc.GetString("anprc-fp-ack-priority", ("slot", context.SlotLabel(next)))
                    : Loc.GetString("anprc-fp-ack-priority-off"));
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-emcon"),
            Value = Loc.GetString(expert.Emcon ? "anprc-fp-opt-emcon-silent" : "anprc-fp-off"),
            Lit = expert.Emcon,
            Style = expert.Emcon ? ANPRCRowStyle.Warn : ANPRCRowStyle.Normal,
            Activate = () =>
            {
                Radio.SetEmcon(!expert.Emcon);
                Host.Acknowledge(Loc.GetString(expert.Emcon ? "anprc-fp-ack-emcon-off" : "anprc-fp-ack-emcon-on"));
            },
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-staked"),
            Value = state.Planted ? ">" : Loc.GetString("anprc-fp-stowed"),
            Style = state.Planted ? ANPRCRowStyle.Normal : ANPRCRowStyle.Note,
            Activate = () => Host.Push(new ANPRCStakedScreen()),
        });

        // ----- station ---------------------------------------------------------------------------

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-opt-station")));

        var station = !string.IsNullOrEmpty(state.Callsign)
            ? state.Callsign
            : !string.IsNullOrEmpty(state.WearerCallsign)
                ? Loc.GetString("anprc-fp-callsign-auto", ("callsign", state.WearerCallsign))
                : Loc.GetString("anprc-fp-unknown");

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-callsign"),
            Value = station,
            Style = string.IsNullOrEmpty(state.Callsign) && string.IsNullOrEmpty(state.WearerCallsign)
                ? ANPRCRowStyle.Warn
                : ANPRCRowStyle.Normal,
            Activate = () => Host.BeginEntry(new ANPRCEntry
            {
                Prompt = Loc.GetString("anprc-fp-entry-station"),
                Mode = ANPRCEntryMode.Letters,
                MaxLength = ANPRCRadioComponent.MaxCallsignLength,
                Initial = state.Callsign,
                Commit = callsign =>
                {
                    Radio.SetCallsign(callsign);
                    Host.Acknowledge(string.IsNullOrEmpty(callsign)
                        ? Loc.GetString("anprc-fp-ack-station-cleared")
                        : Loc.GetString("anprc-fp-ack-station", ("callsign", callsign)));
                },
            }),
        });

        if (!string.IsNullOrEmpty(state.Callsign))
        {
            rows.Add(new ANPRCScreenRow
            {
                Label = Loc.GetString("anprc-fp-opt-clear-override"),
                Value = Loc.GetString("anprc-fp-auto"),
                Activate = () =>
                {
                    Radio.SetCallsign(string.Empty);
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-station-cleared"));
                },
            });
        }

        if (state.CallsignPresets.Count > 0)
        {
            rows.Add(new ANPRCScreenRow
            {
                Label = Loc.GetString("anprc-fp-opt-roster"),
                Value = ">",
                Activate = () => Host.Push(new ANPRCCallsignPresetScreen()),
            });
        }

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-directory"),
            Value = state.Info.HasDirectory ? ">" : Loc.GetString("anprc-fp-none"),
            Style = state.Info.HasDirectory ? ANPRCRowStyle.Normal : ANPRCRowStyle.Note,
            Activate = state.Info.HasDirectory ? () => Radio.OpenDirectory() : null,
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-phone"),
            Value = ">",
            Activate = () => Radio.OpenPhone(),
        });

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-opt-radio-check"),
            Value = Loc.GetString(context.Ready ? "anprc-fp-send" : !context.Deployed ? "anprc-fp-stowed" : "anprc-fp-no-net"),
            Style = context.Ready ? ANPRCRowStyle.Normal : ANPRCRowStyle.Note,
            Activate = context.Ready
                ? () =>
                {
                    Radio.RadioCheck();
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-radio-check"));
                }
                : null,
        });

        // ----- what the set is ------------------------------------------------------------------

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-opt-status")));

        rows.Add(ANPRCScreenRow.Info(
            Loc.GetString("anprc-fp-opt-role"),
            Loc.GetString(state.Planted ? "anprc-fp-role-retrans" : "anprc-fp-role-manpack")));
        rows.Add(ANPRCScreenRow.Info(Loc.GetString("anprc-fp-opt-antenna"),
            string.IsNullOrEmpty(state.AntennaLabel) ? Loc.GetString("anprc-fp-none") : state.AntennaLabel));

        // the server's own answer: which nets the set is anchoring and how far out
        var info = state.Info;

        rows.Add(ANPRCScreenRow.Info(
            Loc.GetString("anprc-fp-opt-relay"),
            info.Relaying
                ? Loc.GetString("anprc-fp-opt-relay-value",
                    ("count", info.RelayedNets.Count),
                    ("full", (int) info.FullRange),
                    ("partial", (int) info.PartialRange))
                : expert.Emcon
                    ? Loc.GetString("anprc-fp-opt-emcon-silent")
                    : Loc.GetString("anprc-fp-standby"),
            info.Relaying ? ANPRCRowStyle.Good : ANPRCRowStyle.Warn));

        if (!info.WearerTrained)
        {
            rows.Add(ANPRCScreenRow.Info(Loc.GetString("anprc-fp-opt-operator"),
                Loc.GetString("anprc-fp-opt-operator-untrained"), ANPRCRowStyle.Bad));
        }

        rows.Add(ANPRCScreenRow.Info(
            Loc.GetString("anprc-fp-opt-audio"),
            Loc.GetString(info.HandsetOut ? "anprc-fp-handset" : "anprc-fp-headset")));

        // ----- the exact link readout the guided panel only puts into words ------------------------

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-opt-link")));

        rows.Add(ANPRCScreenRow.Info(
            Loc.GetString("anprc-fp-opt-link-quality"),
            info.LinkQuality < 0f ? "---" : $"{(int) MathF.Round(info.LinkQuality * 100f)}%",
            info.LinkQuality < 0f ? ANPRCRowStyle.Note
            : info.LinkQuality >= 0.5f ? ANPRCRowStyle.Good
            : info.LinkQuality > 0f ? ANPRCRowStyle.Warn
            : ANPRCRowStyle.Bad));

        rows.Add(ANPRCScreenRow.Info(
            Loc.GetString("anprc-fp-opt-link-via"),
            string.IsNullOrEmpty(expert.CarrierName)
                ? Loc.GetString("anprc-fp-opt-link-via-none")
                : Loc.GetString("anprc-fp-opt-link-via-value",
                    ("name", expert.CarrierName),
                    ("bearing", ANPRCPanelContext.Bearing(expert.CarrierBearingDegrees)),
                    ("distance", (int) expert.CarrierDistance))));

        rows.Add(ANPRCScreenRow.Info(
            Loc.GetString("anprc-fp-opt-draw"),
            Loc.GetString("anprc-fp-opt-draw-value", ("draw", expert.DrawPerSecond.ToString("0.0")))));

        rows.Add(ANPRCScreenRow.Info(
            Loc.GetString("anprc-fp-opt-endurance"),
            expert.BatteryMinutes < 0f
                ? "---"
                : Loc.GetString("anprc-fp-opt-endurance-value", ("minutes", (int) expert.BatteryMinutes)),
            expert.BatteryMinutes is >= 0f and < 10f ? ANPRCRowStyle.Warn : ANPRCRowStyle.Normal));

        if (!context.Deployed)
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-note-wear-or-stake")));
    }

    /// <summary>Steps the watch through every loaded memory that is not the working one, then off.</summary>
    private static int NextWatchSlot(ANPRCRadioState state, int current)
    {
        var candidates = state.Presets.Keys
            .Where(slot => slot != state.ActiveSlot)
            .OrderBy(slot => slot)
            .ToList();

        foreach (var slot in candidates)
        {
            if (slot > current)
                return slot;
        }

        return -1;
    }
}

/// <summary>
///     OPT / STAKED: what only a set staked in the ground can do. Retrans bridges two of its memories
///     so traffic on one is repeated on the other; peaking the antenna buys range until it is packed up.
/// </summary>
public sealed class ANPRCStakedScreen : ANPRCScreen
{
    private int _sideA = -1;
    private int _sideB = -1;

    public override string Title => Loc.GetString("anprc-fp-staked-title");

    public override string Status(ANPRCPanelContext context)
        => Loc.GetString(context.State.Planted ? "anprc-fp-deployed" : "anprc-fp-stowed");

    public override void Build(ANPRCPanelContext context, List<ANPRCScreenRow> rows)
    {
        var state = context.State;
        var expert = state.Expert;

        if (!state.Planted)
        {
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-staked-needs-stake")));
            return;
        }

        var bridged = expert.RetransSlotA >= 0 && expert.RetransSlotB >= 0;

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-staked-retrans")));

        if (bridged)
        {
            rows.Add(ANPRCScreenRow.Info(
                Loc.GetString("anprc-fp-staked-bridge"),
                Loc.GetString("anprc-fp-staked-bridge-value",
                    ("a", context.SlotLabel(expert.RetransSlotA)),
                    ("b", context.SlotLabel(expert.RetransSlotB))),
                ANPRCRowStyle.Good));

            rows.Add(new ANPRCScreenRow
            {
                Label = Loc.GetString("anprc-fp-staked-break"),
                Activate = () =>
                {
                    Radio.SetRetrans(-1, -1);
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-retrans-off"));
                },
            });
        }
        else
        {
            var nets = state.Presets.Keys.OrderBy(slot => slot).ToList();

            rows.Add(new ANPRCScreenRow
            {
                Label = Loc.GetString("anprc-fp-staked-side-a"),
                Value = _sideA >= 0 ? context.SlotLabel(_sideA) : "---",
                Activate = nets.Count > 0 ? () => { _sideA = Next(nets, _sideA); Host.Refresh(); } : null,
            });

            rows.Add(new ANPRCScreenRow
            {
                Label = Loc.GetString("anprc-fp-staked-side-b"),
                Value = _sideB >= 0 ? context.SlotLabel(_sideB) : "---",
                Activate = nets.Count > 0 ? () => { _sideB = Next(nets, _sideB); Host.Refresh(); } : null,
            });

            var ready = _sideA >= 0 && _sideB >= 0 && _sideA != _sideB;

            rows.Add(new ANPRCScreenRow
            {
                Label = Loc.GetString("anprc-fp-staked-make-bridge"),
                Value = ready ? Loc.GetString("anprc-fp-send") : "---",
                Style = ready ? ANPRCRowStyle.Normal : ANPRCRowStyle.Note,
                Activate = ready
                    ? () =>
                    {
                        Radio.SetRetrans(_sideA, _sideB);
                        Host.Acknowledge(Loc.GetString("anprc-fp-ack-retrans-on"));
                    }
                    : null,
            });

            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-staked-retrans-note")));
        }

        rows.Add(ANPRCScreenRow.Heading(Loc.GetString("anprc-fp-staked-antenna")));

        rows.Add(new ANPRCScreenRow
        {
            Label = Loc.GetString("anprc-fp-staked-peak"),
            Value = Loc.GetString(expert.AntennaPeaked ? "anprc-fp-staked-peaked" : "anprc-fp-staked-unpeaked"),
            Lit = expert.AntennaPeaked,
            Style = expert.AntennaPeaked ? ANPRCRowStyle.Good : ANPRCRowStyle.Normal,
            Activate = expert.AntennaPeaked || !context.Powered
                ? null
                : () =>
                {
                    Radio.PeakAntenna();
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-peak"));
                },
        });

        rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-staked-peak-note")));
    }

    private static int Next(List<int> slots, int current)
    {
        foreach (var slot in slots)
        {
            if (slot > current)
                return slot;
        }

        return slots[0];
    }
}

/// <summary>The faction's roster of callsigns, for a station that should answer as one of them.</summary>
public sealed class ANPRCCallsignPresetScreen : ANPRCScreen
{
    public override string Title => Loc.GetString("anprc-fp-roster-title");

    public override void Build(ANPRCPanelContext context, List<ANPRCScreenRow> rows)
    {
        var presets = context.State.CallsignPresets;

        if (presets.Count == 0)
        {
            rows.Add(ANPRCScreenRow.Note(Loc.GetString("anprc-fp-roster-none")));
            return;
        }

        foreach (var preset in presets)
        {
            var captured = preset;

            rows.Add(new ANPRCScreenRow
            {
                Label = captured,
                Activate = () =>
                {
                    Radio.SetCallsign(captured);
                    Host.Acknowledge(Loc.GetString("anprc-fp-ack-station", ("callsign", captured)));
                    Host.Pop();
                },
            });
        }
    }
}
