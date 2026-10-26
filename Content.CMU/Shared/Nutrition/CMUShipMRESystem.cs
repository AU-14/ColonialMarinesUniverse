using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Storage;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Kitchen;
using Content.Shared.CMU14.Marines;
using Content.Shared.GameTicking;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.Nutrition;

/// <summary>
/// Nobody opens an MRE aboard the warship while the mess hall is serving: the MREs stay sealed for the first ten
/// minutes of the round, then only while the DEFAC shutters are up. Once a dropship has been hijacked and crashed
/// into the ship, all bets are off.
/// </summary>
public sealed partial class CMUShipMRESystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedGameTicker _gameTicker = default!;
    [Dependency] private CMUFoodlineSystem _foodline = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    /// <summary>How long MREs stay sealed regardless of the shutters, while the mess technicians set up.</summary>
    private static readonly TimeSpan SetupTime = TimeSpan.FromMinutes(10);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MRERequireOpenBeforeStorageComponent, OpenableOpenAttemptEvent>(OnOpenAttempt);
    }

    private void OnOpenAttempt(Entity<MRERequireOpenBeforeStorageComponent> ent, ref OpenableOpenAttemptEvent args)
    {
        if (args.Cancelled || args.User is not { } user)
            return;

        var map = _transform.GetMap(user);

        if (!_cfg.GetCVar(CCVars.ShipMreLock)
            || !HasComp<WarshipComponent>(map)
            || AnyHijacked())
            return;

        // Past the setup time the shutters decide: sealed only while the foodline is open for business.
        if (_gameTicker.RoundDuration() > SetupTime && !_foodline.IsFoodlineOpen(Transform(map).MapID))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("cmu-mre-ship-mess-hall"), ent, user);
    }

    private bool AnyHijacked()
    {
        var dropships = EntityQueryEnumerator<DropshipComponent>();
        while (dropships.MoveNext(out var dropship))
        {
            if (dropship.Crashed)
                return true;
        }

        return false;
    }
}
