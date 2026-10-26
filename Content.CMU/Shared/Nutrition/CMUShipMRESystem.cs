using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Storage;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Marines;
using Content.Shared.GameTicking;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.Nutrition;

/// <summary>
/// Nobody opens an MRE aboard the warship while the mess hall is serving: the MREs stay sealed for the first ten
/// minutes of the round, then only while the DEFAC shutters are up. Once a dropship has been hijacked and crashed
/// into the ship, all bets are off.
/// </summary>
public sealed partial class CMUShipMRESystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedGameTicker _gameTicker = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    /// <summary>How long MREs stay sealed regardless of the shutters, while the mess technicians set up.</summary>
    private static readonly TimeSpan SetupTime = TimeSpan.FromMinutes(10);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MRERequireOpenBeforeStorageComponent, OpenableOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<WarshipComponent, ComponentStartup>(OnWarshipStartup);
    }

    private void OnWarshipStartup(Entity<WarshipComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<CMUShipMREStateComponent>(ent);
    }

    private void OnOpenAttempt(Entity<MRERequireOpenBeforeStorageComponent> ent, ref OpenableOpenAttemptEvent args)
    {
        if (args.Cancelled || args.User is not { } user)
            return;

        var map = _transform.GetMap(user);

        // Every fact below rides on the map entity, so prediction sees the same gate the server does without
        // querying entities PVS may never have sent the client.
        if (!_cfg.GetCVar(CCVars.ShipMreLock)
            || !TryComp<CMUShipMREStateComponent>(map, out var state)
            || IsHijackLockedOut(map.Value, state))
            return;

        // Past the setup time the shutters decide: sealed only while the foodline is open for business.
        if (_gameTicker.RoundDuration() > SetupTime && !(state.HasFoodline && state.FoodlineOpen))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("cmu-mre-ship-mess-hall"), ent, user);
    }

    private bool IsHijackLockedOut(EntityUid map, CMUShipMREStateComponent state)
    {
        if (state.HijackLockout)
            return true;

        // The crash check is server-only: once it latches, replication carries the verdict to every client.
        if (_net.IsClient || !AnyHijacked())
            return false;

        state.HijackLockout = true;
        Dirty(map, state);
        return true;
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
