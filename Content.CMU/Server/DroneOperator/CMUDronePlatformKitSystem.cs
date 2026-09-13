using Content.Shared.ActionBlocker;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Prototypes;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.DroneOperator;

public sealed partial class CMUDronePlatformKitSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUDronePlatformKitComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<CMUDronePlatformKitComponent, ExaminedEvent>(OnExamined);
        Subs.BuiEvents<CMUDronePlatformKitComponent>(CMUDronePlatformKitUi.Key,
            subs => subs.Event<CMUDronePlatformSelectedMessage>(OnSelected));
    }

    private void OnOpenAttempt(Entity<CMUDronePlatformKitComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<CMUDroneOperatorComponent>(args.User) || ent.Comp.Claimed)
        {
            args.Cancel();
            if (!args.Silent)
                _popup.PopupEntity(Loc.GetString("cmu-drone-operator-required"), ent, args.User);
        }
    }

    private void OnExamined(Entity<CMUDronePlatformKitComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-drone-platform-kit-examine"));
    }

    private void OnSelected(Entity<CMUDronePlatformKitComponent> ent, ref CMUDronePlatformSelectedMessage args)
    {
        TrySelectPlatform(ent, args.Actor, args.Platform);
    }

    /// <summary>Redeems one issued kit and puts the chosen supplies in its bag, or beside the operator.</summary>
    public bool TrySelectPlatform(Entity<CMUDronePlatformKitComponent> ent, EntityUid user, CMUDronePlatform platform)
    {
        if (TerminatingOrDeleted(ent) || ent.Comp.Claimed || !HasComp<CMUDroneOperatorComponent>(user) ||
            !_blocker.CanInteract(user, ent) || !_interaction.InRangeUnobstructed((user, null), (ent.Owner, null)))
            return false;

        EntProtoId pack;
        switch (platform)
        {
            case CMUDronePlatform.Humanoid:
                pack = ent.Comp.HumanoidPack;
                break;
            case CMUDronePlatform.Tracked:
                pack = ent.Comp.TrackedPack;
                break;
            case CMUDronePlatform.Flamer:
                pack = ent.Comp.FlamerPack;
                break;
            default:
                return false;
        }

        if (!_prototypes.Index(pack).TryGetComponent<StorageFillComponent>(out var manifest, _factory))
            return false;

        Entity<StorageComponent>? destination = null;
        if (_containers.TryGetContainingContainer((ent.Owner, null, null), out var container) &&
            TryComp<StorageComponent>(container.Owner, out var storage))
            destination = (container.Owner, storage);
        else if (_inventory.TryGetSlotEntity(user, "back", out var back) &&
                 TryComp<StorageComponent>(back, out var backpack))
            destination = (back.Value, backpack);

        // Consume before spawning so repeated UI messages cannot duplicate the kit.
        ent.Comp.Claimed = true;
        var coordinates = Transform(user).Coordinates;
        foreach (var prototype in EntitySpawnCollection.GetSpawns(manifest.Contents, _random))
        {
            var item = Spawn(prototype, coordinates);
            if (destination is { } bag)
                _storage.Insert(bag, item, out _, out _, storageComp: bag.Comp, playSound: false);
        }

        _popup.PopupEntity(Loc.GetString("cmu-drone-platform-unpacked"), ent, user);
        QueueDel(ent);
        return true;
    }
}
