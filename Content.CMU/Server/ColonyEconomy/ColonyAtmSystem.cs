using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Stack;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.ColonyEconomy;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
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
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;

    private static readonly string[] EmptyLabels = { "", "", "" };

    private const int PinLength = ColonyAtmComponent.PinLength;
    private const int MaxAmountDigits = ColonyAtmComponent.MaxAmountDigits;
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
        SubscribeLocalEvent<ColonyAtmComponent, ColonyAtmCancelBuiMsg>(OnCancel);
        SubscribeLocalEvent<ColonyAtmComponent, ColonyAtmInsertCardBuiMsg>(OnInsertCardMsg);
        SubscribeLocalEvent<ColonyAtmComponent, ColonyAtmEjectCardBuiMsg>(OnEjectCardMsg);
        SubscribeLocalEvent<ColonyAtmComponent, ColonyAtmOwnCardRequestMsg>(OnOwnCardRequest);
        SubscribeLocalEvent<ColonyAtmComponent, ColonyAtmScrollHistoryBuiMsg>(OnScrollHistory);
        SubscribeLocalEvent<ColonyAtmComponent, EntRemovedFromContainerMessage>(OnCardRemoved);
        SubscribeLocalEvent<ColonyAtmComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<ColonyAtmComponent, ColonyAtmTakeCardDoAfterEvent>(OnTakeCardDoAfter);
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
            ResetSession(uid, comp);

        comp.CurrentUser = user;
        return true;
    }

    /// <summary>
    ///     Forgets the PIN and everything typed. A card left in the reader stays there, so the next
    ///     person to walk up finds the machine asking for its PIN.
    /// </summary>
    private void ResetSession(EntityUid uid, ColonyAtmComponent comp)
    {
        comp.PinAuthenticated = false;
        comp.KeypadBuffer = string.Empty;
        comp.StatusMessage = string.Empty;
        comp.Screen = GetCard(uid) != null ? AtmScreen.PinEntry : AtmScreen.Welcome;
        comp.PendingAmount = 0;
        comp.PendingTransferTarget = 0;
        comp.RemoteDepositTarget = 0;
        comp.HistoryOffset = 0;
    }

    // ─── Card slot ─────────────────────────────────────────────────────────

    /// <summary>The ID card sitting in the ATM's reader, if any.</summary>
    public EntityUid? GetCard(EntityUid uid)
    {
        return _container.TryGetContainer(uid, ColonyAtmComponent.CardSlotId, out var slot) && slot is ContainerSlot cardSlot
            ? cardSlot.ContainedEntity
            : null;
    }

    /// <summary>
    ///     Takes <paramref name="card"/> from <paramref name="user"/> into the reader and starts their
    ///     session at the PIN prompt. Refuses while another card is in the slot or someone else is at
    ///     the screen.
    /// </summary>
    public bool TryInsertCard(EntityUid uid, ColonyAtmComponent comp, EntityUid card, EntityUid user)
    {
        if (GetCard(uid) != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-atm-card-slot-occupied"), uid, user);
            return false;
        }

        if (!TryClaim(uid, comp, user))
            return false;

        var slot = _container.EnsureContainer<ContainerSlot>(uid, ColonyAtmComponent.CardSlotId);
        if (!_container.Insert(card, slot))
            return false;

        ResetSession(uid, comp);
        comp.CardInsertedBy = user;
        comp.CardInsertedAt = _timing.CurTime;
        _audio.PlayPvs(comp.InsertSound, uid);
        return true;
    }

    /// <summary>
    ///     Gets the card out for <paramref name="user"/>: straight away if they put it in and are at
    ///     the screen, otherwise only after a do-after - the card is in someone else's session, or its
    ///     owner walked off and left it behind.
    /// </summary>
    public void TryTakeCard(EntityUid uid, ColonyAtmComponent comp, EntityUid user)
    {
        if (GetCard(uid) is not { } card)
            return;

        if (user == comp.CardInsertedBy && _ui.IsUiOpen(uid, ColonyAtmUi.Key, user))
        {
            EjectCard(uid, comp, card, user);
            return;
        }

        var args = new DoAfterArgs(EntityManager, user, comp.TakeCardDelay, new ColonyAtmTakeCardDoAfterEvent(), uid, uid, card)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-atm-take-card-start"), uid, user);
        _popup.PopupEntity(Loc.GetString("cmu-atm-take-card-start-others", ("user", user)), uid,
            Filter.PvsExcept(user), true, PopupType.MediumCaution);
    }

    private void OnTakeCardDoAfter(EntityUid uid, ColonyAtmComponent comp, ColonyAtmTakeCardDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } card || GetCard(uid) != card)
            return;

        args.Handled = true;
        EjectCard(uid, comp, card, args.User);
    }

    private void EjectCard(EntityUid uid, ColonyAtmComponent comp, EntityUid card, EntityUid user)
    {
        if (!_container.TryGetContainer(uid, ColonyAtmComponent.CardSlotId, out var slot) || !_container.Remove(card, slot))
            return;

        _hands.PickupOrDrop(user, card);
        _audio.PlayPvs(comp.EjectSound, uid);
    }

    // However the card leaves - ejected, pulled out, deleted - its session goes with it.
    private void OnCardRemoved(EntityUid uid, ColonyAtmComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ColonyAtmComponent.CardSlotId || TerminatingOrDeleted(uid))
            return;

        comp.CardInsertedBy = null;
        comp.CardInsertedAt = null;
        ResetSession(uid, comp);
        RefreshUi(uid, comp);
    }

    private void OnGetVerbs(EntityUid uid, ColonyAtmComponent comp, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || GetCard(uid) == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-atm-take-card-verb"),
            Act = () => TryTakeCard(uid, comp, user),
        });
    }

    /// <summary>Clicking the empty reader on the screen puts in the user's card.</summary>
    private void OnInsertCardMsg(EntityUid uid, ColonyAtmComponent comp, ColonyAtmInsertCardBuiMsg msg)
    {
        if (msg.Actor != comp.CurrentUser || GetCard(uid) != null)
            return;

        if (!TryFindUsersCard(msg.Actor, out var card))
        {
            _popup.PopupEntity(Loc.GetString("cmu-atm-no-card"), uid, msg.Actor);
            return;
        }

        if (TryInsertCard(uid, comp, card, msg.Actor))
            RefreshUi(uid, comp);
    }

    /// <summary>The card the user would reach for: one in hand first, else the one they wear.</summary>
    private bool TryFindUsersCard(EntityUid user, out EntityUid card)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (!HasComp<IdCardComponent>(held))
                continue;

            card = held;
            return true;
        }

        card = default;
        if (!_idCard.TryFindIdCard(user, out var worn))
            return false;

        card = worn.Owner;
        return true;
    }

    /// <summary>Clicking their own card once signed in logs the user off and hands it back.</summary>
    private void OnEjectCardMsg(EntityUid uid, ColonyAtmComponent comp, ColonyAtmEjectCardBuiMsg msg)
    {
        if (msg.Actor != comp.CurrentUser || !TryGetSessionCard(uid, comp, out _, out _))
            return;

        TryTakeCard(uid, comp, msg.Actor);
    }

    // ─── Card insertion ────────────────────────────────────────────────────

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
        if (!TryInsertCard(uid, comp, args.Used, args.User))
            return;

        _ui.TryOpenUi(uid, ColonyAtmUi.Key, args.User);
        RefreshUi(uid, comp);
    }

    // ─── UI lifecycle ──────────────────────────────────────────────────────

    private void OnUiOpened(EntityUid uid, ColonyAtmComponent comp, BoundUIOpenedEvent args)
    {
        RefreshUi(uid, comp);
    }

    /// <summary>
    ///     Whoever is at the screen is reminded of their own PIN: theirs only, sent to them alone. The
    ///     screen asks once it exists - a reply sent as the UI opens could arrive before it does.
    /// </summary>
    private void OnOwnCardRequest(EntityUid uid, ColonyAtmComponent comp, ColonyAtmOwnCardRequestMsg msg)
    {
        var own = _bank.FindOwnedCard(msg.Actor);
        _ui.ServerSendUiMessage(uid, ColonyAtmUi.Key,
            new ColonyAtmOwnCardMsg(own?.card.AccountNumber ?? 0, own?.card.AtmPin ?? 0), msg.Actor);
    }

    private void OnUiClosed(EntityUid uid, ColonyAtmComponent comp, BoundUIClosedEvent args)
    {
        // Only the person operating the machine ends the session. Their card stays in the reader.
        if (args.Actor != comp.CurrentUser)
            return;

        ResetSession(uid, comp);
        comp.CurrentUser = null;
        RefreshUi(uid, comp);
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

        // CLEAR only ever edits the entry; backing out is CANCEL's job.
        if (comp.KeypadBuffer.Length == 0)
            return;

        comp.KeypadBuffer = comp.KeypadBuffer[..^1];
        RefreshUi(uid, comp);
    }

    /// <summary>
    ///     CANCEL abandons whatever is in progress for the menu, the way a real ATM's does; at the
    ///     menu itself, the PIN prompt or a result it ends the session and hands the card back.
    /// </summary>
    private void OnCancel(EntityUid uid, ColonyAtmComponent comp, ColonyAtmCancelBuiMsg msg)
    {
        if (msg.Actor != comp.CurrentUser)
            return;

        switch (comp.Screen)
        {
            case AtmScreen.Welcome:
                return;
            case AtmScreen.PinEntry:
            case AtmScreen.PinLocked:
            case AtmScreen.MainMenu:
            case AtmScreen.Result:
                Eject(uid, comp, comp.CurrentUser);
                return;
        }

        comp.KeypadBuffer = string.Empty;
        comp.StatusMessage = string.Empty;
        comp.PendingAmount = 0;
        comp.PendingTransferTarget = 0;
        comp.RemoteDepositTarget = 0;
        comp.HistoryOffset = 0;
        comp.Screen = TryGetSessionCard(uid, comp, out _, out _) ? AtmScreen.MainMenu : AtmScreen.Welcome;
        RefreshUi(uid, comp);
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
            case AtmScreen.PinLocked:             Eject(uid, comp, msg.Actor); break;
            case AtmScreen.History:               GoBack(uid, comp); break;
            case AtmScreen.Result:
                comp.Screen = TryGetSessionCard(uid, comp, out _, out _)
                    ? AtmScreen.MainMenu : AtmScreen.Welcome;
                comp.StatusMessage = string.Empty;
                comp.KeypadBuffer = string.Empty;
                RefreshUi(uid, comp);
                break;
        }
    }

    private void OnScrollHistory(EntityUid uid, ColonyAtmComponent comp, ColonyAtmScrollHistoryBuiMsg msg)
    {
        if (msg.Actor != comp.CurrentUser || comp.Screen != AtmScreen.History || !TryGetSessionCard(uid, comp, out var cardUid, out _))
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
        if (!TryGetSessionCard(uid, comp, out _, out _))
        {
            Eject(uid, comp, comp.CurrentUser);
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
            case "6": Eject(uid, comp, comp.CurrentUser); return;
        }
        RefreshUi(uid, comp);
    }

    /// <summary>Steps back one screen (ENTER on the history), ejecting if at the top level.</summary>
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
                comp.Screen = TryGetSessionCard(uid, comp, out _, out _)
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
                Eject(uid, comp, comp.CurrentUser);
                return;
        }
        RefreshUi(uid, comp);
    }

    /// <summary>
    ///     Ends the session: hands the card back (see <see cref="TryTakeCard"/>), or without a card
    ///     simply returns to the public welcome screen.
    /// </summary>
    private void Eject(EntityUid uid, ColonyAtmComponent comp, EntityUid? user)
    {
        if (GetCard(uid) != null && user != null)
        {
            TryTakeCard(uid, comp, user.Value);
            return;
        }

        ResetSession(uid, comp);
        RefreshUi(uid, comp);
    }

    /// <summary>
    ///     The card in the reader, but only once its PIN has been entered correctly this session.
    /// </summary>
    private bool TryGetSessionCard(EntityUid uid, ColonyAtmComponent comp, out EntityUid cardUid, [NotNullWhen(true)] out IdCardComponent? card)
    {
        cardUid = default;
        card = null;
        if (!comp.PinAuthenticated || GetCard(uid) is not { } inserted || !TryComp(inserted, out card))
            return false;

        cardUid = inserted;
        return true;
    }

    // ─── Screen handlers ───────────────────────────────────────────────────

    private void HandlePinConfirm(EntityUid uid, ColonyAtmComponent comp)
    {
        if (GetCard(uid) is not { } cardUid || !TryComp<IdCardComponent>(cardUid, out var card))
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

        if (_bank.TryAuthenticatePin(cardUid, card, entered, out var locked))
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
        if (!TryGetSessionCard(uid, comp, out _, out var card))
        { Eject(uid, comp, comp.CurrentUser); return; }
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
        if (!TryGetSessionCard(uid, comp, out var cardUid, out var card))
        { Eject(uid, comp, comp.CurrentUser); return; }

        var amount = comp.PendingAmount;
        if (amount <= 0 || amount > card.AccountBalance)
        { ShowResult(uid, comp, "Insufficient funds."); return; }

        card.AccountBalance -= amount;
        Dirty(cardUid, card);
        _bank.RecordTransaction(cardUid, AtmHistoryKind.Withdrawal, amount);

        var taxRate = _adminConsole.GetIncomeTax();
        var taxAmount = (int)Math.Floor(amount * taxRate);
        var netAmount = amount - taxAmount;

        if (netAmount > 0)
        {
            _stack.SpawnMultipleNextToOrDrop(CashPrototype, netAmount, uid);
            comp.CashDispensedAt = _timing.CurTime;
            comp.CashAmount = netAmount;
            _audio.PlayPvs(comp.DispenseSound, uid);
        }
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
        if (!TryGetSessionCard(uid, comp, out var cardUid, out var card))
        { Eject(uid, comp, comp.CurrentUser); return; }
        if (comp.CurrentUser == null || !HasEnoughCash(comp.CurrentUser.Value, amount))
        {
            comp.StatusMessage = "Insufficient cash in hand.";
            comp.KeypadBuffer = string.Empty;
            RefreshUi(uid, comp); return;
        }

        ConsumeCash(uid, comp, comp.CurrentUser.Value, amount);
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

        ConsumeCash(uid, comp, comp.CurrentUser.Value, amount);
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
        if (!TryGetSessionCard(uid, comp, out _, out var self))
        { Eject(uid, comp, comp.CurrentUser); return; }
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
        if (!TryGetSessionCard(uid, comp, out _, out var card))
        { Eject(uid, comp, comp.CurrentUser); return; }
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
        if (!TryGetSessionCard(uid, comp, out var senderUid, out var sender))
        { Eject(uid, comp, comp.CurrentUser); return; }

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
        string? cardPrototype = null;
        if (GetCard(uid) is { } inserted)
        {
            TryComp(inserted, out card);
            cardPrototype = MetaData(inserted).EntityPrototype?.ID;
        }

        // Like the balance, the history is only sent once the PIN has been entered, and only to the history screen.
        ColonyAccountHistoryEntry[]? history = null;
        var historyTotal = 0;
        if (comp.Screen == AtmScreen.History && TryGetSessionCard(uid, comp, out var historyCard, out _))
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
            historyTotal,
            card != null,
            comp.CardInsertedAt,
            comp.CashDispensedAt,
            comp.CashDepositedAt,
            cardPrototype,
            HasComp<SapperAtmHackedComponent>(uid),
            comp.CashAmount
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

    /// <summary>Feeds <paramref name="amount"/> dollars from the user's hands into the machine.</summary>
    private void ConsumeCash(EntityUid uid, ColonyAtmComponent comp, EntityUid user, int amount)
    {
        comp.CashDepositedAt = _timing.CurTime;
        comp.CashAmount = amount;
        _audio.PlayPvs(comp.DepositSound, uid);

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


