using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.MedVendorLock;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.Server.AU14.MedVendorLock;

/// <summary>
/// Locks <see cref="AU14MedVendorDistressLockComponent"/> medical vendors and chem dispensers
/// during a distress-signal round until govfor first lands on the planet.
///
/// The moment any dropship lands on the planet map the lock is lifted, since that is when
/// govfor is considered "planetside" and medbay can operate normally.
/// </summary>
public sealed class AU14MedVendorDistressLockSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    // Reset each round; set true when the first dropship lands on the planet.
    private bool _govforHasLanded;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AU14MedVendorDistressLockComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AU14MedVendorDistressLockComponent, ActivatableUIOpenAttemptEvent>(OnVendorOpenAttempt);
        // Chem dispensers are RMC14 entities placed directly in maps; intercept their UI open event.
        SubscribeLocalEvent<RMCChemicalDispenserComponent, ActivatableUIOpenAttemptEvent>(OnChemDispenserOpenAttempt);
        SubscribeLocalEvent<DropshipLandedOnPlanetEvent>(OnDropshipLanded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _govforHasLanded = false;
    }

    private bool IsDistressSignalActive()
    {
        var q = EntityQueryEnumerator<CMDistressSignalRuleComponent>();
        return q.MoveNext(out _, out _);
    }

    private void OnMapInit(EntityUid uid, AU14MedVendorDistressLockComponent comp, MapInitEvent args)
    {
        // Already unlocked this round — don't lock late-spawning vendors.
        if (_govforHasLanded)
            return;

        if (IsDistressSignalActive())
            comp.Locked = true;
    }

    private void OnDropshipLanded(ref DropshipLandedOnPlanetEvent ev)
    {
        if (_govforHasLanded)
            return;

        _govforHasLanded = true;

        var query = EntityQueryEnumerator<AU14MedVendorDistressLockComponent>();
        while (query.MoveNext(out var uid, out var comp))
            comp.Locked = false;
    }

    private void OnVendorOpenAttempt(EntityUid uid, AU14MedVendorDistressLockComponent comp, ActivatableUIOpenAttemptEvent args)
    {
        if (!comp.Locked)
            return;

        args.Cancel();
        _popup.PopupEntity(
            Loc.GetString("au14-med-vendor-locked"),
            uid,
            args.User,
            PopupType.Medium);
    }

    private void OnChemDispenserOpenAttempt(EntityUid uid, RMCChemicalDispenserComponent comp, ActivatableUIOpenAttemptEvent args)
    {
        if (_govforHasLanded || !IsDistressSignalActive())
            return;

        args.Cancel();
        _popup.PopupEntity(
            Loc.GetString("au14-med-vendor-locked"),
            uid,
            args.User,
            PopupType.Medium);
    }
}
