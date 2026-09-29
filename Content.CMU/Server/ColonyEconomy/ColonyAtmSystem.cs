using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Stack;
using Content.Shared.Access.Components;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ColonyEconomy;

public sealed partial class ColonyAtmSystem : EntitySystem
{
    private static readonly EntProtoId CashPrototype = "RMCSpaceCash";

    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private StackSystem _stack = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private AdminConsoleSystem _adminConsole = default!;
    [Dependency] private ColonyBudgetSystem _colonyBudget = default!;
    [Dependency] private ColonyBankSystem _bank = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private static readonly string[] EmptyLabels = { "", "", "" };

    private const int PinLength = 4;
    private const int MaxAmountDigits = 9;
    private const int MaxRecentLogins = 10;

    // History lines per page: as many as the screen shows, with room for one to wrap.
    private const int HistoryPageSize = 6;

    // Stack type shared by every dollar-bill denomination (RMCSpaceCash1, 10, 100, 1000, ...).
    private const string CashStackType = "Dollar";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColonyAtmComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ColonyAtmComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ColonyAtmComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<ColonyAtmComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<ColonyAtmComponent, ColonyAtmDigitBuiMsg>(OnDigit);
        SubscribeLocalEvent<ColonyAtmComponent, ColonyAtmBackspaceBuiMsg>(OnBackspace);
        SubscribeLocalEvent<ColonyAtmComponent, ColonyAtmConfirmBuiMsg>(OnConfirm);
        SubscribeLocalEvent<ColonyAtmComponent, ColonyAtmScrollHistoryBuiMsg>(OnScrollHistory);
    }

    // ─── Activation (no card) ──────────────────────────────────────────────

    private void OnActivate(EntityUid uid, ColonyAtmComponent comp, ActivateInWorldEvent args)
    {
        // Other machines (e.g. the ASRS console) carry ColonyAtm for cash intake but have no ATM screen.
        if (args.Handled || !args.Complex || !_ui.HasUi(uid, ColonyAtmUi.Key))
            return;

        args.Handled = true;
        if (!TryClaim(uid, comp, args.User))
            return;

        _ui.TryOpenUi(uid, ColonyAtmUi.Key, args.User);
        RefreshUi(uid, comp);
    }

    /// <summary>
    ///     An ATM serves one person at a time. Refuses (with a popup) while someone else has it open,
    ///     otherwise hands the machine to <paramref name="user"/>, starting a fresh session if they are new.
    /// </summary>
    private bool TryClaim(EntityUid uid, ColonyAtmComponent comp, EntityUid user)
    {
        if (comp.CurrentUser is { } current && current != user && _ui.IsUiOpen(uid, ColonyAtmUi.Key, current))
        {
            _popup.PopupEntity("Someone else is using this ATM.", uid, user);
            return false;
        }

        if (comp.CurrentUser != user)
            ResetSession(comp);

        comp.CurrentUser = user;
        return true;
    }

    private static void ResetSession(ColonyAtmComponent comp)
    {
        comp.SwipedCard = null;
        comp.PinAuthenticated = false;
        comp.KeypadBuffer = string.Empty;
        comp.StatusMessage = string.Empty;
        comp.Screen = AtmScreen.Welcome;
        comp.PendingAmount = 0;
        comp.PendingTransferTarget = 0;
        comp.RemoteDepositTarget = 0;
        comp.HistoryOffset = 0;
    }

    // ─── Card swipe ────────────────────────────────────────────────────────

    private void OnInteractUsing(EntityUid uid, ColonyAtmComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || !_ui.HasUi(uid, ColonyAtmUi.Key))
            return;

        // Multitool: tampering check
        if (TryComp<ToolComponent>(args.Used, out var tool) && tool.Qualities.Contains("Pulsing"))
        {
            args.Handled = true;
            if (HasComp<ColonyAtmTamperedComponent>(uid))
                _popup.PopupEntity("Warning: This device shows signs of electronic tampering.", uid, args.User);
            else
                _popup.PopupEntity("The ATM appears to be functioning normally.", uid, args.User);
            return;
        }

        if (!HasComp<IdCardComponent>(args.Used))
            return;

        args.Handled = true;
        if (!TryClaim(uid, comp, args.User))
            return;

        ResetSession(comp);
        comp.SwipedCard = args.Used;
        comp.Screen = AtmScreen.PinEntry;
        _ui.TryOpenUi(uid, ColonyAtmUi.Key, args.User);
        RefreshUi(uid, comp);
    }

    // ─── UI lifecycle ──────────────────────────────────────────────────────

    private void OnUiOpened(EntityUid uid, ColonyAtmComponent comp, BoundUIOpenedEvent args)
    {
        RefreshUi(uid, comp);
    }

    private void OnUiClosed(EntityUid uid, ColonyAtmComponent comp, BoundUIClosedEvent args)
    {
        // Only the person operating the machine ends the session.
        if (args.Actor != comp.CurrentUser)
            return;

        ResetSession(comp);
        comp.CurrentUser = null;
    }

    // ─── Input handlers ────────────────────────────────────────────────────

    private void OnDigit(EntityUid uid, ColonyAtmComponent comp, ColonyAtmDigitBuiMsg msg)
    {
        if (msg.Actor != comp.CurrentUser || msg.Digit.Length != 1 || !char.IsAsciiDigit(msg.Digit[0]))
            return;

        switch (comp.Screen)
        {
            case AtmScreen.Welcome:
                HandleWelcomeMenu(uid, comp, msg.Digit);
                return;
            case AtmScreen.MainMenu:
                HandleMainMenu(uid, comp, msg.Digit);
                return;
            default:
                // Numeric entry screens buffer the digit; everything else ignores it.
                var maxLength = comp.Screen == AtmScreen.PinEntry ? PinLength : MaxAmountDigits;
                if (IsInputScreen(comp.Screen) && comp.KeypadBuffer.Length < maxLength)
                    comp.KeypadBuffer += msg.Digit;
                RefreshUi(uid, comp);
                return;
        }
    }

    private void OnBackspace(EntityUid uid, ColonyAtmComponent comp, ColonyAtmBackspaceBuiMsg msg)
    {
        if (msg.Actor != comp.CurrentUser)
            return;

        // DEL edits the current entry, or steps back a screen when the entry is empty.
        if (comp.KeypadBuffer.Length > 0)
        {
            comp.KeypadBuffer = comp.KeypadBuffer[..^1];
            RefreshUi(uid, comp);
            return;
        }

        GoBack(uid, comp);
    }

    private void OnConfirm(EntityUid uid, ColonyAtmComponent comp, ColonyAtmConfirmBuiMsg msg)
    {
        if (msg.Actor != comp.CurrentUser)
            return;

        switch (comp.Screen)
        {
            case AtmScreen.PinEntry:              HandlePinConfirm(uid, comp); break;
            case AtmScreen.Withdraw:              HandleWithdrawAmountConfirm(uid, comp); break;
            case AtmScreen.WithdrawConfirm:       ExecuteWithdraw(uid, comp); break;
            case AtmScreen.Deposit:               HandleDepositAmountConfirm(uid, comp); break;
            case AtmScreen.RemoteDeposit:         HandleRemoteDepositAccountConfirm(uid, comp); break;
            case AtmScreen.RemoteDepositAmount:   HandleRemoteDepositAmountConfirm(uid, comp); break;
            case AtmScreen.RemoteDepositConfirm:  ExecuteRemoteDeposit(uid, comp); break;
            case AtmScreen.Transfer:              HandleTransferAccountConfirm(uid, comp); break;
            case AtmScreen.TransferAmount:        HandleTransferAmountConfirm(uid, comp); break;
            case AtmScreen.TransferConfirm:       ExecuteTransfer(uid, comp); break;
            case AtmScreen.PinLocked:             Eject(uid, comp); break;
            case AtmScreen.History:               GoBack(uid, comp); break;
            case AtmScreen.Result:
                comp.Screen = comp.PinAuthenticated && comp.SwipedCard != null
                    ? AtmScreen.MainMenu : AtmScreen.Welcome;
                comp.StatusMessage = string.Empty;
                comp.KeypadBuffer = string.Empty;
                RefreshUi(uid, comp);
                break;
        }
    }

    private void OnScrollHistory(EntityUid uid, ColonyAtmComponent comp, ColonyAtmScrollHistoryBuiMsg msg)
    {
        if (msg.Actor != comp.CurrentUser || comp.Screen != AtmScreen.History || !TryGetSessionCard(comp, out var cardUid, out _))
            return;

        // Page by page; ignore scrolling past either end.
        var offset = comp.HistoryOffset + (msg.Older ? HistoryPageSize : -HistoryPageSize);
        if (offset < 0 || offset >= _bank.GetHistory(cardUid).Count)
            return;

        comp.HistoryOffset = offset;
        RefreshUi(uid, comp);
    }

    private void HandleWelcomeMenu(EntityUid uid, ColonyAtmComponent comp, string digit)
    {
        if (digit == "1")
        {
            comp.Screen = AtmScreen.RemoteDeposit;
            comp.KeypadBuffer = string.Empty;
            comp.StatusMessage = string.Empty;
        }
        RefreshUi(uid, comp);
    }

    private void HandleMainMenu(EntityUid uid, ColonyAtmComponent comp, string digit)
    {
        if (!TryGetSessionCard(comp, out _, out _))
        {
            Eject(uid, comp);
            return;
        }

        comp.KeypadBuffer = string.Empty;
        comp.StatusMessage = string.Empty;
        switch (digit)
        {
            case "1": comp.Screen = AtmScreen.Withdraw; break;
            case "2": comp.Screen = AtmScreen.Deposit; break;
            case "3": comp.Screen = AtmScreen.Transfer; break;
            case "4": comp.Screen = AtmScreen.RemoteDeposit; break;
            case "5": comp.Screen = AtmScreen.History; comp.HistoryOffset = 0; break;
            case "6": Eject(uid, comp); return;
        }
        RefreshUi(uid, comp);
    }

    /// <summary>Steps back one screen (DEL on an empty entry), ejecting if at the top level.</summary>
    private void GoBack(EntityUid uid, ColonyAtmComponent comp)
    {
        comp.StatusMessage = string.Empty;
        switch (comp.Screen)
        {
            case AtmScreen.Withdraw:
            case AtmScreen.Deposit:
            case AtmScreen.Transfer:
            case AtmScreen.History:
                comp.Screen = AtmScreen.MainMenu;
                break;
            case AtmScreen.RemoteDeposit:
                comp.Screen = comp.PinAuthenticated && comp.SwipedCard != null
                    ? AtmScreen.MainMenu : AtmScreen.Welcome;
                break;
            case AtmScreen.RemoteDepositAmount:
                comp.Screen = AtmScreen.RemoteDeposit;
                break;
            case AtmScreen.RemoteDepositConfirm:
                comp.Screen = AtmScreen.RemoteDepositAmount;
                break;
            case AtmScreen.TransferAmount:
                comp.Screen = AtmScreen.Transfer;
                break;
            case AtmScreen.TransferConfirm:
                comp.Screen = AtmScreen.TransferAmount;
                break;
            case AtmScreen.WithdrawConfirm:
                comp.Screen = AtmScreen.Withdraw;
                break;
            case AtmScreen.PinEntry:
            case AtmScreen.MainMenu:
            case AtmScreen.PinLocked:
            case AtmScreen.Result:
                Eject(uid, comp);
                return;
        }
        RefreshUi(uid, comp);
    }

    /// <summary>Ends the current card session and returns to the public welcome screen.</summary>
    private void Eject(EntityUid uid, ColonyAtmComponent comp)
    {
        ResetSession(comp);
        RefreshUi(uid, comp);
    }

    /// <summary>
    ///     The swiped card, but only once its PIN has been entered correctly this session.
    /// </summary>
    private bool TryGetSessionCard(ColonyAtmComponent comp, out EntityUid cardUid, [NotNullWhen(true)] out IdCardComponent? card)
    {
        cardUid = default;
        card = null;
        if (!comp.PinAuthenticated || comp.SwipedCard is not { } swiped || !TryComp(swiped, out card))
            return false;

        cardUid = swiped;
        return true;
    }

    // ─── Screen handlers ───────────────────────────────────────────────────

    private void HandlePinConfirm(EntityUid uid, ColonyAtmComponent comp)
    {
        if (comp.SwipedCard == null || !TryComp<IdCardComponent>(comp.SwipedCard.Value, out var card))
        {
            comp.Screen = AtmScreen.Welcome;
            RefreshUi(uid, comp);
            return;
        }

        if (_bank.IsLocked(card, out _))
        {
            comp.Screen = AtmScreen.PinLocked;
            RefreshUi(uid, comp);
            return;
        }

        if (comp.KeypadBuffer.Length != PinLength || !int.TryParse(comp.KeypadBuffer, out var entered))
        {
            comp.StatusMessage = $"Invalid PIN. Enter all {PinLength} digits.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp);
            return;
        }

        comp.KeypadBuffer = string.Empty;

        if (_bank.TryAuthenticatePin(comp.SwipedCard.Value, card, entered, out var locked))
        {
            comp.PinAuthenticated = true;
            comp.Screen = AtmScreen.MainMenu;
            comp.StatusMessage = string.Empty;

            RecordLogin(comp, card);
        }
        else if (locked)
        {
            comp.Screen = AtmScreen.PinLocked;
        }
        else
        {
            comp.StatusMessage = $"Incorrect PIN. Attempt {card.PinAttempts}/{ColonyBankSystem.MaxPinAttempts}.";
        }

        RefreshUi(uid, comp);
    }

    /// <summary>
    ///     Caches the login so a siphon rig clamped onto this ATM can leak it later.
    /// </summary>
    private static void RecordLogin(ColonyAtmComponent comp, IdCardComponent card)
    {
        comp.RecentLogins.RemoveAll(a => a.AccountNumber == card.AccountNumber);
        comp.RecentLogins.Add(new SkimmedAccount
        {
            AccountNumber = card.AccountNumber,
            Name = card.FullName ?? "Unknown",
            Pin = card.AtmPin,
        });

        if (comp.RecentLogins.Count > MaxRecentLogins)
            comp.RecentLogins.RemoveAt(0);
    }

    // ─── Withdraw ──────────────────────────────────────────────────────────

    private void HandleWithdrawAmountConfirm(EntityUid uid, ColonyAtmComponent comp)
    {
        if (!ParsePositiveInt(comp.KeypadBuffer, out var amount))
        {
            comp.StatusMessage = "Enter a valid amount.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        if (!TryGetSessionCard(comp, out _, out var card))
        { Eject(uid, comp); return; }
        if (amount > card.AccountBalance)
        {
            comp.StatusMessage = "Insufficient funds.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        var net = amount - (int)Math.Floor(amount * _adminConsole.GetIncomeTax());
        comp.PendingAmount = amount;
        comp.Screen = AtmScreen.WithdrawConfirm;
        comp.StatusMessage = $"Withdraw ${amount}? You receive ${net} after tax.";
        comp.KeypadBuffer = string.Empty;
        RefreshUi(uid, comp);
    }

    private void ExecuteWithdraw(EntityUid uid, ColonyAtmComponent comp)
    {
        if (!TryGetSessionCard(comp, out var cardUid, out var card))
        { Eject(uid, comp); return; }

        var amount = comp.PendingAmount;
        if (amount <= 0 || amount > card.AccountBalance)
        { ShowResult(uid, comp, "Insufficient funds."); return; }

        card.AccountBalance -= amount;
        Dirty(cardUid, card);
        _bank.RecordTransaction(cardUid, AtmHistoryKind.Withdrawal, amount);

        var taxRate = _adminConsole.GetIncomeTax();
        var taxAmount = (int)Math.Floor(amount * taxRate);
        var netAmount = amount - taxAmount;

        if (netAmount > 0) _stack.SpawnMultipleNextToOrDrop(CashPrototype, netAmount, uid);
        if (taxAmount > 0) _colonyBudget.AddToBudget(taxAmount);

        ShowResult(uid, comp, $"Dispensed ${netAmount}. Balance: ${card.AccountBalance}.");
    }

    // ─── Deposit ───────────────────────────────────────────────────────────

    private void HandleDepositAmountConfirm(EntityUid uid, ColonyAtmComponent comp)
    {
        if (!ParsePositiveInt(comp.KeypadBuffer, out var amount))
        {
            comp.StatusMessage = "Enter a valid amount.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        if (!TryGetSessionCard(comp, out var cardUid, out var card))
        { Eject(uid, comp); return; }
        if (comp.CurrentUser == null || !HasEnoughCash(comp.CurrentUser.Value, amount))
        {
            comp.StatusMessage = "Insufficient cash in hand.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }

        ConsumeCash(comp.CurrentUser.Value, amount);
        card.AccountBalance += amount;
        Dirty(cardUid, card);
        _bank.RecordTransaction(cardUid, AtmHistoryKind.Deposit, amount);
        ShowResult(uid, comp, $"Deposited ${amount}. Balance: ${card.AccountBalance}.");
    }

    // ─── Remote Deposit ────────────────────────────────────────────────────

    private void HandleRemoteDepositAccountConfirm(EntityUid uid, ColonyAtmComponent comp)
    {
        if (!ParsePositiveInt(comp.KeypadBuffer, out var acct) || acct < 10000 || acct > 99999)
        {
            comp.StatusMessage = "Enter a valid 5-digit account number.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        var found = _bank.FindAccount(acct);
        if (found == null)
        {
            comp.StatusMessage = "Account not found.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        comp.RemoteDepositTarget = acct;
        comp.Screen = AtmScreen.RemoteDepositAmount;
        comp.StatusMessage = $"To: {found.Value.card.FullName ?? "Unknown"}. Enter amount:";
        comp.KeypadBuffer = string.Empty;
        RefreshUi(uid, comp);
    }

    private void HandleRemoteDepositAmountConfirm(EntityUid uid, ColonyAtmComponent comp)
    {
        if (!ParsePositiveInt(comp.KeypadBuffer, out var amount))
        {
            comp.StatusMessage = "Enter a valid amount.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        if (comp.CurrentUser == null || !HasEnoughCash(comp.CurrentUser.Value, amount))
        {
            comp.StatusMessage = "Insufficient cash in hand.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        comp.PendingAmount = amount;
        comp.Screen = AtmScreen.RemoteDepositConfirm;
        comp.StatusMessage = $"Deposit ${amount} to account #{comp.RemoteDepositTarget}?";
        comp.KeypadBuffer = string.Empty;
        RefreshUi(uid, comp);
    }

    private void ExecuteRemoteDeposit(EntityUid uid, ColonyAtmComponent comp)
    {
        var found = _bank.FindAccount(comp.RemoteDepositTarget);
        if (found == null) { ShowResult(uid, comp, "Account not found."); return; }

        var amount = comp.PendingAmount;
        if (comp.CurrentUser == null || !HasEnoughCash(comp.CurrentUser.Value, amount))
        { ShowResult(uid, comp, "Insufficient cash."); return; }

        ConsumeCash(comp.CurrentUser.Value, amount);
        found.Value.card.AccountBalance += amount;
        Dirty(found.Value.uid, found.Value.card);
        _bank.RecordTransaction(found.Value.uid, AtmHistoryKind.CashDeposit, amount);
        ShowResult(uid, comp, $"Deposited ${amount} to #{comp.RemoteDepositTarget}.");
    }

    // ─── Transfer ──────────────────────────────────────────────────────────

    private void HandleTransferAccountConfirm(EntityUid uid, ColonyAtmComponent comp)
    {
        if (!ParsePositiveInt(comp.KeypadBuffer, out var acct) || acct < 10000 || acct > 99999)
        {
            comp.StatusMessage = "Enter a valid 5-digit account number.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        if (!TryGetSessionCard(comp, out _, out var self))
        { Eject(uid, comp); return; }
        if (self.AccountNumber == acct)
        {
            comp.StatusMessage = "Cannot transfer to own account.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        var found = _bank.FindAccount(acct);
        if (found == null)
        {
            comp.StatusMessage = "Account not found.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        comp.PendingTransferTarget = acct;
        comp.Screen = AtmScreen.TransferAmount;
        comp.StatusMessage = $"To: {found.Value.card.FullName ?? "Unknown"}. Enter amount:";
        comp.KeypadBuffer = string.Empty;
        RefreshUi(uid, comp);
    }

    private void HandleTransferAmountConfirm(EntityUid uid, ColonyAtmComponent comp)
    {
        if (!ParsePositiveInt(comp.KeypadBuffer, out var amount))
        {
            comp.StatusMessage = "Enter a valid amount.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        if (!TryGetSessionCard(comp, out _, out var card))
        { Eject(uid, comp); return; }
        if (amount > card.AccountBalance)
        {
            comp.StatusMessage = "Insufficient funds.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }
        comp.PendingAmount = amount;
        comp.Screen = AtmScreen.TransferConfirm;
        comp.StatusMessage = $"Transfer ${amount} to #{comp.PendingTransferTarget}?";
        comp.KeypadBuffer = string.Empty;
        RefreshUi(uid, comp);
    }

    private void ExecuteTransfer(EntityUid uid, ColonyAtmComponent comp)
    {
        if (!TryGetSessionCard(comp, out var senderUid, out var sender))
        { Eject(uid, comp); return; }

        var target = _bank.FindAccount(comp.PendingTransferTarget);
        if (target == null) { ShowResult(uid, comp, "Account not found."); return; }
        if (target.Value.uid == senderUid) { ShowResult(uid, comp, "Cannot transfer to own account."); return; }

        var amount = comp.PendingAmount;
        if (amount <= 0 || amount > sender.AccountBalance)
        { ShowResult(uid, comp, "Insufficient funds."); return; }

        sender.AccountBalance -= amount;
        Dirty(senderUid, sender);
        target.Value.card.AccountBalance += amount;
        Dirty(target.Value.uid, target.Value.card);
        _bank.RecordTransaction(senderUid, AtmHistoryKind.TransferOut, amount, target.Value.card.AccountNumber);
        _bank.RecordTransaction(target.Value.uid, AtmHistoryKind.TransferIn, amount, sender.AccountNumber);

        ShowResult(uid, comp, $"Transferred ${amount}. Balance: ${sender.AccountBalance}.");
    }

    // ─── UI building ───────────────────────────────────────────────────────

    private void RefreshUi(EntityUid uid, ColonyAtmComponent comp)
    {
        IdCardComponent? card = null;
        if (comp.SwipedCard != null)
            TryComp(comp.SwipedCard.Value, out card);

        // Like the balance, the history is only sent once the PIN has been entered, and only to the history screen.
        ColonyAccountHistoryEntry[]? history = null;
        var historyTotal = 0;
        if (comp.Screen == AtmScreen.History && TryGetSessionCard(comp, out var historyCard, out _))
        {
            var all = _bank.GetHistory(historyCard);
            historyTotal = all.Count;
            history = all.Reverse().Skip(comp.HistoryOffset).Take(HistoryPageSize).ToArray();
        }

        _bank.IsLocked(card, out var lockExpiry);

        // The balance is only revealed once the PIN has been entered.
        var state = new ColonyAtmBuiState(
            comp.Screen,
            comp.PinAuthenticated ? card?.AccountBalance ?? 0 : 0,
            card?.FullName ?? "---",
            card?.AccountNumber ?? 0,
            _adminConsole.GetIncomeTax() * 100f,
            lockExpiry,
            comp.StatusMessage,
            comp.Screen == AtmScreen.PinEntry ? new string('*', comp.KeypadBuffer.Length) : comp.KeypadBuffer,
            EmptyLabels,
            EmptyLabels,
            HasComp<ColonyAtmTamperedComponent>(uid),
            history,
            comp.HistoryOffset,
            historyTotal
        );

        _ui.SetUiState(uid, ColonyAtmUi.Key, state);
    }

    private void ShowResult(EntityUid uid, ColonyAtmComponent comp, string msg)
    {
        comp.Screen = AtmScreen.Result;
        comp.StatusMessage = msg;
        comp.KeypadBuffer = string.Empty;
        RefreshUi(uid, comp);
    }

    // ─── Cash helpers ──────────────────────────────────────────────────────

    private bool HasEnoughCash(EntityUid user, int amount)
    {
        var total = 0;
        foreach (var item in _hands.EnumerateHeld(user))
        {
            if (TryComp<StackComponent>(item, out var stack) &&
                stack.StackTypeId == CashStackType)
                total += stack.Count;
        }
        return total >= amount;
    }

    private void ConsumeCash(EntityUid user, int amount)
    {
        var remaining = amount;
        foreach (var item in _hands.EnumerateHeld(user).ToList())
        {
            if (remaining <= 0) break;
            if (!TryComp<StackComponent>(item, out var stack) || stack.StackTypeId != CashStackType) continue;

            if (stack.Count <= remaining)
            {
                remaining -= stack.Count;
                QueueDel(item);
            }
            else
            {
                _stack.SetCount((item, stack), stack.Count - remaining);
                remaining = 0;
            }
        }
    }

    private static bool ParsePositiveInt(string input, out int value) =>
        int.TryParse(input, out value) && value > 0;

    private static bool IsInputScreen(AtmScreen screen) =>
        screen is AtmScreen.PinEntry or AtmScreen.Withdraw or AtmScreen.Deposit
            or AtmScreen.RemoteDeposit or AtmScreen.RemoteDepositAmount
            or AtmScreen.Transfer or AtmScreen.TransferAmount;
}


