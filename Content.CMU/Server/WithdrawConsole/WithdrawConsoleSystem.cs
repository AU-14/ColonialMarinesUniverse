using System.Linq;
using Content.Server.GameTicking;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Marines;
using Content.Server._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.AU14.ColonyEvacuation;
using Content.Shared.AU14.WithdrawConsole;
using Content.Shared._RMC14.Rules;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Server.AU14.WithdrawConsole;

public sealed class WithdrawConsoleSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    private TimeSpan _withdrawDuration;

    public bool IsDropdownBlocked(string faction)
    {
        var query = EntityQueryEnumerator<WithdrawConsoleComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.DropdownLockApplied && string.Equals(comp.Faction, faction, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public bool IsHijackBlocked(string faction)
    {
        var query = EntityQueryEnumerator<WithdrawConsoleComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.HijackLockApplied && string.Equals(comp.Faction, faction, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public bool IsWithdrawReturnUnlocked(string faction)
    {
        var query = EntityQueryEnumerator<WithdrawConsoleComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (!comp.UseAccessCheck &&
                comp.HijackLockApplied &&
                string.Equals(comp.Faction, faction, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private TimeSpan GetCancelWindow(WithdrawConsoleComponent console) =>
        console.CancelWindowOverride ?? _withdrawDuration * 0.5;

    private TimeSpan GetAnnouncementElapsed(WithdrawConsoleComponent console) =>
        console.AnnouncementElapsedOverride ?? _withdrawDuration * 0.5;

    private TimeSpan GetHijackLockRemaining(WithdrawConsoleComponent console) =>
        console.HijackLockRemainingOverride ?? _withdrawDuration / 3.0;

    private TimeSpan GetDropdownLockRemaining(WithdrawConsoleComponent console) =>
        console.DropdownLockRemainingOverride ?? _withdrawDuration / 6.0;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_config, RMCCVars.RMCWithdrawTimerMinutes, v => _withdrawDuration = TimeSpan.FromMinutes(v), true);

        SubscribeLocalEvent<WithdrawConsoleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<WithdrawConsoleComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<WithdrawConsoleComponent, BoundUIOpenedEvent>(OnUIOpened);

        Subs.BuiEvents<WithdrawConsoleComponent>(WithdrawConsoleUiKey.Key, subs =>
        {
            subs.Event<WithdrawConsoleToggleWithdrawMsg>(OnToggleWithdraw);
            subs.Event<WithdrawConsoleCancelMsg>(OnCancelWithdraw);
            subs.Event<WithdrawConsoleToggleStalemateMsg>(OnToggleStalemate);
        });
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WithdrawConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (!console.WithdrawActive || console.WithdrawStartTime == null || console.RoundEndTriggered)
                continue;

            var elapsed = now - console.WithdrawStartTime.Value;
            var remaining = _withdrawDuration - elapsed;
            var dirty = false;

            if (!console.AnnouncementSent && elapsed >= GetAnnouncementElapsed(console))
            {
                console.AnnouncementSent = true;
                dirty = true;
                SendWithdrawAnnouncement(uid, console, remaining);
            }

            if (!console.HijackLockApplied && remaining <= GetHijackLockRemaining(console))
            {
                console.HijackLockApplied = true;
                dirty = true;
                SendMilestoneAnnouncement(uid, console, remaining, "withdraw-console-announcement-hijack-lock");

                if (console.UseAccessCheck)
                {
                    var colonyEv = new ColonyWithdrawEvacEnabledEvent();
                    RaiseLocalEvent(ref colonyEv);
                }
                else
                {
                    var ev = new WithdrawFactionHijackLockEvent(console.Faction);
                    RaiseLocalEvent(ref ev);
                }
            }

            if (!console.DropdownLockApplied && remaining <= GetDropdownLockRemaining(console))
            {
                console.DropdownLockApplied = true;
                dirty = true;
                SendMilestoneAnnouncement(uid, console, remaining, "withdraw-console-announcement-dropdown-lock");
            }

            if (remaining <= TimeSpan.Zero)
            {
                console.RoundEndTriggered = true;
                dirty = true;
                EndWithdraw(console.Faction, isStalemate: false);
            }

            if (dirty)
            {
                Dirty(uid, console);
                UpdateUI(uid, console);
            }
        }
    }

    private void OnInteractUsing(Entity<WithdrawConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.IsUnlocked)
            return;

        if (ent.Comp.UseAccessCheck)
        {
            OnInteractUsingAccessCheck(ent, ref args);
            return;
        }

        if (!TryComp<IdCardComponent>(args.Used, out var idCard))
            return;

        if (idCard.OriginalOwner == null)
        {
            _popup.PopupEntity(Loc.GetString("withdraw-console-id-unbound"), ent, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var owner = idCard.OriginalOwner.Value;
        if (!TryComp<MarineComponent>(owner, out var marine) ||
            !string.Equals(marine.Faction, ent.Comp.Faction, StringComparison.OrdinalIgnoreCase))
        {
            _popup.PopupEntity(Loc.GetString("withdraw-console-id-wrong-faction"), ent, args.User, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        var netOwner = GetNetEntity(owner);
        if (ent.Comp.SwipedOwners.Contains(netOwner))
        {
            _popup.PopupEntity(Loc.GetString("withdraw-console-id-already-swiped"), ent, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        ent.Comp.SwipedOwners.Add(netOwner);

        if (ent.Comp.SwipedOwners.Count >= ent.Comp.RequiredIdCount)
        {
            ent.Comp.IsUnlocked = true;
            _popup.PopupEntity(Loc.GetString("withdraw-console-unlocked"), ent, args.User, PopupType.Medium);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("withdraw-console-id-accepted", ("count", ent.Comp.SwipedOwners.Count)), ent, args.User, PopupType.Small);
        }

        Dirty(ent);
        args.Handled = true;
    }

    private void OnInteractUsingAccessCheck(Entity<WithdrawConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (!_accessReader.IsAllowed(args.User, ent))
        {
            _popup.PopupEntity(Loc.GetString("withdraw-console-id-wrong-faction"), ent, args.User, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        var netUser = GetNetEntity(args.User);
        if (ent.Comp.SwipedOwners.Contains(netUser))
        {
            _popup.PopupEntity(Loc.GetString("withdraw-console-id-already-swiped"), ent, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        ent.Comp.SwipedOwners.Add(netUser);

        if (ent.Comp.SwipedOwners.Count >= ent.Comp.RequiredIdCount)
        {
            ent.Comp.IsUnlocked = true;
            _popup.PopupEntity(Loc.GetString("withdraw-console-unlocked"), ent, args.User, PopupType.Medium);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString("withdraw-console-id-accepted", ("count", ent.Comp.SwipedOwners.Count)), ent, args.User, PopupType.Small);
        }

        Dirty(ent);
        args.Handled = true;
    }

    private void OnOpenAttempt(Entity<WithdrawConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!IsFactionAllowedToWithdraw(ent.Comp.Faction))
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("withdraw-console-faction-not-allowed"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!ent.Comp.IsUnlocked)
        {
            args.Cancel();
            _popup.PopupEntity(Loc.GetString("withdraw-console-locked"), ent, args.User, PopupType.SmallCaution);
        }
    }

    private void OnUIOpened(Entity<WithdrawConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUI(ent, ent.Comp);
    }

    private void OnToggleWithdraw(Entity<WithdrawConsoleComponent> ent, ref WithdrawConsoleToggleWithdrawMsg args)
    {
        if (ent.Comp.WithdrawActive || ent.Comp.RoundEndTriggered)
            return;

        if (!IsFactionAllowedToWithdraw(ent.Comp.Faction))
            return;

        ent.Comp.WithdrawActive = true;
        ent.Comp.WithdrawStartTime = _timing.CurTime;
        Dirty(ent);

        UpdateUI(ent, ent.Comp);
    }

    private void OnCancelWithdraw(Entity<WithdrawConsoleComponent> ent, ref WithdrawConsoleCancelMsg args)
    {
        if (!ent.Comp.WithdrawActive || ent.Comp.RoundEndTriggered || ent.Comp.WithdrawStartTime == null)
            return;

        var elapsed = _timing.CurTime - ent.Comp.WithdrawStartTime.Value;
        if (elapsed >= GetCancelWindow(ent.Comp))
            return;

        ent.Comp.WithdrawActive = false;
        ent.Comp.WithdrawStartTime = null;
        ent.Comp.AnnouncementSent = false;
        ent.Comp.HijackLockApplied = false;
        ent.Comp.DropdownLockApplied = false;
        Dirty(ent);

        UpdateUI(ent, ent.Comp);
    }

    private void OnToggleStalemate(Entity<WithdrawConsoleComponent> ent, ref WithdrawConsoleToggleStalemateMsg args)
    {
        if (ent.Comp.RoundEndTriggered)
            return;

        ent.Comp.StalemateToggled = !ent.Comp.StalemateToggled;
        Dirty(ent);

        CheckStalemate();
        UpdateUI(ent, ent.Comp);
    }

    private void CheckStalemate()
    {
        var consoles = new List<(EntityUid uid, WithdrawConsoleComponent comp)>();
        var query = EntityQueryEnumerator<WithdrawConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
            consoles.Add((uid, comp));

        // Need consoles from at least 2 distinct factions and all must have stalemate toggled
        if (consoles.Count < 2)
            return;

        var factions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, comp) in consoles)
            factions.Add(comp.Faction);

        if (factions.Count < 2)
            return;

        if (!consoles.All(c => c.comp.StalemateToggled))
            return;

        foreach (var (uid, comp) in consoles)
        {
            if (comp.RoundEndTriggered)
                return; // Already ended
            comp.RoundEndTriggered = true;
            Dirty(uid, comp);
        }

        EndWithdraw(null, isStalemate: true);
    }

    private bool IsFactionAllowedToWithdraw(string faction)
    {
        var query = EntityQueryEnumerator<RMCPlanetComponent>();
        while (query.MoveNext(out _, out var planet))
        {
            if (planet.AllowedWithdrawFactions.Count == 0)
                return false;

            return planet.AllowedWithdrawFactions.Any(f => string.Equals(f, faction, StringComparison.OrdinalIgnoreCase));
        }

        // No planet loaded — allow by default so test maps without a planet still work
        return true;
    }

    private void SendMilestoneAnnouncement(EntityUid uid, WithdrawConsoleComponent console, TimeSpan remaining, string locKey)
    {
        var factionName = console.Faction.ToUpperInvariant();
        var minutes = (int) Math.Ceiling(remaining.TotalMinutes);
        var message = Loc.GetString(locKey, ("faction", factionName), ("minutes", minutes));
        var sound =    new SoundPathSpecifier("/Audio/_RMC14/Announcements/Marine/notice2.ogg");
        _marineAnnounce.AnnounceToMarines(message, sound, faction: console.Faction);
    }

    private void SendWithdrawAnnouncement(EntityUid uid, WithdrawConsoleComponent console, TimeSpan remaining)
    {
        var factionName = console.Faction.ToUpperInvariant();
        var minutes = (int) Math.Ceiling(remaining.TotalMinutes);
        var message = Loc.GetString("withdraw-console-announcement", ("faction", factionName), ("minutes", minutes));
        var sound =    new SoundPathSpecifier("/Audio/_RMC14/Announcements/Marine/notice2.ogg");

        _marineAnnounce.AnnounceToMarines(message, sound, faction: console.Faction);
    }

    private void EndWithdraw(string? faction, bool isStalemate)
    {
        string text;
        if (isStalemate)
            text = Loc.GetString("withdraw-console-round-end-stalemate");
        else
            text = Loc.GetString("withdraw-console-round-end-withdrawn", ("faction", faction?.ToUpperInvariant() ?? "UNKNOWN"));

        _gameTicker.EndRound(text);
    }

    private void UpdateUI(EntityUid uid, WithdrawConsoleComponent console)
    {
        if (!_ui.IsUiOpen(uid, WithdrawConsoleUiKey.Key))
            return;

        double? secondsRemaining = null;
        var canCancel = false;

        if (console.WithdrawActive && console.WithdrawStartTime != null)
        {
            var elapsed = _timing.CurTime - console.WithdrawStartTime.Value;
            var remaining = _withdrawDuration - elapsed;
            secondsRemaining = Math.Max(0, remaining.TotalSeconds);
            canCancel = elapsed < GetCancelWindow(console);
        }

        var state = new WithdrawConsoleBuiState(
            console.Faction,
            console.IsUnlocked,
            console.SwipedOwners.Count,
            console.WithdrawActive,
            console.StalemateToggled,
            canCancel,
            secondsRemaining,
            console.RoundEndTriggered
        );

        _ui.SetUiState(uid, WithdrawConsoleUiKey.Key, state);
    }
}
