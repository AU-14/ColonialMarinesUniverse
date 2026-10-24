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
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared._RMC14.Explosion;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using System;

namespace Content.Server.CMU14.Hygiene;

public sealed partial class CMUHygieneSystem
{
    [Dependency] private SharedExplosionSystem _laundryExplosion = default!;

    [Dependency] private IComponentFactory _laundryFactory = default!;

    [Dependency] private TagSystem _laundryTags = default!;

    [Dependency] private TriggerSystem _laundryTrigger = default!;

    [Dependency] private SharedGunSystem _laundryGun = default!;

    [Dependency] private IRobustRandom _laundryRandom = default!;

    [Dependency] private SharedTransformSystem _laundryTransform = default!;

    private readonly HashSet<EntityUid> _washerPriming = new();
    private static readonly ProtoId<TagPrototype>[] GrenadeTags = ["Grenade", "HandGrenade", "LauncherCompatibleGrenade"];

    private void OnLaundryVocalize(Entity<CMULaundryHeatedGrenadeComponent> ent, ref ActiveTimerTriggerEvent args)
    {
        // Only the synchronous washer activation loses its user callout; timer attribution stays intact.
        if (_washerPriming.Contains(ent.Owner))
            args = new ActiveTimerTriggerEvent(null);
    }

    private bool IsLaundryGrenade(EntityUid uid)
    {
        foreach (var tag in GrenadeTags)
        {
            if (_laundryTags.HasTag(uid, tag))
                return true;
        }
        return HasComp<ProjectileGrenadeComponent>(uid) ||
               HasComp<ScatteringGrenadeComponent>(uid) ||
               HasComp<PayloadCaseComponent>(uid);
    }

    private bool IsLaundryGrenadePrototype(EntProtoId id)
    {
        var prototype = ProtoMan.Index(id);
        if (prototype.HasComp<ProjectileGrenadeComponent>(_laundryFactory)
            || prototype.HasComp<ScatteringGrenadeComponent>(_laundryFactory)
            || prototype.HasComp<PayloadCaseComponent>(_laundryFactory))
            return true;
        if (!prototype.TryComp<TagComponent>(out var tags, _laundryFactory))
            return false;
        foreach (var tag in GrenadeTags)
        {
            if (_laundryTags.HasTag(tags, tag))
                return true;
        }
        return false;
    }

    private void CookOffLaundry(Entity<CMULaundryComponent> washer, StorageComponent storage, bool ammunition)
    {
        if (washer.Comp.FinishAt != null)
            _cookoffJobs.Enqueue(new LaundryCookoffJob(washer.Owner, washer.Comp.CycleId, ammunition,
                storage.Container));
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
            EnsureComp<CMULaundryHeatedGrenadeComponent>(payload);
            if (_laundryTrigger.Trigger(payload, user, key: null))
            {
                QueueDel(grenade);
                if (!TerminatingOrDeleted(washer.Owner))
                    _popup.PopupEntity(message, washer);
                return true;
            }
            if (HasComp<ExplosiveComponent>(payload))
            {
                _laundryExplosion.TriggerExplosive(payload, user: user);
                QueueDel(grenade);
                if (!TerminatingOrDeleted(washer.Owner))
                    _popup.PopupEntity(message, washer);
                return true;
            }
            QueueDel(payload);
            if (GetPopcornCapacity(washer.Owner) == 0)
                return false;
            QueueDel(grenade);
            _popup.PopupEntity(message, washer);
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
        {
            args.ExplosionType = half;
            return;
        }

        args.Cancelled = true;
        Log.Error($"Missing washer explosion profile {half} for {ToPrettyString(ent)}.");
        _popup.PopupEntity(Loc.GetString("cmu-laundry-unsupported-explosive"), ent);
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
