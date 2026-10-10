using Content.Shared._RMC14.Anchor;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Map;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Trigger.Components;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Weapons.Grenades;

public sealed partial class CMUGrenadeBoxSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedRMCExplosionSystem _explosion = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCMapSystem _rmcMap = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUGrenadeBoxComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
        SubscribeLocalEvent<CMUGrenadeBoxComponent, ContainerIsRemovingAttemptEvent>(OnRemoveAttempt);
        SubscribeLocalEvent<CMUGrenadeBoxComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<CMUGrenadeBoxComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInsertAttempt(Entity<CMUGrenadeBoxComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Cancelled || args.Container.ID != StorageComponent.ContainerId)
            return;

        // Don't mix different grenades in one box, and don't put an armed one back in.
        if (ent.Comp.CookOffAt != null ||
            HasComp<ActiveTimerTriggerComponent>(args.EntityUid) ||
            ent.Comp.Grenade is { } grenade && MetaData(args.EntityUid).EntityPrototype?.ID != grenade.Id)
        {
            args.Cancel();
        }
    }

    private void OnRemoveAttempt(Entity<CMUGrenadeBoxComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (ent.Comp.CookOffAt != null && args.Container.ID == StorageComponent.ContainerId)
            args.Cancel();
    }

    private void OnInteractHand(Entity<CMUGrenadeBoxComponent> ent, ref InteractHandEvent args)
    {
        // A deployed box is static and can't be picked up, so a click opens it instead.
        if (args.Handled ||
            !TryComp(ent, out DeployableItemComponent? deployable) ||
            deployable.Position == DeployableItemPosition.None)
        {
            return;
        }

        args.Handled = true;
        _storage.OpenStorageUI(ent, args.User);
    }

    private void OnExamined(Entity<CMUGrenadeBoxComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.CookOffAt != null)
            args.PushMarkup(Loc.GetString("cmu-grenade-box-examine-burning"));
    }

    private int CountGrenades(EntityUid box)
    {
        return TryComp(box, out StorageComponent? storage) ? storage.Container.ContainedEntities.Count : 0;
    }

    private bool InFire(EntityUid box)
    {
        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(box);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<TileFireComponent>(uid))
                return true;
        }

        return false;
    }

    private void StartCookOff(Entity<CMUGrenadeBoxComponent> ent)
    {
        ent.Comp.CookOffAt = _timing.CurTime + ent.Comp.CookOffDelay;
        Dirty(ent);
        _appearance.SetData(ent, CMUGrenadeBoxVisuals.CookingOff, true);
        _popup.PopupEntity(Loc.GetString("cmu-grenade-box-cook-off", ("box", ent)), ent, PopupType.LargeCaution);
    }

    private void CookOff(Entity<CMUGrenadeBoxComponent> ent)
    {
        // A handful of grenades fizzles, a full box takes the whole tile with it.
        var count = CountGrenades(ent);
        var total = count * ent.Comp.CookOffIntensityPerGrenade;
        if (total > 0)
        {
            _explosion.QueueExplosion(_transform.GetMapCoordinates(ent), "RMC", total, ent.Comp.CookOffSlope,
                ent.Comp.CookOffMaxIntensity, ent);
        }

        QueueDel(ent);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUGrenadeBoxComponent>();
        while (query.MoveNext(out var uid, out var box))
        {
            if (box.CookOffAt is { } at)
            {
                if (time >= at)
                    CookOff((uid, box));

                continue;
            }

            if (time < box.NextFireCheck)
                continue;

            box.NextFireCheck = time + box.FireCheckInterval;
            if (box.CookOffIntensityPerGrenade <= 0 || _container.IsEntityInContainer(uid))
                continue;

            if (CountGrenades(uid) >= box.CookOffMinimum && InFire(uid))
                StartCookOff((uid, box));
        }
    }
}
