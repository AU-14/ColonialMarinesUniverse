using Content.Server.Ghost.Roles.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Atmos;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;

namespace Content.Server.CMU14.Atmos;

// ghost roles and call-ins only carry what their loadout says, and most loadouts were written for maps with air.
// on a breathing map they'd spawn (or land) and choke, so top them up with a mask and a tank once
public sealed partial class CMUMapBreathingSystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    // where a spare tank goes when suit storage won't take it, before falling back to hands
    private static readonly string[] CallInTankStorageSlots = ["back", "belt", "outerClothing", "pocket1", "pocket2"];

    // sweeps to wait after first seeing a body, so loadouts that equip after spawn get to go first
    private const int CallInKitGraceSweeps = 2;

    private readonly List<EntityUid> _callInScratch = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, GhostRoleSpawnerUsedEvent>(OnGhostRoleSpawned);
    }

    private void OnGhostRoleSpawned(Entity<CMUHumanMedicalComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!HasComp<CMUCallInBreathingKitGivenComponent>(ent))
            EnsureComp<CMUCallInBreathingKitPendingComponent>(ent);
    }

    private void UpdateCallInKits()
    {
        // ghost role bodies keep GhostRoleComponent after someone takes them, so this also covers erts still on their ship
        _callInScratch.Clear();
        var ghosts = EntityQueryEnumerator<GhostRoleComponent, CMUHumanMedicalComponent>();
        while (ghosts.MoveNext(out var uid, out _, out _))
        {
            if (!HasComp<CMUCallInBreathingKitGivenComponent>(uid) && !HasComp<CMUCallInBreathingKitPendingComponent>(uid))
                _callInScratch.Add(uid);
        }

        foreach (var uid in _callInScratch)
        {
            AddComp<CMUCallInBreathingKitPendingComponent>(uid);
        }

        _callInScratch.Clear();
        var pending = EntityQueryEnumerator<CMUCallInBreathingKitPendingComponent, TransformComponent>();
        while (pending.MoveNext(out var uid, out var comp, out var xform))
        {
            if (++comp.Sweeps < CallInKitGraceSweeps)
                continue;

            // not on a breathing map yet (still on the ship etc), check again once they land
            if (xform.MapUid is not { } map || !RequiresBreathing(map))
                continue;

            _callInScratch.Add(uid);
        }

        // spawning gear inside the query would touch the transform dict we're walking, so do it after
        foreach (var uid in _callInScratch)
        {
            RemComp<CMUCallInBreathingKitPendingComponent>(uid);
            EnsureComp<CMUCallInBreathingKitGivenComponent>(uid);

            if (Transform(uid).MapUid is { } map && GetBreathingMarker(map) is { } marker)
                GiveCallInKit(uid, marker);
        }
    }

    private CMUBreathingRequiredMapComponent? GetBreathingMarker(EntityUid map)
    {
        foreach (var member in _zLevels.GetAllNetworkMaps(map))
        {
            if (TryComp<CMUBreathingRequiredMapComponent>(member, out var marker))
                return marker;
        }

        return null;
    }

    public void GiveCallInKit(EntityUid uid, CMUBreathingRequiredMapComponent marker)
    {
        if (!TryComp<InternalsComponent>(uid, out var internals))
            return;

        if (internals.BreathTools.Count == 0)
            EquipCallInMask(uid, marker);

        // FindBestGasTank only returns a dry tank when nothing on them has air left
        if (!(_internals.FindBestGasTank(uid) is { } tank && SharedInternalsSystem.CMUHasAir(tank)) &&
            marker.CallInTank is { } tankProto)
        {
            var spawned = Spawn(tankProto, Transform(uid).Coordinates);
            StowCallInTank(uid, spawned);
        }

        // same retrying hookup the bosenmori survivors use
        if (!_internals.AreInternalsWorking(internals))
            EnsureComp<CMUSpawnWithInternalsComponent>(uid);
    }

    private void EquipCallInMask(EntityUid uid, CMUBreathingRequiredMapComponent marker)
    {
        if (_inventory.TryGetSlotEntity(uid, "mask", out var worn))
        {
            // a breath mask that's just pulled down, leave it to them
            if (HasComp<BreathToolComponent>(worn))
                return;

            // balaclavas and the like aren't sealed, swap them out and hand them back
            if (!_inventory.TryUnequip(uid, "mask", silent: true, force: true))
                return;

            _hands.PickupOrDrop(uid, worn.Value, checkActionBlocker: false, animate: false);
        }

        // a gas mask already in their hands or pockets just needs putting on
        foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
        {
            if (item != worn && HasComp<BreathToolComponent>(item) &&
                _inventory.TryEquip(uid, item, "mask", silent: true))
            {
                return;
            }
        }

        if (marker.CallInMask is not { } maskProto)
            return;

        var mask = Spawn(maskProto, Transform(uid).Coordinates);
        if (!_inventory.TryEquip(uid, mask, "mask", silent: true, force: true))
            _hands.PickupOrDrop(uid, mask, checkActionBlocker: false, animate: false);
    }

    private void StowCallInTank(EntityUid uid, EntityUid tank)
    {
        if (_inventory.TryEquip(uid, tank, "suitstorage", silent: true))
            return;

        foreach (var slot in CallInTankStorageSlots)
        {
            if (_inventory.TryGetSlotEntity(uid, slot, out var worn) &&
                TryComp<StorageComponent>(worn, out var storage) &&
                _storage.Insert(worn.Value, tank, out _, storageComp: storage, playSound: false))
            {
                return;
            }
        }

        _hands.PickupOrDrop(uid, tank, checkActionBlocker: false, animate: false);
    }
}

[RegisterComponent]
public sealed partial class CMUCallInBreathingKitPendingComponent : Component
{
    [ViewVariables]
    public int Sweeps;
}

// only ever topped up once, so dropping your mask later doesn't hand out free ones
[RegisterComponent]
public sealed partial class CMUCallInBreathingKitGivenComponent : Component;
