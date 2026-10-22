using Content.Server._RMC14.Marines;
using Content.Server._RMC14.Marines.Roles.Ranks;
using Content.Server.CMU14.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Roles;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;
// cmu edit start
using Content.Server.Body;
using Content.Server.Humanoid.Systems;
using Content.Shared.Body;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
// cmu edit end

namespace Content.Server._RMC14.Ghost;

public sealed partial class GhostRoleApplySpecialSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private LoadoutSystem _loadout = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SquadSystem _squad = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private RankSystem _rank = default!;
    [Dependency] private MarineSystem _marine = default!;
    [Dependency] private RoundJobProfileSystem _roundJobProfiles = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    // cmu edit start
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private IGameTiming _timing = default!;
    // cmu edit end

    public override void Initialize()
    {
        base.Initialize();
        // cmu edit start: hands come from the hand organs the body spawns at map init, so equipping on startup (or
        // before the body is built) dropped every in-hand item on the floor. Equip once the body and its looks are set.
        SubscribeLocalEvent<GhostRoleApplySpecialComponent, MapInitEvent>(OnStartup,
            after: [typeof(SharedHandsSystem), typeof(InitialBodySystem), typeof(VisualBodySystem), typeof(RandomHumanoidAppearanceSystem)]);
        // cmu edit end
    }

    private void OnStartup(Entity<GhostRoleApplySpecialComponent> ent, ref MapInitEvent args) // cmu edit
    {
        if (!TryComp<GhostRoleComponent>(ent, out var ghostRole) ||
            !TryComp(ent, out MetaDataComponent? metaData))
            return;

        if (ghostRole.JobProto is not { } jobProto)
            return;

        if (!_prototype.TryIndex(jobProto, out var job))
            return;

        if (job.StartingGear is { } gear)
        {
            _loadout.Equip(ent, [gear], null);
            CMUPickUpDroppedGear(ent); // cmu edit
        }

        if (_inventory.TryGetSlotContainer(ent, "id", out var container, out _))
        {
            foreach (var item in container.ContainedEntities)
            {
                if (TryComp<IdCardComponent>(item, out var card))
                {
                    card.FullName = metaData.EntityName;
                    _idCard.TryChangeOriginalOwner(item, ent.Owner, card);
                    _meta.SetEntityName(item, $"{metaData.EntityName} ({job.LocalizedName})");
                }
            }
        }

        AddComp(ent, new OriginalRoleComponent() { Job = jobProto });
        foreach (var special in job.Special)
            special.AfterEquip(ent);
        _roundJobProfiles.ApplyJobProfile(ent.Owner, job);

        if (job.Ranks is { } ranks)
        {
            foreach (var rank in ranks)
            {
                if (rank.Value is null || rank.Value.Count == 0)
                {
                    _rank.SetRank(ent, rank.Key);
                    break;
                }
            }
        }

        if (ent.Comp.Squad is { } squadProto && _squad.TryEnsureSquad(squadProto, out var squad))
        {
            if (_squad.TryGetSquadLeader(squad, out _))
                RemComp<SquadLeaderComponent>(ent);

            _squad.AssignSquad(ent, squad.Owner, jobProto);
        }

        if (HasComp<MarineComponent>(ent) &&
            job.Icon != "CMJobIconEmpty" &&
            _prototype.TryIndex(job.Icon, out var icon))
            _marine.SetMarineIcon(ent, icon.Icon);

        RemComp<GhostRoleApplySpecialComponent>(ent);
    }

    // cmu edit start
    /// <summary>
    ///     Anything the loadout couldn't place lands at the mob's feet. Put it in a free hand or a worn bag instead, so
    ///     nobody spawns standing on their own gear.
    /// </summary>
    private void CMUPickUpDroppedGear(EntityUid mob)
    {
        var tick = _timing.CurTick;
        var coords = Transform(mob).Coordinates;
        foreach (var item in _lookup.GetEntitiesInRange<ItemComponent>(coords, 0.5f, LookupFlags.Uncontained))
        {
            if (MetaData(item).CreationTick != tick || _container.IsEntityInContainer(item))
                continue;

            if (CMUTryWear(mob, item) || _hands.TryPickupAnyHand(mob, item, checkActionBlocker: false))
                continue;

            var slots = _inventory.GetSlotEnumerator(mob);
            while (slots.MoveNext(out var slot))
            {
                if (slot.ContainedEntity is { } worn &&
                    TryComp(worn, out StorageComponent? storage) &&
                    _storage.Insert(worn, item, out _, storageComp: storage, playSound: false))
                {
                    break;
                }
            }
        }
    }

    private bool CMUTryWear(EntityUid mob, EntityUid item)
    {
        var slots = _inventory.GetSlotEnumerator(mob);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity == null && _inventory.TryEquip(mob, item, slot.ID, silent: true))
                return true;
        }

        return false;
    }
    // cmu edit end
}
