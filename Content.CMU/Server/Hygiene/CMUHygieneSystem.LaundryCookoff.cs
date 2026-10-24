using Content.Server.Explosion.Components;
using Content.Server._RMC14.Explosion;
using Content.Shared.CMU14.Hygiene;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Explosion;
using Content.Shared.Payload.Components;
using Content.Shared.Projectiles;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;
using Content.Shared.Trigger;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared._RMC14.Explosion;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMUHygieneSystem
{
    [Dependency] private SharedExplosionSystem _laundryExplosion = default!;

    [Dependency] private TagSystem _laundryTags = default!;

    [Dependency] private TriggerSystem _laundryTrigger = default!;

    [Dependency] private SharedGunSystem _laundryGun = default!;

    [Dependency] private IRobustRandom _laundryRandom = default!;

    [Dependency] private SharedTransformSystem _laundryTransform = default!;

    private readonly HashSet<EntityUid> _washerPriming = new();

    private void OnLaundryVocalize(Entity<CMULaundryHeatedGrenadeComponent> ent, ref ActiveTimerTriggerEvent args)
    {
        // Only the synchronous washer activation loses its user callout; timer attribution stays intact.
        if (_washerPriming.Contains(ent.Owner))
            args = new ActiveTimerTriggerEvent(null);
    }

    private bool IsLaundryGrenade(EntityUid uid)
    {
        return _laundryTags.HasTag(uid, "Grenade") ||
               _laundryTags.HasTag(uid, "HandGrenade") ||
               _laundryTags.HasTag(uid, "LauncherCompatibleGrenade") ||
               HasComp<ProjectileGrenadeComponent>(uid) ||
               HasComp<ScatteringGrenadeComponent>(uid) ||
               HasComp<PayloadCaseComponent>(uid);
    }

    private void CookOffLaundry(Entity<CMULaundryComponent> washer, StorageComponent storage, bool ammunition)
    {
        var hazards = new List<EntityUid>();
        var visited = new HashSet<EntityUid>();
        var pending = new Stack<EntityUid>(storage.Container.ContainedEntities);
        while (pending.TryPop(out var item))
        {
            if (!visited.Add(item) || TerminatingOrDeleted(item))
                continue;
            // Stop at a grenade or magazine: its own payload is handled as a unit below.
            if (IsLaundryGrenade(item) || HasComp<BallisticAmmoProviderComponent>(item) ||
                HasComp<CartridgeAmmoComponent>(item))
            {
                hazards.Add(item);
                continue;
            }
            // Includes clothing storage, attached webbing, pouches, and further nested containers.
            if (!TryComp<ContainerManagerComponent>(item, out var manager))
                continue;
            foreach (var container in manager.Containers.Values)
            foreach (var contained in container.ContainedEntities)
                pending.Push(contained);
        }

        var user = washer.Comp.CycleUser;
        if (user is { } actor && TerminatingOrDeleted(actor))
            user = null;
        var totalRounds = 0;
        foreach (var item in hazards)
        {
            if (TerminatingOrDeleted(washer.Owner))
                break;
            if (TerminatingOrDeleted(item))
                continue;
            if (IsLaundryGrenade(item))
            {
                if (!ammunition)
                    PrimeLaundryGrenade(washer, item, user, ref totalRounds);
                continue;
            }
            if (TryComp<BallisticAmmoProviderComponent>(item, out var magazine))
            {
                if (!ammunition)
                {
                    foreach (var contained in magazine.Container.ContainedEntities.ToArray())
                    {
                        if (IsLaundryGrenade(contained))
                            PrimeLaundryGrenade(washer, contained, user, ref totalRounds);
                    }
                    continue;
                }
                var count = new GetAmmoCountEvent();
                RaiseLocalEvent(item, ref count);
                if (count.Count <= 0)
                    continue;
                // Materialize lazy ammo through the gun API, including launcher rounds in magazines.
                var take = new TakeAmmoEvent(count.Count, new(), Transform(washer.Owner).Coordinates, user);
                RaiseLocalEvent(item, take);
                var rounds = 0;
                var containsGrenades = false;
                foreach (var (ammo, _) in take.Ammo)
                {
                    if (ammo is not { } round || TerminatingOrDeleted(round))
                        continue;
                    if (IsLaundryGrenade(round))
                    {
                        // Leave launcher rounds in their magazine for the separate seven-second grenade stage.
                        containsGrenades = true;
                        _laundryGun.TryBallisticInsert((item, magazine), round, user, suppressInsertionSound: true);
                    }
                    else if (TryComp<CartridgeAmmoComponent>(round, out var cartridge) && !cartridge.Spent)
                    {
                        rounds++;
                        QueueDel(round);
                    }
                }
                if (rounds > 0)
                {
                    totalRounds += rounds;
                    if (!containsGrenades)
                        QueueDel(item);
                }
                continue;
            }
            if (ammunition && TryComp<CartridgeAmmoComponent>(item, out var loose) && !loose.Spent)
            {
                QueueDel(item);
                totalRounds++;
            }
        }
        if (totalRounds > 0 && !TerminatingOrDeleted(washer.Owner))
        {
            StartLaundryPopcorn(washer, totalRounds, user);
            _popup.PopupEntity(Loc.GetString("cmu-laundry-cookoff"), washer);
        }
    }

    private bool PrimeLaundryGrenade(Entity<CMULaundryComponent> washer, EntityUid grenade,
        EntityUid? user, ref int totalRounds)
    {
        var message = Loc.GetString("cmu-laundry-explosive-melting", ("item", grenade));
        if (TryComp<CartridgeAmmoComponent>(grenade, out var cartridge))
        {
            if (cartridge.Spent)
                return false;
            var payload = Spawn(cartridge.Prototype, Transform(washer.Owner).Coordinates);
            QueueDel(grenade);
            _popup.PopupEntity(message, washer);
            EnsureComp<CMULaundryHeatedGrenadeComponent>(payload);
            if (_laundryTrigger.Trigger(payload, user, key: null))
                return true;
            if (HasComp<ExplosiveComponent>(payload))
            {
                _laundryExplosion.TriggerExplosive(payload, user: user);
                return true;
            }
            QueueDel(payload);
            totalRounds++;
            return true;
        }
        if (TryComp<TimerTriggerComponent>(grenade, out var timer))
        {
            var alreadyHeated = HasComp<CMULaundryHeatedGrenadeComponent>(grenade);
            EnsureComp<CMULaundryHeatedGrenadeComponent>(grenade);
            _washerPriming.Add(grenade);
            var popup = timer.Popup;
            timer.Popup = null;
            bool primed;
            try
            {
                primed = _laundryTrigger.ActivateTimerTrigger((grenade, timer), user);
            }
            finally
            {
                _washerPriming.Remove(grenade);
                timer.Popup = popup;
            }
            if (!primed && !alreadyHeated && !HasComp<ActiveTimerTriggerComponent>(grenade))
                RemComp<CMULaundryHeatedGrenadeComponent>(grenade);
            if (primed)
                _popup.PopupEntity(message, washer);
            return primed;
        }
        EnsureComp<CMULaundryHeatedGrenadeComponent>(grenade);
        var triggered = _laundryTrigger.Trigger(grenade, user, key: null);
        if (triggered)
            _popup.PopupEntity(message, washer);
        return triggered;
    }

    private static readonly SoundSpecifier LaundryPop = new SoundPathSpecifier("/Audio/Weapons/pop.ogg",
        AudioParams.Default.WithVolume(-10f).WithVariation(0.2f));

    private void OnLaundryExplosion(Entity<CMULaundryHeatedGrenadeComponent> ent, ref CMUExplosionPreparingEvent args)
    {
        var half = "CMULaundryHalf" + args.ExplosionType.Id;
        if (ProtoMan.HasIndex<ExplosionPrototype>(half))
            args.ExplosionType = half;
    }

    private void OnLaundryFragments(Entity<CMULaundryHeatedGrenadeComponent> ent, ref CMClusterSpawnedEvent args)
    {
        foreach (var spawned in args.Spawned)
        {
            if (TerminatingOrDeleted(spawned))
                continue;
            if (TryComp<ProjectileComponent>(spawned, out var projectile))
            {
                projectile.Damage = projectile.Damage * 0.5f;
                Dirty(spawned, projectile);
            }
            if (IsLaundryGrenade(spawned))
                EnsureComp<CMULaundryHeatedGrenadeComponent>(spawned);
        }
    }

}
