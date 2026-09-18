using System.Text;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Dialog;

public sealed partial class DialogSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        Subs.BuiEvents<DialogComponent>(DialogUiKey.Key, subs =>
        {
            subs.Event<DialogOptionBuiMsg>(OnDialogOption);
            subs.Event<DialogInputBuiMsg>(OnDialogInput);
            subs.Event<DialogConfirmBuiMsg>(OnDialogConfirm);
            subs.Event<BoundUIClosedEvent>(OnDialogClosed);
        });
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<DialogComponent>();
        while (query.MoveNext(out var uid, out var dialog))
        {
            if (dialog.CloseAt is not { } closeAt || _timing.CurTime < closeAt)
                continue;

            CloseDialog((uid, dialog), false);
        }
    }

    private void OnDialogOption(Entity<DialogComponent> ent, ref DialogOptionBuiMsg args)
    {
        var index = args.Index;
        object? optionEvent = null;
        var valid = false;
        if (index >= 0 && ent.Comp.Options.TryGetValue(index, out var option))
        {
            optionEvent = option.Event;
            valid = true;
        }

        CloseDialog(ent, true);

        if (!valid)
            return;

        var ev = new DialogChosenEvent(args.Actor, index);
        RaiseLocalEvent(ent.Owner, ref ev);

        if (optionEvent != null)
            RaiseLocalEvent(ent.Owner, ref optionEvent, true);
    }

    private void OnDialogInput(Entity<DialogComponent> ent, ref DialogInputBuiMsg args)
    {
        var inputEvent = ent.Comp.InputEvent;
        var msg = TrimToLimit(args.Input, ent.Comp.CharacterLimit, ent.Comp.SmartCheck);

        CloseDialog(ent, true);

        if (inputEvent == null)
            return;

        inputEvent = inputEvent with { Message = msg };
        RaiseLocalEvent(ent.Owner, (object) inputEvent);
    }

    private void OnDialogConfirm(Entity<DialogComponent> ent, ref DialogConfirmBuiMsg args)
    {
        var confirmEvent = ent.Comp.ConfirmEvent;

        CloseDialog(ent, true);

        if (confirmEvent != null)
            RaiseLocalEvent(ent.Owner, confirmEvent);
    }

    private void OnDialogClosed(Entity<DialogComponent> ent, ref BoundUIClosedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (!ent.Comp.SuppressCancelEvent && ent.Comp.CancelEvent != null)
            RaiseLocalEvent(ent, ent.Comp.CancelEvent);

        RemComp<DialogComponent>(ent);
    }

    private void CloseDialog(Entity<DialogComponent> ent, bool suppressCancelEvent)
    {
        ent.Comp.SuppressCancelEvent = suppressCancelEvent;
        _ui.CloseUi(ent.Owner, DialogUiKey.Key);

        if (!TryComp(ent.Owner, out DialogComponent? dialog))
            return;

        if (!dialog.SuppressCancelEvent && dialog.CancelEvent != null)
            RaiseLocalEvent(ent.Owner, dialog.CancelEvent);

        RemComp<DialogComponent>(ent.Owner);
    }

    public void OpenOptions(EntityUid target, EntityUid actor, string title, List<DialogOption> options, string message = "", object? cancelEvent = null, TimeSpan? timeout = null)
    {
        var dialog = EnsureComp<DialogComponent>(target);
        dialog.Title = title;
        dialog.Message = new DialogOption(message);
        dialog.DialogType = DialogType.Options;
        dialog.Options = options;
        dialog.InputEvent = null;
        dialog.ConfirmEvent = null;
        dialog.CancelEvent = cancelEvent;
        dialog.SuppressCancelEvent = false;
        dialog.CloseAt = timeout != null ? _timing.CurTime + timeout.Value : null;
        Dirty(target, dialog);

        _ui.TryOpenUi(target, DialogUiKey.Key, actor);
    }

    public void OpenOptions(EntityUid actor, string title, List<DialogOption> options, string message = "", object? cancelEvent = null, TimeSpan? timeout = null)
    {
        OpenOptions(actor, actor, title, options, message, cancelEvent, timeout);
    }

    public void OpenInput(EntityUid target, EntityUid actor, string message, DialogInputEvent? ev, bool largeInput = false, int characterLimit = 200, int minCharacterLimit = 0, bool smartCheck = false, bool autoFocus = true, string title = "")
    {
        var dialog = EnsureComp<DialogComponent>(target);
        dialog.DialogType = DialogType.Input;
        dialog.Title = title;
        dialog.Message = new DialogOption(message, ev);
        dialog.InputEvent = ev;
        dialog.LargeInput = largeInput;
        dialog.CharacterLimit = characterLimit;
        dialog.MinCharacterLimit = minCharacterLimit;
        dialog.SmartCheck = smartCheck;
        dialog.AutoFocus = autoFocus;
        dialog.ConfirmEvent = null;
        dialog.CancelEvent = null;
        dialog.SuppressCancelEvent = false;
        dialog.CloseAt = null;

        Dirty(target, dialog);

        _ui.TryOpenUi(target, DialogUiKey.Key, actor);
    }

    public void OpenInput(EntityUid actor, string message, DialogInputEvent? ev, bool largeInput = false, int characterLimit = 200, int minCharacterLimit = 0, bool smartCheck = false, bool autoFocus = true, string title = "")
    {
        OpenInput(actor, actor, message, ev, largeInput, characterLimit, minCharacterLimit, smartCheck, autoFocus, title);
    }

    public void OpenConfirmation(EntityUid target, EntityUid actor, string title, string message, object ev)
    {
        var dialog = EnsureComp<DialogComponent>(target);
        dialog.DialogType = DialogType.Confirm;
        dialog.Title = title;
        dialog.Message = new DialogOption(message, ev);
        dialog.ConfirmEvent = ev;
        dialog.InputEvent = null;
        dialog.CancelEvent = null;
        dialog.SuppressCancelEvent = false;
        dialog.CloseAt = null;
        Dirty(target, dialog);

        _ui.TryOpenUi(target, DialogUiKey.Key, actor);
    }

    public void OpenConfirmation(EntityUid actor, string title, string message, object ev)
    {
        OpenConfirmation(actor, actor, title, message, ev);
    }

    public int CalculateEffectiveLength(ReadOnlySpan<char> text, bool smartCheck = false)
    {
        if (!smartCheck)
        {
            return text.Length;
        }

        var length = 0;
        var previousSpace = false;

        foreach (var ch in text)
        {
            var isSpace = ch == ' ';

            if (isSpace && previousSpace)
                continue;

            length++;
            previousSpace = isSpace;
        }

        return length;
    }

    public string TrimToLimit(ReadOnlySpan<char> text, int maxLength, bool smartCheck = false)
    {
        if (maxLength <= 0)
            return string.Empty;

        if (!smartCheck)
        {
            if (text.Length <= maxLength)
                return text.ToString();

            return text[..maxLength].ToString();
        }

        var builder = new StringBuilder(text.Length);
        var length = 0;
        var consecutiveSpaces = 0;
        var previousSpace = false;

        foreach (var ch in text)
        {
            var isSpace = ch == ' ';

            if (isSpace)
            {
                if (previousSpace)
                {
                    consecutiveSpaces++;
                    // Skip 4th and subsequent consecutive spaces
                    if (consecutiveSpaces >= 3)
                        continue;
                }
                else
                {
                    consecutiveSpaces = 1;
                }
            }
            else
            {
                consecutiveSpaces = 0;
            }

            var countsTowardsLimit = !(isSpace && previousSpace);

            if (countsTowardsLimit && length >= maxLength)
                break;

            if (countsTowardsLimit)
                length++;

            builder.Append(ch);
            previousSpace = isSpace;
        }

        return builder.ToString();
    }
}
