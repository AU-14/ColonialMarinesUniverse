using System;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.ColonyEconomy;

/// <summary>
///     Walks a <see cref="ColonyAtmWindow"/> through a scripted customer session for
///     <c>cmu.panel_preview=atm</c>: the machine booting, a card going in, PIN, a withdrawal paid
///     out, a deposit taken in, the history, a lockout, the card coming back out, a tampered machine
///     and a seized one, then round again with a different card.
/// </summary>
/// <remarks>
///     The real window only opens for someone standing at an ATM with an ID card, mid-round, and
///     most of its states take several deliberate steps to reach - the lockout takes three wrong
///     PINs, the seized screen a sapper with a siphon rig. Nothing here talks to a server: every
///     state is fabricated and the keypad is inert. Each step is logged, and also mirrored into the
///     OS window title: redirected stdout reaches a capture script in batches, long after the step it
///     names, while the title changes the moment the step does.
/// </remarks>
public sealed class ColonyAtmPreview
{
    private const string Owner = "Jenette Vasquez";
    private const int Account = 10427;

    // A different card each time round, to show the reader takes whatever the player carries: an
    // upright colony ID, then the landscape AEGIS badge, which goes in on its side.
    private static readonly string[] Cards = { "AU14IDCardCLFCivilian", "RMCIDCardAegis", "CMIDCardGold" };

    private static readonly ColonyAccountHistoryEntry[] History =
    {
        new(TimeSpan.FromMinutes(74), AtmHistoryKind.Withdrawal, 100, 0),
        new(TimeSpan.FromMinutes(61), AtmHistoryKind.TransferIn, 250, 20931),
        new(TimeSpan.FromMinutes(48), AtmHistoryKind.Deposit, 40, 0),
        new(TimeSpan.FromMinutes(33), AtmHistoryKind.TransferOut, 75, 31188),
        new(TimeSpan.FromMinutes(20), AtmHistoryKind.CashDeposit, 500, 0),
        new(TimeSpan.FromMinutes(9), AtmHistoryKind.Withdrawal, 60, 0),
    };

    private readonly ISawmill _log = Logger.GetSawmill("cmu.atm.preview");
    private readonly IClydeWindow _osWindow = IoCManager.Resolve<IClyde>().MainWindow;
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly string _title;
    private readonly (string Name, float Seconds, Func<ColonyAtmBuiState> State)[] _steps;
    private ColonyAtmWindow _window = new();
    private int _step = -1;
    private int _round;
    private float _timer;

    // Event stamps carried from state to state, as the server's would be.
    private TimeSpan? _cardAt;
    private TimeSpan? _dispensedAt;
    private TimeSpan? _depositedAt;
    private int _cashAmount;

    public ColonyAtmPreview()
    {
        _title = _osWindow.Title;
        _steps = new (string, float, Func<ColonyAtmBuiState>)[]
        {
            ("boot", 6f, () => State(AtmScreen.Welcome, card: false)),
            ("card-insert", 3.5f, () => State(AtmScreen.PinEntry, cardAt: Now)),
            ("pin-typed", 3.5f, () => State(AtmScreen.PinEntry, buffer: "***")),
            ("pin-wrong", 3.5f, () => State(AtmScreen.PinEntry, status: "Incorrect PIN. Attempt 1/3.")),
            ("main-menu", 3.5f, () => State(AtmScreen.MainMenu, authed: true)),
            ("withdraw-amount", 3.5f, () => State(AtmScreen.Withdraw, authed: true, buffer: "300")),
            ("withdraw-confirm", 3.5f, () => State(AtmScreen.WithdrawConfirm, authed: true,
                status: "Withdraw $300? You receive $270 after tax.")),
            // $270 comes out as a stack of four notes, $120 goes in as three.
            ("cash-dispense", 3.5f, () => State(AtmScreen.Result, authed: true, balance: 950,
                status: "Dispensed $270. Balance: $950.", dispensedAt: Now, cash: 270)),
            ("cash-deposit", 3.5f, () => State(AtmScreen.Result, authed: true, balance: 1070,
                status: "Deposited $120. Balance: $1070.", depositedAt: Now, cash: 120)),
            ("history", 3.5f, () => State(AtmScreen.History, authed: true, balance: 1070)),
            ("locked", 3.5f, () => State(AtmScreen.PinLocked)),
            ("card-eject", 3.5f, () => State(AtmScreen.Welcome, card: false)),
            ("tampered", 3.5f, () => State(AtmScreen.Welcome, card: false, tampered: true)),
            ("seized", 4.5f, () => State(AtmScreen.Welcome, card: false, tampered: true, seized: true)),
        };
    }

    public void Open()
    {
        _window.OpenCentered();
        _window.ShowOwnCard(Account, 4711);
        Advance();
    }

    public void Update(float frameTime)
    {
        if (!_window.IsOpen)
            return;

        // No catching up after a hitch: a burst of steps would flash past unseen.
        _timer += frameTime;
        if (_timer < _steps[_step].Seconds)
            return;

        _timer = 0f;
        Advance();
    }

    private void Advance()
    {
        _step = (_step + 1) % _steps.Length;
        if (_step == 0 && _round++ > 0)
        {
            // Each round starts from a cold machine, so the boot is seen again.
            var position = _window.Position;
            _window.Close();
            _window = new ColonyAtmWindow();
            _window.Open(position);
            _window.ShowOwnCard(Account, 4711);
            _cardAt = _dispensedAt = _depositedAt = null;
        }

        var (name, _, state) = _steps[_step];
        _window.UpdateDisplay(state());

        // A window opened while the lobby is still building can end up underneath it.
        _window.MoveToFront();
        _log.Info($"step {_step} {name}");
        _osWindow.Title = $"{_title} [atm-preview {_step} {name}]";
    }

    private TimeSpan? Now => _timing.CurTime;

    private ColonyAtmBuiState State(
        AtmScreen screen,
        bool card = true,
        bool authed = false,
        int balance = 1250,
        string buffer = "",
        string status = "",
        bool tampered = false,
        bool seized = false,
        TimeSpan? cardAt = null,
        TimeSpan? dispensedAt = null,
        TimeSpan? depositedAt = null,
        int? cash = null)
    {
        _cardAt = card ? cardAt ?? _cardAt : null;
        _dispensedAt = dispensedAt ?? _dispensedAt;
        _depositedAt = depositedAt ?? _depositedAt;
        _cashAmount = cash ?? _cashAmount;
        var history = screen == AtmScreen.History ? History : null;
        return new ColonyAtmBuiState(
            screen,
            authed ? balance : 0,
            card ? Owner : "---",
            card ? Account : 0,
            10f,
            null,
            status,
            buffer,
            Array.Empty<string>(),
            Array.Empty<string>(),
            tampered,
            history,
            0,
            history == null ? 0 : 9,
            card,
            _cardAt,
            _dispensedAt,
            _depositedAt,
            card ? Cards[Math.Max(0, _round - 1) % Cards.Length] : null,
            seized,
            _cashAmount);
    }
}
