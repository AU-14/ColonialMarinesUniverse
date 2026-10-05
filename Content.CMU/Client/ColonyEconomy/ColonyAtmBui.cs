using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Client.CMU14.ColonyEconomy;

public sealed partial class ColonyAtmBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly SoundSpecifier SelectSound  = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_select.ogg");
    private static readonly SoundSpecifier EnterSound    = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_enter.ogg");
    // The card locking after too many wrong PINs, and any refusal: wrong PIN, not enough money, no such account.
    private static readonly SoundSpecifier LockedSound   = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_locked.wav");
    private static readonly SoundSpecifier ErrorSound    = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_error.wav");
    // The tube's switch click and high-voltage thump; the screen's power-on frames are timed to it.
    private static readonly SoundSpecifier StartupSound  = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/crt_startup.ogg");
    // A terminal tick for each character the screen prints, never twice at the same pitch.
    private static readonly SoundSpecifier TypeSound     = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_type.wav");
    // A key going down and up, pitched per key.
    private static readonly SoundSpecifier KeySound      = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_key.wav");
    // Arcing from a seized reader's cut wires; its sparks are animated to this sound's zaps.
    private static readonly SoundSpecifier ZapSound      = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_zap.wav");

    // Each key keeps its own tone, as a phone keypad's do: the key sound is pitched by the key's DTMF
    // pair - its row and column frequencies on a phone pad, summed - against the pad's middle.
    private static readonly float[] ToneRows = { 697f, 770f, 852f, 941f };
    private static readonly float[] ToneColumns = { 1209f, 1336f, 1477f, 1633f };
    private const float ToneMiddle = 2156f;

    private EntityUid? _zap;

    private SharedAudioSystem _audio = default!;
    private ColonyAtmWindow? _window;

    private AtmScreen? _prevScreen;
    private string _prevStatus = string.Empty;

    protected override void Open()
    {
        base.Open();
        IoCManager.InjectDependencies(this);
        _audio = _entities.System<SharedAudioSystem>();
        // Like the tactical map, the screen can move into a window of its own, so the session ends when
        // the machine's last window closes rather than the in-game one.
        _window = this.CreateDisposableControl<ColonyAtmWindow>();
        _window.OnFinalClose += Close;
        if (_entities.System<UserInterfaceSystem>().TryGetPosition(Owner, UiKey, out var position))
            _window.Open(position);
        else
            _window.OpenCentered();
        Play(StartupSound, -3f);

        // Numpad digits (also used for numbered menu selection), each with its (row, column) on a
        // phone pad for its tone. CLEAR sits on *, 00 on #, CANCEL and ENTER on C and D.
        _window.Btn1.OnPressed += _ => Digit("1", 0, 0);
        _window.Btn2.OnPressed += _ => Digit("2", 0, 1);
        _window.Btn3.OnPressed += _ => Digit("3", 0, 2);
        _window.Btn4.OnPressed += _ => Digit("4", 1, 0);
        _window.Btn5.OnPressed += _ => Digit("5", 1, 1);
        _window.Btn6.OnPressed += _ => Digit("6", 1, 2);
        _window.Btn7.OnPressed += _ => Digit("7", 2, 0);
        _window.Btn8.OnPressed += _ => Digit("8", 2, 1);
        _window.Btn9.OnPressed += _ => Digit("9", 2, 2);
        _window.Btn0.OnPressed += _ => Digit("0", 3, 1);
        _window.Btn00.OnPressed += _ =>
        {
            Digit("0", 3, 2);
            SendPredictedMessage(new ColonyAtmDigitBuiMsg("0"));
        };

        // Function keys, as on a real ATM: ENTER goes ahead, CLEAR rubs out a digit, CANCEL backs out.
        _window.BtnEnter.OnPressed  += _ => { Key(3, 3); Play(EnterSound, -2f); SendPredictedMessage(new ColonyAtmConfirmBuiMsg()); };
        _window.BtnClear.OnPressed  += _ => { Key(3, 0); SendPredictedMessage(new ColonyAtmBackspaceBuiMsg()); };
        _window.BtnCancel.OnPressed += _ => { Key(2, 3); SendPredictedMessage(new ColonyAtmCancelBuiMsg()); };

        // The nav bar's reminder of the player's own PIN; the server answers this player alone.
        SendMessage(new ColonyAtmOwnCardRequestMsg());

        // The card reader itself: click it to put your card in, click your card to log off.
        _window.InsertCardPressed += () => SendPredictedMessage(new ColonyAtmInsertCardBuiMsg());
        _window.LogOffPressed += () => SendPredictedMessage(new ColonyAtmEjectCardBuiMsg());

        _window.TextTyped += () => Play(TypeSound, -9f, _random.NextFloat(0.85f, 1.2f));

        // History scroll arrows.
        _window.BtnScrollUp.OnPressed   += _ => { Play(SelectSound, -4f); SendPredictedMessage(new ColonyAtmScrollHistoryBuiMsg(false)); };
        _window.BtnScrollDown.OnPressed += _ => { Play(SelectSound, -4f); SendPredictedMessage(new ColonyAtmScrollHistoryBuiMsg(true)); };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not ColonyAtmBuiState s)
            return;

        // Audio feedback on meaningful state transitions.
        if (s.Screen == AtmScreen.PinLocked && _prevScreen != AtmScreen.PinLocked)
            Play(LockedSound, 2f);
        else if (s.StatusMessage != _prevStatus && IsErrorMessage(s.StatusMessage))
            Play(ErrorSound, -5f);

        _prevScreen = s.Screen;
        _prevStatus = s.StatusMessage;

        // A seized reader arcs for as long as the machine is out. The loop starts with the state that
        // seizes it, as do the sparks drawn to it.
        if (s.OutOfService && _zap == null)
            _zap = _audio.PlayGlobal(ZapSound, Filter.Local(), false, AudioParams.Default.WithVolume(-6f).WithLoop(true))?.Entity;
        else if (!s.OutOfService)
            _zap = _audio.Stop(_zap);

        _window.UpdateDisplay(s);
    }

    private void Digit(string digit, int row, int column)
    {
        Key(row, column);
        SendPredictedMessage(new ColonyAtmDigitBuiMsg(digit));
    }

    private void Key(int row, int column)
        => Play(KeySound, -8f, (ToneRows[row] + ToneColumns[column]) / ToneMiddle);

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is ColonyAtmOwnCardMsg own)
            _window?.ShowOwnCard(own.AccountNumber, own.Pin);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _zap = _audio.Stop(_zap);
        if (_window != null)
        {
            _window.OnFinalClose -= Close;
            _window.DisposePopOut();
        }
    }

    private void Play(SoundSpecifier sound, float volume, float pitch = 1f)
        => _audio.PlayGlobal(sound, Filter.Local(), false, AudioParams.Default.WithVolume(volume).WithPitchScale(pitch));

    /// <summary>Whether the server's status line is reporting a failure (the screen says so in words only).</summary>
    internal static bool IsErrorMessage(string msg)
    {
        if (string.IsNullOrEmpty(msg))
            return false;

        return msg.Contains("Incorrect", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Invalid", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Insufficient", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Cannot", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Error", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("valid amount", StringComparison.OrdinalIgnoreCase);
    }
}

