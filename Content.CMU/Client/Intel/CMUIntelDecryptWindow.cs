using System.Linq;
using System.Numerics;
using Content.Client.CMU14.UI;
using Content.Client.Resources;
using Content.Shared.CMU14.Intel;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Intel;

/// <summary>
/// Decryption terminal for an intel data disk. Type a code (keyboard or keypad) and press Enter or Decrypt.
/// Each guess is logged with green pegs for digits right and in place, and yellow pegs for digits right but in
/// the wrong place. A notes row lets you mark digits as in or out of the code, which tints the keypad; past
/// guesses can be loaded back into the input with one click.
/// </summary>
public sealed class CMUIntelDecryptWindow : DefaultWindow
{
    public event Action<string>? OnGuess;

    private enum DigitNote : byte
    {
        Unknown,
        In,
        Out,
    }

    private static readonly Color SlotBg = Color.FromHex("#101418");
    private static readonly Color SlotBorder = Color.FromHex("#3A4450");
    private static readonly Color SlotActive = Color.FromHex("#4FA3FF");
    private static readonly Color ExactColor = Color.FromHex("#3DDC84");
    private static readonly Color MisplacedColor = Color.FromHex("#F5C542");
    private static readonly Color EmptyPegColor = Color.FromHex("#2A3038");
    private static readonly Color InColor = Color.FromHex("#2E7D4F");
    private static readonly Color OutColor = Color.FromHex("#7D2E2E");

    private readonly Font _bigFont;
    private readonly Font _midFont;

    private readonly Label _status;
    private readonly Label _info;
    private readonly BoxContainer _slots;
    private readonly LineEdit _input;
    private readonly BoxContainer _keypad;
    private readonly BoxContainer _notes;
    private readonly BoxContainer _history;
    private readonly Button _submit;
    private readonly Button _backspace;
    private readonly Button _clear;

    private readonly List<CMUIntelDecryptGuess> _guesses = new();
    private DigitNote[] _digitNotes = new DigitNote[6];

    private int _codeLength = 4;
    private int _digitRange = 6;
    private bool _done;

