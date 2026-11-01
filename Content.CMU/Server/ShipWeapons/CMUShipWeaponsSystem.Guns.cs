using Content.Shared.CCVar;
using Content.Shared.CMU14.ShipWeapons;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ShipWeapons;

public sealed partial class CMUShipWeaponsSystem
{
    [Dependency] private CMUShipWeaponGunSystem _guns = default!;
    private bool _requireGuns;
    private TimeSpan _nextTerminalRefresh;

    private void InitializeGuns()
    {
        Subs.CVar(_config, CCVars.ShipWeaponsRequireGuns, value =>
        {
            _requireGuns = value;
            RefreshTerminals();
        }, true);
    }

    private void RefreshTerminals()
    {
        var terminals = EntityQueryEnumerator<CMUShipWeaponsTerminalComponent>();
        while (terminals.MoveNext(out var uid, out _))
            UpdateTerminal(uid);
    }

    private HashSet<ProtoId<CMUShipWeaponPrototype>> GetSupportedWeapons(EntityUid ship)
    {
        var weapons = new HashSet<ProtoId<CMUShipWeaponPrototype>>();
        var guns = EntityQueryEnumerator<CMUShipWeaponGunComponent, TransformComponent>();
        while (guns.MoveNext(out var uid, out var gun, out var xform))
        {
            if (!xform.Anchored || TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
                !TryGetShip(uid, out var owner) || owner != ship)
                continue;

            weapons.UnionWith(gun.Weapons);
        }
        return weapons;
    }

    /// <summary>Finds an installed battery on this ship that can fire the mission and isn't waiting on repairs.</summary>
    private bool TryGetFiringGun(EntityUid ship, ProtoId<CMUShipWeaponPrototype> weapon,
        out Entity<CMUShipWeaponGunComponent> gun)
    {
        gun = default;
        var guns = EntityQueryEnumerator<CMUShipWeaponGunComponent, TransformComponent>();
        while (guns.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!xform.Anchored || TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
                !comp.Ready || !comp.Weapons.Contains(weapon) || !TryGetShip(uid, out var owner) || owner != ship)
                continue;

            gun = (uid, comp);
            return true;
        }
        return false;
    }
}
