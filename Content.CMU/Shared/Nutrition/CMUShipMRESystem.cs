using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Holiday;
using Content.Shared._RMC14.Storage;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Kitchen;
using Content.Shared.CMU14.Marines;
using Content.Shared.GameTicking;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Nutrition;

/// <summary>
/// Nobody opens an MRE aboard the warship while the mess hall is serving: the MREs stay sealed for the first ten
/// minutes of the round, then only while the mess has food to serve. Once a dropship has been hijacked and
/// crashed into the ship, all bets are off.
/// </summary>
public sealed partial class CMUShipMRESystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedGameTicker _gameTicker = default!;
    [Dependency] private CMUFoodlineSystem _foodline = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedRMCHolidaySystem _holidays = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private INetManager _net = default!;

    /// <summary>How long MREs stay sealed regardless of the mess, while the mess technicians set up.</summary>
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

        if (!_cfg.GetCVar(CCVars.ShipMreLock)
            || !HasComp<WarshipComponent>(_transform.GetMap(user)))
            return;

        // Vendors and shutters may be outside client PVS. Wait for the server's open state.
        args.Cancelled = true;
        if (_net.IsClient)
            return;

        // Sealed through the setup window even when the mess is dark. After it only a mess with food keeps them locked.
        if (AnyHijacked()
            || (_gameTicker.RoundDuration() > SetupTime && !HasMessFood(Transform(user).MapID)))
        {
            args.Cancelled = false;
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-mre-ship-mess-hall"), ent, user);
    }

    private bool HasMessFood(MapId map)
    {
        if (_foodline.IsFoodlineOpen(map))
            return true;

        var vendors = EntityQueryEnumerator<CMUFoodlineVendorComponent, CMAutomatedVendorComponent, TransformComponent>();
        while (vendors.MoveNext(out var uid, out _, out var vendor, out var xform))
        {
            if (xform.MapID != map || TerminatingOrDeleted(uid))
                continue;

            foreach (var section in vendor.Sections)
            {
                if (section.Holidays.Count > 0 && !section.Holidays.Exists(_holidays.IsActiveHoliday))
                    continue;

                foreach (var entry in section.Entries)
                {
                    // Cutlery and unopened field rations cannot substitute for a ready meal.
                    if (entry.Amount is <= 0 || !_prototypes.TryIndex(entry.Id, out var prototype) ||
                        !prototype.HasComp<EdibleComponent>(_factory))
                        continue;

                    return true;
                }
            }
        }

        return false;
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
