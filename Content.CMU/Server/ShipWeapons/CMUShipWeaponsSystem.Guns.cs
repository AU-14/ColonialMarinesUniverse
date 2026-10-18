using System.Linq;
using Content.Shared.CCVar;
using Content.Shared.CMU14.ShipWeapons;
using Content.Shared.Examine;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ShipWeapons;

public sealed partial class CMUShipWeaponsSystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    private bool _requireGuns;
    private TimeSpan _nextTerminalRefresh;

    private void InitializeGuns()
    {
        SubscribeLocalEvent<CMUShipWeaponGunComponent, ExaminedEvent>(OnGunExamined);
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

    private void OnGunExamined(Entity<CMUShipWeaponGunComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-ship-weapons-gun-supports",
            ("weapons", string.Join(", ", ent.Comp.Weapons.Select(w => Loc.GetString(_prototypes.Index(w).Name))))));
        if (TryGetLoadedRound(ent, out var round) && TryComp(round, out CMUShipWeaponAmmoComponent? ammo))
            args.PushMarkup(Loc.GetString("cmu-ship-weapons-gun-loaded",
                ("weapon", Loc.GetString(_prototypes.Index(ammo.Weapon).Name))));
        else
            args.PushMarkup(Loc.GetString("cmu-ship-weapons-gun-empty"));
    }

    private bool TryGetLoadedRound(Entity<CMUShipWeaponGunComponent> gun, out EntityUid round)
    {
        round = default;
        if (!_containers.TryGetContainer(gun, gun.Comp.AmmoSlot, out var slot) ||
            slot.ContainedEntities.Count != 1 ||
            TerminatingOrDeleted(slot.ContainedEntities[0]) ||
            EntityManager.IsQueuedForDeletion(slot.ContainedEntities[0]))
            return false;

        round = slot.ContainedEntities[0];
        return true;
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

    private bool TryGetFiringGun(
        EntityUid ship,
        ProtoId<CMUShipWeaponPrototype> weapon,
        out EntityUid gunUid,
        out EntityUid round)
    {
        gunUid = round = default;
        var guns = EntityQueryEnumerator<CMUShipWeaponGunComponent, TransformComponent>();
        while (guns.MoveNext(out var uid, out var gun, out var xform))
        {
            if (!xform.Anchored || TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid) ||
                !gun.Weapons.Contains(weapon) || !TryGetShip(uid, out var owner) || owner != ship ||
                !TryGetLoadedRound((uid, gun), out var candidate) ||
                !TryComp(candidate, out CMUShipWeaponAmmoComponent? ammo) || ammo.Weapon != weapon)
                continue;

            gunUid = uid;
            round = candidate;
            return true;
        }
        return false;
    }
}
