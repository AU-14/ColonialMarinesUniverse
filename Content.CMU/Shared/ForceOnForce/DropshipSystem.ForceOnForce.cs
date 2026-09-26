using Content.Shared.CMU14;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.CMU14.Round;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Popups;

namespace Content.Shared._RMC14.Dropship;

public abstract partial class SharedDropshipSystem
{
    // A completed hack authorizes one destination choice on this console for a short time.
    private readonly Dictionary<EntityUid, (EntityUid Computer, TimeSpan Expires)> _forceOnForceHacks = new();

    private void InitializeForceOnForceHijack()
    {
        SubscribeLocalEvent<DropshipNavigationComputerComponent, ForceOnForceHijackDoAfterEvent>(OnForceOnForceHack);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnForceOnForceRoundRestart);
    }

    private void OnForceOnForceRoundRestart(RoundRestartCleanupEvent args) => _forceOnForceHacks.Clear();

    protected virtual bool IsForceOnForceHijacker(EntityUid computer, EntityUid user) => false;
    protected virtual bool IsForceOnForceHuman(EntityUid user) => false;

    private void StartForceOnForceHijack(Entity<DropshipNavigationComputerComponent> computer, EntityUid user)
    {
        if (_net.IsClient || !computer.Comp.Hijackable || !TryDropshipLaunchPopup(computer, user, false) ||
            !TryDropshipHijackPopup(computer, user, false))
            return;

        _popup.PopupEntity(Loc.GetString("rmc-dropship-hijack-human-hacking"), computer, user, PopupType.LargeCaution);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(60),
            new ForceOnForceHijackDoAfterEvent(), computer, computer)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnRest = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            CancelDuplicate = true,
        });
    }

    private void OnForceOnForceHack(Entity<DropshipNavigationComputerComponent> computer, ref ForceOnForceHijackDoAfterEvent args)
    {
        if (_net.IsClient || args.Cancelled || args.Handled || !IsForceOnForceHijacker(computer, args.User) ||
            !computer.Comp.Hijackable || !TryDropshipHijackPopup(computer, args.User, false))
            return;

        args.Handled = true;
        if (GetHijackDestinations(args.User).Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-fof-hijack-no-destination"), computer, args.User);
            return;
        }

        _forceOnForceHacks[args.User] = (computer, _timing.CurTime + TimeSpan.FromMinutes(1));
        OpenHijackDestinationMenu(computer, args.User);
    }

    protected bool IsOpposingCarrierDestination(EntityUid user, EntityUid destination)
    {
        var enemy = ForceOnForceSystem.Opponent(EntityManager.System<ForceOnForceSystem>().GetFaction(user));
        return HasComp<DropshipHijackDestinationComponent>(destination) &&
            enemy != null && string.Equals(GetCarrierFaction(destination), enemy, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Resolves the carrier on every connected deck, including destinations with no explicit controller.</summary>
    protected string? GetCarrierFaction(EntityUid destination)
    {
        // Two carrier grids can share a space map. Prefer the marker's actual ancestors
        // before resolving connected decks, rather than accepting the first ship on a map.
        var ancestor = destination;
        while (ancestor.IsValid())
        {
            if (!HasComp<DropshipComponent>(ancestor) && TryComp<ShipFactionComponent>(ancestor, out var owner))
                return owner.Faction;
            ancestor = Transform(ancestor).ParentUid;
        }

        var map = Transform(destination).MapUid;
        if (map == null)
            return null;

        string? result = null;
        var query = EntityQueryEnumerator<ShipFactionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var faction, out var xform))
        {
            if (HasComp<DropshipComponent>(uid) || xform.MapUid is not { } shipMap)
                continue;

            foreach (var deck in _zLevels.GetAllNetworkMaps(shipMap))
            {
                if (deck == map)
                {
                    if (result != null && !string.Equals(result, faction.Faction, StringComparison.OrdinalIgnoreCase))
                        return null;
                    result = faction.Faction;
                    break;
                }
            }
        }

        return result;
    }

    private bool TryHandleForceOnForceHijack(Entity<DropshipNavigationComputerComponent> computer, EntityUid user, EntityUid destination)
    {
        if (!_forceOnForceHacks.Remove(user, out var hack))
            return IsForceOnForceHuman(user);

        if (hack.Computer != computer.Owner || hack.Expires < _timing.CurTime ||
            !IsForceOnForceHijacker(computer, user) || !IsOpposingCarrierDestination(user, destination) ||
            !computer.Comp.Hijackable || !TryDropshipHijackPopup(computer, user, false))
            return true;

        if (FlyTo(computer, destination, user, hijack: true) && TryGetGridDropship(computer, out var dropship))
        {
            var faction = EntityManager.System<ForceOnForceSystem>().GetFaction(user);
            var ev = new DropshipHijackStartEvent(dropship, faction, DropshipHijackerType.Human);
            RaiseLocalEvent(ref ev);
        }

        return true;
    }
}
