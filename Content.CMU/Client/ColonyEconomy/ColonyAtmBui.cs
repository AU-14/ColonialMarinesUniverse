using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client.CMU14.ColonyEconomy;

public sealed partial class ColonyAtmBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IEntityManager _entities = default!;

    private static readonly SoundSpecifier SelectSound  = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_select.ogg");
    private static readonly SoundSpecifier EnterSound    = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_enter.ogg");
    private static readonly SoundSpecifier WarningSound  = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_warning.ogg");
    private static readonly SoundSpecifier WrongSound    = new SoundPathSpecifier("/Audio/CMU14/ColonyEconomy/atm_wrong.ogg");

    private SharedAudioSystem _audio = default!;
    private ColonyAtmWindow? _window;

    private AtmScreen? _prevScreen;
    private string _prevStatus = string.Empty;

    protected override void Open()
    {
        base.Open();
        IoCManager.InjectDependencies(this);
        _audio = _entities.System<SharedAudioSystem>();
        _window = this.CreateWindow<ColonyAtmWindow>();

        // Numpad digits (also used for numbered menu selection)
        _window.Btn0.OnPressed += _ => Digit("0");
        _window.Btn1.OnPressed += _ => Digit("1");
        _window.Btn2.OnPressed += _ => Digit("2");
        _window.Btn3.OnPressed += _ => Digit("3");
        _window.Btn4.OnPressed += _ => Digit("4");
        _window.Btn5.OnPressed += _ => Digit("5");
        _window.Btn6.OnPressed += _ => Digit("6");
        _window.Btn7.OnPressed += _ => Digit("7");
        _window.Btn8.OnPressed += _ => Digit("8");
        _window.Btn9.OnPressed += _ => Digit("9");

        // Control keys: OK/ENTER confirm, DEL backspaces / goes back.
        _window.BtnEnter.OnPressed += _ => { Play(EnterSound, -2f);  SendPredictedMessage(new ColonyAtmConfirmBuiMsg()); };
        _window.BtnOk.OnPressed    += _ => { Play(EnterSound, -2f);  SendPredictedMessage(new ColonyAtmConfirmBuiMsg()); };
        _window.BtnDel.OnPressed   += _ => { Play(SelectSound, -4f); SendPredictedMessage(new ColonyAtmBackspaceBuiMsg()); };

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
            Play(WarningSound, -1f);
        else if (s.StatusMessage != _prevStatus && IsErrorMessage(s.StatusMessage))
            Play(WrongSound, -2f);

        _prevScreen = s.Screen;
        _prevStatus = s.StatusMessage;

        _window.UpdateDisplay(s);
    }

    private void Digit(string digit)
    {
        Play(SelectSound, -4f);
        SendPredictedMessage(new ColonyAtmDigitBuiMsg(digit));
    }

    private void Play(SoundSpecifier sound, float volume)
        => _audio.PlayGlobal(sound, Filter.Local(), false, AudioParams.Default.WithVolume(volume));

    private static bool IsErrorMessage(string msg)
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