    public CMUIntelDecryptWindow()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        _bigFont = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 22);
        _midFont = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 13);

        Title = "Data Disk Decryption";
        MinSize = new Vector2(460, 600);
        SetSize = new Vector2(460, 640);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(10),
            SeparationOverride = 6,
        };

        _status = new Label { Text = "Encrypted disk detected.", FontOverride = _midFont };
        _info = new Label { FontColorOverride = Color.Gray };
        root.AddChild(_status);
        root.AddChild(_info);

        // Code slots, mirroring the input.
        _slots = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            SeparationOverride = 6,
            Margin = new Thickness(0, 6),
        };
        root.AddChild(_slots);

        _input = new LineEdit
        {
            PlaceHolder = "Type the code, Enter to decrypt",
            HorizontalExpand = true,
        };
        _input.IsValid = text => text.Length <= _codeLength && text.All(IsValidDigit);
        _input.OnTextChanged += _ => Refresh();
        _input.OnTextEntered += _ => Submit();
        root.AddChild(_input);

        _keypad = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            SeparationOverride = 4,
        };
        root.AddChild(_keypad);

        var actions = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            SeparationOverride = 4,
        };
        _backspace = MakeButton("Backspace", () => SetInput(_input.Text.Length > 0 ? _input.Text[..^1] : string.Empty));
        _clear = MakeButton("Clear", () => SetInput(string.Empty));
        _submit = MakeButton("Decrypt", Submit);
        _submit.MinSize = new Vector2(110, 32);
        actions.AddChild(_backspace);
        actions.AddChild(_clear);
        actions.AddChild(_submit);
        root.AddChild(actions);

        // Scratch notes: click a digit to cycle unknown -> in the code -> not in the code.
        root.AddChild(new Label { Text = "Notes (click to mark a digit in / out of the code):", FontColorOverride = Color.Gray, Margin = new Thickness(0, 6, 0, 0) });
        _notes = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalAlignment = HAlignment.Center,
            SeparationOverride = 4,
        };
        root.AddChild(_notes);

        var legend = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6, Margin = new Thickness(0, 6, 0, 0) };
        legend.AddChild(MakePeg(ExactColor));
        legend.AddChild(new Label { Text = "right digit, right place" });
        legend.AddChild(MakePeg(MisplacedColor));
        legend.AddChild(new Label { Text = "right digit, wrong place" });
        root.AddChild(legend);

        root.AddChild(new Label { Text = "Attempt log (click Use to load a guess):", FontColorOverride = Color.Gray });
        _history = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 2 };
        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_history);
        root.AddChild(scroll);

        Contents.AddChild(root);
        Rebuild();
    }

    protected override void Opened()
    {
        base.Opened();
        _input.GrabKeyboardFocus();
    }

    private bool IsValidDigit(char c) => c >= '0' && c < '0' + _digitRange;

    private Button MakeButton(string text, Action onPressed)
    {
        var button = new Button { Text = text, MinSize = new Vector2(80, 32) };
        GmodStyle.Modernize(button);
        button.OnPressed += _ => onPressed();
        return button;
    }

    private static PanelContainer MakePeg(Color color, float size = 12)
    {
        return new PanelContainer
        {
            MinSize = new Vector2(size, size),
            SetSize = new Vector2(size, size),
            VerticalAlignment = VAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = color },
        };
    }

    private PanelContainer MakeSlot(string text, Color border, float size, Font font)
    {
        var slot = new PanelContainer
        {
            MinSize = new Vector2(size, size),
            SetSize = new Vector2(size, size),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = SlotBg,
                BorderColor = border,
                BorderThickness = new Thickness(2),
            },
        };
        slot.AddChild(new Label
        {
            Text = text,
            FontOverride = font,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        });
        return slot;
    }

    private void SetInput(string text)
    {
        _input.Text = text;
        _input.CursorPosition = text.Length;
        Refresh();
        _input.GrabKeyboardFocus();
    }

    private void Submit()
    {
        var guess = _input.Text;
        if (_done || guess.Length != _codeLength || _guesses.Any(g => g.Guess == guess))
            return;

        OnGuess?.Invoke(guess);
        SetInput(string.Empty);
    }

    private Color? NoteColor(int digit)
    {
        return _digitNotes[digit] switch
        {
            DigitNote.In => InColor,
            DigitNote.Out => OutColor,
            _ => null,
        };
    }

    /// <summary>Rebuilds the keypad and notes, after the code's shape changes or a note is toggled.</summary>
    private void Rebuild()
    {
        if (_digitNotes.Length != _digitRange)
            _digitNotes = new DigitNote[_digitRange];

        _keypad.RemoveAllChildren();
        _notes.RemoveAllChildren();
        for (var i = 0; i < _digitRange; i++)
        {
            var digit = i;
            var text = i.ToString();

            var key = new Button { Text = text, MinSize = new Vector2(48, 40) };
            GmodStyle.Modernize(key);
            key.ModulateSelfOverride = NoteColor(digit);
            key.OnPressed += _ =>
            {
                if (_input.Text.Length < _codeLength)
                    SetInput(_input.Text + text);
            };
            _keypad.AddChild(key);

            var note = new Button
            {
                Text = _digitNotes[digit] switch
                {
                    DigitNote.In => $"{text} in",
                    DigitNote.Out => $"{text} out",
                    _ => $"{text} ?",
                },
                MinSize = new Vector2(48, 26),
            };
            GmodStyle.Modernize(note);
            note.ModulateSelfOverride = NoteColor(digit);
            note.OnPressed += _ =>
            {
                _digitNotes[digit] = (DigitNote) (((int) _digitNotes[digit] + 1) % 3);
                Rebuild();
            };
            _notes.AddChild(note);
        }

        Refresh();
    }

    private void Refresh()
    {
        var text = _input.Text;

        _slots.RemoveAllChildren();
        for (var i = 0; i < _codeLength; i++)
        {
            var filled = i < text.Length;
            var active = !_done && i == text.Length;
            _slots.AddChild(MakeSlot(filled ? text.Substring(i, 1) : string.Empty,
                active ? SlotActive : SlotBorder, 52, _bigFont));
        }

        var duplicate = text.Length == _codeLength && _guesses.Any(g => g.Guess == text);
        _submit.Disabled = _done || text.Length != _codeLength || duplicate;
        _submit.Text = duplicate ? "Already tried" : "Decrypt";
        _backspace.Disabled = _done || text.Length == 0;
        _clear.Disabled = _done || text.Length == 0;
        _input.Editable = !_done;
        foreach (var child in _keypad.Children)
        {
            if (child is Button b)
                b.Disabled = _done || text.Length >= _codeLength;
        }

        _info.Text = _done
            ? $"Upload complete after {_guesses.Count} attempt{(_guesses.Count == 1 ? "" : "s")}."
            : $"Code: {_codeLength} digits, each 0-{_digitRange - 1}, repeats allowed. Attempts so far: {_guesses.Count}.";
    }

    public void UpdateState(CMUIntelDecryptBuiState state)
    {
        if (state.DigitRange != _digitRange || state.CodeLength != _codeLength)
        {
            _digitRange = state.DigitRange;
            _codeLength = state.CodeLength;
            _input.Text = string.Empty;
        }

        _done = state.Done;
        _guesses.Clear();
        _guesses.AddRange(state.History);

        _status.Text = state.Status;
        _status.FontColorOverride = state.Done ? ExactColor : null;

        _history.RemoveAllChildren();
        for (var i = _guesses.Count - 1; i >= 0; i--)
            _history.AddChild(MakeHistoryRow(i, _guesses[i]));

        Rebuild();
    }

    private Control MakeHistoryRow(int index, CMUIntelDecryptGuess guess)
    {
        var solved = guess.Exact == _codeLength;
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };

        row.AddChild(new Label { Text = $"#{index + 1}", MinSize = new Vector2(32, 0), VerticalAlignment = VAlignment.Center });

        for (var i = 0; i < guess.Guess.Length; i++)
            row.AddChild(MakeSlot(guess.Guess.Substring(i, 1), solved ? ExactColor : SlotBorder, 28, _midFont));

        var pegs = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 3,
            Margin = new Thickness(10, 0),
            VerticalAlignment = VAlignment.Center,
        };
        for (var i = 0; i < _codeLength; i++)
        {
            var color = i < guess.Exact ? ExactColor
                : i < guess.Exact + guess.Misplaced ? MisplacedColor
                : EmptyPegColor;
            pegs.AddChild(MakePeg(color));
        }
        row.AddChild(pegs);

        row.AddChild(new Label
        {
            Text = $"{guess.Exact} exact, {guess.Misplaced} misplaced",
            FontColorOverride = Color.Gray,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
        });

        if (!_done)
        {
            var use = new Button { Text = "Use", MinSize = new Vector2(44, 26) };
            GmodStyle.Modernize(use);
            use.OnPressed += _ => SetInput(guess.Guess);
            row.AddChild(use);
        }

        return row;
    }
}
